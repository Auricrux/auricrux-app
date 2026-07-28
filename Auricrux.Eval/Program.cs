using System.Text.Json;
using System.Text.Json.Serialization;
using Auricrux.Shared.Models;
using Auricrux.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auricrux.Eval;

/// <summary>
/// Real, runnable construction-specialist eval suite (AUX-017/018/019 evidence).
/// Loads the production ConstructionIntelligenceService (same corpus + prompts the API serves),
/// runs it against eval/construction_god_suite_v1.json, and scores keyword coverage per case.
/// Works with or without Ollama reachable: falls back to the deterministic corpus-grounded
/// response path (same graceful degradation the product uses) so the suite is always runnable.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var webProjectDir = Path.Combine(repoRoot, "Auricrux.Web");
        var suitePath = Path.Combine(repoRoot, "eval", "construction_god_suite_v1.json");
        var reportsDir = Path.Combine(repoRoot, "eval", "reports");
        Directory.CreateDirectory(reportsDir);

        if (!Directory.Exists(webProjectDir))
        {
            Console.Error.WriteLine($"Could not locate Auricrux.Web project under {repoRoot}");
            return 2;
        }

        if (!File.Exists(suitePath))
        {
            Console.Error.WriteLine($"Eval suite not found: {suitePath}");
            return 2;
        }

        var suite = JsonSerializer.Deserialize<EvalSuite>(
            await File.ReadAllTextAsync(suitePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to parse eval suite.");

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = webProjectDir,
            EnvironmentName = "Evaluation"
        });

        var appsettings = Path.Combine(webProjectDir, "appsettings.json");
        if (File.Exists(appsettings))
        {
            builder.Configuration.AddJsonFile(appsettings, optional: true, reloadOnChange: false);
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http", LogLevel.Error);

        builder.Services.AddHttpClient(nameof(ConstructionIntelligenceService));
        builder.Services.AddSingleton<ConstructionIntelligenceService>();

        using var app = builder.Build();
        var intelligence = app.Services.GetRequiredService<ConstructionIntelligenceService>();

        Console.WriteLine($"Auricrux construction eval suite: {suite.SuiteId}");
        Console.WriteLine($"Corpus entries loaded: {intelligence.CorpusEntryCount}");
        Console.WriteLine($"Cases: {suite.Cases.Count} | Pass threshold: {suite.PassThresholdPercent}%");
        Console.WriteLine(new string('-', 72));

        var results = new List<EvalCaseResult>();
        foreach (var testCase in suite.Cases)
        {
            var result = await RunCaseAsync(intelligence, testCase);
            results.Add(result);
            var marker = result.Passed ? "PASS" : "FAIL";
            Console.WriteLine($"[{marker}] {result.Id,-28} {result.KeywordsMatched}/{result.KeywordsTotal} keywords  ({result.Category})");
        }

        var passed = results.Count(r => r.Passed);
        var passRatePercent = results.Count == 0 ? 0 : Math.Round(100.0 * passed / results.Count, 1);
        var suitePassed = passRatePercent >= suite.PassThresholdPercent;

        Console.WriteLine(new string('-', 72));
        Console.WriteLine($"Result: {passed}/{results.Count} cases passed ({passRatePercent}%) — suite {(suitePassed ? "PASS" : "FAIL")} at >= {suite.PassThresholdPercent}% threshold");

        var report = new EvalReport(
            SuiteId: suite.SuiteId,
            RunAtUtc: DateTime.UtcNow,
            CorpusEntries: intelligence.CorpusEntryCount,
            TotalCases: results.Count,
            PassedCases: passed,
            PassRatePercent: passRatePercent,
            PassThresholdPercent: suite.PassThresholdPercent,
            SuitePassed: suitePassed,
            Cases: results);

        var reportJsonPath = Path.Combine(reportsDir, $"{suite.SuiteId}_report.json");
        await File.WriteAllTextAsync(reportJsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }));

        var reportMdPath = Path.Combine(reportsDir, $"{suite.SuiteId}_report.md");
        await File.WriteAllTextAsync(reportMdPath, BuildMarkdown(report));

        Console.WriteLine($"Report written: {reportJsonPath}");
        Console.WriteLine($"Report written: {reportMdPath}");

        return suitePassed ? 0 : 1;
    }

    private static async Task<EvalCaseResult> RunCaseAsync(ConstructionIntelligenceService intelligence, EvalCase testCase)
    {
        var search = intelligence.Search(new SearchRequest { Query = testCase.Query, Scope = SearchScope.Both });
        var thinking = await intelligence.ThinkAsync(new ThinkingRequest { Query = testCase.Query, Mode = ThinkingMode.Auto }, model: null, CancellationToken.None);
        var chat = await intelligence.ChatAsync(new ChatRequest { Query = testCase.Query }, model: null, CancellationToken.None);

        var aggregate = string.Join(" \n ", new[]
            {
                string.Join(" ", search.Results.Select(r => r.Title + " " + r.Snippet)),
                thinking.Result,
                chat.Content
            })
            .ToLowerInvariant();

        var matched = testCase.ExpectedKeywords.Count(k => aggregate.Contains(k.ToLowerInvariant(), StringComparison.Ordinal));
        var required = Math.Max(1, (int)Math.Ceiling(testCase.ExpectedKeywords.Length * 0.5));

        return new EvalCaseResult(
            Id: testCase.Id,
            Category: testCase.Category,
            Query: testCase.Query,
            KeywordsMatched: matched,
            KeywordsTotal: testCase.ExpectedKeywords.Length,
            Passed: matched >= required,
            Answer: chat.Content);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AuricruxApp.slnx"))
                || File.Exists(Path.Combine(dir.FullName, "AuricruxApp.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string BuildMarkdown(EvalReport report)
    {
        var lines = new List<string>
        {
            $"# Construction Eval Suite Report — {report.SuiteId}",
            "",
            $"Run at (UTC): {report.RunAtUtc:O}",
            $"Corpus entries: {report.CorpusEntries}",
            $"Result: **{report.PassedCases}/{report.TotalCases} cases passed ({report.PassRatePercent}%)** — suite **{(report.SuitePassed ? "PASS" : "FAIL")}** at >= {report.PassThresholdPercent}% threshold",
            "",
            "| Case | Category | Keywords | Result |",
            "|------|----------|----------|--------|"
        };

        lines.AddRange(report.Cases.Select(c =>
            $"| {c.Id} | {c.Category} | {c.KeywordsMatched}/{c.KeywordsTotal} | {(c.Passed ? "PASS" : "FAIL")} |"));

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}

public sealed class EvalSuite
{
    public string SuiteId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double PassThresholdPercent { get; set; } = 80;
    public List<EvalCase> Cases { get; set; } = [];
}

public sealed class EvalCase
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string[] ExpectedKeywords { get; set; } = [];
}

public sealed record EvalCaseResult(
    string Id,
    string Category,
    string Query,
    int KeywordsMatched,
    int KeywordsTotal,
    bool Passed,
    string Answer);

public sealed record EvalReport(
    string SuiteId,
    DateTime RunAtUtc,
    int CorpusEntries,
    int TotalCases,
    int PassedCases,
    double PassRatePercent,
    double PassThresholdPercent,
    bool SuitePassed,
    List<EvalCaseResult> Cases);
