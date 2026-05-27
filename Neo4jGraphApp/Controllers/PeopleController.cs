using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Neo4jGraphApp.Models;
using Neo4jGraphApp.Services;

namespace Neo4jGraphApp.Controllers
{
    public class PeopleController : Controller
    {
        private readonly INeo4jService _neo4jService;

        public PeopleController(INeo4jService neo4jService)
        {
            _neo4jService = neo4jService;
        }

        // 1. LISTADO DE PERSONAS
        public async Task<IActionResult> Index()
        {
            try
            {
                var people = await _neo4jService.GetPeopleAsync();
                return View(people);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al conectar con la base de datos de Neo4j: " + ex.Message;
                return View(Enumerable.Empty<Person>().ToList());
            }
        }

        // 2. DETALLES DE UNA PERSONA (Incluye conexiones y habilidades)
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            try
            {
                var person = await _neo4jService.GetPersonByIdAsync(id);
                if (person == null) return NotFound();

                // Cargar datos complementarios para los desplegables de relaciones en la vista
                var allPeople = await _neo4jService.GetPeopleAsync();
                var allSkills = await _neo4jService.GetSkillsAsync();

                // Filtrar para no conectar consigo mismo ni con personas que ya conoce
                var existingConnIds = person.Connections.Select(c => c.PersonId).Append(person.Id).ToHashSet();
                ViewBag.AvailablePeople = allPeople.Where(p => !existingConnIds.Contains(p.Id)).ToList();

                // Filtrar habilidades que la persona ya tiene asignadas
                var existingSkillIds = person.Skills.Select(s => s.SkillId).ToHashSet();
                ViewBag.AvailableSkills = allSkills.Where(s => !existingSkillIds.Contains(s.Id)).ToList();

                return View(person);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al cargar los detalles: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // 3. CREACIÓN (GET)
        public IActionResult Create()
        {
            return View(new Person());
        }

        // 3. CREACIÓN (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Person person)
        {
            if (!ModelState.IsValid) return View(person);

            try
            {
                person.Id = Guid.NewGuid().ToString(); // Asignar nuevo GUID
                await _neo4jService.CreatePersonAsync(person);
                TempData["SuccessMessage"] = $"Persona '{person.Name}' creada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar en la base de datos: " + ex.Message);
                return View(person);
            }
        }

        // 4. EDICIÓN (GET)
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            try
            {
                var person = await _neo4jService.GetPersonByIdAsync(id);
                if (person == null) return NotFound();
                return View(person);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al cargar la persona para editar: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // 4. EDICIÓN (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Person person)
        {
            if (id != person.Id) return NotFound();
            if (!ModelState.IsValid) return View(person);

            try
            {
                await _neo4jService.UpdatePersonAsync(person);
                TempData["SuccessMessage"] = $"Perfil de '{person.Name}' actualizado correctamente.";
                return RedirectToAction(nameof(Details), new { id = person.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al actualizar la base de datos: " + ex.Message);
                return View(person);
            }
        }

        // 5. ELIMINACIÓN (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            try
            {
                await _neo4jService.DeletePersonAsync(id);
                TempData["SuccessMessage"] = "Registro eliminado correctamente del grafo.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al eliminar la persona: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // GESTIÓN DE RELACIONES (ACTIONS POST)
        // ==========================================

        // Conectar Persona A con Persona B (KNOWS)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddConnection(string personId, string friendId)
        {
            if (string.IsNullOrEmpty(personId) || string.IsNullOrEmpty(friendId)) return BadRequest();

            try
            {
                await _neo4jService.ConnectPeopleAsync(personId, friendId);
                TempData["SuccessMessage"] = "Nueva conexión profesional establecida en el grafo.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al establecer conexión: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = personId });
        }

        // Desconectar Personas (Eliminar relación KNOWS)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveConnection(string personId, string friendId)
        {
            if (string.IsNullOrEmpty(personId) || string.IsNullOrEmpty(friendId)) return BadRequest();

            try
            {
                await _neo4jService.DisconnectPeopleAsync(personId, friendId);
                TempData["SuccessMessage"] = "Conexión removida con éxito.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al remover la conexión: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = personId });
        }

        // Asignar Habilidad a Persona (HAS_SKILL)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSkill(string personId, string skillId, string proficiency)
        {
            if (string.IsNullOrEmpty(personId) || string.IsNullOrEmpty(skillId)) return BadRequest();

            try
            {
                await _neo4jService.AddSkillToPersonAsync(personId, skillId, proficiency);
                TempData["SuccessMessage"] = "Habilidad asociada correctamente en el grafo.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al asociar habilidad: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = personId });
        }

        // Desasociar Habilidad de Persona (Eliminar HAS_SKILL)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSkill(string personId, string skillId)
        {
            if (string.IsNullOrEmpty(personId) || string.IsNullOrEmpty(skillId)) return BadRequest();

            try
            {
                await _neo4jService.RemoveSkillFromPersonAsync(personId, skillId);
                TempData["SuccessMessage"] = "Habilidad desasociada del perfil.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al remover la habilidad: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = personId });
        }
    }
}
