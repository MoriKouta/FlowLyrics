using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
