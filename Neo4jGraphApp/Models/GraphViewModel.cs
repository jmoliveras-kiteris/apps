using System.Collections.Generic;

namespace Neo4jGraphApp.Models
{
    public class GraphNode
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty; // "Person" o "Skill"
        public string Title { get; set; } = string.Empty; // Tooltip on hover
    }

    public class GraphEdge
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty; // "KNOWS", "HAS_SKILL"
        public string Title { get; set; } = string.Empty; // Tooltip
        public string Arrows { get; set; } = "to"; // Indica dirección de la relación
    }

    public class GraphData
    {
        public List<GraphNode> Nodes { get; set; } = new();
        public List<GraphEdge> Edges { get; set; } = new();
    }

    public class DashboardViewModel
    {
        public GraphData Graph { get; set; } = new();
        public int TotalPeopleCount { get; set; }
        public int TotalSkillsCount { get; set; }
        public int TotalConnectionsCount { get; set; }
        public List<RecommendationViewModel> TopRecommendations { get; set; } = new();
    }
}
