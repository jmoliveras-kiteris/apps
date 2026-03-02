using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var baseUrl = configuration["Jira:BaseUrl"] ?? throw new Exception("Missing Jira:BaseUrl in appsettings.json");
var email = configuration["Jira:Email"] ?? throw new Exception("Missing Jira:Email in appsettings.json");
var apiToken = configuration["Jira:ApiToken"] ?? throw new Exception("Missing Jira:ApiToken in appsettings.json");
var fromStr = configuration["Export:From"] ?? throw new Exception("Missing Export:From in appsettings.json");
var from = DateTime.Parse(fromStr);
var to = from.AddMonths(1);
var targetAuthors = configuration.GetSection("Export:TargetAuthors").Get<string[]>() ?? throw new Exception("Missing Export:TargetAuthors in appsettings.json");

using var http = new HttpClient();
http.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue(
        "Basic",
        Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{email}:{apiToken}")
        )
    );

http.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/json")
);

var rows = new List<string>
{
    "Date;Account;WorkerInComment;Issue;Hours;Comment"
};

// ---------------- STEP 1: GET UPDATED WORKLOG IDS ----------------

long since = new DateTimeOffset(from).ToUnixTimeMilliseconds();
var worklogIds = new List<long>();
var nextUrl = $"{baseUrl}/rest/api/3/worklog/updated?since={since}";

while (nextUrl != null)
{
    var updatedResponse = await http.GetStringAsync(nextUrl);
    var updatedJson = JsonDocument.Parse(updatedResponse);
    
    foreach (var v in updatedJson.RootElement.GetProperty("values").EnumerateArray())
    {
        worklogIds.Add(v.GetProperty("worklogId").GetInt64());
    }

    nextUrl = updatedJson.RootElement.TryGetProperty("nextPage", out var np) ? np.GetString() : null;
}

Console.WriteLine($"🔍 Found {worklogIds.Count} updated worklogs since {from:yyyy-MM-dd}");

// ---------------- STEP 2: FETCH WORKLOG DETAILS IN BATCHES ----------------
var issueKeyMap = new Dictionary<string, string>();

foreach (var batch in worklogIds.Chunk(100))
{
    var payload = new { ids = batch };

    var response = await http.PostAsync(
        $"{baseUrl}/rest/api/3/worklog/list",
        new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        )
    );

    response.EnsureSuccessStatusCode();

    var json =
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var worklogs = json.RootElement.EnumerateArray().ToList();
    var uniqueIssueIds = worklogs.Select(w => w.GetProperty("issueId").GetString()!).Distinct().ToList();
    var missingIssueIds = uniqueIssueIds.Where(id => !issueKeyMap.ContainsKey(id)).ToList();

    if (missingIssueIds.Count != 0)
    {
        var tasks = missingIssueIds.Select(async id =>
        {
            try
            {
                var r = await http.GetAsync($"{baseUrl}/rest/api/3/issue/{id}?fields=key");

                if (r.IsSuccessStatusCode)
                {
                    var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                    return (id, key: j.RootElement.GetProperty("key").GetString()!);
                }
            }
            catch { }

            return (id, key: (string?)null);
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (id, k) in results) if (k != null) issueKeyMap[id] = k;
    }

    foreach (var wl in worklogs)
    {
        var started = DateTime.Parse(wl.GetProperty("started").GetString()!);

        if (started < from || started >= to)
            continue;

        var issueId = wl.GetProperty("issueId").GetString()!;
        var issueKey = issueKeyMap.TryGetValue(issueId, out var key) ? key : issueId;
        var seconds = wl.GetProperty("timeSpentSeconds").GetInt32();
        var hours = seconds / 3600.0;
        var authorElem = wl.GetProperty("author");
        var authorDisplayName = authorElem.GetProperty("displayName").GetString();
        var authorEmail = authorElem.TryGetProperty("emailAddress", out var ee) ? ee.GetString() : null;
        var comment = ExtractCommentText(wl);
        var workerInComment = ExtractWorkerFromComment(comment);

        // Apply filter if specified
        if (targetAuthors.Length != 0)
        {
            var isTarget = IsTargetUser(authorEmail, authorDisplayName, workerInComment, targetAuthors);
            if (!isTarget) continue;
        }

        // Output: Date; Account (Original User); Worker (Guess from comment); Issue; Hours; Comment
        rows.Add($"{started:yyyy-MM-dd};{Escape(authorDisplayName!)};{Escape(workerInComment ?? "")};{issueKey};{hours:F2};{Escape(comment)}");
    }
}

var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", $"jira-worklogs_{timestamp}.csv");
await File.WriteAllLinesAsync(downloadsPath, rows, Encoding.UTF8);

Console.WriteLine($"\n✅ Export completed. File saved: {downloadsPath}");

try
{
    Process.Start(new ProcessStartInfo(downloadsPath) { UseShellExecute = true });
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  Could not open the file automatically: {ex.Message}");
}

// ---------------- HELPERS ----------------

static bool IsTargetUser(string? email, string? name, string? worker, string[] targets)
{
    foreach (var t in targets)
    {
        // Exact email match
        if (email != null && string.Equals(email, t, StringComparison.OrdinalIgnoreCase)) return true;
        
        // Exact display name match
        if (name != null && string.Equals(name, t, StringComparison.OrdinalIgnoreCase)) return true;

        // Smarter name match: "daniel.lopez@domain" -> check if name or worker contains "daniel" and "lopez"
        var emailNamePart = t.Split('@')[0]; // "daniel.lopez" or "Antonio Parras"
        var parts = emailNamePart.Split('.', '-', '_', ' '); // ["daniel", "lopez"] or ["Antonio", "Parras"]
        
        if (MatchParts(name, parts) || MatchParts(worker, parts)) return true;
    }
    return false;
}

static bool MatchParts(string? input, string[] parts)
{
    if (string.IsNullOrWhiteSpace(input)) return false;
    var normalized = RemoveDiacritics(input).ToLower();
    return parts.All(p => normalized.Contains(RemoveDiacritics(p).ToLower()));
}


static string RemoveDiacritics(string text)
{
    return string.Concat(
        text.Normalize(NormalizationForm.FormD)
        .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != 
                      System.Globalization.UnicodeCategory.NonSpacingMark)
    ).Normalize(NormalizationForm.FormC);
}

static string ExtractCommentText(JsonElement wl)
{
    if (!wl.TryGetProperty("comment", out var c) ||
        !c.TryGetProperty("content", out var blocks))
        return "";

    var sb = new StringBuilder();
    foreach (var b in blocks.EnumerateArray())
        if (b.TryGetProperty("content", out var inner))
            foreach (var p in inner.EnumerateArray())
                if (p.TryGetProperty("text", out var t))
                    sb.Append(t.GetString()).Append(' ');

    return sb.ToString().Trim();
}

static string? ExtractWorkerFromComment(string comment)
{
    if (string.IsNullOrWhiteSpace(comment))
        return null;

    var i = comment.IndexOf("by ", StringComparison.OrdinalIgnoreCase);
    if (i >= 0)
        return comment[(i + 3)..].Trim();

    return comment.Split(' ').Length <= 3 ? comment : null;
}

static string Escape(string s) =>
    s.Contains(';') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;