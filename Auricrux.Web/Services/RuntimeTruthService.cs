using System.Text.Json;

namespace Auricrux.Web.Services;

/// <summary>
/// Operational truth snapshot for verifying which package/model a host is serving.
/// No secrets, no absolute filesystem paths, no full internal URLs with credentials.
/// </summary>
public sealed class RuntimeTruthService(
    PackageIdentityService packageIdentity,
    BackendHealthService health,
    IConfiguration config,
    IWebHostEnvironment env,
    ILogger<RuntimeTruthService> logger)
{
    public async Task<RuntimeTruthReport> GetAsync(string? requestHost = null, CancellationToken ct = default)
    {
        var pkg = packageIdentity.GetIdentity(requestHost);
        var healthReport = await health.ProbeAsync(ct);

        var primary = string.IsNullOrWhiteSpace(healthReport.PrimaryModel)
            ? (pkg.PrimaryModel ?? config["Auricrux:PrimaryModel"] ?? "auricrux-fca")
            : healthReport.PrimaryModel;

        var hostProfile = FirstNonEmpty(
            pkg.HostProfile,
            config["Auricrux:HostProfile"],
            Environment.GetEnvironmentVariable("AURICRUX_HOST_PROFILE"),
            "product-gce");

        var recipeProfile = FirstNonEmpty(
            pkg.RecipeProfile,
            config["Auricrux:RecipeProfile"],
            Environment.GetEnvironmentVariable("AURICRUX_RECIPE_PROFILE"),
            "product_gguf_serve_v1");

        var deploymentSource = FirstNonEmpty(
            pkg.DeploymentSource,
            config["Auricrux:DeploymentSource"],
            Environment.GetEnvironmentVariable("AURICRUX_DEPLOYMENT_SOURCE"),
            pkg.StampFilePresent ? "package_stamp" : "runtime-only");

        var suiteTarget = string.IsNullOrWhiteSpace(pkg.SuiteTarget)
            ? "construction_god_suite_v1"
            : pkg.SuiteTarget;
        var suiteCompatible = suiteTarget.Equals("construction_god_suite_v1", StringComparison.OrdinalIgnoreCase)
                              || suiteTarget.StartsWith("construction_god_suite", StringComparison.OrdinalIgnoreCase);

        var fallbackActive = IsFallbackActive(healthReport.RuntimeMode, primary, config);
        var fallbackReason = DescribeFallback(fallbackActive, healthReport.RuntimeMode, primary);

        var report = new RuntimeTruthReport
        {
            SchemaVersion = 1,
            Purpose = "operational-truth-verification",
            ObservedAtUtc = DateTime.UtcNow.ToString("o"),
            ActiveModel = primary,
            ActiveModelReady = healthReport.PrimaryModelReady,
            ActivePackageVersion = pkg.PackageVersion,
            ActiveDllVersion = FirstNonEmpty(pkg.DllFileVersion, pkg.DllInformationalVersion, "(unknown)"),
            ActiveDllInformationalVersion = pkg.DllInformationalVersion,
            ActiveDllSha256 = pkg.DllSha256,
            CorpusVersion = TruncateSha(pkg.CorpusSha256),
            CorpusSha256 = pkg.CorpusSha256,
            CorpusEntries = pkg.CorpusEntries > 0 ? pkg.CorpusEntries : healthReport.CorpusEntries,
            HostProfile = hostProfile,
            HostReported = SanitizeHost(pkg.HostReported),
            RecipeProfile = recipeProfile,
            SuiteCompatibility = new SuiteCompatibilityInfo
            {
                SuiteTarget = suiteTarget,
                SuiteVersion = string.IsNullOrWhiteSpace(pkg.SuiteVersion) ? "v1" : pkg.SuiteVersion,
                CompatibleWithProductBar = suiteCompatible,
                PassThresholdPercent = 80
            },
            BuildTimestampUtc = pkg.BuildTimestampUtc,
            DeploymentSource = SanitizeDeploymentSource(deploymentSource),
            FallbackModeActive = fallbackActive,
            FallbackReason = fallbackReason,
            RuntimeMode = healthReport.RuntimeMode,
            OllamaReachable = healthReport.OllamaReachable,
            StampFilePresent = pkg.StampFilePresent,
            EnvironmentName = env.EnvironmentName,
            ManifestEvalStatus = RedactIfPath(pkg.ManifestEvalStatus),
            ManifestModelId = pkg.ManifestModelId,
            ExpandSearchTermsBuiltIn = pkg.ExpandSearchTermsBuiltIn,
            TruthEndpoint = "/api/runtime-truth"
        };

        logger.LogInformation(
            "Runtime truth model={Model} package={Package} fallback={Fallback} deploy={Deploy}",
            report.ActiveModel,
            report.ActivePackageVersion,
            report.FallbackModeActive,
            report.DeploymentSource);

        return report;
    }

    private static bool IsFallbackActive(string runtimeMode, string primaryModel, IConfiguration config)
    {
        if (config.GetValue("Auricrux:ForceCorpusFallback", false))
        {
            return true;
        }

        if (primaryModel.Contains("dev-fallback", StringComparison.OrdinalIgnoreCase)
            || primaryModel.Equals("llama3.2", StringComparison.OrdinalIgnoreCase)
            || primaryModel.StartsWith("llama3.2:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return runtimeMode.Equals("corpus-fallback", StringComparison.OrdinalIgnoreCase)
               || runtimeMode.Equals("ollama-degraded", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeFallback(bool active, string runtimeMode, string primaryModel)
    {
        if (!active)
        {
            return "none";
        }

        if (primaryModel.Contains("dev-fallback", StringComparison.OrdinalIgnoreCase))
        {
            return "dev-fallback-model-tag";
        }

        if (primaryModel.StartsWith("llama3.2", StringComparison.OrdinalIgnoreCase))
        {
            return "interim-llama3.2-primary";
        }

        if (runtimeMode.Equals("corpus-fallback", StringComparison.OrdinalIgnoreCase))
        {
            return "corpus-fallback-no-live-model";
        }

        if (runtimeMode.Equals("ollama-degraded", StringComparison.OrdinalIgnoreCase))
        {
            return "ollama-degraded-primary-not-ready";
        }

        return "fallback-active";
    }

    private static string SanitizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "";
        }

        // Strip credentials if someone stuffed a URL into hostReported.
        if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
        {
            return string.IsNullOrWhiteSpace(uri.Host) ? host : uri.Host;
        }

        if (host.Contains('@'))
        {
            return host[(host.LastIndexOf('@') + 1)..];
        }

        return host;
    }

    private static string SanitizeDeploymentSource(string source)
    {
        // Allow only short labels — never paths or URLs with query strings.
        var s = source.Trim();
        if (s.Contains('\\') || s.Contains('/') && s.Contains(':') && s.Length > 64)
        {
            return "redacted-path";
        }

        if (s.Contains('?') || s.Contains("://"))
        {
            if (Uri.TryCreate(s, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }

            return "redacted-url";
        }

        return s.Length > 64 ? s[..64] : s;
    }

    private static string RedactIfPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        if (value.Contains(":\\") || value.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase))
        {
            return "(redacted-path)";
        }

        return value;
    }

    private static string TruncateSha(string? sha) =>
        string.IsNullOrWhiteSpace(sha) ? "" : sha.Length <= 12 ? sha : sha[..12] + "…";

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v.Trim();
            }
        }

        return "";
    }
}

