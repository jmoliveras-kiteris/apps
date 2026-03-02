namespace JiraExporter.Web.Models;

public class ExportViewModel
{
    public DateTime SelectedMonth { get; set; } = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
    
    // List of all possible users to select from
    public List<string> AvailableUsers { get; set; } = new();
    
    // The users actually selected by the user
    public List<string> SelectedUsers { get; set; } = new();
}
