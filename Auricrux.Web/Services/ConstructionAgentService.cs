using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Auricrux.Shared.Models;

namespace Auricrux.Web.Services;

/// <summary>
/// Construction agent with real tools: corpus search, web browse, calculator.
/// Not a full plugin marketplace — bounded tool loop with SSRF-safe browse + deterministic calc.
/// </summary>
public sealed class ConstructionAgentService(
    ConstructionIntelligenceService intelligence,
    WebBrowseService webBrowse,
    ConstructionCalculatorService calculator,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<ConstructionAgentService> logger)
{
    private const int MaxSteps = 4;

    public IReadOnlyList<AgentToolDescriptor> ListTools() =>
    [
        new("corpus_search", "Search the grounded construction corpus (CSI/OSHA/contracts/etc).", """{"query":"string","scope":"Internal|Public|Both"}"""),
        new("web_browse", "Fetch an https URL and summarize for construction Q&A (SSRF-guarded).", """{"url":"https://...","question":"string"}"""),
        new("concrete_volume_cy", "Slab/footing volume in cubic yards.", """{"lengthFt":number,"widthFt":number,"depthIn":number}"""),
        new("rebar_weight_lb", "Rebar weight using ASTM bar weights.", """{"pieces":number,"lengthFt":number,"barSize":number}"""),
        new("board_feet", "Lumber board-feet.", """{"thicknessIn":number,"widthIn":number,"lengthFt":number,"pieces":number}"""),
        new("percent_of", "Percent of an amount (retainage, markup).", """{"amount":number,"percent":number}"""),
        new("unit_convert", "Convert ft/m, in/mm, lb/kg, cy/cf, sf/sm.", """{"value":number,"from":"ft","to":"m"}""")
    ];

    public async Task<AgentResponse> RunAsync(AgentRequest request, string? model, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new AgentResponse { Success = false, Error = "Query is required." };
        }

        var steps = new List<AgentStep>();
        var observations = new List<string>();

        // Always ground with corpus first — construction moat.
        var searchHit = intelligence.Search(new SearchRequest { Query = request.Query, Scope = SearchScope.Both });
        steps.Add(new AgentStep
        {
            Tool = "corpus_search",
            ArgumentsJson = JsonSerializer.Serialize(new { query = request.Query, scope = "Both" }),
            Result = JsonSerializer.Serialize(new
            {
                total = searchHit.TotalResults,
                titles = searchHit.Results.Take(5).Select(r => r.Title).ToArray()
            })
        });
        observations.Add($"corpus_search: {searchHit.TotalResults} hits — " +
                         string.Join("; ", searchHit.Results.Take(3).Select(r => r.Title)));

        // Heuristic calc if the query looks quantitative.
        var heuristic = calculator.TryHeuristic(request.Query);
        if (heuristic is { Success: true })
        {
            steps.Add(new AgentStep
            {
                Tool = heuristic.Operation ?? "calc",
                ArgumentsJson = "{}",
                Result = heuristic.ToToolText()
            });
            observations.Add($"calc: {heuristic.Detail}");
        }

        // Optional LLM planner for additional tools (browse / specific calc).
        var planned = await PlanAsync(request.Query, observations, model, ct);
        foreach (var plan in planned.Take(MaxSteps - steps.Count))
        {
            ct.ThrowIfCancellationRequested();
            var result = await ExecuteToolAsync(plan, ct);
            steps.Add(new AgentStep
            {
                Tool = plan.Tool,
                ArgumentsJson = plan.ArgumentsJson,
                Result = result
            });
            observations.Add($"{plan.Tool}: {Truncate(result, 400)}");
        }

        var final = await FinalizeAsync(request.Query, observations, model, ct);
        sw.Stop();
        return new AgentResponse
        {
            Success = true,
            Query = request.Query,
            FinalAnswer = final,
            Steps = steps,
            ToolsAvailable = ListTools().Select(t => t.Name).ToList(),
            ProcessingTimeMs = (int)sw.ElapsedMilliseconds,
            Mode = "bounded-tool-loop"
        };
    }

    private async Task<List<PlannedTool>> PlanAsync(
        string query,
        IReadOnlyList<string> observations,
        string? model,
        CancellationToken ct)
    {
        // Fast path: URL in query → browse
        var urlMatch = Regex.Match(query, @"https?://[^\s)]+", RegexOptions.IgnoreCase);
        if (urlMatch.Success)
        {
            return
            [
                new PlannedTool("web_browse", JsonSerializer.Serialize(new
                {
                    url = urlMatch.Value.TrimEnd('.', ',', ';'),
                    question = query
                }))
            ];
        }

        var system = """
            You are Auricrux construction agent planner. Reply with ONLY a JSON array of 0-2 tool calls.
            Each item: {"tool":"<name>","args":{...}}.
            Tools: web_browse, concrete_volume_cy, rebar_weight_lb, board_feet, percent_of, unit_convert.
            Do NOT call corpus_search (already done). If no extra tool needed, reply [].
            """;
        var user = $"QUERY:\n{query}\n\nOBSERVATIONS:\n{string.Join("\n", observations)}";
        try
        {
            var raw = await CompleteAsync(system, user, model, ct);
            var json = ExtractJsonArray(raw);
            if (json is null) return [];
            using var doc = JsonDocument.Parse(json);
            var list = new List<PlannedTool>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var tool = el.TryGetProperty("tool", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(tool)) continue;
                var args = el.TryGetProperty("args", out var a) ? a.GetRawText() : "{}";
                list.Add(new PlannedTool(tool!, args));
            }

            return list;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Agent planner failed; continuing with heuristic tools only");
            return [];
        }
    }

    private async Task<string> ExecuteToolAsync(PlannedTool plan, CancellationToken ct)
    {
        try
        {
            using var argsDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(plan.ArgumentsJson) ? "{}" : plan.ArgumentsJson);
            var root = argsDoc.RootElement;

            switch (plan.Tool.ToLowerInvariant())
            {
                case "web_browse":
                {
                    var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
                    var question = root.TryGetProperty("question", out var q) ? q.GetString() : null;
                    var browse = await webBrowse.BrowseAsync(new WebBrowseRequest { Url = url ?? "", Question = question }, ct);
                    return JsonSerializer.Serialize(browse);
                }
                case "concrete_volume_cy":
                case "rebar_weight_lb":
                case "board_feet":
                case "percent_of":
                {
                    var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in root.EnumerateObject())
                    {
                        if (p.Value.ValueKind == JsonValueKind.Number)
                        {
                            dict[p.Name] = p.Value.GetDouble();
                        }
                    }

                    return calculator.Evaluate(plan.Tool, dict).ToToolText();
                }
                case "unit_convert":
                {
                    var value = root.GetProperty("value").GetDouble();
                    var from = root.GetProperty("from").GetString() ?? "ft";
                    var to = root.GetProperty("to").GetString() ?? "m";
                    return calculator.ConvertUnits(value, from, to).ToToolText();
                }
                case "corpus_search":
                {
                    var query = root.TryGetProperty("query", out var qe) ? qe.GetString() ?? "" : "";
                    var scope = SearchScope.Both;
                    if (root.TryGetProperty("scope", out var sc) && Enum.TryParse<SearchScope>(sc.GetString(), true, out var parsed))
                    {
                        scope = parsed;
                    }

                    var search = intelligence.Search(new SearchRequest { Query = query, Scope = scope });
                    return JsonSerializer.Serialize(search);
                }
                default:
                    return JsonSerializer.Serialize(new { success = false, error = $"Unknown tool '{plan.Tool}'." });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private async Task<string> FinalizeAsync(string query, IReadOnlyList<string> observations, string? model, CancellationToken ct)
    {
        var system = """
            You are Auricrux construction agent. Using the tool observations, answer the user with field-grade precision.
            Cite tool results (numbers, corpus titles, browse summary). Do not invent OSHA/code section numbers.
            """;
        var user = $"USER QUERY:\n{query}\n\nTOOL OBSERVATIONS:\n{string.Join("\n", observations)}";
        try
        {
            return await CompleteAsync(system, user, model, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Agent finalize failed; returning observation summary");
            return "Agent tools completed. Summary:\n" + string.Join("\n", observations);
        }
    }

    private async Task<string> CompleteAsync(string system, string user, string? model, CancellationToken ct)
    {
        var ollamaBase = (config["Auricrux:OllamaUrl"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        var selected = string.IsNullOrWhiteSpace(model)
            ? (config["Auricrux:PrimaryModel"] ?? "auricrux-fca")
            : model;
        var http = httpClientFactory.CreateClient(nameof(ConstructionIntelligenceService));
        using var response = await http.PostAsJsonAsync(
            $"{ollamaBase}/api/chat",
            new
            {
                model = selected,
                stream = false,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            },
            ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Ollama returned {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.GetProperty("message").GetProperty("content").GetString()
               ?? throw new InvalidOperationException("Empty Ollama content");
    }

    private static string? ExtractJsonArray(string raw)
    {
        var start = raw.IndexOf('[');
        var end = raw.LastIndexOf(']');
        if (start < 0 || end <= start) return null;
        return raw[start..(end + 1)];
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private sealed record PlannedTool(string Tool, string ArgumentsJson);
}

public sealed class AgentRequest
{
    public string Query { get; set; } = "";
}

public sealed class AgentResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Query { get; set; }
    public string? FinalAnswer { get; set; }
    public List<AgentStep> Steps { get; set; } = [];
    public List<string> ToolsAvailable { get; set; } = [];
    public int ProcessingTimeMs { get; set; }
    public string? Mode { get; set; }
}

public sealed class AgentStep
{
    public string Tool { get; set; } = "";
    public string ArgumentsJson { get; set; } = "{}";
    public string Result { get; set; } = "";
}

public sealed record AgentToolDescriptor(string Name, string Description, string ArgsSchema);
