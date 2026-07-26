using Microsoft.Extensions.Logging;
using Auricrux.Shared.Services;
using Auricrux.Shared.Models;

namespace Auricrux.Mobile;

public static class MauiProgram
{
	// Auricrux standalone backend — independent of FCA Ecosystem
	private const string DefaultApiEndpoint = "http://localhost:5000";

	private static string ResolveApiEndpoint()
	{
		var configured = Environment.GetEnvironmentVariable("AURICRUX_API_ENDPOINT")
			?? Environment.GetEnvironmentVariable("AURICRUX__API_ENDPOINT");

		if (!string.IsNullOrWhiteSpace(configured))
		{
			return configured.Trim();
		}

		return DefaultApiEndpoint;
	}

	public static MauiApp CreateMauiApp()
	{
		var auricruxConfig = new AuricruxConfig
		{
			ApiEndpoint = ResolveApiEndpoint(),
			DefaultThinkingMode = ThinkingMode.Auto,
			DefaultSearchScope = SearchScope.Both,
			EnableAutoSpeak = false,
			TimeoutSeconds = 30,
			EnableLogging = true
		};

		var builder = MauiApp.CreateBuilder();
		
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			.Services
			.AddLogging(logging =>
			{
#if DEBUG
				logging.AddDebug();
#endif
				logging.SetMinimumLevel(LogLevel.Information);
			})
			.AddSingleton(auricruxConfig)
			.AddSingleton<MainPage>()
			.AddSingleton<MainPageViewModel>()
			.AddHttpClient<AuricruxApiClient>()
			.ConfigureHttpClient(client =>
			{
				client.BaseAddress = new Uri(auricruxConfig.ApiEndpoint);
				client.Timeout = TimeSpan.FromSeconds(auricruxConfig.TimeoutSeconds);
			});

		builder.Services.AddSingleton<TextToSpeechService>();
		builder.Services.AddSingleton<AuricruxService>();

		return builder.Build();
	}
}

