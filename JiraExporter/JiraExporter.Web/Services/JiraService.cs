using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using JiraExporter.Web.Models;
using System.Collections.Concurrent;

namespace JiraExporter.Web.Services;

public class JiraService
{
    private readonly HttpClient _http;
    private readonly JiraSettings _settings;
    private readonly IMemoryCache _cache;

    // issueId → "VOL-1234", persists for the app lifetime (singleton)
    private readonly ConcurrentDictionary<string, string> _issueKeyCache = new();

    // issueId → in-flight fetch Task — deduplicates concurrent callers
    private readonly ConcurrentDictionary<string, Task<string?>> _issueKeyTasks = new();

    // Per-cacheKey semaphore — prevents cache stampede under concurrent requests
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public JiraService(HttpClient http, IOptions<JiraSettings> settings, IMemoryCache cache)
    {
        _http     = http;
        _settings = settings.Value;
        _cache    = cache;

        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Email}:{_settings.ApiToken}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public List<string> GetDefaultUsers() => _settings.DefaultTargetAuthors;

    // ─── Public entry point ───────────────────────────────────────────────────────

    public async Task<List<WorklogItem>> GetWorklogItemsAsync(DateTime from, DateTime to, string[] selectedUserNames)
    {
        // Level-2 cache: filtered result per (date range + user selection)
        // Costs nothing after the first load for this exact combo (e.g. Export button).
        var sortedUsers     = selectedUserNames.OrderBy(u => u).ToArray();
        var filteredCacheKey = $"wl|{from:yyyyMMdd}|{to:yyyyMMdd}|{string.Join(",", sortedUsers)}";

        if (_cache.TryGetValue(filteredCacheKey, out List<WorklogItem>? cached) && cached != null)
            return cached;

        var sem = _locks.GetOrAdd(filteredCacheKey, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            if (_cache.TryGetValue(filteredCacheKey, out cached) && cached != null)
                return cached;

            // Level-1 cache: raw worklogs for the date range, independent of user selection.
            // Changing the user selection only re-runs the cheap in-memory filter below —
            // the expensive Jira API calls are NOT repeated.
            var rawWorklogs = await GetRawWorklogsForPeriodAsync(from, to);

            var selectedUsers = _settings.Users
                .Where(u => selectedUserNames.Contains(u.Name))
                .ToList();

            if (!selectedUsers.Any())
            {
                _cache.Set(filteredCacheKey, new List<WorklogItem>(), TimeSpan.FromMinutes(5));
                return new List<WorklogItem>();
            }

            var toTimestamp = to.AddDays(1).AddSeconds(-1);
            var result      = FilterAndMap(rawWorklogs, from, toTimestamp, selectedUsers);

            // Resolve issue keys only for the filtered subset
            await ResolveIssueKeysAsync(result.Select(c => c.IssueKey).Distinct().ToList());

            // Replace numeric IDs with resolved keys
            foreach (var item in result)
            {
                if (_issueKeyCache.TryGetValue(item.IssueKey, out var key))
                {
                    item.JiraUrl  = $"{_settings.BaseUrl}/browse/{key}";
                    item.IssueKey = key;
                }
            }

            var ordered = result.OrderByDescending(x => x.Started).ToList();
            _cache.Set(filteredCacheKey, ordered, TimeSpan.FromMinutes(5));
            return ordered;
        }
        finally
        {
            sem.Release();
        }
    }

    // ─── Level-1: Raw worklog fetch, cached by date range only ───────────────────

