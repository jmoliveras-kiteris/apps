using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Neo4jGraphApp.Models;
using Neo4jGraphApp.Services;

namespace Neo4jGraphApp.Controllers
{
    public class SkillsController : Controller
    {
        private readonly INeo4jService _neo4jService;

        public SkillsController(INeo4jService neo4jService)
        {
            _neo4jService = neo4jService;
        }

        // Listar Habilidades + Cargar Formulario de adición rápida
        public async Task<IActionResult> Index()
        {
            try
            {
                var skills = await _neo4jService.GetSkillsAsync();
                return View(skills);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al conectar con Neo4j para listar habilidades: " + ex.Message;
                return View(Enumerable.Empty<Skill>().ToList());
            }
        }

        // Crear nueva Habilidad (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Skill skill)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Datos de habilidad no válidos.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                skill.Id = Guid.NewGuid().ToString();
                await _neo4jService.CreateSkillAsync(skill);
                TempData["SuccessMessage"] = $"Habilidad '{skill.Name}' agregada al catálogo.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al registrar la habilidad: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // Eliminar Habilidad (POST - Detach Delete automático)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            try
            {
                await _neo4jService.DeleteSkillAsync(id);
                TempData["SuccessMessage"] = "Habilidad eliminada de la base de datos (y desasociada de todos los perfiles).";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al eliminar la habilidad: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
