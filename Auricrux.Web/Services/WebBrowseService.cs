using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Auricrux.Web.Services;

/// <summary>
/// Live URL fetch + construction-oriented LLM summarize (AUX-001/002 web-browse gap).
/// Not an autonomous browser agent — operator supplies the URL; we fetch, strip, summarize.
/// </summary>
public sealed class WebBrowseService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<WebBrowseService> logger)
{
    private static readonly Regex TagStrip = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex WhitespaceCollapse = new(@"\s+", RegexOptions.Compiled);
    private const int MaxBytes = 512_000;
    private const int MaxPlainChars = 12_000;

    public async Task<WebBrowseResponse> BrowseAsync(WebBrowseRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Url))
        {
            return Fail("Url is required.");
        }

        if (!Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Fail("Only absolute http/https URLs are allowed.");
        }

        if (IsBlockedHost(uri.Host))
        {
            return Fail("That host is blocked for SSRF safety.");
        }

        string plain;
        try
        {
            var http = httpClientFactory.CreateClient(nameof(WebBrowseService));
            using var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                return Fail($"Upstream returned {(int)resp.StatusCode} {resp.ReasonPhrase}.");
            }

            var media = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (media.Length > 0
                && !media.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !media.Contains("text", StringComparison.OrdinalIgnoreCase)
                && !media.Contains("json", StringComparison.OrdinalIgnoreCase)
                && !media.Contains("xml", StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"Unsupported content-type '{media}'.");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var limited = new LimitedReadStream(stream, MaxBytes);
            using var reader = new StreamReader(limited, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var raw = await reader.ReadToEndAsync(ct);
            plain = Normalize(raw);
            if (string.IsNullOrWhiteSpace(plain))
            {
                return Fail("Page had no extractable text.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Web browse fetch failed for {Url}", uri);
            return Fail($"Fetch failed: {ex.Message}");
        }

        var question = string.IsNullOrWhiteSpace(request.Question)
            ? "Summarize the construction-relevant points for a field/project manager."
            : request.Question.Trim();

        var summary = await SummarizeAsync(uri.ToString(), plain, question, ct);
        return new WebBrowseResponse
        {
            Success = true,
            Url = uri.ToString(),
            Question = question,
            ExtractedChars = plain.Length,
            Summary = summary,
            Mode = "url-fetch-llm-summarize"
        };
    }

    private async Task<string> SummarizeAsync(string url, string plain, string question, CancellationToken ct)
    {
        var ollamaBase = (config["Auricrux:OllamaUrl"] ?? "http://127.0.0.1:11434").TrimEnd('/');
        var model = config["Auricrux:PrimaryModel"] ?? "auricrux-fca";
        var system =
            "You are Auricrux construction web browse. Summarize fetched page text for contractors. " +
            "Cite concrete requirements, numbers, and risks. If the page is unrelated, say so. Do not invent citations.";
        var user = $"URL: {url}\nQUESTION: {question}\n\nPAGE TEXT:\n{plain}";

        try
        {
            var http = httpClientFactory.CreateClient(nameof(ConstructionIntelligenceService));
            using var response = await http.PostAsJsonAsync(
                $"{ollamaBase}/api/chat",
                new
                {
                    model,
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
                return TruncateFallback(plain, question);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString()
                   ?? TruncateFallback(plain, question);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ollama summarize failed during web browse");
            return TruncateFallback(plain, question);
        }
    }

    private static string TruncateFallback(string plain, string question) =>
        $"Browse fetch succeeded (Ollama summarize unavailable). Question: {question}\n\n" +
        plain[..Math.Min(plain.Length, 1800)];

    private static string Normalize(string raw)
    {
        var noScript = Regex.Replace(raw, "<(script|style)[^>]*>.*?</\\1>", " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var text = WebUtility.HtmlDecode(TagStrip.Replace(noScript, " "));
        text = WhitespaceCollapse.Replace(text, " ").Trim();
        return text.Length <= MaxPlainChars ? text : text[..MaxPlainChars];
    }

    private static bool IsBlockedHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return true;
        var h = host.Trim().ToLowerInvariant();
        if (h is "localhost" or "metadata.google.internal" or "metadata") return true;
        if (h.EndsWith(".local", StringComparison.Ordinal) || h.EndsWith(".internal", StringComparison.Ordinal)) return true;
        if (IPAddress.TryParse(h, out var ip))
        {
            if (IPAddress.IsLoopback(ip)) return true;
            var bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && bytes.Length == 4)
            {
                // 10/8, 172.16/12, 192.168/16, 169.254/16, 127/8
                if (bytes[0] == 10) return true;
                if (bytes[0] == 127) return true;
                if (bytes[0] == 169 && bytes[1] == 254) return true;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                if (bytes[0] == 192 && bytes[1] == 168) return true;
            }
        }

        return false;
    }

    private static WebBrowseResponse Fail(string error) => new() { Success = false, Error = error };

    private sealed class LimitedReadStream(Stream inner, long maxBytes) : Stream
    {
        private long _read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_read >= maxBytes) return 0;
            var allowed = (int)Math.Min(count, maxBytes - _read);
            var n = inner.Read(buffer, offset, allowed);
            _read += n;
            return n;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public sealed class WebBrowseRequest
{
    public string Url { get; set; } = "";
    public string? Question { get; set; }
}

public sealed class WebBrowseResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Url { get; set; }
    public string? Question { get; set; }
    public int ExtractedChars { get; set; }
    public string? Summary { get; set; }
    public string? Mode { get; set; }
}
