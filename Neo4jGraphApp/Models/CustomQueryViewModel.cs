using System.Collections.Generic;

namespace Neo4jGraphApp.Models
{
    public class CustomQueryViewModel
    {
        public string Query { get; set; } = "MATCH (p:Person)-[r:KNOWS]->(friend) RETURN p.name AS Persona, r.since AS Desde, friend.name AS Amigo LIMIT 10";
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public long ExecutionTimeMs { get; set; }
        public bool QueryExecuted { get; set; }
    }
}
