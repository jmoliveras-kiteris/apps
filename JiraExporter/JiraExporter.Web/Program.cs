using JiraExporter.Web.Models;
using JiraExporter.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

// Pin culture so decimal/date formatting is identical on every server (local, Azure, etc.)
// Without this, Azure App Service defaults to en-US, giving "8.50" instead of "8,50" in CSVs.
var culture = CultureInfo.GetCultureInfo("es-ES");
CultureInfo.DefaultThreadCurrentCulture   = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Bind JiraSettings
builder.Services.Configure<JiraSettings>(builder.Configuration.GetSection("Jira"));

// Register caching
builder.Services.AddMemoryCache();

// Register JiraService as Singleton so the issue-key cache and
// worklog result cache persist across requests.
builder.Services.AddHttpClient<JiraService>();
builder.Services.AddSingleton<JiraService>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var http    = factory.CreateClient(nameof(JiraService));
    var opts    = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JiraSettings>>();
    var cache   = sp.GetRequiredService<IMemoryCache>();
    return new JiraService(http, opts, cache);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();
app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
