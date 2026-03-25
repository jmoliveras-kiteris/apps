namespace JiraExporter.Web.Models;

public class WorklogItem
{
    public DateTime Started { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string WorkerInComment { get; set; } = string.Empty;
    public string IssueKey { get; set; } = string.Empty;
    public string Epic { get; set; } = string.Empty;
    public double Hours { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string JiraUrl { get; set; } = string.Empty;
}

public class DashboardViewModel
{
    public DateTime DateFrom { get; set; } = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
    public DateTime DateTo { get; set; } = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1).AddDays(-1);
    
    public List<string> SelectedUsers { get; set; } = new();
    public List<string> AvailableUsers { get; set; } = new();

    public List<WorklogItem> Worklogs { get; set; } = new();
    
    // Quick filters for the UI
    public string FilterUser { get; set; } = string.Empty;
    public string FilterWorker { get; set; } = string.Empty;
    public string FilterTicket { get; set; } = string.Empty;
    public string FilterEpic { get; set; } = string.Empty;
    public string FilterComment { get; set; } = string.Empty;

    // Token echoed back as a cookie so the JS export spinner knows the download started
    public string DownloadToken { get; set; } = string.Empty;
}
