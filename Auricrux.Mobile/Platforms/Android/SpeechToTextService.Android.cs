#if ANDROID
using Android.Content;
using Android.OS;
using Android.Speech;
using Microsoft.Extensions.Logging;

namespace Auricrux.Mobile;

public partial class SpeechToTextService
{
	private partial Task<string?> ListenPlatformAsync(CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var activity = Platform.CurrentActivity;
		if (activity is null)
		{
			return Task.FromResult<string?>(null);
		}

		if (!SpeechRecognizer.IsRecognitionAvailable(activity))
		{
			_logger.LogWarning("Speech recognition not available on this device");
			return Task.FromResult<string?>(null);
		}

		activity.RunOnUiThread(() =>
		{
			SpeechRecognizer? recognizer = null;
			try
			{
				recognizer = SpeechRecognizer.CreateSpeechRecognizer(activity);
				var listener = new RecognitionListener(
					onResults: text =>
					{
						tcs.TrySetResult(text);
						recognizer?.Destroy();
					},
					onError: code =>
					{
						_logger.LogWarning("SpeechRecognizer error {Code}", code);
						tcs.TrySetResult(null);
						recognizer?.Destroy();
					});
				recognizer.SetRecognitionListener(listener);

				var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
				intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
				intent.PutExtra(RecognizerIntent.ExtraPartialResults, false);
				intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
				recognizer.StartListening(intent);

				cancellationToken.Register(() =>
				{
					try { recognizer?.StopListening(); } catch { /* ignore */ }
					tcs.TrySetResult(null);
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to start SpeechRecognizer");
				tcs.TrySetResult(null);
				recognizer?.Destroy();
			}
		});

		return tcs.Task;
	}

	private sealed class RecognitionListener : Java.Lang.Object, IRecognitionListener
	{
		private readonly Action<string?> _onResults;
		private readonly Action<SpeechRecognizerError> _onError;

		public RecognitionListener(Action<string?> onResults, Action<SpeechRecognizerError> onError)
		{
			_onResults = onResults;
			_onError = onError;
		}

		public void OnBeginningOfSpeech() { }
		public void OnBufferReceived(byte[]? buffer) { }
		public void OnEndOfSpeech() { }
		public void OnEvent(int eventType, Bundle? params) { }
		public void OnPartialResults(Bundle? partialResults) { }
		public void OnReadyForSpeech(Bundle? params) { }
		public void OnRmsChanged(float rmsdB) { }

		public void OnError(SpeechRecognizerError error) => _onError(error);

		public void OnResults(Bundle? results)
		{
			var matches = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
			_onResults(matches is { Count: > 0 } ? matches[0] : null);
		}
	}
}
#endif
