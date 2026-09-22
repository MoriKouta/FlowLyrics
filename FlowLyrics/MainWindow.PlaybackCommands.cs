using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowLyrics.Core;
using FlowLyrics.Services;

namespace FlowLyrics;

public partial class MainWindow
{
	private PlaybackCommandCoordinator _playbackCommands = null!;
	private Button? _repeatButton;
	private Canvas? _repeatDots;
	private TextBlock? _repeatOne;

	private void InitializePlaybackCommands()
	{
		_playbackCommands = new(_mediaSessionService, new AppLogger(_settingsService.AppDataDirectory));
		Grid icon = new() { Width = 21, Height = 17, IsHitTestVisible = false };
		_repeatDots = new Canvas { Width = 21, Height = 17 };
		string[] rows = ["000000100", "011111110", "010000100", "010000010", "001000010", "011111110", "001000000"];
		for (int y = 0; y < rows.Length; y++)
			for (int x = 0; x < rows[y].Length; x++)
				if (rows[y][x] == '1')
				{
					Ellipse dot = new() { Width = 1.5, Height = 1.5, Fill = Brushes.White };
					Canvas.SetLeft(dot, x * 2.3); Canvas.SetTop(dot, y * 2.3); _repeatDots.Children.Add(dot);
				}
		icon.Children.Add(_repeatDots);
		_repeatOne = new TextBlock { Text = "1", FontFamily = LocalizedUiFont.EnglishDotFont, FontSize = 7,
			HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(1, 1, 0, 0) };
		LocalizedUiFont.Technical(_repeatOne); icon.Children.Add(_repeatOne);
		_repeatButton = new Button { Name = "RepeatButton", Content = icon, Width = 30, Height = 30,
			Margin = new Thickness(2, 0, 2, 0), Padding = new Thickness(0), Style = (Style)Resources["SmallMediaButton"] };
		_repeatButton.Click += async (_, _) => await RunPlaybackCommandAsync((_, _) => _playbackCommands.CycleRepeatAsync());
		if (NextButton.Parent is Panel transport) transport.Children.Insert(transport.Children.IndexOf(NextButton) + 1, _repeatButton);
		_playbackCommands.Changed += (_, _) => UpdateRepeatButton();
		_playbackCommands.CommandFailed += (_, _) => _tray?.ShowMessage("FlowLyrics", T("Could not control the selected media player. Start playback and try again."));
		UpdateRepeatButton();
	}

	private void UpdateRepeatButton()
	{
		if (_repeatButton == null || _repeatDots == null || _repeatOne == null) return;
		MediaRepeatMode? mode = _playbackCommands.RepeatMode;
		bool active = mode is MediaRepeatMode.List or MediaRepeatMode.Track;
		_repeatButton.IsEnabled = _playbackCommands.CanRepeat && !_playbackCommands.RepeatBusy && !_playbackCommands.IsArming;
		Brush ink = active ? CreateDisplayBrush(_settings.UiColor, 1, Colors.Orange, preservePlayerUi: true, ignoreSourceAlpha: true)
			: _settings.ReverseColors ? new SolidColorBrush(Color.FromRgb(29, 32, 30)) : Brushes.White;
		foreach (Shape dot in _repeatDots.Children) dot.Fill = ink;
		_repeatDots.Opacity = active ? 1 : .72;
		_repeatOne.Foreground = ink; _repeatOne.Visibility = mode == MediaRepeatMode.Track ? Visibility.Visible : Visibility.Collapsed;
		_repeatButton.ToolTip = !_playbackCommands.CanRepeat ? T("Repeat is not supported by this player.")
			: mode == MediaRepeatMode.Track ? T("Repeat track") : mode == MediaRepeatMode.List ? T("Repeat list") : T("Repeat off");
	}
}
