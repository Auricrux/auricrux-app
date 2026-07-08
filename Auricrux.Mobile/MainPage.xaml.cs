using System.Globalization;
using Auricrux.Shared.Models;
using Auricrux.Shared.Services;
using Microsoft.Maui.Controls;

namespace Auricrux.Mobile;

public partial class MainPage : ContentPage
{
	private readonly MainPageViewModel _viewModel;
	private readonly AuricruxService _auricruxService;

	public MainPage(MainPageViewModel viewModel, AuricruxService auricruxService)
	{
		InitializeComponent();
		_viewModel = viewModel;
		_auricruxService = auricruxService;
		BindingContext = _viewModel;

		SetupUI();
	}

	private void SetupUI()
	{
		// Populate thinking mode picker
		ThinkingModePicker.ItemsSource = Enum.GetValues(typeof(ThinkingMode)).Cast<ThinkingMode>().ToList();
		ThinkingModePicker.SelectedItem = _viewModel.SelectedThinkingMode;
		ThinkingModePicker.SelectedIndexChanged += (s, e) =>
		{
			if (ThinkingModePicker.SelectedItem is ThinkingMode mode)
				_viewModel.SelectedThinkingMode = mode;
		};

		// Populate search scope picker
		SearchScopePicker.ItemsSource = Enum.GetValues(typeof(SearchScope)).Cast<SearchScope>().ToList();
		SearchScopePicker.SelectedItem = _viewModel.SelectedSearchScope;
		SearchScopePicker.SelectedIndexChanged += (s, e) =>
		{
			if (SearchScopePicker.SelectedItem is SearchScope scope)
				_viewModel.SelectedSearchScope = scope;
		};

		// Bind auto-speak toggle
		AutoSpeakCheckBox.CheckedChanged += (s, e) =>
		{
			_viewModel.AutoSpeakEnabled = e.Value;
		};

		// Bind UI controls to ViewModel
		InputEntry.SetBinding(Entry.TextProperty, new Binding("UserInput", mode: BindingMode.TwoWay));
		StatusLabel.SetBinding(Label.TextProperty, new Binding("StatusMessage", mode: BindingMode.OneWay));
		LoadingIndicator.SetBinding(ActivityIndicator.IsRunningProperty, new Binding("IsLoading", mode: BindingMode.OneWay));
		LoadingIndicator.SetBinding(ActivityIndicator.IsVisibleProperty, new Binding("IsLoading", mode: BindingMode.OneWay));

		// Bind messages collection
		MessagesCollectionView.SetBinding(CollectionView.ItemsSourceProperty, new Binding("Messages", mode: BindingMode.OneWay));

		// Set up button commands
		SendButton.Clicked += (s, e) =>
		{
			_viewModel.SendMessageCommand.Execute(null);
		};
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
	}
}

/// <summary>
/// Converter for determining message background color
/// </summary>
public class UserMessageColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool isUser)
		{
			return isUser
				? Colors.Blue
				: Colors.LightGray;
		}
		return Colors.LightGray;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}

/// <summary>
/// Converter for determining message text color
/// </summary>
public class UserMessageTextColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool isUser)
		{
			return isUser ? Colors.White : Colors.Black;
		}
		return Colors.Black;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}

/// <summary>
/// Converter for showing processing time visibility
/// </summary>
public class ProcessingTimeVisibilityConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is long time)
		{
			return time > 0;
		}
		return false;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
