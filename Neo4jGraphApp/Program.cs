using Neo4j.Driver;
using Neo4jGraphApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configurar Driver de Neo4j
var neo4jSection = builder.Configuration.GetSection("Neo4j");
var uri = neo4jSection.GetValue<string>("Uri") ?? "bolt://localhost:7687";
var username = neo4jSection.GetValue<string>("Username") ?? "neo4j";
var password = neo4jSection.GetValue<string>("Password") ?? "password";

builder.Services.AddSingleton<IDriver>(sp =>
{
    return GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
});

// Registrar nuestro servicio personalizado
builder.Services.AddScoped<INeo4jService, Neo4jService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
