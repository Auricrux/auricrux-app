using Microsoft.Extensions.Logging;
using Auricrux.Shared.Services;
using Auricrux.Shared.Models;

namespace Auricrux.Mobile;

public static class MauiProgram
{
	public const string ProductionApiEndpoint = "https://auricrux.futurecontractorsofamerica.com";

	private static string ResolveApiEndpoint()
	{
		var configured = Environment.GetEnvironmentVariable("AURICRUX_API_ENDPOINT")
			?? Environment.GetEnvironmentVariable("AURICRUX__API_ENDPOINT");

		if (!string.IsNullOrWhiteSpace(configured))
		{
			return configured.Trim().TrimEnd('/');
		}

		return ProductionApiEndpoint;
	}

	public static MauiApp CreateMauiApp()
	{
		var auricruxConfig = new AuricruxConfig
		{
			ApiEndpoint = ResolveApiEndpoint(),
			DefaultThinkingMode = ThinkingMode.Auto,
			DefaultSearchScope = SearchScope.Both,
			EnableAutoSpeak = false,
			TimeoutSeconds = 180,
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
			.AddSingleton<SpeechToTextService>()
			.AddSingleton<AnswerExportService>()
			.AddHttpClient<AuricruxApiClient>()
			.ConfigureHttpClient((sp, client) =>
			{
				var config = sp.GetRequiredService<AuricruxConfig>();
				client.BaseAddress = new Uri(config.ApiEndpoint.TrimEnd('/') + "/");
				client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
			});

		builder.Services.AddSingleton<TextToSpeechService>();
		builder.Services.AddSingleton<AuricruxService>();

		return builder.Build();
	}
}
