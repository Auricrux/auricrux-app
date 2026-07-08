namespace Auricrux.Shared.Models;

/// <summary>
/// Thinking mode for LLM responses
/// </summary>
public enum ThinkingMode
{
    /// <summary>Quick response with minimal reasoning</summary>
    Quick,
    
    /// <summary>Balanced thinking and response time</summary>
    Auto,
    
    /// <summary>Deep analysis with extended thinking</summary>
    Deep
}

/// <summary>
/// Search scope for queries
/// </summary>
public enum SearchScope
{
    /// <summary>Search only internal documents</summary>
    Internal,
    
    /// <summary>Search public resources</summary>
    Public,
    
    /// <summary>Search both internal and public</summary>
    Both
}

/// <summary>
/// User feedback rating for chat interactions
/// </summary>
public class StarRating
{
    /// <summary>Rating from 1 to 5</summary>
    public int Stars { get; set; }

    /// <summary>Optional comment from user</summary>
    public string? Comment { get; set; }

    /// <summary>Timestamp of feedback</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request sent to the Auricrux backend
/// </summary>
public class ChatRequest
{
    /// <summary>User's query or message</summary>
    public required string Query { get; set; }

    /// <summary>Thinking mode to use for the response</summary>
    public ThinkingMode ThinkingMode { get; set; } = ThinkingMode.Auto;

    /// <summary>Search scope for the query</summary>
    public SearchScope SearchScope { get; set; } = SearchScope.Both;

    /// <summary>Conversation history for context</summary>
    public List<ChatMessage> ConversationHistory { get; set; } = new();

    /// <summary>Session identifier for tracking</summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Response from the Auricrux backend
/// </summary>
public class ChatResponse
{
    /// <summary>The AI-generated response</summary>
    public required string Content { get; set; }

    /// <summary>Thinking process (if available)</summary>
    public string? ThinkingContent { get; set; }

    /// <summary>Sources used for the response</summary>
    public List<Source> Sources { get; set; } = new();

    /// <summary>Response timestamp</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Duration of processing in milliseconds</summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>Confidence score (0-1)</summary>
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Single message in conversation history
/// </summary>
public class ChatMessage
{
    /// <summary>Message role (user or assistant)</summary>
    public required string Role { get; set; }

    /// <summary>Message content</summary>
    public required string Content { get; set; }

    /// <summary>Timestamp of the message</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Source reference for a response
/// </summary>
public class Source
{
    /// <summary>Title or name of the source</summary>
    public required string Title { get; set; }

    /// <summary>URL or identifier for the source</summary>
    public string? Url { get; set; }

    /// <summary>Relevance score (0-1)</summary>
    public double RelevanceScore { get; set; }
}

/// <summary>
/// Auricrux interaction record for tracking and analytics
/// </summary>
public class AuricruxInteraction
{
    /// <summary>Unique identifier for this interaction</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Session identifier</summary>
    public required string SessionId { get; set; }

    /// <summary>The original query</summary>
    public required string Query { get; set; }

    /// <summary>The response from Auricrux</summary>
    public required string Response { get; set; }

    /// <summary>Thinking mode used</summary>
    public ThinkingMode ThinkingMode { get; set; }

    /// <summary>Search scope used</summary>
    public SearchScope SearchScope { get; set; }

    /// <summary>User's star rating feedback</summary>
    public StarRating? Feedback { get; set; }

    /// <summary>Timestamp of interaction</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Processing time in milliseconds</summary>
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// Configuration for the Auricrux client
/// </summary>
public class AuricruxConfig
{
    /// <summary>Backend API endpoint</summary>
    public string ApiEndpoint { get; set; } = "http://localhost:5000";

    /// <summary>Default thinking mode</summary>
    public ThinkingMode DefaultThinkingMode { get; set; } = ThinkingMode.Auto;

    /// <summary>Default search scope</summary>
    public SearchScope DefaultSearchScope { get; set; } = SearchScope.Both;

    /// <summary>Enable audio/TTS by default</summary>
    public bool EnableAutoSpeak { get; set; } = false;

    /// <summary>API timeout in seconds</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Enable request logging</summary>
    public bool EnableLogging { get; set; } = true;
}
