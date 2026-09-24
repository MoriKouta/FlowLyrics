using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowLyrics.Core;
using FlowLyrics.Controls;
using FlowLyrics.Services;

namespace FlowLyrics;

public partial class MainWindow
{
	private PlaybackCommandCoordinator _playbackCommands = null!;
	private Button? _repeatButton;
	private Canvas? _repeatDots;
	private TextBlock? _repeatOne;
	private Button? _shuffleButton;
	private Canvas? _shuffleDots;

	private void InitializePlaybackCommands()
	{
		_playbackCommands = new(_mediaSessionService, new AppLogger(_settingsService.AppDataDirectory));
		double extent = PlayerControlVisuals.DotIconExtent;
		Grid icon = new() { Width = extent, Height = extent, IsHitTestVisible = false };
		_repeatDots = PlayerControlVisuals.RepeatIcon();
		icon.Children.Add(_repeatDots);
		_repeatOne = new TextBlock { Text = "1", FontFamily = LocalizedUiFont.EnglishDotFont, FontSize = 9,
			HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
		LocalizedUiFont.Technical(_repeatOne); icon.Children.Add(_repeatOne);
		_repeatButton = new Button { Name = "RepeatButton", Content = icon, Style = (Style)Resources["SmallMediaButton"] };
		_repeatButton.Click += async (_, _) => await RunPlaybackCommandAsync((_, _) => _playbackCommands.CycleRepeatAsync());
		PlayerControlVisuals.Size(_repeatButton);
		PlayerControlVisuals.TrackHover(_repeatButton, UpdateRepeatButton);
		if (NextButton.Parent is Panel transport) transport.Children.Insert(transport.Children.IndexOf(NextButton) + 1, _repeatButton);
		_shuffleDots = PlayerControlVisuals.ShuffleIcon();
		_shuffleButton = new Button { Name = "ShuffleButton", Content = _shuffleDots, Style = (Style)Resources["SmallMediaButton"] };
		_shuffleButton.Click += async (_, _) => await RunPlaybackCommandAsync((_, _) => _playbackCommands.ToggleShuffleAsync());
		PlayerControlVisuals.Size(_shuffleButton);
		PlayerControlVisuals.TrackHover(_shuffleButton, UpdateShuffleButton);
		if (PreviousButton.Parent is Panel controls) controls.Children.Insert(controls.Children.IndexOf(PreviousButton), _shuffleButton);
		_playbackCommands.Changed += (_, _) => { UpdateRepeatButton(); UpdateShuffleButton(); };
		_playbackCommands.CommandFailed += (_, _) => _tray?.ShowMessage("FlowLyrics", T("Could not control the selected media player. Start playback and try again."));
		UpdateRepeatButton();
		UpdateShuffleButton();
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
		PlayerControlVisuals.IconState(_repeatButton, _repeatDots, active);
		_repeatOne.Foreground = ink; _repeatOne.Visibility = mode == MediaRepeatMode.Track ? Visibility.Visible : Visibility.Collapsed;
		_repeatButton.ToolTip = !_playbackCommands.CanRepeat ? T("Repeat is not supported by this player.")
			: _playbackCommands.IsFallbackRepeat ? (active ? "REPEAT ONE" : "REPEAT OFF") + " · FLOWLYRICS"
			: mode == MediaRepeatMode.Track ? T("Repeat track") : mode == MediaRepeatMode.List ? T("Repeat list") : T("Repeat off");
	}

	private void UpdateShuffleButton()
	{
		if (_shuffleButton == null || _shuffleDots == null) return;
		bool active = _playbackCommands.ShuffleActive == true;
		_shuffleButton.IsEnabled = _playbackCommands.CanShuffle && !_playbackCommands.ShuffleBusy;
		Brush ink = active ? CreateDisplayBrush(_settings.UiColor, 1, Colors.Orange, preservePlayerUi: true, ignoreSourceAlpha: true)
			: _settings.ReverseColors ? new SolidColorBrush(Color.FromRgb(29, 32, 30)) : Brushes.White;
		foreach (Shape dot in _shuffleDots.Children) dot.Fill = ink;
		PlayerControlVisuals.IconState(_shuffleButton, _shuffleDots, active);
		_shuffleButton.ToolTip = !_playbackCommands.CanShuffle ? T("Shuffle is unavailable for this player.")
			: active ? T("Shuffle on") : T("Shuffle off");
	}
}
