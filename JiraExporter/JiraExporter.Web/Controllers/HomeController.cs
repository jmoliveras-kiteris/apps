using Microsoft.AspNetCore.Mvc;
using JiraExporter.Web.Models;
using JiraExporter.Web.Services;

namespace JiraExporter.Web.Controllers;

public class HomeController : Controller
{
    private readonly JiraService _jiraService;

    public HomeController(JiraService jiraService)
    {
        _jiraService = jiraService;
    }

    public async Task<IActionResult> Index(DashboardViewModel model)
    {
        var defaultUsers = _jiraService.GetDefaultUsers();
        model.AvailableUsers = defaultUsers;

        // Set defaults if it's the first load
        if (model.DateFrom == default)
            model.DateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        
        if (model.DateTo == default)
            model.DateTo = model.DateFrom.AddMonths(1).AddDays(-1);

        if (model.SelectedUsers == null || !model.SelectedUsers.Any())
            model.SelectedUsers = defaultUsers;

        var targetUsers = model.SelectedUsers.ToArray();
        // Single call to get all items from the requested period
        var allWorklogs = await _jiraService.GetWorklogItemsAsync(model.DateFrom, model.DateTo, targetUsers);
        model.Worklogs = allWorklogs;

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Export(DashboardViewModel model)
    {
        var targetUsers = model.SelectedUsers?.ToArray() ?? Array.Empty<string>();
        var worklogs = await _jiraService.GetWorklogItemsAsync(model.DateFrom, model.DateTo, targetUsers);

        // Apply same filters for export
        var filtered = worklogs
            .Where(w => string.IsNullOrEmpty(model.FilterUser) || 
                       w.AccountName.Contains(model.FilterUser, StringComparison.OrdinalIgnoreCase))
            .Where(w => string.IsNullOrEmpty(model.FilterWorker) || 
                       w.WorkerInComment.Contains(model.FilterWorker, StringComparison.OrdinalIgnoreCase))
            .Where(w => string.IsNullOrEmpty(model.FilterTicket) || 
                       w.IssueKey.Contains(model.FilterTicket, StringComparison.OrdinalIgnoreCase))
            .Where(w => string.IsNullOrEmpty(model.FilterComment) || 
                       w.Comment.Contains(model.FilterComment, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var csvBytes = await _jiraService.GenerateCsvAsync(filtered);

        // Signal the browser that the file is ready — the JS export spinner polls for this cookie.
        var token = model.DownloadToken;
        if (!string.IsNullOrEmpty(token))
        {
            Response.Cookies.Append(token, "1", new Microsoft.AspNetCore.Http.CookieOptions
            {
                MaxAge  = TimeSpan.FromMinutes(1),
                Path    = "/",
                Secure  = false,   // must be readable by JS
                HttpOnly = false
            });
        }

        var fileName = $"jira-worklogs_{model.DateFrom:yyyyMMdd}_{model.DateTo:yyyyMMdd}.csv";
        return File(csvBytes, "text/csv", fileName);
    }
}
