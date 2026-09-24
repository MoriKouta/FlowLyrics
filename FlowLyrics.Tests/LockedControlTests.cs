using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class LockedControlTests
{
	[Fact]
	public void LockedHitTest_TracksVisibleControlsAndEntireSeekWidth()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-lock-" + Guid.NewGuid().ToString("N"));
		try
		{
			Sta(() =>
			{
				using MediaSessionService media = new(new EmptyProvider());
				var window = new MainWindow(new SettingsService(directory), media);
				try
				{
					window.Show(); Pump(); StopTimers();
					window.Width = 1000; window.Height = 480;
					typeof(MainWindow).GetField("_snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window,
						new PlaybackSnapshot(new("Test", "Artist", "", TimeSpan.FromSeconds(180)), TimeSpan.Zero, false, DateTimeOffset.UtcNow));
					typeof(MainWindow).GetField("_lyrics", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window,
						new LyricsResult([new(TimeSpan.Zero, "A")], null, "Test"));
					var settings = Read<AppSettings>(window, "_settings");
					settings.ShowShuffleButton = settings.ShowRepeatButton = settings.ShowReverseButton = settings.ShowPersonalSyncButton = true;
					Invoke(window, "UpdateChromeVisibility"); Pump();
					if (!Read<bool>(window, "_isLocked")) Invoke(window, "ToggleLock");
					Read<PlaybackCommandCoordinator>(window, "_playbackCommands").Observe(new(FlowLyrics.Core.MediaMetadataState.Stable, Session: new()
					{ Capabilities = new(true, true, true, true, true, true, true, true, true), RepeatMode = FlowLyrics.Core.MediaRepeatMode.None, ShuffleActive = false }));
					string[] buttons = ["_shuffleButton", "PreviousButton", "PlayPauseButton", "NextButton", "_repeatButton", "_reverseColorsButton", "_personalSyncButton", "VolumeButton", "LockButton", "SettingsButton"];
					foreach (string field in buttons)
					{
						Button button = Read<Button>(window, field); button.IsEnabled = true;
						var root = Read<Grid>(window, "HitTestRoot"); var local = root.PointFromScreen(Center(button));
						Assert.True(Hit(Center(button)), field + $" visible={button.IsVisible} point={local} root={root.RenderSize} hit={root.InputHitTest(local)?.GetType().Name} enabled={button.IsEnabled}");
						button.IsHitTestVisible = false; Assert.False(Hit(Center(button)), field + " no input"); button.IsHitTestVisible = true;
					}
					var bar = Read<Grid>(window, "ControlBar"); bar.IsEnabled = false;
					foreach (string field in buttons) Assert.False(Hit(Center(Read<Button>(window, field))), field + " disabled");
					bar.IsEnabled = true;
					Slider seek = Read<Slider>(window, "PlaybackSeekSlider"); seek.IsEnabled = true;
					foreach (double fraction in new[] { .01, .25, .5, .75, .9, .99 })
						Assert.True(Hit(seek.PointToScreen(new(seek.ActualWidth * fraction, seek.ActualHeight / 2))), "seek " + fraction);
					var transport = (Panel)Read<Button>(window, "NextButton").Parent;
					Button future = new() { Content = "test", Width = 30, Height = 30 }; transport.Children.Add(future); Pump();
					Assert.True(Hit(Center(future))); Point prior = Center(future);
					future.Visibility = Visibility.Hidden; Pump(); Assert.False(Hit(prior));
					Assert.False(Hit(Read<Grid>(window, "HitTestRoot").PointToScreen(new(150, 100))));
					UiUxRuntimeTests.Capture(window, "locked-player-controls");
				}
				finally
				{
					StopTimers(); Read<IDisposable?>(window, "_hotkeys")?.Dispose(); Read<IDisposable?>(window, "_tray")?.Dispose(); Read<LyricsService>(window, "_lyricsService").Dispose();
					typeof(MainWindow).GetField("_allowClose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true); window.Close();
				}
				bool Hit(Point screen) => (bool)(typeof(MainWindow).GetMethod("IsPointOverInteractiveControl", BindingFlags.Instance | BindingFlags.NonPublic)
					?? typeof(MainWindow).GetMethod("IsPointOverPlaybackButton", BindingFlags.Instance | BindingFlags.NonPublic))!.Invoke(window, [screen])!;
				static Point Center(FrameworkElement element) => element.PointToScreen(new(element.ActualWidth / 2, element.ActualHeight / 2));
				void StopTimers() { foreach (var field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)) if (field.GetValue(window) is DispatcherTimer timer) timer.Stop(); }
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
