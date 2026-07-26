using System.Globalization;
using Auricrux.Shared.Services;

namespace Auricrux.Mobile;

public partial class MainPage : ContentPage
{
	private readonly MainPageViewModel _viewModel;

	public MainPage(MainPageViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = _viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _viewModel.CheckHealthAsync();
	}
}

public class UserMessageColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (Application.Current?.Resources.TryGetValue("UserBubble", out var user) == true
		    && Application.Current.Resources.TryGetValue("AssistantBubble", out var assistant) == true
		    && value is bool isUser)
		{
			return isUser ? user : assistant;
		}

		return value is bool u && u ? Color.FromArgb("#5A320A") : Color.FromArgb("#ECE8DF");
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}

public class UserMessageTextColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is bool isUser && isUser ? Colors.White : Color.FromArgb("#1A1208");

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}

public class ProcessingTimeVisibilityConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is long time && time > 0;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}

public class OnlineColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is true ? Color.FromArgb("#2F9E44") : Color.FromArgb("#C92A2A");

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}

public class MicLabelConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is true ? "…" : "Mic";

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}
