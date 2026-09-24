using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
public sealed class TimelinePresentationTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Repeat_ResetsOnlyPresentation_InTheSnapshotUpdate(bool plain)
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-loop-ui-" + Guid.NewGuid().ToString("N"));
		try
		{
			using TimelineLoopTests.Fixture f = new(179.7, MediaRepeatMode.Track);
			await f.Stabilize();
			var initial = (await f.Media.GetSnapshotAsync())!;
			var lines = Enumerable.Range(0, 60).Select(i => new LyricLine(TimeSpan.FromSeconds(i * 3), "Line " + i)).ToArray();
			LyricsResult lyrics = plain ? new([], string.Join("\n", lines.Select(l => l.Text)), "LRCLIB") : new(lines, null, "LRCLIB");
			LyricsLookupResult lookup = new() { Lyrics = lyrics, LrclibRecord = new() { Id = 17 }, SelectedManually = true };
			PersonalSyncContext context = PersonalSyncIdentity.Create(initial, lookup);
			PersonalSyncStore store = new(directory);
			var profile = await store.UpsertAsync(new() { Track = context.Track, Source = context.Source, Lyrics = context.Lyrics,
				Mode = PersonalSyncMode.Advanced, OffsetSeconds = 1, Anchors = [new() { PlaybackSeconds = 20, LyricsSeconds = 18 }],
				Segments = [new() { Type = PersonalSyncSegmentType.Hold, PlaybackStartSeconds = 30, PlaybackEndSeconds = 35, LyricsTimeSeconds = 28 }] });
			string saved = await File.ReadAllTextAsync(store.FilePath);
			Sta(() =>
			{
				MainWindow window = new(new SettingsService(directory), f.Media);
				void Set(string field, object? value) => typeof(MainWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
				void StopTimers() { foreach (var field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)) if (field.GetValue(window) is DispatcherTimer t) t.Stop(); }
				try
				{
					Set("_activeTrackKey", initial.Track.CacheKey); Set("_snapshot", initial); Set("_lyrics", lyrics); Set("_lyricsLookup", lookup);
					Set("_lyricsReady", true); Set("_plainLyricsScrollMode", plain); Set("_personalSyncActiveProfile", profile);
					Set("_personalSyncContextKey", context.Track.StableTrackKey + "|" + context.Source.StableSourceKey + "|" + context.Lyrics.Key);
					var settings = Read<AppSettings>(window, "_settings"); settings.GlobalLyricsOffsetMs = 3000; settings.ShowAllLyrics = false; settings.PlainLyricsAutoScroll = true;
					window.ShowActivated = false; window.Show(); Pump(); StopTimers();
					Invoke(window, "RenderLyrics"); Pump();
					var scroll = Read<ScrollViewer>(window, "LyricsScrollViewer"); scroll.ScrollToBottom(); Pump();
					Set("_plainLyricsAutoScrollAnchorActive", true); Set("_plainLyricsUserScrollPaused", true);
					// Queue work from the ending cycle; it must not overwrite the new scroll.
					if (!plain) Invoke(window, "CenterActiveLine", 59, true);
					object? cancellation = Read<object?>(window, "_lyricsCancellation");
					long cacheRevision = Read<LyricsService>(window, "_lyricsService").CacheRevision;
					f.Sample(0.2);
					Task polling = (Task)typeof(MainWindow).GetMethod("PollMediaAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
					WaitUntil(() => polling.IsCompleted); polling.GetAwaiter().GetResult();
					Assert.InRange(Read<Slider>(window, "PlaybackSeekSlider").Value, 0, .01);
					Assert.Equal("0:00", Read<TextBlock>(window, "_playbackPositionText").Text);
					Assert.False(Read<bool>(window, "_plainLyricsAutoScrollAnchorActive"));
					Assert.False(Read<bool>(window, "_plainLyricsUserScrollPaused"));
					if (!plain)
					{
						Assert.Equal(0, Read<int>(window, "_lastLineIndex"));
						Assert.Same(profile, Read<PersonalSyncProfile>(window, "_personalSyncActiveProfile"));
						var mapped = (TimeSpan)typeof(MainWindow).GetMethod("GetEffectiveLyricsPosition", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
						Assert.Equal(2.2, mapped.TotalSeconds, 3);
					}
					Pump(); Assert.False(Read<bool>(window, "_scrollAnimationActive"));
					Assert.True(scroll.VerticalOffset < Math.Max(3, scroll.ScrollableHeight * .05));
					Assert.Same(lyrics, Read<LyricsResult>(window, "_lyrics")); Assert.Same(lookup, Read<LyricsLookupResult>(window, "_lyricsLookup"));
					Assert.Same(cancellation, Read<object?>(window, "_lyricsCancellation"));
					Assert.Equal(cacheRevision, Read<LyricsService>(window, "_lyricsService").CacheRevision);
				}
				finally
				{
					StopTimers(); Read<IDisposable?>(window, "_hotkeys")?.Dispose(); Read<IDisposable?>(window, "_tray")?.Dispose();
					Read<LyricsService>(window, "_lyricsService").Dispose(); Set("_allowClose", true); window.Close();
				}
			});
			Assert.Equal(saved, await File.ReadAllTextAsync(store.FilePath));
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
