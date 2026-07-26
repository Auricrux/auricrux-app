using Microsoft.Extensions.Logging;

namespace Auricrux.Mobile;

/// <summary>
/// Platform speech-to-text. Android uses SpeechRecognizer; other platforms return null.
/// </summary>
public partial class SpeechToTextService
{
	private readonly ILogger<SpeechToTextService> _logger;

	public SpeechToTextService(ILogger<SpeechToTextService> logger)
	{
		_logger = logger;
	}

	public bool IsListening { get; private set; }

	public async Task<string?> ListenAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var status = await Permissions.RequestAsync<Permissions.Microphone>();
			if (status != PermissionStatus.Granted)
			{
				_logger.LogWarning("Microphone permission denied");
				return null;
			}

			IsListening = true;
			return await ListenPlatformAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Speech recognition failed");
			return null;
		}
		finally
		{
			IsListening = false;
		}
	}

#if ANDROID
	private partial Task<string?> ListenPlatformAsync(CancellationToken cancellationToken);
#else
	private Task<string?> ListenPlatformAsync(CancellationToken cancellationToken)
		=> Task.FromResult<string?>(null);
#endif
}