    private async Task<List<RawWorklog>> GetRawWorklogsForPeriodAsync(DateTime from, DateTime to)
    {
        var rawKey = $"raw|{from:yyyyMMdd}|{to:yyyyMMdd}";

        if (_cache.TryGetValue(rawKey, out List<RawWorklog>? rawCached) && rawCached != null)
            return rawCached;

        var sem = _locks.GetOrAdd(rawKey, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            if (_cache.TryGetValue(rawKey, out rawCached) && rawCached != null)
                return rawCached;

            long since  = new DateTimeOffset(from).ToUnixTimeMilliseconds();
            var ids     = await CollectWorklogIdsAsync(since);
            var raw     = await FetchWorklogDetailsAsync(ids);

            // 10-minute TTL for raw data — longer than filtered result TTL since
            // it's the expensive part and changing users should not re-trigger it.
            _cache.Set(rawKey, raw, TimeSpan.FromMinutes(10));
            return raw;
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<List<long>> CollectWorklogIdsAsync(long since)
    {
        var ids     = new List<long>();
        var nextUrl = $"{_settings.BaseUrl}/rest/api/3/worklog/updated?since={since}";
        while (nextUrl != null)
        {
            var resp = await CallJiraAsync(() => _http.GetStringAsync(nextUrl));
            if (resp == null) break;
            using var doc = JsonDocument.Parse(resp);
            foreach (var v in doc.RootElement.GetProperty("values").EnumerateArray())
                ids.Add(v.GetProperty("worklogId").GetInt64());
            nextUrl = doc.RootElement.TryGetProperty("nextPage", out var np) ? np.GetString() : null;
        }
        return ids;
    }

    private async Task<List<RawWorklog>> FetchWorklogDetailsAsync(List<long> ids)
    {
        var bag     = new ConcurrentBag<RawWorklog>();
        var batches = ids.Chunk(100).ToList();

        await Parallel.ForEachAsync(batches, new ParallelOptions { MaxDegreeOfParallelism = 5 }, async (batch, ct) =>
        {
            var payload = new StringContent(
                JsonSerializer.Serialize(new { ids = batch }),
                Encoding.UTF8, "application/json");

            var json = await CallJiraAsync(async () =>
            {
                var r = await _http.PostAsync($"{_settings.BaseUrl}/rest/api/3/worklog/list", payload, ct);
                r.EnsureSuccessStatusCode();
                return await r.Content.ReadAsStringAsync(ct);
            });

            if (json == null) return;
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                // Snapshot all fields we'll need later so the JsonDocument can be disposed
                var authorEmail   = el.GetProperty("author").TryGetProperty("emailAddress", out var ea) ? ea.GetString() : null;
                var authorDisplay = el.GetProperty("author").SafeGetString("displayName") ?? "";
                var startedStr    = el.SafeGetString("started");
                var issueId       = el.SafeGetString("issueId") ?? "";
                var seconds       = el.TryGetProperty("timeSpentSeconds", out var ts) ? ts.GetInt32() : 0;
                var comment       = ExtractCommentText(el);
                var worker        = ExtractWorkerFromComment(comment);

                bag.Add(new RawWorklog(authorEmail, authorDisplay, startedStr, issueId, seconds, comment, worker));
            }
        });

        return bag.ToList();
    }

    // ─── Level-2: In-memory filter + map ─────────────────────────────────────────

    private List<WorklogItem> FilterAndMap(List<RawWorklog> rawWorklogs, DateTime from, DateTime toTimestamp, List<JiraUser> selectedUsers)
    {
        var result = new List<WorklogItem>();

        foreach (var wl in rawWorklogs)
        {
            if (wl.StartedStr == null) continue;
            var started = DateTime.Parse(wl.StartedStr);
            if (started < from || started > toTimestamp) continue;

            // Match by Jira author (email preferred, then display name, then diacritic-tolerant)
            var matchedUser = selectedUsers.FirstOrDefault(u =>
                string.Equals(u.Email, wl.AuthorEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Name,  wl.AuthorDisplay, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(RemoveDiacritics(u.Name), RemoveDiacritics(wl.AuthorDisplay), StringComparison.OrdinalIgnoreCase));

            // Fallback: shared account (e.g. "Kiteris Wiki SRE") — worker name is in the comment.
            // e.g. comment = "Rodrigo Díaz" → match against selected users.
            if (matchedUser == null && wl.WorkerInComment != null)
            {
                var workerNorm = RemoveDiacritics(wl.WorkerInComment).ToLowerInvariant();
                matchedUser = selectedUsers.FirstOrDefault(u =>
                {
                    // Exact diacritic-tolerant match
                    if (string.Equals(RemoveDiacritics(u.Name), workerNorm, StringComparison.OrdinalIgnoreCase))
                        return true;
                    // Partial: all words of the config name appear in the comment worker name
                    var parts = RemoveDiacritics(u.Name).ToLowerInvariant()
                                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length >= 2 && parts.All(p => workerNorm.Contains(p));
                });
            }

            if (matchedUser == null) continue;

            // IssueKey is initially the numeric issueId; resolved later by ResolveIssueKeysAsync
            result.Add(new WorklogItem
            {
                Started         = started,
                AccountName     = matchedUser.Name,
                WorkerInComment = wl.WorkerInComment ?? "",
                IssueKey        = wl.IssueId,   // may be numeric; resolved after
                Hours           = wl.Seconds / 3600.0,
                Comment         = wl.Comment,
                JiraUrl         = $"{_settings.BaseUrl}/browse/{wl.IssueId}"
            });
        }

        return result;
    }

    // ─── Issue-key resolution ─────────────────────────────────────────────────────

    private async Task ResolveIssueKeysAsync(List<string> issueIds)
    {
        var missing = issueIds.Where(id => !_issueKeyCache.ContainsKey(id)).ToList();
        if (!missing.Any()) return;

        await Parallel.ForEachAsync(missing, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (issueId, _) =>
        {
            if (_issueKeyCache.ContainsKey(issueId)) return;

            var task = _issueKeyTasks.GetOrAdd(issueId, id => Task.Run(async () =>
            {
                var url  = $"{_settings.BaseUrl}/rest/api/3/issue/{id}?fields=key";
                var resp = await CallJiraAsync(() => _http.GetStringAsync(url));
                if (resp == null) return (string?)null;
                using var doc = JsonDocument.Parse(resp);
                return doc.RootElement.SafeGetString("key");
            }));

            var key = await task;
            if (key != null) _issueKeyCache.TryAdd(issueId, key);
            _issueKeyTasks.TryRemove(new KeyValuePair<string, Task<string?>>(issueId, task));
        });
    }

    // ─── CSV export ───────────────────────────────────────────────────────────────

    public async Task<byte[]> GenerateCsvAsync(List<WorklogItem> items)
    {
        var rows = new List<string> { "Fecha;Cuenta;TrabajadorEnComentario;Ticket;Horas;Comentario" };
        foreach (var item in items)
            rows.Add($"{item.Started:yyyy-MM-dd};{Escape(item.AccountName)};{Escape(item.WorkerInComment)};{item.IssueKey};{item.Hours:F2};{Escape(item.Comment)}");

        return Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, rows)))
            .ToArray();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private static string ExtractCommentText(JsonElement wl)
    {
        if (!wl.TryGetProperty("comment", out var c) || !c.TryGetProperty("content", out var blocks)) return "";
        var sb = new StringBuilder();
        foreach (var b in blocks.EnumerateArray())
            if (b.TryGetProperty("content", out var inner))
                foreach (var p in inner.EnumerateArray())
                    if (p.TryGetProperty("text", out var t)) sb.Append(t.GetString()).Append(' ');
        return sb.ToString().Trim();
    }

