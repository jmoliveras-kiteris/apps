using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Neo4jGraphApp.Models;
using Neo4jGraphApp.Services;

namespace Neo4jGraphApp.Controllers
{
    public class QueriesController : Controller
    {
        private readonly INeo4jService _neo4jService;

        public QueriesController(INeo4jService neo4jService)
        {
            _neo4jService = neo4jService;
        }

        // GET: Cargar la consola Cypher con una consulta por defecto
        public IActionResult Index()
        {
            var model = new CustomQueryViewModel();
            return View(model);
        }

        // POST: Ejecutar la consulta ingresada por el usuario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CustomQueryViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Query))
            {
                model.ErrorMessage = "La consulta Cypher no puede estar vacía.";
                return View(model);
            }

            try
            {
                // Ejecutar la consulta de manera dinámica
                var result = await _neo4jService.ExecuteCustomCypherAsync(model.Query);
                return View(result);
            }
            catch (Exception ex)
            {
                model.ErrorMessage = "Excepción en el cliente: " + ex.Message;
                model.QueryExecuted = false;
                return View(model);
            }
        }
    }
}
