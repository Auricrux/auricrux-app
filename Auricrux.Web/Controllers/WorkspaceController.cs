using Auricrux.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Auricrux.Web.Controllers;

[ApiController]
[Route("api/workspace")]
public sealed class WorkspaceController(WorkspaceStorageService workspace) : ControllerBase
{
    [HttpGet]
    public ActionResult<WorkspaceListing> List([FromQuery] string? path = null)
        => Ok(workspace.List(path));

    [HttpPost("folders")]
    public ActionResult<WorkspaceEntry> CreateFolder([FromBody] CreateFolderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { error = "Path is required." });
        }

        return Ok(workspace.CreateFolder(request.Path.Trim()));
    }

    [HttpPost("files")]
    [RequestSizeLimit(104_857_600)]
    public async Task<ActionResult<WorkspaceEntry>> Upload(
        IFormFile file,
        [FromForm] string? folder,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "File is required." });
        }

        await using var stream = file.OpenReadStream();
        var entry = await workspace.SaveFileAsync(folder, file.FileName, stream, cancellationToken);
        return Created($"/api/workspace/files/{entry.Path}", entry);
    }

    [HttpGet("files/{*path}")]
    public IActionResult Download(string path)
    {
        var opened = workspace.OpenFile(path);
        if (opened is null) return NotFound();
        return File(opened.Value.Stream, opened.Value.ContentType, opened.Value.FileName);
    }

    [HttpDelete("{*path}")]
    public IActionResult Delete(string path)
        => workspace.Delete(path) ? NoContent() : NotFound();
}

public sealed class CreateFolderRequest
{
    public string? Path { get; set; }
}
