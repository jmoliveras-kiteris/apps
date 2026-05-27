using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo4j.Driver;
using Neo4jGraphApp.Models;

namespace Neo4jGraphApp.Services
{
    public class Neo4jService : INeo4jService
    {
        private readonly IDriver _driver;

        public Neo4jService(IDriver driver)
        {
            _driver = driver;
        }

        // ==========================================
        // CRUD de Personas (Nodes)
        // ==========================================

        public async Task<List<Person>> GetPeopleAsync()
        {
            using var session = _driver.AsyncSession();
            return await session.ExecuteReadAsync(async tx =>
            {
                var result = await tx.RunAsync(
                    "MATCH (p:Person) RETURN p.id AS id, p.name AS name, p.title AS title, p.bio AS bio, p.email AS email ORDER BY p.name"
                );
                var list = new List<Person>();
                while (await result.FetchAsync())
                {
                    list.Add(new Person
                    {
                        Id = result.Current["id"].As<string>(),
                        Name = result.Current["name"].As<string>(),
                        Title = result.Current["title"]?.As<string>() ?? "",
                        Bio = result.Current["bio"]?.As<string>() ?? "",
                        Email = result.Current["email"]?.As<string>() ?? ""
                    });
                }
                return list;
            });
        }

        public async Task<Person?> GetPersonByIdAsync(string id)
        {
            using var session = _driver.AsyncSession();
            return await session.ExecuteReadAsync(async tx =>
            {
                var personResult = await tx.RunAsync(
                    "MATCH (p:Person {id: $id}) RETURN p.id AS id, p.name AS name, p.title AS title, p.bio AS bio, p.email AS email",
                    new { id }
                );

                if (!await personResult.FetchAsync()) return null;

                var person = new Person
                {
                    Id = personResult.Current["id"].As<string>(),
                    Name = personResult.Current["name"].As<string>(),
                    Title = personResult.Current["title"]?.As<string>() ?? "",
                    Bio = personResult.Current["bio"]?.As<string>() ?? "",
                    Email = personResult.Current["email"]?.As<string>() ?? ""
                };

                // Obtener conexiones KNOWS
                var connResult = await tx.RunAsync(
                    @"MATCH (p:Person {id: $id})-[r:KNOWS]-(friend:Person) 
                      RETURN friend.id AS id, friend.name AS name, friend.title AS title, r.since AS since",
                    new { id }
                );
                var processedFriends = new HashSet<string>();
                while (await connResult.FetchAsync())
                {
                    var friendId = connResult.Current["id"].As<string>();
                    // Evitar duplicados por relaciones bidireccionales en la vista de detalles
                    if (processedFriends.Contains(friendId)) continue;
                    processedFriends.Add(friendId);

                    var sinceVal = connResult.Current["since"];
                    DateTime sinceDate = DateTime.Now;
                    if (sinceVal != null)
                    {
                        if (sinceVal is string strSince)
                        {
                            DateTime.TryParse(strSince, out sinceDate);
                        }
                        else if (sinceVal is DateTime dtSince)
                        {
                            sinceDate = dtSince;
                        }
                    }

                    person.Connections.Add(new PersonConnection
                    {
                        PersonId = friendId,
                        PersonName = connResult.Current["name"].As<string>(),
                        PersonTitle = connResult.Current["title"]?.As<string>() ?? "",
                        Since = sinceDate
                    });
                }

                // Obtener habilidades HAS_SKILL
                var skillResult = await tx.RunAsync(
                    @"MATCH (p:Person {id: $id})-[r:HAS_SKILL]->(s:Skill) 
                      RETURN s.id AS id, s.name AS name, s.category AS category, r.proficiency AS proficiency",
                    new { id }
                );
                while (await skillResult.FetchAsync())
                {
                    person.Skills.Add(new PersonSkillProficiency
                    {
                        SkillId = skillResult.Current["id"].As<string>(),
                        SkillName = skillResult.Current["name"].As<string>(),
                        SkillCategory = skillResult.Current["category"]?.As<string>() ?? "",
                        Proficiency = skillResult.Current["proficiency"]?.As<string>() ?? "Intermediate"
                    });
                }

                return person;
            });
        }

