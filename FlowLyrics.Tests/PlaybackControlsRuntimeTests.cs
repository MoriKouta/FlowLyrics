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
				MainWindow window = new(new SettingsService(directory), media);
				try
				{
					window.ShowActivated = false; window.Show(); Pump(); StopTimers();
					var commands = Read<PlaybackCommandCoordinator>(window, "_playbackCommands");
					var repeat = Read<Button>(window, "_repeatButton");
					Assert.False(repeat.IsEnabled); Assert.NotNull(repeat.Parent);
					provider.Current = new() { SessionId = "test", IsCurrentSession = true, SourceAppUserModelId = "player",
						Metadata = new("Track", "Artist", "", TimeSpan.FromSeconds(180)), Position = TimeSpan.FromSeconds(10),
						CapturedAtUtc = DateTimeOffset.UtcNow, HasTimeline = true, PlaybackState = MediaPlaybackState.Playing,
						Capabilities = new(true, true, true, true, true, true, true, true), RepeatMode = MediaRepeatMode.List };
					commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.True(repeat.IsEnabled); Assert.Equal(Visibility.Collapsed, Read<TextBlock>(window, "_repeatOne").Visibility);
					provider.Current = provider.Current with { RepeatMode = MediaRepeatMode.Track }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.Equal(Visibility.Visible, Read<TextBlock>(window, "_repeatOne").Visibility);
					provider.AcceptRepeat = false; Task<bool> change = commands.CycleRepeatAsync(); WaitUntil(() => change.IsCompleted);
					Assert.False(change.Result); Assert.Equal(Visibility.Visible, Read<TextBlock>(window, "_repeatOne").Visibility);
					provider.Current = provider.Current with { RepeatMode = MediaRepeatMode.None }; commands.Observe(new(MediaMetadataState.Stable, Session: provider.Current));
					Assert.Equal(Visibility.Collapsed, Read<TextBlock>(window, "_repeatOne").Visibility);
					Assert.DoesNotContain(Logical<Button>(window), button => button.Name == "StopAfterTrackButton");
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
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
