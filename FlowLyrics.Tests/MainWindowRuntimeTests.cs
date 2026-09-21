using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using FlowLyrics.Controls;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class MainWindowRuntimeTests
{
	[Fact]
	public void LoadedPlayer_HasControlsAndGlowPreservesSharpTextWithZeroFallback()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-main-runtime-" + Guid.NewGuid().ToString("N"));
		try
		{
			Sta(() =>
			{
				using MediaSessionService media = new(new EmptyProvider());
				MainWindow window = new(new SettingsService(directory), media);
				try
				{
					window.ShowActivated = false; window.Show(); Pump();
					foreach (string field in new[] { "_reverseColorsButton", "_personalSyncButton", "VolumeButton", "SettingsButton" })
						Assert.NotNull(Read<Button>(window, field).Parent);
					Assert.NotNull(Read<Grid>(window, "_playbackTimeline").Parent);
					AppSettings settings = Read<AppSettings>(window, "_settings");
					settings.GlowColor = "#FFFF8000"; settings.GlowStrength = 12; settings.GlowOpacity = 0.7;
					var controls = Read<List<OutlinedText>>(window, "_lineControls");
					controls.Add(new OutlinedText { Text = "Sharp lyric" });
					Invoke(window, "ApplyTextSettingsToControls");
					Assert.Null(controls[0].Effect);
					Assert.True(controls[0].HasGlow);
					Assert.Equal(12, controls[0].GlowRadius); Assert.Equal(.7, controls[0].GlowOpacity);
					settings.GlowStrength = 0; Invoke(window, "ApplyTextSettingsToControls"); Assert.False(controls[0].HasGlow);
					settings.GlowStrength = 12; settings.GlowOpacity = 0; Invoke(window, "ApplyTextSettingsToControls"); Assert.False(controls[0].HasGlow);
					foreach (FieldInfo field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
						if (field.GetValue(window) is DispatcherTimer timer) timer.Stop();
					settings.GlowStrength = 40; settings.GlowOpacity = 1;
					LyricLine[] lines = [new(TimeSpan.Zero, "Sharp words, soft light"), new(TimeSpan.FromSeconds(5), "左右の発光を確認"), new(TimeSpan.FromSeconds(10), "A bright line near the edge")];
					TrackInfo track = new("Timing study", "Artist A, Artist B, Artist C", "", TimeSpan.FromSeconds(60));
					LyricsResult lyrics = new(lines, null, "LRCLIB");
					void Set(string name, object value) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
					Set("_snapshot", new PlaybackSnapshot(track, TimeSpan.FromSeconds(5), false, DateTimeOffset.UtcNow));
					Set("_lyrics", lyrics);
					Set("_lyricsLookup", new LyricsLookupResult { Lyrics = lyrics, LrclibRecord = new() { Id = 123 }, Status = LyricsLookupStatus.LrclibAuto });
					Invoke(window, "RebuildLineControls", 3, lines, 0);
					Invoke(window, "DisplayLyricContext", lines, 1, false);
					Invoke(window, "UpdateCurrentTrackHeader", track, "FlowLyrics"); Pump();
					UiUxRuntimeTests.Capture(window, "overlay-strong-glow");
					Thickness strongMargin = Read<StackPanel>(window, "LyricsStackPanel").Margin;
					settings.GlowStrength = 0; Invoke(window, "ApplyTextSettingsToControls");
					Assert.Equal(strongMargin, Read<StackPanel>(window, "LyricsStackPanel").Margin);
					Invoke(window, "OpenSettings"); Pump();
					SettingsWindow settingsWindow = Read<SettingsWindow>(window, "_settingsWindow");
					Read<Button>(settingsWindow, "_openSyncButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
					PersonalSyncWindow editor = Read<PersonalSyncWindow>(window, "_personalSyncAdvancedWindow");
					Assert.True(editor.IsVisible);
					Read<Button>(settingsWindow, "_openSyncButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Assert.Same(editor, Read<PersonalSyncWindow>(window, "_personalSyncAdvancedWindow"));
					editor.Close(); settingsWindow.Close(); Pump();
				}
				finally
				{
					foreach (FieldInfo field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
						if (field.GetValue(window) is DispatcherTimer timer) timer.Stop();
					Read<IDisposable?>(window, "_hotkeys")?.Dispose();
					Read<IDisposable?>(window, "_tray")?.Dispose();
					Read<LyricsService>(window, "_lyricsService").Dispose();
					typeof(MainWindow).GetField("_allowClose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
					window.Close();
				}
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
