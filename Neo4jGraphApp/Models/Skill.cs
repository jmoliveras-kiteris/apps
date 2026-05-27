using System;

namespace Neo4jGraphApp.Models
{
    public class Skill
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // e.g. Frontend, Backend, Database, Cloud
    }
}
