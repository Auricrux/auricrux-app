using System.Text.Json;

namespace Auricrux.Web.Services;

/// <summary>
/// Document / file / folder workspace for Auricrux (ChatGPT-class attachments + project folders).
/// </summary>
public sealed class WorkspaceStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly object _gate = new();

    public WorkspaceStorageService(IWebHostEnvironment env)
    {
        _env = env;
        Directory.CreateDirectory(Root);
    }

    private string Root => Path.Combine(_env.ContentRootPath, "Data", "workspace");

    public WorkspaceListing List(string? relativePath = null)
    {
        var dir = ResolveDir(relativePath);
        var folders = Directory.GetDirectories(dir)
            .Select(d => new WorkspaceEntry("folder", Path.GetFileName(d)!, ToRelative(d), null, Directory.GetLastWriteTimeUtc(d)))
            .OrderBy(e => e.Name)
            .ToList();
        var files = Directory.GetFiles(dir)
            .Select(f => new WorkspaceEntry("file", Path.GetFileName(f)!, ToRelative(f), new FileInfo(f).Length, File.GetLastWriteTimeUtc(f)))
            .OrderBy(e => e.Name)
            .ToList();
        return new WorkspaceListing(ToRelative(dir), folders.Concat(files).ToList());
    }

    public WorkspaceEntry CreateFolder(string relativePath)
    {
        var full = ResolvePath(relativePath, mustExist: false);
        Directory.CreateDirectory(full);
        return new WorkspaceEntry("folder", Path.GetFileName(full)!, ToRelative(full), null, DateTime.UtcNow);
    }

    public async Task<WorkspaceEntry> SaveFileAsync(string? folder, string fileName, Stream content, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new InvalidOperationException("File name required.");
        }

        var dir = ResolveDir(folder);
        var full = Path.Combine(dir, safeName);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return new WorkspaceEntry("file", safeName, ToRelative(full), new FileInfo(full).Length, DateTime.UtcNow);
    }

    public (Stream Stream, string ContentType, string FileName)? OpenFile(string relativePath)
    {
        var full = ResolvePath(relativePath, mustExist: true);
        if (!File.Exists(full)) return null;
        Stream stream = File.OpenRead(full);
        return (stream, GuessContentType(full), Path.GetFileName(full));
    }

    public bool Delete(string relativePath)
    {
        var full = ResolvePath(relativePath, mustExist: true);
        if (File.Exists(full))
        {
            File.Delete(full);
            return true;
        }

        if (Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
            return true;
        }

        return false;
    }

    private string ResolveDir(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == "/" || relativePath == ".")
        {
            return Root;
        }

        var full = ResolvePath(relativePath, mustExist: false);
        Directory.CreateDirectory(full);
        return full;
    }

    private string ResolvePath(string relativePath, bool mustExist)
    {
        var cleaned = relativePath.Replace('\\', '/').TrimStart('/');
        if (cleaned.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Path traversal rejected.");
        }

        var full = Path.GetFullPath(Path.Combine(Root, cleaned));
        if (!full.StartsWith(Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path escapes workspace.");
        }

        if (mustExist && !File.Exists(full) && !Directory.Exists(full))
        {
            throw new FileNotFoundException("Workspace path not found", relativePath);
        }

        return full;
    }

    private string ToRelative(string fullPath)
    {
        var root = Path.GetFullPath(Root);
        var full = Path.GetFullPath(fullPath);
        return full.Equals(root, StringComparison.OrdinalIgnoreCase)
            ? ""
            : full[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
    }

    private static string GuessContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".svg" => "image/svg+xml",
        ".txt" => "text/plain",
        ".json" => "application/json",
        ".md" => "text/markdown",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream"
    };
}

public sealed record WorkspaceEntry(string Kind, string Name, string Path, long? SizeBytes, DateTime ModifiedUtc);
public sealed record WorkspaceListing(string Path, IReadOnlyList<WorkspaceEntry> Entries);
