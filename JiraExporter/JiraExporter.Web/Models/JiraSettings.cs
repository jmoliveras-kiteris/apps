namespace JiraExporter.Web.Models;

public class JiraUser
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class JiraSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public List<JiraUser> Users { get; set; } = new();
    
    // Legacy support for older code if needed
    public List<string> DefaultTargetAuthors => Users.Select(u => u.Name).ToList();
}
