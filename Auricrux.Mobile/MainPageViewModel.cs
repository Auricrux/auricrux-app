using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Auricrux.Shared.Models;
using Auricrux.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Auricrux.Mobile;

public class MainPageViewModel : INotifyPropertyChanged
{
	private readonly AuricruxService _auricruxService;
	private readonly AuricruxApiClient _apiClient;
	private readonly AuricruxConfig _config;
	private readonly TextToSpeechService _ttsService;
	private readonly SpeechToTextService _sttService;
	private readonly AnswerExportService _exportService;
	private readonly ILogger<MainPageViewModel> _logger;

	private string _userInput = string.Empty;
	private bool _isLoading;
	private bool _isListening;
	private bool _isOnline;
	private string _statusMessage = "Checking connection…";
	private string _connectionLabel = "";
	private ThinkingMode _selectedThinkingMode = ThinkingMode.Auto;
	private SearchScope _selectedSearchScope = SearchScope.Both;
	private bool _autoSpeakEnabled;
	private string _lastQuestion = string.Empty;
	private string _lastAnswer = string.Empty;

	public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();
	public ObservableCollection<string> QuickPrompts { get; } = new()
	{
		"What is a sill plate?",
		"Rough estimate for a 20x30 garage slab",
		"OSHA fall protection basics",
		"Sequence for a residential roof tear-off"
	};

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

	public bool IsListening
	{
		get => _isListening;
		set { _isListening = value; OnPropertyChanged(); }
	}

