using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;
using static FlowLyrics.Tests.PersonalSyncRuntimeTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class PlaybackControlsRuntimeTests
{
	[Fact]
	public void RepeatButton_ReflectsCapabilityExternalStateAndFailure_StopIsEditorOnly()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-playback-ui-" + Guid.NewGuid().ToString("N"));
		try
		{
			Sta(() =>
			{
				PlaybackCommandTests.Provider provider = new();
				using MediaSessionService media = new(provider);
				SettingsService settings = new(directory);
				Task.Run(() => settings.SaveAsync(new() { ShowShuffleButton = true, ShowRepeatButton = true, ShowReverseButton = true, ShowPersonalSyncButton = true })).GetAwaiter().GetResult();
				MainWindow window = new(settings, media);
				try
				{
					window.ShowActivated = false; window.Show(); Pump(); StopTimers();
					var commands = Read<PlaybackCommandCoordinator>(window, "_playbackCommands");
					var repeat = Read<Button>(window, "_repeatButton");
					var shuffle = Read<Button>(window, "_shuffleButton");
					Assert.False(shuffle.IsEnabled); Assert.Null(provider.RequestedShuffle); Assert.Null(provider.RequestedRepeat);
					var transport = (Panel)repeat.Parent;
					Assert.Equal(transport.Children.IndexOf(Read<Button>(window, "PreviousButton")) - 1, transport.Children.IndexOf(shuffle));
					Assert.False(repeat.IsEnabled); Assert.NotNull(repeat.Parent);
					provider.Current = new() { SessionId = "test", IsCurrentSession = true, SourceAppUserModelId = "player",
						Metadata = new("Track", "Artist", "", TimeSpan.FromSeconds(180)), Position = TimeSpan.FromSeconds(10),
						CapturedAtUtc = DateTimeOffset.UtcNow, HasTimeline = true, PlaybackState = MediaPlaybackState.Playing,
						Capabilities = new(true, true, true, true, true, true, true, true, true), ShuffleActive = false, RepeatMode = MediaRepeatMode.List };
					commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.True(repeat.IsEnabled); Assert.Equal(Visibility.Collapsed, Read<TextBlock>(window, "_repeatOne").Visibility);
					Assert.True(shuffle.IsEnabled);
					Assert.Null(provider.RequestedShuffle); Assert.Null(provider.RequestedRepeat);
					Assert.Equal(repeat.BorderBrush.ToString(), shuffle.BorderBrush.ToString());
					var shuffleIcon = Read<Canvas>(window, "_shuffleDots");
					Assert.Equal(Read<Canvas>(window, "_repeatDots").Width, shuffleIcon.Width);
					Assert.Equal(.72, shuffleIcon.Opacity);
					Task<bool> shuffleRequest = commands.ToggleShuffleAsync(); WaitUntil(() => shuffleRequest.IsCompleted);
					Assert.True(shuffleRequest.Result); Assert.Equal(.72, shuffleIcon.Opacity);
					provider.Current = provider.Current with { ShuffleActive = true }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.Equal(1, shuffleIcon.Opacity);
					provider.AcceptShuffle = false; shuffleRequest = commands.ToggleShuffleAsync(); WaitUntil(() => shuffleRequest.IsCompleted);
					Assert.False(shuffleRequest.Result); Assert.Equal(1, shuffleIcon.Opacity);
					provider.Current = provider.Current with { ShuffleActive = null }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.False(shuffle.IsEnabled); Assert.Equal(.72, shuffleIcon.Opacity);
					provider.Current = provider.Current with { ShuffleActive = false, Capabilities = provider.Current.Capabilities with { CanShuffle = false } }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.False(shuffle.IsEnabled); Assert.True(repeat.IsEnabled);
					UiUxRuntimeTests.Capture(window, "player-shuffle-disabled");
					provider.Current = provider.Current with { Capabilities = provider.Current.Capabilities with { CanShuffle = true } }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					provider.Current = provider.Current with { RepeatMode = MediaRepeatMode.Track }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.Equal(Visibility.Visible, Read<TextBlock>(window, "_repeatOne").Visibility);
					provider.AcceptRepeat = false; Task<bool> change = commands.CycleRepeatAsync(); WaitUntil(() => change.IsCompleted);
					Assert.False(change.Result); Assert.Equal(Visibility.Visible, Read<TextBlock>(window, "_repeatOne").Visibility);
					provider.Current = provider.Current with { RepeatMode = MediaRepeatMode.None }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.Equal(Visibility.Collapsed, Read<TextBlock>(window, "_repeatOne").Visibility);
					Set("_snapshot", new PlaybackSnapshot(new("Control study", "Artist", "", TimeSpan.FromSeconds(180)), TimeSpan.FromSeconds(20), false, DateTimeOffset.UtcNow, SourceAppUserModelId: "player"));
					Set("_lyrics", new LyricsResult([new(TimeSpan.Zero, "Control study")], null, "LRCLIB"));
					Invoke(window, "UpdatePlaybackChrome"); Invoke(window, "UpdatePersonalSyncButton");
					window.Width = 760; window.Height = 350; window.Background = System.Windows.Media.Brushes.Black; Pump();
					foreach (var mode in new[] { MediaRepeatMode.None, MediaRepeatMode.List, MediaRepeatMode.Track })
					{
						provider.Current = provider.Current with { RepeatMode = mode, ShuffleActive = mode != MediaRepeatMode.None }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current)); Pump();
						UiUxRuntimeTests.Capture(window, "player-controls-" + mode); GlowOverlayTests.CaptureNative(window, "player-controls-" + mode);
						foreach (double dpi in new[] { 120.0, 144 }) UiUxRuntimeTests.Capture(window, $"player-controls-{mode}-{dpi}dpi", dpi);
					}
					provider.Current = provider.Current with { RepeatMode = MediaRepeatMode.None }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.DoesNotContain(Logical<Button>(window), button => button.Name == "StopAfterTrackButton");
					// Resize the actual BAML control bar, including the runtime-added buttons.
					foreach (double width in new[] { 760.0, 500, 430, 390, 350, 310, 250, 216 })
					{
						window.Width = width; Pump(); Invoke(window, "UpdateChromeVisibility"); Pump();
						var buttons = new[] { "LockButton", "_shuffleButton", "PreviousButton", "PlayPauseButton", "NextButton", "_repeatButton",
							"_reverseColorsButton", "_personalSyncButton", "VolumeButton", "SettingsButton" }.Select(field => Read<Button>(window, field))
							.Where(button => button.IsVisible).Select(button => (button.Name, Bounds: new Rect(button.TranslatePoint(new Point(), window), button.RenderSize))).ToArray();
						for (int i = 0; i < buttons.Length; i++)
							for (int j = i + 1; j < buttons.Length; j++)
								Assert.False(buttons[i].Bounds.IntersectsWith(buttons[j].Bounds), $"width={width}: {buttons[i].Name} overlaps {buttons[j].Name}");
						UiUxRuntimeTests.Capture(window, "player-width-" + width);
					}
					PersonalSyncWindow editor = new(new(directory), new(new(), new(), new()), null, [], () => null, () => TimeSpan.Zero, "en-US");
					try
					{
						editor.AttachPlaybackCommands(commands); editor.ShowActivated = false; editor.Show(); Pump();
						Assert.True(Read<Button>(editor, "_stopAfterTrackButton").IsEnabled);
						Task<bool> arm = commands.ToggleStopAfterTrackAsync(); WaitUntil(() => arm.IsCompleted); Assert.True(arm.Result);
						Assert.Contains("armed", Read<Button>(editor, "_stopAfterTrackButton").ToolTip.ToString(), StringComparison.OrdinalIgnoreCase);
					}
					finally { editor.Close(); commands.Cancel(); }
				}
				finally
				{
					StopTimers(); Read<IDisposable?>(window, "_hotkeys")?.Dispose(); Read<IDisposable?>(window, "_tray")?.Dispose();
					Read<LyricsService>(window, "_lyricsService").Dispose();
					typeof(MainWindow).GetField("_allowClose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true); window.Close();
				}
				void StopTimers()
				{
					foreach (FieldInfo field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
						if (field.GetValue(window) is DispatcherTimer timer) timer.Stop();
				}
				void Set(string field, object value) => typeof(MainWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
