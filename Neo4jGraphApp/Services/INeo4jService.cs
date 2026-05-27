using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4jGraphApp.Models;

namespace Neo4jGraphApp.Services
{
    public interface INeo4jService
    {
        // CRUD de Personas
        Task<List<Person>> GetPeopleAsync();
        Task<Person?> GetPersonByIdAsync(string id);
        Task CreatePersonAsync(Person person);
        Task UpdatePersonAsync(Person person);
        Task DeletePersonAsync(string id);

        // CRUD de Habilidades (Skills)
        Task<List<Skill>> GetSkillsAsync();
        Task CreateSkillAsync(Skill skill);
        Task DeleteSkillAsync(string id);

        // Relaciones entre nodos
        Task ConnectPeopleAsync(string personId1, string personId2);
        Task DisconnectPeopleAsync(string personId1, string personId2);
        Task AddSkillToPersonAsync(string personId, string skillId, string proficiency);
        Task RemoveSkillFromPersonAsync(string personId, string skillId);

        // Visualización, Recomendaciones y Consultas Personalizadas
        Task<GraphData> GetGraphDataAsync();
        Task<List<RecommendationViewModel>> GetRecommendationsAsync(string? personId = null);
        Task<CustomQueryViewModel> ExecuteCustomCypherAsync(string query);
        Task<Dictionary<string, int>> GetGraphStatsAsync();
    }
}