        public async Task CreatePersonAsync(Person person)
        {
            using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "CREATE (p:Person {id: $id, name: $name, title: $title, bio: $bio, email: $email})",
                    new { id = person.Id, name = person.Name, title = person.Title, bio = person.Bio, email = person.Email }
                );
            });
        }

        public async Task UpdatePersonAsync(Person person)
        {
            using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "MATCH (p:Person {id: $id}) SET p.name = $name, p.title = $title, p.bio = $bio, p.email = $email",
                    new { id = person.Id, name = person.Name, title = person.Title, bio = person.Bio, email = person.Email }
                );
            });
        }

        public async Task DeletePersonAsync(string id)
        {
            using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                // DETACH DELETE borra automáticamente todas las relaciones que conectaban a este nodo
                await tx.RunAsync("MATCH (p:Person {id: $id}) DETACH DELETE p", new { id });
            });
        }


        // ==========================================
        // CRUD de Habilidades (Skills)
        // ==========================================

        public async Task<List<Skill>> GetSkillsAsync()
        {
            using var session = _driver.AsyncSession();
            return await session.ExecuteReadAsync(async tx =>
            {
                var result = await tx.RunAsync(
                    "MATCH (s:Skill) RETURN s.id AS id, s.name AS name, s.category AS category ORDER BY s.name"
                );
                var list = new List<Skill>();
                while (await result.FetchAsync())
                {
                    list.Add(new Skill
                    {
                        Id = result.Current["id"].As<string>(),
                        Name = result.Current["name"].As<string>(),
                        Category = result.Current["category"]?.As<string>() ?? ""
                    });
                }
                return list;
            });
        }

        public async Task CreateSkillAsync(Skill skill)
        {
            using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "CREATE (s:Skill {id: $id, name: $name, category: $category})",
                    new { id = skill.Id, name = skill.Name, category = skill.Category }
                );
            });
        }

        public async Task DeleteSkillAsync(string id)
        {
            using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync("MATCH (s:Skill {id: $id}) DETACH DELETE s", new { id });
            });
        }


        // ==========================================
        // Gestión de Relaciones
        // ==========================================

        public async Task ConnectPeopleAsync(string personId1, string personId2)
        {
            using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    @"MATCH (p1:Person {id: $id1}), (p2:Person {id: $id2})
                      WHERE p1.id <> p2.id
                      MERGE (p1)-[r:KNOWS]->(p2)
                      ON CREATE SET r.since = $since",
                    new { id1 = personId1, id2 = personId2, since = DateTime.UtcNow.ToString("o") }
                );
            });
        }

        public async Task DisconnectPeopleAsync(string personId1, string personId2)
        {
            using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                // Elimina la relación en cualquier dirección
                await tx.RunAsync(
                    @"MATCH (p1:Person {id: $id1})-[r:KNOWS]-(p2:Person {id: $id2})
                      DELETE r",
                    new { id1 = personId1, id2 = personId2 }
                );
            });
        }

        public async Task AddSkillToPersonAsync(string personId, string skillId, string proficiency)
        {
            using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    @"MATCH (p:Person {id: $personId}), (s:Skill {id: $skillId})
                      MERGE (p)-[r:HAS_SKILL]->(s)
                      SET r.proficiency = $proficiency",
                    new { personId, skillId, proficiency }
                );
            });
        }

        public async Task RemoveSkillFromPersonAsync(string personId, string skillId)
        {
            using var session = _driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    @"MATCH (p:Person {id: $personId})-[r:HAS_SKILL]->(s:Skill {id: $skillId})
                      DELETE r",
                    new { personId, skillId }
                );
            });
        }


        // ==========================================
        // Visualización del Grafo Completo
        // ==========================================

        public async Task<GraphData> GetGraphDataAsync()
        {
            using var session = _driver.AsyncSession();
            return await session.ExecuteReadAsync(async tx =>
            {
                var graphData = new GraphData();

                // 1. Obtener Nodos
                var nodesResult = await tx.RunAsync(
                    "MATCH (n) RETURN labels(n)[0] AS type, n.id AS id, n.name AS name, n.title AS title"
                );
                while (await nodesResult.FetchAsync())
                {
                    var type = nodesResult.Current["type"].As<string>();
                    var id = nodesResult.Current["id"].As<string>();
                    var name = nodesResult.Current["name"].As<string>();
                    var title = type == "Person" ? (nodesResult.Current["title"]?.As<string>() ?? "") : "";

                    graphData.Nodes.Add(new GraphNode
                    {
                        Id = id,
                        Label = name,
                        Group = type,
                        Title = type == "Person" ? $"{name} - {title}" : $"Habilidad: {name}"
                    });
                }

                // 2. Obtener Relaciones
                var relResult = await tx.RunAsync(
                    "MATCH (a)-[r]->(b) RETURN a.id AS from, b.id AS to, type(r) AS type"
                );
                while (await relResult.FetchAsync())
                {
                    var from = relResult.Current["from"].As<string>();
                    var to = relResult.Current["to"].As<string>();
                    var type = relResult.Current["type"].As<string>();

                    graphData.Edges.Add(new GraphEdge
                    {
                        From = from,
                        To = to,
                        Label = type,
                        Title = type == "KNOWS" ? "Conoce a" : "Tiene habilidad",
                        Arrows = "to"
                    });
                }

                return graphData;
            });
        }


        // ==========================================
        // Algoritmo de Recomendación de Grafos (Amigos de Amigos)
        // ==========================================

        public async Task<List<RecommendationViewModel>> GetRecommendationsAsync(string? personId = null)
        {
            using var session = _driver.AsyncSession();
            return await session.ExecuteReadAsync(async tx =>
            {
                string query;
                object parameters;

                if (string.IsNullOrEmpty(personId))
                {
                    // Recomendaciones generales para cualquier persona en la base de datos
                    query = @"
                        MATCH (p:Person)-[:KNOWS]-(friend:Person)-[:KNOWS]-(fof:Person)
                        WHERE p.id <> fof.id AND NOT (p)-[:KNOWS]-(fof)
                        RETURN p.id AS sourceId, p.name AS sourceName, 
                               fof.id AS targetId, fof.name AS targetName, fof.title AS targetTitle,
                               count(friend) AS commonCount, collect(friend.name) AS commonList
                        ORDER BY commonCount DESC
                        LIMIT 10";
                    parameters = new { };
                }
                else
                {
                    // Recomendación específica para una persona
                    query = @"
                        MATCH (p:Person {id: $personId})-[:KNOWS]-(friend:Person)-[:KNOWS]-(fof:Person)
                        WHERE p.id <> fof.id AND NOT (p)-[:KNOWS]-(fof)
                        RETURN p.id AS sourceId, p.name AS sourceName, 
                               fof.id AS targetId, fof.name AS targetName, fof.title AS targetTitle,
                               count(friend) AS commonCount, collect(friend.name) AS commonList
                        ORDER BY commonCount DESC
                        LIMIT 5";
                    parameters = new { personId };
                }

                var result = await tx.RunAsync(query, parameters);
                var recs = new List<RecommendationViewModel>();
                while (await result.FetchAsync())
                {
                    var commonListVal = result.Current["commonList"] as List<object>;
                    var names = commonListVal != null ? string.Join(", ", commonListVal.Select(v => v.ToString())) : "";
                    
                    recs.Add(new RecommendationViewModel
                    {
                        SourcePersonId = result.Current["sourceId"].As<string>(),
                        SourcePersonName = result.Current["sourceName"].As<string>(),
                        TargetPersonId = result.Current["targetId"].As<string>(),
                        TargetPersonName = result.Current["targetName"].As<string>(),
                        TargetPersonTitle = result.Current["targetTitle"]?.As<string>() ?? "",
                        Reason = "Conexión sugerida (Amigo en común)",
                        CommonConnectionsCount = Convert.ToInt32(result.Current["commonCount"]),
                        CommonConnectionsList = names
                    });
                }
                return recs;
            });
        }


        // ==========================================
        // Ejecutor Libre de Consultas Cypher
        // ==========================================

        public async Task<CustomQueryViewModel> ExecuteCustomCypherAsync(string query)
        {
            using var session = _driver.AsyncSession();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var model = new CustomQueryViewModel { Query = query };

            try
            {
                await session.ExecuteReadAsync(async tx =>
                {
                    var result = await tx.RunAsync(query);
                    var keys = await result.KeysAsync();
                    model.Columns = new List<string>(keys);

                    while (await result.FetchAsync())
                    {
                        var row = new Dictionary<string, object>();
                        foreach (var key in keys)
                        {
                            var val = result.Current[key];

                            if (val == null)
                            {
                                row[key] = "NULL";
                            }
                            else if (val is INode node)
                            {
                                var labelsStr = string.Join(", ", node.Labels);
                                var propsStr = string.Join(", ", node.Properties.Select(p => $"{p.Key}: '{p.Value}'"));
                                row[key] = $"Node({labelsStr}) {{{propsStr}}}";
                            }
                            else if (val is IRelationship rel)
                            {
                                var propsStr = string.Join(", ", rel.Properties.Select(p => $"{p.Key}: '{p.Value}'"));
                                row[key] = $"Relationship({rel.Type}) {{{propsStr}}}";
                            }
                            else if (val is List<object> list)
                            {
                                row[key] = $"[{string.Join(", ", list.Select(v => v?.ToString() ?? "null"))}]";
                            }
                            else
                            {
                                row[key] = val;
                            }
                        }
                        model.Rows.Add(row);
                    }
                    return true;
                });
                model.QueryExecuted = true;
            }
            catch (Exception ex)
            {
                model.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                model.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return model;
        }


        // ==========================================
        // Estadísticas Generales del Grafo
        // ==========================================

        public async Task<Dictionary<string, int>> GetGraphStatsAsync()
        {
            using var session = _driver.AsyncSession();
            return await session.ExecuteReadAsync(async tx =>
            {
                var query = @"
                    OPTIONAL MATCH (p:Person) WITH count(p) AS pc 
                    OPTIONAL MATCH (s:Skill) WITH pc, count(s) AS sc 
                    OPTIONAL MATCH ()-[r]->() 
                    RETURN pc AS people, sc AS skills, count(r) AS rels";
                
                var result = await tx.RunAsync(query);
                var stats = new Dictionary<string, int>
                {
                    { "people", 0 },
                    { "skills", 0 },
                    { "relationships", 0 }
                };

                if (await result.FetchAsync())
                {
                    stats["people"] = Convert.ToInt32(result.Current["people"]);
                    stats["skills"] = Convert.ToInt32(result.Current["skills"]);
                    stats["relationships"] = Convert.ToInt32(result.Current["rels"]);
                }

                return stats;
            });
        }
    }
}
