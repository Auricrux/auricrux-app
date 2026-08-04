using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Auricrux.Web.Services;

/// <summary>
/// Makes it obvious which Auricrux package a running host is serving
/// (version stamp, build time, DLL/corpus hashes, suite target, manifest + ledger linkage).
/// </summary>
public sealed class PackageIdentityService(
    IWebHostEnvironment env,
    IConfiguration config,
    ConstructionIntelligenceService intelligence,
    ILogger<PackageIdentityService> logger)
{
    private readonly object _gate = new();
    private PackageIdentitySnapshot? _cached;

    public PackageIdentitySnapshot GetIdentity(string? requestHost = null)
    {
        lock (_gate)
        {
            _cached ??= BuildIdentity(requestHost);
            if (!string.IsNullOrWhiteSpace(requestHost) &&
                string.IsNullOrWhiteSpace(_cached.HostReported))
            {
                _cached.HostReported = requestHost;
            }

            return _cached;
        }
    }

    public void InvalidateCache()
    {
        lock (_gate) { _cached = null; }
    }

    private PackageIdentitySnapshot BuildIdentity(string? requestHost)
    {
        var stamp = ReadStampFile();
        var asm = typeof(PackageIdentityService).Assembly;
        var dllPath = asm.Location;
        var dllSha = TrySha256(dllPath);
        var dllFileVer = asm.GetName().Version?.ToString() ?? "";
        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? dllFileVer;
        var fileVer = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? dllFileVer;

        var corpusPath = Path.Combine(env.ContentRootPath, "Data", "construction-corpus.json");
        if (!File.Exists(corpusPath))
        {
            corpusPath = Path.Combine(AppContext.BaseDirectory, "Data", "construction-corpus.json");
        }

        var corpusSha = TrySha256(corpusPath);
        var corpusEntries = intelligence.CorpusEntryCount;

        var (manifestPath, evalStatus, modelId, ggufReport) = ReadManifestHints();

        var packageVersion = !string.IsNullOrWhiteSpace(stamp?.PackageVersion)
            ? stamp!.PackageVersion!
            : (config["Auricrux:PackageVersion"] ?? "1.3.0");

        var buildUtc = !string.IsNullOrWhiteSpace(stamp?.BuildTimestampUtc)
            ? stamp!.BuildTimestampUtc!
            : (File.Exists(dllPath)
                ? File.GetLastWriteTimeUtc(dllPath).ToString("o")
                : DateTime.UtcNow.ToString("o"));

        var host = requestHost
                   ?? config["Auricrux:PublicHost"]
                   ?? Environment.GetEnvironmentVariable("AURICRUX_PUBLIC_HOST")
                   ?? "";

        var primaryModel = config["Auricrux:PrimaryModel"] ?? "auricrux-fca";
        var ollamaRaw = (config["Auricrux:OllamaUrl"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        var ollamaHost = TryHostOnly(ollamaRaw);

        var identity = new PackageIdentitySnapshot
        {
            PackageVersion = packageVersion,
            BuildTimestampUtc = buildUtc,
            DllFileVersion = fileVer,
            DllInformationalVersion = infoVer,
            DllSha256 = dllSha,
            CorpusSha256 = corpusSha,
            CorpusEntries = corpusEntries,
            CorpusPath = Relativize(corpusPath),
            SuiteTarget = stamp?.SuiteTarget ?? "construction_god_suite_v1",
            SuiteVersion = stamp?.SuiteVersion ?? "v1",
            SuitePath = stamp?.SuitePath ?? "eval/construction_god_suite_v1.json",
            HostReported = host,
            ManifestPath = Relativize(manifestPath),
            ManifestModelId = modelId,
            ManifestEvalStatus = evalStatus,
            ManifestGgufGenerativeReport = ggufReport,
            EvidenceLedgerPath = "docs/runtime-proof/auricrux_evidence_ledger_v1.json",
            EvidenceLedgerJsonlPath = "docs/runtime-proof/auricrux_evidence_ledger_v1.jsonl",
            StampFilePresent = stamp is not null,
            StampSource = stamp is not null ? "package_stamp.json+runtime" : "runtime-only",
            ObservedAtUtc = DateTime.UtcNow.ToString("o"),
            PrimaryModel = primaryModel,
            ExpectedProductModel = "auricrux-fca",
            OllamaEndpointHost = ollamaHost,
            ExpandSearchTermsBuiltIn = true,
            EnvPrimaryModelSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Auricrux__PrimaryModel"))
                                 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AURICRUX_PRIMARY_MODEL")),
            EnvOllamaUrlSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Auricrux__OllamaUrl"))
                              || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AURICRUX_OLLAMA_URL")),
            EnvPublicHostSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AURICRUX_PUBLIC_HOST")),
            HostProfile = stamp?.HostProfile,
            RecipeProfile = stamp?.RecipeProfile,
            DeploymentSource = stamp?.DeploymentSource
        };

        logger.LogInformation(
            "Package identity version={Version} dllSha={Dll} corpusSha={Corpus} stamp={Stamp}",
            identity.PackageVersion,
            Truncate(identity.DllSha256),
            Truncate(identity.CorpusSha256),
            identity.StampSource);

        return identity;
    }

    private PackageStampFile? ReadStampFile()
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(env.ContentRootPath, "auricrux", "system", "package_stamp.json"),
                     Path.Combine(AppContext.BaseDirectory, "auricrux", "system", "package_stamp.json"),
                     Path.Combine(env.ContentRootPath, "Data", "package_stamp.json"),
                     Path.Combine(AppContext.BaseDirectory, "Data", "package_stamp.json")
                 })
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                return JsonSerializer.Deserialize<PackageStampFile>(
                    File.ReadAllText(candidate),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse package stamp {Path}", candidate);
            }
        }

        return null;
    }

    private static (string? path, string evalStatus, string modelId, string ggufReport) ReadManifestHints()
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "auricrux", "system", "model_manifest.json"),
                     Path.Combine(Directory.GetCurrentDirectory(), "auricrux", "system", "model_manifest.json")
                 })
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                var root = doc.RootElement;
                var modelId = root.TryGetProperty("modelId", out var mid) ? mid.GetString() ?? "" : "";
                var eval = "";
                var report = "";
                if (root.TryGetProperty("adapter", out var adapter))
                {
                    if (adapter.TryGetProperty("evalStatus", out var es))
                    {
                        eval = es.GetString() ?? "";
                    }

                    if (adapter.TryGetProperty("ggufGenerativeReport", out var gr))
                    {
                        report = gr.GetString() ?? "";
                    }
                }

                return (candidate, eval, modelId, report);
            }
            catch
            {
                return (candidate, "", "", "");
            }
        }

        return (null, "", "", "");
    }

    private static string TrySha256(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "";
        }

        try
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }

    private static string? Relativize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(path);
            var cwd = Path.GetFullPath(Directory.GetCurrentDirectory());
            if (full.StartsWith(cwd, StringComparison.OrdinalIgnoreCase))
            {
                return full[cwd.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
            }
        }
        catch
        {
            // ignore
        }

        return path.Replace('\\', '/');
    }

    private static string Truncate(string? sha) =>
        string.IsNullOrWhiteSpace(sha) ? "(none)" : sha.Length <= 12 ? sha : sha[..12] + "…";

    private static string TryHostOnly(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return string.IsNullOrWhiteSpace(uri.Host) ? url : uri.Host;
            }
        }
        catch
        {
            // ignore
        }

        return url;
    }
}