public sealed class RuntimeTruthReport
{
    public int SchemaVersion { get; set; }
    public string Purpose { get; set; } = "operational-truth-verification";
    public string ObservedAtUtc { get; set; } = "";
    public string ActiveModel { get; set; } = "";
    public bool ActiveModelReady { get; set; }
    public string ActivePackageVersion { get; set; } = "";
    public string ActiveDllVersion { get; set; } = "";
    public string ActiveDllInformationalVersion { get; set; } = "";
    public string ActiveDllSha256 { get; set; } = "";
    public string CorpusVersion { get; set; } = "";
    public string CorpusSha256 { get; set; } = "";
    public int CorpusEntries { get; set; }
    public string HostProfile { get; set; } = "";
    public string HostReported { get; set; } = "";
    public string RecipeProfile { get; set; } = "";
    public SuiteCompatibilityInfo SuiteCompatibility { get; set; } = new();
    public string BuildTimestampUtc { get; set; } = "";
    public string DeploymentSource { get; set; } = "";
    public bool FallbackModeActive { get; set; }
    public string FallbackReason { get; set; } = "none";
    public string RuntimeMode { get; set; } = "";
    public bool OllamaReachable { get; set; }
    public bool StampFilePresent { get; set; }
    public string EnvironmentName { get; set; } = "";
    public string ManifestEvalStatus { get; set; } = "";
    public string ManifestModelId { get; set; } = "";
    public bool ExpandSearchTermsBuiltIn { get; set; }
    public string TruthEndpoint { get; set; } = "/api/runtime-truth";
}

public sealed class SuiteCompatibilityInfo
{
    public string SuiteTarget { get; set; } = "construction_god_suite_v1";
    public string SuiteVersion { get; set; } = "v1";
    public bool CompatibleWithProductBar { get; set; }
    public int PassThresholdPercent { get; set; } = 80;
}
