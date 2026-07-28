using Auricrux.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Auricrux.Tests;

/// <summary>
/// AUX-008/AUX-031: TextToSpeechService is shared by the MAUI mobile client. Outside a real
/// MAUI application host (e.g. this xunit process), platform TTS APIs are unavailable — the
/// service must degrade gracefully (no throw) rather than crash callers, exactly like the
/// production Ollama-unreachable fallback in ConstructionIntelligenceService. This proves
/// that graceful-fallback behavior for real, not mocked.
/// </summary>
public sealed class ClientServiceGracefulFallbackTests
{
    [Fact]
    public async Task TextToSpeech_initializes_and_speaks_without_throwing_outside_maui_host()
    {
        var tts = new TextToSpeechService(NullLogger<TextToSpeechService>.Instance);

        await tts.InitializeAsync();
        await tts.SpeakAsync("Verify sill plate anchor bolt spacing before decking.");
        await tts.StopAsync();

        // No exception propagated — graceful degradation confirmed. Reaching this point is the assertion.
        Assert.True(true);
    }

    [Fact]
    public async Task TextToSpeech_speak_with_empty_text_is_a_safe_no_op()
    {
        var tts = new TextToSpeechService(NullLogger<TextToSpeechService>.Instance);
        await tts.SpeakAsync(string.Empty);
        Assert.True(true);
    }
}
