using Microsoft.Extensions.Logging;

namespace Auricrux.Mobile;

/// <summary>
/// Generates a markdown/text file from an answer and opens the system share sheet.
/// </summary>
public class AnswerExportService
{
	private readonly ILogger<AnswerExportService> _logger;

	public AnswerExportService(ILogger<AnswerExportService> logger)
	{
		_logger = logger;
	}

	public async Task ShareAnswerAsync(string question, string answer, CancellationToken cancellationToken = default)
	{
		try
		{
			var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
			var fileName = $"auricrux-answer-{stamp}.md";
			var path = Path.Combine(FileSystem.CacheDirectory, fileName);
			var body =
				$"# Auricrux Construction Answer\n\n" +
				$"**Generated:** {DateTime.UtcNow:u}\n\n" +
				$"## Question\n\n{question}\n\n" +
				$"## Answer\n\n{answer}\n";

			await File.WriteAllTextAsync(path, body, cancellationToken);

			await Share.Default.RequestAsync(new ShareFileRequest
			{
				Title = "Share Auricrux answer",
				File = new ShareFile(path)
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to export answer");
			throw;
		}
	}
}
