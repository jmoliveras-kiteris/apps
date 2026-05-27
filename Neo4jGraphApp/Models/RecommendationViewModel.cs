namespace Neo4jGraphApp.Models
{
    public class RecommendationViewModel
    {
        public string SourcePersonId { get; set; } = string.Empty;
        public string SourcePersonName { get; set; } = string.Empty;
        
        public string TargetPersonId { get; set; } = string.Empty;
        public string TargetPersonName { get; set; } = string.Empty;
        public string TargetPersonTitle { get; set; } = string.Empty;
        
        public string Reason { get; set; } = string.Empty; // e.g., "Amigo de amigo", "Tiene habilidades que te interesan"
        public int CommonConnectionsCount { get; set; }
        public string CommonConnectionsList { get; set; } = string.Empty; // comma separated names
    }
}
