using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Auricrux.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace Auricrux.Shared.Services;

/// <summary>
/// Client for communicating with the Auricrux backend API
/// </summary>
public class AuricruxApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AuricruxConfig _config;
    private readonly ILogger<AuricruxApiClient> _logger;

    public AuricruxApiClient(HttpClient httpClient, AuricruxConfig config, ILogger<AuricruxApiClient> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        // Set base address and default headers
        _httpClient.BaseAddress = new Uri(_config.ApiEndpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Auricrux-Client/1.0");
    }

    /// <summary>
    /// Send a chat query and get a response from Auricrux backend
    /// </summary>
    public async Task<ChatResponse?> SendChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_config.EnableLogging)
            {
                _logger.LogInformation($"Sending chat query: {request.Query} (Mode: {request.ThinkingMode}, Scope: {request.SearchScope})");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/chat", request, options, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"API Error: {response.StatusCode} - {response.Content}");
                return null;
            }

            var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken);

            if (_config.EnableLogging)
            {
                _logger.LogInformation($"Received response in {chatResponse?.ProcessingTimeMs}ms");
            }

            return chatResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"HTTP Request failed: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError($"Request timeout: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Check if the backend is available
    /// </summary>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Submit user feedback/rating for an interaction
    /// </summary>
    public async Task<bool> SubmitFeedbackAsync(string interactionId, StarRating rating, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_config.EnableLogging)
            {
                _logger.LogInformation($"Submitting feedback for interaction {interactionId}: {rating.Stars} stars");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };

            var response = await _httpClient.PostAsJsonAsync($"/api/feedback/{interactionId}", rating, options, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to submit feedback: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Service for text-to-speech functionality
/// </summary>
public class TextToSpeechService
{
    private readonly ILogger<TextToSpeechService> _logger;
    private bool _isInitialized;

    public TextToSpeechService(ILogger<TextToSpeechService> logger)
    {
        _logger = logger;
        _isInitialized = false;
    }

    /// <summary>
    /// Initialize TTS (platform-specific implementation)
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (_isInitialized) return;

            // Platform-specific initialization (handled by platform implementations)
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                // Windows initialization
                _isInitialized = true;
            }
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // iOS initialization
                _isInitialized = true;
            }
            else if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                // Android initialization
                _isInitialized = true;
            }
            else if (DeviceInfo.Platform == DevicePlatform.macOS)
            {
                // macOS initialization
                _isInitialized = true;
            }

            _logger.LogInformation("TTS initialized successfully");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError($"TTS initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Speak text using platform's TTS
    /// </summary>
    public async Task SpeakAsync(string text)
    {
        try
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            if (string.IsNullOrWhiteSpace(text)) return;

            // Use MAUI's built-in TextToSpeech
            await TextToSpeech.SpeakAsync(text, new SpeechOptions
            {
                Volume = 1.0f,
                Pitch = 1.0f
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"TTS failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop current TTS playback
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            // MAUI TextToSpeech doesn't have a direct stop method
            // This would need platform-specific implementation
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to stop TTS: {ex.Message}");
        }
    }
}

/// <summary>
/// Service for managing Auricrux interactions and local state
/// </summary>
public class AuricruxService
{
    private readonly AuricruxApiClient _apiClient;
    private readonly TextToSpeechService _ttsService;
    private readonly ILogger<AuricruxService> _logger;
    private readonly List<AuricruxInteraction> _interactionHistory;
    public string SessionId { get; private set; }

    public AuricruxService(AuricruxApiClient apiClient, TextToSpeechService ttsService, ILogger<AuricruxService> logger)
    {
        _apiClient = apiClient;
        _ttsService = ttsService;
        _logger = logger;
        _interactionHistory = new List<AuricruxInteraction>();
        SessionId = Guid.NewGuid().ToString();

        _logger.LogInformation($"AuricruxService initialized with session: {SessionId}");
    }

    /// <summary>
    /// Process a user query and get a response
    /// </summary>
    public async Task<(ChatResponse? response, AuricruxInteraction? interaction)> ProcessQueryAsync(
        string query,
        ThinkingMode thinkingMode = ThinkingMode.Auto,
        SearchScope searchScope = SearchScope.Both,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;

            var chatRequest = new ChatRequest
            {
                Query = query,
                ThinkingMode = thinkingMode,
                SearchScope = searchScope,
                SessionId = SessionId,
                ConversationHistory = _interactionHistory
                    .OrderByDescending(x => x.Timestamp)
                    .Take(10)
                    .Select(x => new ChatMessage { Role = "assistant", Content = x.Response })
                    .ToList()
            };

            var response = await _apiClient.SendChatAsync(chatRequest, cancellationToken);

            if (response == null)
            {
                _logger.LogError("Failed to get response from API");
                return (null, null);
            }

            var interaction = new AuricruxInteraction
            {
                SessionId = SessionId,
                Query = query,
                Response = response.Content,
                ThinkingMode = thinkingMode,
                SearchScope = searchScope,
                ProcessingTimeMs = response.ProcessingTimeMs,
                Timestamp = startTime
            };

            _interactionHistory.Add(interaction);

            _logger.LogInformation($"Query processed successfully: {interaction.Id}");

            return (response, interaction);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing query: {ex.Message}");
            return (null, null);
        }
    }

    /// <summary>
    /// Submit feedback for an interaction
    /// </summary>
    public async Task<bool> SubmitFeedbackAsync(string interactionId, int stars, string? comment = null)
    {
        try
        {
            var interaction = _interactionHistory.FirstOrDefault(x => x.Id == interactionId);
            if (interaction == null)
            {
                _logger.LogWarning($"Interaction not found: {interactionId}");
                return false;
            }

            var rating = new StarRating { Stars = stars, Comment = comment };
            interaction.Feedback = rating;

            var success = await _apiClient.SubmitFeedbackAsync(interactionId, rating);

            if (success)
            {
                _logger.LogInformation($"Feedback submitted for {interactionId}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error submitting feedback: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get conversation history
    /// </summary>
    public IReadOnlyList<AuricruxInteraction> GetHistory() => _interactionHistory.AsReadOnly();

    /// <summary>
    /// Clear conversation history
    /// </summary>
    public void ClearHistory()
    {
        _interactionHistory.Clear();
        SessionId = Guid.NewGuid().ToString();
        _logger.LogInformation("Conversation history cleared, new session started");
    }
}
