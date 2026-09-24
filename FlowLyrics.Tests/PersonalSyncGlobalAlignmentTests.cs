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

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class PersonalSyncGlobalAlignmentTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AlignNow_RepairsSelectedLegacyRewind_AndPersistsLikeFreshProfile(bool legacy)
	{
		string directory = Temp(); var context = Context(); var store = new PersonalSyncStore(directory);
		try
		{
			var profile = new PersonalSyncProfile { Track = context.Track, Source = context.Source, Lyrics = context.Lyrics, Mode = PersonalSyncMode.Advanced };
			if (legacy) profile.Anchors.Add(new() { PlaybackSeconds = 35, LyricsSeconds = 20 });
			await store.UpsertAsync(profile);
			Sta(() =>
			{
				LyricLine[] lines = [new(TimeSpan.FromSeconds(20), "A"), new(TimeSpan.FromSeconds(24), "B"), new(TimeSpan.FromSeconds(28), "C")];
				var window = new PersonalSyncWindow(store, context, profile, lines, () => 0, () => TimeSpan.FromSeconds(35), "ja-JP");
				try
				{
					window.Show(); Pump(); Invoke(window, "MatchSelectedLine_Click", window, new RoutedEventArgs());
					Check(Read<PersonalSyncProfile>(window, "_profile"));
				}
				finally { window.Close(); PersonalSyncRuntimeTests.WaitUntil(() => !window.IsVisible); }
			});
			Check((await new PersonalSyncStore(directory).ResolveAsync(context)).Profile!);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
		static void Check(PersonalSyncProfile changed)
		{
			Assert.Equal(15, changed.OffsetSeconds); Assert.Empty(changed.Anchors);
			Assert.Equal(35, PersonalSyncTimeline.PlaybackForLyric(20, changed));
			Assert.True(PersonalSyncMapper.MapPlaybackToLyrics(20, changed) < 20);
			Assert.True(PersonalSyncMapper.MapPlaybackToLyrics(34.9, changed) < 20);
			Assert.Equal(20, PersonalSyncMapper.MapPlaybackToLyrics(35, changed));
			Assert.Equal(24, PersonalSyncMapper.MapPlaybackToLyrics(39, changed));
			Assert.Equal(28, PersonalSyncMapper.MapPlaybackToLyrics(43, changed));
			Assert.Equal(55, PersonalSyncMapper.MapPlaybackToLyrics(70, changed));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void GlobalAlign_PreservesResumeAnchorsHoldsAndUnrelatedResync(bool legacyHold)
	{
		var resume = new PersonalSyncAnchor { PlaybackSeconds = 35, LyricsSeconds = 20 };
		var unrelated = new PersonalSyncAnchor { PlaybackSeconds = 100, LyricsSeconds = 70 };
		var hold = new PersonalSyncSegment { Type = PersonalSyncSegmentType.Hold, PlaybackStartSeconds = 30, PlaybackEndSeconds = 35,
			LyricsTimeSeconds = 19, ResumeAnchorId = legacyHold ? null : resume.Id };
		var blank = new PersonalSyncSegment { Type = PersonalSyncSegmentType.Hold, PlaybackStartSeconds = 60, PlaybackEndSeconds = 65, LyricsTimeSeconds = 0 };
		var profile = new PersonalSyncProfile { Mode = PersonalSyncMode.Advanced, Anchors = [resume, unrelated], Segments = [hold, blank] };
		Assert.True(PersonalSyncTimeline.AlignGlobally(profile, 20, 35));
		Assert.Equal(15, profile.OffsetSeconds); Assert.Equal(2, profile.Anchors.Count); Assert.Equal(2, profile.Segments.Count);
		Assert.Equal(legacyHold ? null : (Guid?)resume.Id, hold.ResumeAnchorId); Assert.Equal(50, resume.PlaybackSeconds); Assert.Equal(50, hold.PlaybackEndSeconds);
		Assert.Equal(115, unrelated.PlaybackSeconds); Assert.Equal(70, unrelated.LyricsSeconds);
		Assert.Equal(0, PersonalSyncMapper.MapPlaybackToLyrics(77, profile));
		Assert.Equal(19, PersonalSyncMapper.MapPlaybackToLyrics(47, profile));
		Assert.False(PersonalSyncTimeline.AlignGlobally(profile, 20, 35));
	}

	[Fact]
	public async Task HistoryGlobalNudge_MovesPointsAndHoldsAndPersistsTheWholeTimeline()
	{
		string directory = Temp();
		try
		{
			var context = Context(); var profile = Advanced(context); var store = new PersonalSyncStore(directory);
			await store.UpsertAsync(profile);
			Sta(() =>
			{
				var window = new PersonalSyncManagerWindow(store, "en-US", profile.Id);
				try
				{
					window.ShowActivated = false; window.Show(); Pump();
					PersonalSyncRuntimeTests.WaitUntil(() => Read<ListBox>(window, "_list").Items.Count == 1);
					Task edit = (Task)Invoke(window, "EditOffsetAsync", 0.5)!;
					PersonalSyncRuntimeTests.WaitUntil(() => edit.IsCompleted); edit.GetAwaiter().GetResult();
					Assert.Contains("+5.5", Read<TextBlock>(window, "_offsetValue").Text);
				}
				finally { window.Close(); }
			});
			AssertShifted((await new PersonalSyncStore(directory).ResolveAsync(context)).Profile!, .5);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Theory]
	[InlineData("menu-nudge", .5)]
	[InlineData("editing-nudge", .5)]
	[InlineData("legacy-align", 11)]
	public void OverlayTimingCommands_PreserveAdvancedEdits(string command, double delta)
	{
		string directory = Temp();
		try
		{
			Sta(() =>
			{
				using MediaSessionService media = new(new EmptyProvider());
				MainWindow window = new(new SettingsService(directory), media);
				try
				{
					window.ShowActivated = false; window.Show(); Pump(); StopTimers();
					TrackInfo track = new("Study", "Artist", "", TimeSpan.FromSeconds(150));
					LyricsResult lyrics = new([new(TimeSpan.FromSeconds(20), "A"), new(TimeSpan.FromSeconds(24), "B")], null, "LRCLIB");
					LyricsLookupResult lookup = new() { Lyrics = lyrics, LrclibRecord = new() { Id = 321 }, Status = LyricsLookupStatus.LrclibAuto };
					PlaybackSnapshot snapshot = new(track, TimeSpan.FromSeconds(36), false, DateTimeOffset.UtcNow);
					var profile = Advanced(PersonalSyncIdentity.Create(snapshot, lookup));
					Set("_snapshot", snapshot); Set("_lyrics", lyrics); Set("_lyricsLookup", lookup); Set("_personalSyncActiveProfile", profile);
					Read<AppSettings>(window, "_settings").GlobalLyricsOffsetMs = 0;
					if (command == "menu-nudge")
					{
						Invoke(window, "AdjustCurrentTrackOffset", -500);
						PersonalSyncRuntimeTests.WaitUntil(() => !ReferenceEquals(profile, Read<PersonalSyncProfile>(window, "_personalSyncActiveProfile")));
					}
					else
					{
						Set("_personalSyncEditingProfile", profile); Set("_personalSyncSelectedLineIndex", 0);
						if (command == "editing-nudge") Invoke(window, "AdjustPersonalSyncOffset", .5);
						else Invoke(window, "PersonalSyncAlign_Click", window, new RoutedEventArgs());
					}
					AssertShifted(Read<PersonalSyncProfile>(window, "_personalSyncActiveProfile"), delta);
				}
				finally
				{
					StopTimers(); Read<IDisposable?>(window, "_hotkeys")?.Dispose(); Read<IDisposable?>(window, "_tray")?.Dispose();
					Read<LyricsService>(window, "_lyricsService").Dispose(); Set("_allowClose", true); window.Close();
				}
				void Set(string field, object value) => typeof(MainWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
				void StopTimers()
				{
					foreach (FieldInfo field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
						if (field.GetValue(window) is DispatcherTimer timer) timer.Stop();
				}
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	private static PersonalSyncProfile Advanced(PersonalSyncContext context)
	{
		var anchor = new PersonalSyncAnchor { PlaybackSeconds = 75, LyricsSeconds = 55 };
		return new() { Track = context.Track, Source = context.Source, Lyrics = context.Lyrics, Mode = PersonalSyncMode.Advanced,
			OffsetSeconds = 5, Anchors = [anchor], Segments = [new() { Type = PersonalSyncSegmentType.Hold,
				PlaybackStartSeconds = 60, PlaybackEndSeconds = 75, LyricsTimeSeconds = 55, ResumeAnchorId = anchor.Id }] };
	}
	private static void AssertShifted(PersonalSyncProfile profile, double delta)
	{
		Assert.Equal(PersonalSyncMode.Advanced, profile.Mode); Assert.Equal(5 + delta, profile.OffsetSeconds, 6);
		var anchor = Assert.Single(profile.Anchors); var hold = Assert.Single(profile.Segments);
		Assert.Equal(75 + delta, anchor.PlaybackSeconds, 6); Assert.Equal(55, anchor.LyricsSeconds);
		Assert.Equal(60 + delta, hold.PlaybackStartSeconds, 6); Assert.Equal(75 + delta, hold.PlaybackEndSeconds, 6);
		Assert.Equal(55, hold.LyricsTimeSeconds); Assert.Equal(anchor.Id, hold.ResumeAnchorId);
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(5, false)]
	[InlineData(5, true)]
	public void PrimaryAlign_20SecondsTo36_UpdatesOffsetWithoutEarlyDisplayOrRewind(double existingOffset, bool advanced)
	{
		string directory = Temp();
		try
		{
			Sta(() =>
			{
				var context = Context();
				var profile = new PersonalSyncProfile { Track = context.Track, Source = context.Source, Lyrics = context.Lyrics, OffsetSeconds = existingOffset };
				if (advanced)
				{
					profile.Mode = PersonalSyncMode.Advanced;
					profile.Anchors.Add(new() { PlaybackSeconds = 75, LyricsSeconds = 55 });
					profile.Segments.Add(new() { Type = PersonalSyncSegmentType.Hold, PlaybackStartSeconds = 60, PlaybackEndSeconds = 75,
						LyricsTimeSeconds = 55, ResumeAnchorId = profile.Anchors[0].Id });
				}
				LyricLine[] lines = [new(TimeSpan.FromSeconds(20), "A"), new(TimeSpan.FromSeconds(24), "B"), new(TimeSpan.FromSeconds(28), "C")];
				var window = new PersonalSyncWindow(new(directory), context, profile, lines, () => 0, () => TimeSpan.FromSeconds(36), "ja-JP");
				try
				{
					window.Show(); Pump();
					int previews = 0; window.PreviewChanged += (_, _) => previews++;
					Invoke(window, "MatchSelectedLine_Click", window, new RoutedEventArgs());
					var changed = Read<PersonalSyncProfile>(window, "_profile");
					Assert.Equal(16, changed.OffsetSeconds); Assert.Equal(advanced ? 1 : 0, changed.Anchors.Count);
					if (advanced) { Assert.Equal(86, changed.Anchors[0].PlaybackSeconds); Assert.Equal(71, changed.Segments[0].PlaybackStartSeconds); }
					Assert.Contains("+16.0", Read<TextBlock>(window, "_offsetText").Text); Assert.Equal(1, previews);
					Assert.Equal(4, PersonalSyncMapper.MapPlaybackToLyrics(20, changed));
					Assert.True(PersonalSyncMapper.MapPlaybackToLyrics(35.9, changed) < 20);
					Assert.Equal(20, PersonalSyncMapper.MapPlaybackToLyrics(36, changed));
					Assert.Equal(24, PersonalSyncMapper.MapPlaybackToLyrics(40, changed));
					Assert.Equal(28, PersonalSyncMapper.MapPlaybackToLyrics(44, changed));
					Invoke(window, "MatchSelectedLine_Click", window, new RoutedEventArgs()); Assert.Equal(1, previews);
					Invoke(window, "AlignLineAt", 1, 42.0); Assert.Equal(advanced ? 2 : 1, changed.Anchors.Count);
					Assert.Equal(20, PersonalSyncMapper.MapPlaybackToLyrics(36, changed));
					Assert.Equal(24, PersonalSyncMapper.MapPlaybackToLyrics(42, changed));
				}
				finally { window.Close(); PersonalSyncRuntimeTests.WaitUntil(() => !window.IsVisible); }
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Theory]
	[InlineData(11)]
	[InlineData(-70)]
	public async Task WholeShift_PreservesAdvancedTimelineAndSourceTimes_AfterReopen(double delta)
	{
		string directory = Temp();
		try
		{
			var context = Context();
			var profile = new PersonalSyncProfile { Track = context.Track, Source = context.Source, Lyrics = context.Lyrics,
				Mode = PersonalSyncMode.Advanced, OffsetSeconds = 5 };
			profile.Anchors.Add(new() { PlaybackSeconds = 40, LyricsSeconds = 35 });
			var resume = new PersonalSyncAnchor { PlaybackSeconds = 75, LyricsSeconds = 55 }; profile.Anchors.Add(resume);
			profile.Segments.Add(new() { Type = PersonalSyncSegmentType.Hold, PlaybackStartSeconds = 60,
				PlaybackEndSeconds = 75, LyricsTimeSeconds = 55, ResumeAnchorId = resume.Id });
			var before = profile.Clone();
			PersonalSyncTimeline.ShiftWholeTrack(profile, delta);
			Assert.Equal(5 + delta, profile.OffsetSeconds);
			Assert.Equal(35, profile.Anchors[1].PlaybackSeconds - profile.Anchors[0].PlaybackSeconds);
			Assert.Equal(15, profile.Segments[0].PlaybackEndSeconds - profile.Segments[0].PlaybackStartSeconds);
			Assert.Equal(before.Anchors.Select(a => a.LyricsSeconds), profile.Anchors.Select(a => a.LyricsSeconds));
			Assert.Equal(55, profile.Segments[0].LyricsTimeSeconds);
			await new PersonalSyncStore(directory).UpsertAsync(profile);
			var reopened = (await new PersonalSyncStore(directory).ResolveAsync(context)).Profile!;
			Assert.Equal(profile.Anchors[0].PlaybackSeconds, reopened.Anchors[0].PlaybackSeconds);
			Assert.Equal(profile.Segments[0].PlaybackStartSeconds, reopened.Segments[0].PlaybackStartSeconds);
			foreach (double oldTime in new[] { 20, 39.9, 40, 59.9, 60, 70, 74.9, 75, 80, 100 }.Where(t => t + delta >= 0))
				Assert.Equal(PersonalSyncMapper.MapPlaybackToLyrics(oldTime, before), PersonalSyncMapper.MapPlaybackToLyrics(oldTime + delta, reopened), 6);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
	private static string Temp() => Path.Combine(Path.GetTempPath(), "FlowLyrics-global-align-" + Guid.NewGuid().ToString("N"));
	private static PersonalSyncContext Context() => PersonalSyncIdentity.Create(new(new("Study", "Artist", "", TimeSpan.FromSeconds(150)), TimeSpan.Zero, false, DateTimeOffset.UtcNow), new() { LrclibRecord = new() { Id = 321 } });
}
