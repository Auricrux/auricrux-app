using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Auricrux.Shared.Models;
using Auricrux.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Auricrux.Mobile;

/// <summary>
/// ViewModel for the main chat page
/// </summary>
public class MainPageViewModel : INotifyPropertyChanged
{
    private readonly AuricruxService _auricruxService;
    private readonly ILogger<MainPageViewModel> _logger;
    private string _userInput = string.Empty;
    private bool _isLoading = false;
    private string _statusMessage = "Ready";
    private ThinkingMode _selectedThinkingMode = ThinkingMode.Auto;
    private SearchScope _selectedSearchScope = SearchScope.Both;
    private bool _autoSpeakEnabled = false;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    public string UserInput
    {
        get => _userInput;
        set { _userInput = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public ThinkingMode SelectedThinkingMode
    {
        get => _selectedThinkingMode;
        set { _selectedThinkingMode = value; OnPropertyChanged(); }
    }

    public SearchScope SelectedSearchScope
    {
        get => _selectedSearchScope;
        set { _selectedSearchScope = value; OnPropertyChanged(); }
    }

    public bool AutoSpeakEnabled
    {
        get => _autoSpeakEnabled;
        set { _autoSpeakEnabled = value; OnPropertyChanged(); }
    }

    public ICommand SendMessageCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand RateLast5StarsCommand { get; }
    public ICommand RateLast4StarsCommand { get; }
    public ICommand RateLast3StarsCommand { get; }
    public ICommand RateLast2StarsCommand { get; }
    public ICommand RateLast1StarCommand { get; }

    public MainPageViewModel(AuricruxService auricruxService, ILogger<MainPageViewModel> logger)
    {
        _auricruxService = auricruxService;
        _logger = logger;

        SendMessageCommand = new AsyncRelayCommand(SendMessage);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistory);
        RateLast5StarsCommand = new AsyncRelayCommand(() => RateLastMessage(5));
        RateLast4StarsCommand = new AsyncRelayCommand(() => RateLastMessage(4));
        RateLast3StarsCommand = new AsyncRelayCommand(() => RateLastMessage(3));
        RateLast2StarsCommand = new AsyncRelayCommand(() => RateLastMessage(2));
        RateLast1StarCommand = new AsyncRelayCommand(() => RateLastMessage(1));

        Messages.Add(new ChatMessageViewModel
        {
            Role = "assistant",
            Content = "Hello! I'm Auricrux, your AI assistant. How can I help you today?",
            IsUser = false,
            Timestamp = DateTime.Now
        });
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;

        try
        {
            IsLoading = true;
            var query = UserInput;
            UserInput = string.Empty;
            StatusMessage = "Sending...";

            // Add user message to chat
            Messages.Add(new ChatMessageViewModel
            {
                Role = "user",
                Content = query,
                IsUser = true,
                Timestamp = DateTime.Now
            });

            // Process query
            var (response, interaction) = await _auricruxService.ProcessQueryAsync(
                query,
                SelectedThinkingMode,
                SelectedSearchScope
            );

            if (response != null)
            {
                // Add response to chat
                var responseVm = new ChatMessageViewModel
                {
                    Role = "assistant",
                    Content = response.Content,
                    IsUser = false,
                    Timestamp = DateTime.Now,
                    InteractionId = interaction?.Id,
                    ThinkingContent = response.ThinkingContent,
                    ProcessingTimeMs = response.ProcessingTimeMs,
                    Sources = response.Sources
                };
                Messages.Add(responseVm);

                StatusMessage = $"Completed in {response.ProcessingTimeMs}ms";

                // Auto-speak if enabled
                if (AutoSpeakEnabled)
                {
                    var ttsService = MauiProgram.CreateMauiApp().Services.GetRequiredService<TextToSpeechService>();
                    await ttsService.SpeakAsync(response.Content);
                }
            }
            else
            {
                Messages.Add(new ChatMessageViewModel
                {
                    Role = "assistant",
                    Content = "I'm sorry, I encountered an error processing your request. Please try again.",
                    IsUser = false,
                    Timestamp = DateTime.Now
                });
                StatusMessage = "Error occurred";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending message: {ex.Message}");
            StatusMessage = "Error sending message";
            Messages.Add(new ChatMessageViewModel
            {
                Role = "assistant",
                Content = $"Error: {ex.Message}",
                IsUser = false,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ClearHistory()
    {
        _auricruxService.ClearHistory();
        Messages.Clear();
        Messages.Add(new ChatMessageViewModel
        {
            Role = "assistant",
            Content = "Conversation cleared. How can I help you?",
            IsUser = false,
            Timestamp = DateTime.Now
        });
        StatusMessage = "Ready";
        await Task.CompletedTask;
    }

    private async Task RateLastMessage(int stars)
    {
        var lastAssistantMessage = Messages.LastOrDefault(m => !m.IsUser);
        if (lastAssistantMessage?.InteractionId != null)
        {
            var success = await _auricruxService.SubmitFeedbackAsync(lastAssistantMessage.InteractionId, stars);
            StatusMessage = success ? $"Rated {stars} star(s)" : "Failed to submit rating";
        }
        else
        {
            StatusMessage = "No recent message to rate";
        }
        await Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// ViewModel for a single chat message
/// </summary>
public class ChatMessageViewModel
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
    public string? InteractionId { get; set; }
    public string? ThinkingContent { get; set; }
    public long ProcessingTimeMs { get; set; }
    public List<Source> Sources { get; set; } = new();
}

/// <summary>
/// AsyncRelayCommand for use with async methods
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isExecuting = false;

    public AsyncRelayCommand(Func<Task> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting;

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;

        try
        {
            _isExecuting = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