	public bool IsOnline
	{
		get => _isOnline;
		set { _isOnline = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConnectionStatusText)); }
	}

	public string StatusMessage
	{
		get => _statusMessage;
		set { _statusMessage = value; OnPropertyChanged(); }
	}

	public string ConnectionLabel
	{
		get => _connectionLabel;
		set { _connectionLabel = value; OnPropertyChanged(); }
	}

	public string ConnectionStatusText => IsOnline ? "Online" : "Backend offline / warming";

	public ThinkingMode SelectedThinkingMode
	{
		get => _selectedThinkingMode;
		set { _selectedThinkingMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThinkingChipLabel)); }
	}

	public SearchScope SelectedSearchScope
	{
		get => _selectedSearchScope;
		set { _selectedSearchScope = value; OnPropertyChanged(); OnPropertyChanged(nameof(SearchChipLabel)); }
	}

	public string ThinkingChipLabel => $"Think: {SelectedThinkingMode}";
	public string SearchChipLabel => $"Scope: {SelectedSearchScope}";

	public bool AutoSpeakEnabled
	{
		get => _autoSpeakEnabled;
		set { _autoSpeakEnabled = value; OnPropertyChanged(); }
	}

	public ICommand SendMessageCommand { get; }
	public ICommand ClearHistoryCommand { get; }
	public ICommand RateUpCommand { get; }
	public ICommand RateDownCommand { get; }
	public ICommand CycleThinkingCommand { get; }
	public ICommand CycleSearchCommand { get; }
	public ICommand QuickPromptCommand { get; }
	public ICommand MicCommand { get; }
	public ICommand ShareLastCommand { get; }
	public ICommand SpeakLastCommand { get; }
	public ICommand RefreshHealthCommand { get; }

	public MainPageViewModel(
		AuricruxService auricruxService,
		AuricruxApiClient apiClient,
		AuricruxConfig config,
		TextToSpeechService ttsService,
		SpeechToTextService sttService,
		AnswerExportService exportService,
		ILogger<MainPageViewModel> logger)
	{
		_auricruxService = auricruxService;
		_apiClient = apiClient;
		_config = config;
		_ttsService = ttsService;
		_sttService = sttService;
		_exportService = exportService;
		_logger = logger;

		ConnectionLabel = _config.ApiEndpoint;

		SendMessageCommand = new AsyncRelayCommand(SendMessage);
		ClearHistoryCommand = new AsyncRelayCommand(ClearHistory);
		RateUpCommand = new AsyncRelayCommand(() => RateLastMessage(5));
		RateDownCommand = new AsyncRelayCommand(() => RateLastMessage(1));
		CycleThinkingCommand = new Command(CycleThinking);
		CycleSearchCommand = new Command(CycleSearch);
		QuickPromptCommand = new AsyncRelayCommand<string>(SendQuickPrompt);
		MicCommand = new AsyncRelayCommand(ListenMic);
		ShareLastCommand = new AsyncRelayCommand(ShareLast);
		SpeakLastCommand = new AsyncRelayCommand(SpeakLast);
		RefreshHealthCommand = new AsyncRelayCommand(CheckHealthAsync);

		Messages.Add(new ChatMessageViewModel
		{
			Role = "assistant",
			Content = "Auricrux Construction Specialist ready. Ask a field, estimate, or safety question — or tap a prompt below.",
			IsUser = false,
			Timestamp = DateTime.Now
		});
	}

	public async Task CheckHealthAsync()
	{
		StatusMessage = "Checking connection…";
		IsOnline = await _apiClient.HealthCheckAsync();
		StatusMessage = IsOnline
			? "Ready"
			: "Backend offline or model warming — wait a moment and retry";
	}

	private void CycleThinking()
	{
		SelectedThinkingMode = SelectedThinkingMode switch
		{
			ThinkingMode.Quick => ThinkingMode.Auto,
			ThinkingMode.Auto => ThinkingMode.Deep,
			_ => ThinkingMode.Quick
		};
	}

	private void CycleSearch()
	{
		SelectedSearchScope = SelectedSearchScope switch
		{
			SearchScope.Internal => SearchScope.Public,
			SearchScope.Public => SearchScope.Both,
			_ => SearchScope.Internal
		};
	}

	private async Task SendQuickPrompt(string? prompt)
	{
		if (string.IsNullOrWhiteSpace(prompt)) return;
		UserInput = prompt;
		await SendMessage();
	}

	private async Task ListenMic()
	{
		try
		{
			IsListening = true;
			StatusMessage = "Listening…";
			var text = await _sttService.ListenAsync();
			if (!string.IsNullOrWhiteSpace(text))
			{
				UserInput = text;
				StatusMessage = "Transcript ready — tap Send or edit";
			}
			else
			{
				StatusMessage = "No speech captured";
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Mic failed");
			StatusMessage = "Microphone unavailable";
		}
		finally
		{
			IsListening = false;
		}
	}

	private async Task ShareLast()
	{
		if (string.IsNullOrWhiteSpace(_lastAnswer))
		{
			StatusMessage = "No answer to share yet";
			return;
		}

		try
		{
			await _exportService.ShareAnswerAsync(_lastQuestion, _lastAnswer);
			StatusMessage = "Share sheet opened";
		}
		catch
		{
			StatusMessage = "Could not export answer";
		}
	}

	private async Task SpeakLast()
	{
		if (string.IsNullOrWhiteSpace(_lastAnswer))
		{
			StatusMessage = "No answer to speak";
			return;
		}

		await _ttsService.StopAsync();
		await _ttsService.SpeakAsync(_lastAnswer);
		StatusMessage = "Speaking answer";
	}

	private async Task SendMessage()
	{
		if (string.IsNullOrWhiteSpace(UserInput) || IsLoading) return;

		try
		{
			IsLoading = true;
			var query = UserInput.Trim();
			UserInput = string.Empty;
			_lastQuestion = query;
			StatusMessage = IsOnline ? "Thinking…" : "Sending (backend may be warming)…";

			Messages.Add(new ChatMessageViewModel
			{
				Role = "user",
				Content = query,
				IsUser = true,
				Timestamp = DateTime.Now
			});

			var (response, interaction) = await _auricruxService.ProcessQueryAsync(
				query,
				SelectedThinkingMode,
				SelectedSearchScope);

			if (response != null)
			{
				_lastAnswer = response.Content;
				Messages.Add(new ChatMessageViewModel
				{
					Role = "assistant",
					Content = response.Content,
					IsUser = false,
					Timestamp = DateTime.Now,
					InteractionId = interaction?.Id,
					ThinkingContent = response.ThinkingContent,
					ProcessingTimeMs = response.ProcessingTimeMs,
					Sources = response.Sources
				});

				IsOnline = true;
				StatusMessage = $"Completed in {response.ProcessingTimeMs}ms";

				if (AutoSpeakEnabled)
				{
					await _ttsService.StopAsync();
					await _ttsService.SpeakAsync(response.Content);
				}
			}
			else
			{
				IsOnline = await _apiClient.HealthCheckAsync();
				var detail = IsOnline
					? "Model is warming or returned an error. Try again in a moment."
					: "Cannot reach Auricrux API. Check network, then tap status to retry.";
				Messages.Add(new ChatMessageViewModel
				{
					Role = "assistant",
					Content = detail,
					IsUser = false,
					Timestamp = DateTime.Now
				});
				StatusMessage = IsOnline ? "Model error" : "Backend offline";
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error sending message");
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
		_lastAnswer = string.Empty;
		_lastQuestion = string.Empty;
		Messages.Add(new ChatMessageViewModel
		{
			Role = "assistant",
			Content = "Conversation cleared. How can I help on site?",
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
			StatusMessage = success ? (stars >= 4 ? "Thanks for the feedback" : "Feedback noted") : "Failed to submit rating";
		}
		else
		{
			StatusMessage = "No recent message to rate";
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

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

public class AsyncRelayCommand : ICommand
{
	private readonly Func<Task> _execute;
	private bool _isExecuting;

	public AsyncRelayCommand(Func<Task> execute) => _execute = execute;

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

public class AsyncRelayCommand<T> : ICommand
{
	private readonly Func<T?, Task> _execute;
	private bool _isExecuting;

	public AsyncRelayCommand(Func<T?, Task> execute) => _execute = execute;

	public event EventHandler? CanExecuteChanged;
	public bool CanExecute(object? parameter) => !_isExecuting;

	public async void Execute(object? parameter)
	{
		if (_isExecuting) return;
		try
		{
			_isExecuting = true;
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
			await _execute(parameter is T t ? t : default);
		}
		finally
		{
			_isExecuting = false;
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
