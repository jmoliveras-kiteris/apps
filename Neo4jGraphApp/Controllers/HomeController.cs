using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Neo4jGraphApp.Models;
using Neo4jGraphApp.Services;

namespace Neo4jGraphApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly INeo4jService _neo4jService;

        public HomeController(INeo4jService neo4jService)
        {
            _neo4jService = neo4jService;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel();
            ViewBag.IsConnected = true;
            ViewBag.ErrorMessage = null;

            try
            {
                // 1. Obtener estadísticas del grafo
                var stats = await _neo4jService.GetGraphStatsAsync();
                model.TotalPeopleCount = stats.ContainsKey("people") ? stats["people"] : 0;
                model.TotalSkillsCount = stats.ContainsKey("skills") ? stats["skills"] : 0;
                model.TotalConnectionsCount = stats.ContainsKey("relationships") ? stats["relationships"] : 0;

                // 2. Obtener datos para la visualización del grafo (vis.js)
                model.Graph = await _neo4jService.GetGraphDataAsync();

                // 3. Obtener recomendaciones ("Amigos de amigos")
                model.TopRecommendations = await _neo4jService.GetRecommendationsAsync();
            }
            catch (Exception ex)
            {
                // Capturar el error de conexión de forma amigable en lugar de colapsar la app
                ViewBag.IsConnected = false;
                ViewBag.ErrorMessage = ex.Message;

                // Proveer datos de muestra vacíos para que la UI cargue elegantemente
                model.Graph = new GraphData();
                model.TopRecommendations = new List<RecommendationViewModel>();
            }

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