public sealed class PackageStampFile
{
    public string? PackageVersion { get; set; }
    public string? BuildTimestampUtc { get; set; }
    public string? SuiteTarget { get; set; }
    public string? SuiteVersion { get; set; }
    public string? SuitePath { get; set; }
    public string? EvidenceLedgerPath { get; set; }
    public string? HostProfile { get; set; }
    public string? RecipeProfile { get; set; }
    public string? DeploymentSource { get; set; }
}

public sealed class PackageIdentitySnapshot
{
    public string PackageVersion { get; set; } = "";
    public string BuildTimestampUtc { get; set; } = "";
    public string DllFileVersion { get; set; } = "";
    public string DllInformationalVersion { get; set; } = "";
    public string DllSha256 { get; set; } = "";
    public string CorpusSha256 { get; set; } = "";
    public int CorpusEntries { get; set; }
    public string? CorpusPath { get; set; }
    public string SuiteTarget { get; set; } = "construction_god_suite_v1";
    public string SuiteVersion { get; set; } = "v1";
    public string SuitePath { get; set; } = "eval/construction_god_suite_v1.json";
    public string HostReported { get; set; } = "";
    public string? ManifestPath { get; set; }
    public string ManifestModelId { get; set; } = "";
    public string ManifestEvalStatus { get; set; } = "";
    public string ManifestGgufGenerativeReport { get; set; } = "";
    public string EvidenceLedgerPath { get; set; } = "docs/runtime-proof/auricrux_evidence_ledger_v1.json";
    public string EvidenceLedgerJsonlPath { get; set; } = "docs/runtime-proof/auricrux_evidence_ledger_v1.jsonl";
    public bool StampFilePresent { get; set; }
    public string StampSource { get; set; } = "runtime-only";
    public string ObservedAtUtc { get; set; } = "";
    public string PrimaryModel { get; set; } = "";
    public string ExpectedProductModel { get; set; } = "auricrux-fca";
    public string OllamaEndpointHost { get; set; } = "";
    public bool ExpandSearchTermsBuiltIn { get; set; }
    public bool EnvPrimaryModelSet { get; set; }
    public bool EnvOllamaUrlSet { get; set; }
    public bool EnvPublicHostSet { get; set; }
    public string? HostProfile { get; set; }
    public string? RecipeProfile { get; set; }
    public string? DeploymentSource { get; set; }
}
