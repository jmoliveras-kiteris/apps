using System;
using System.Collections.Generic;

namespace Neo4jGraphApp.Models
{
    public class Person
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        // Propiedades de navegación para la UI (relaciones en el grafo)
        public List<PersonConnection> Connections { get; set; } = new();
        public List<PersonSkillProficiency> Skills { get; set; } = new();
    }

    public class PersonConnection
    {
        public string PersonId { get; set; } = string.Empty;
        public string PersonName { get; set; } = string.Empty;
        public string PersonTitle { get; set; } = string.Empty;
        public DateTime Since { get; set; }
    }

    public class PersonSkillProficiency
    {
        public string SkillId { get; set; } = string.Empty;
        public string SkillName { get; set; } = string.Empty;
        public string SkillCategory { get; set; } = string.Empty;
        public string Proficiency { get; set; } = "Intermediate"; // Beginner, Intermediate, Expert
    }
}