    private static string? ExtractWorkerFromComment(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return null;

        string? candidate = null;
        var i = comment.LastIndexOf("by ", StringComparison.OrdinalIgnoreCase);
        if (i >= 0 && i < comment.Length - 3)
            candidate = comment[(i + 3)..].Trim();
        else
        {
            var words = comment.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1 && words.Length <= 4) candidate = comment.Trim();
        }

        if (string.IsNullOrEmpty(candidate)) return null;

        var parts = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts.Length > 4) return null;
        if (!char.IsUpper(parts[0][0])) return null;
        if (parts.Count(p => p.Length > 0 && char.IsUpper(p[0])) < 2) return null;

        return string.Join(" ", parts);
    }

    private static string RemoveDiacritics(string text) =>
        string.Concat(
            text.Normalize(NormalizationForm.FormD)
                .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                             != System.Globalization.UnicodeCategory.NonSpacingMark)
        ).Normalize(NormalizationForm.FormC);

    private static string Escape(string s) =>
        s.Contains(';') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

    private async Task<string?> CallJiraAsync(Func<Task<string>> action)
    {
        int retries = 3;
        int delay   = 1000;
        while (retries-- > 0)
        {
            try { return await action(); }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests || (int?)ex.StatusCode >= 500)
            {
                if (retries == 0) throw;
                await Task.Delay(delay);
                delay *= 2;
            }
            catch (Exception)
            {
                if (retries == 0) return null;
                await Task.Delay(500);
            }
        }
        return null;
    }
}

// ─── Supporting types ─────────────────────────────────────────────────────────

public static class JsonExtensions
{
    public static string? SafeGetString(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var p) ? p.GetString() : null;
}

/// <summary>Snapshot of a raw Jira worklog with all fields pre-extracted.</summary>
internal sealed record RawWorklog(
    string?  AuthorEmail,
    string   AuthorDisplay,
    string?  StartedStr,
    string   IssueId,
    int      Seconds,
    string   Comment,
    string?  WorkerInComment);

/// <summary>A worklog element that survived the date + author pre-filter.</summary>
internal sealed class WorklogCandidate(JsonElement worklog, DateTime started, JiraUser user)
{
    public JsonElement Worklog { get; } = worklog;
    public DateTime Started { get; } = started;
    public JiraUser User { get; } = user;
}
