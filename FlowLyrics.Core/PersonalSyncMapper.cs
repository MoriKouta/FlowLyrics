using System;
using System.Collections.Generic;
using System.Linq;
using FlowLyrics.Models;

namespace FlowLyrics.Core;

/// <summary>
/// Pure playback-time to lyrics-time conversion. Original LRC timestamps are
/// never mutated; every seek can be mapped independently.
/// </summary>
public static class PersonalSyncMapper
{
	public static double MapPlaybackToLyrics(double playbackSeconds, PersonalSyncProfile? profile) =>
		MapPlaybackToLyrics(TimeSpan.FromSeconds(Math.Max(0.0, playbackSeconds)), profile).TotalSeconds;

	public static TimeSpan MapPlaybackToLyrics(TimeSpan playbackTime, PersonalSyncProfile? profile)
	{
		double playback = Math.Max(0.0, playbackTime.TotalSeconds);
		if (profile == null || profile.Mode == PersonalSyncMode.None)
		{
			return TimeSpan.FromSeconds(playback);
		}

		double lyrics = playback - ClampOffset(profile.OffsetSeconds);
		if (profile.Mode == PersonalSyncMode.Advanced)
		{
			lyrics = MapAdvanced(playback, profile, lyrics);
		}
		return TimeSpan.FromSeconds(Math.Max(0.0, lyrics));
	}

	public static double CalculateOffsetSeconds(TimeSpan playbackTime, TimeSpan lyricsTime)
	{
		return ClampOffset(playbackTime.TotalSeconds - lyricsTime.TotalSeconds);
	}

	private static double MapAdvanced(double playback, PersonalSyncProfile profile, double defaultLyrics)
	{
		IReadOnlyList<PersonalSyncSegment> holds = profile.Segments
			.Where(segment => segment.Type == PersonalSyncSegmentType.Hold
				&& segment.PlaybackEndSeconds > segment.PlaybackStartSeconds)
			.OrderBy(segment => segment.PlaybackStartSeconds)
			.ToArray();

		PersonalSyncSegment? activeHold = holds.FirstOrDefault(segment =>
			playback >= segment.PlaybackStartSeconds && playback < segment.PlaybackEndSeconds);
		if (activeHold != null)
		{
			return activeHold.LyricsTimeSeconds;
		}

		double completedHoldDuration = holds
			.Where(segment => playback >= segment.PlaybackEndSeconds)
			.Sum(segment => segment.PlaybackEndSeconds - segment.PlaybackStartSeconds);
		double baseLyrics = defaultLyrics - completedHoldDuration;

		PersonalSyncAnchor? anchor = profile.Anchors
			.Where(candidate => candidate.PlaybackSeconds <= playback)
			.OrderByDescending(candidate => candidate.PlaybackSeconds)
			.FirstOrDefault();
		if (anchor == null) return baseLyrics;

		double holdDurationAtAnchor = holds
			.Where(segment => anchor.PlaybackSeconds >= segment.PlaybackEndSeconds)
			.Sum(segment => segment.PlaybackEndSeconds - segment.PlaybackStartSeconds);
		double baseAtAnchor = anchor.PlaybackSeconds - ClampOffset(profile.OffsetSeconds) - holdDurationAtAnchor;
		return baseLyrics + (anchor.LyricsSeconds - baseAtAnchor);
	}

	public static string DescribeActiveSegment(double playbackSeconds, PersonalSyncProfile? profile)
	{
		if (profile?.Mode != PersonalSyncMode.Advanced) return "Normal";
		PersonalSyncSegment? hold = profile.Segments.FirstOrDefault(segment =>
			segment.Type == PersonalSyncSegmentType.Hold
			&& playbackSeconds >= segment.PlaybackStartSeconds
			&& playbackSeconds < segment.PlaybackEndSeconds);
		return hold == null
			? "Normal"
			: $"Hold {hold.PlaybackStartSeconds:0.000}-{hold.PlaybackEndSeconds:0.000}s";
	}

	private static double ClampOffset(double seconds) => Math.Clamp(seconds, -3600.0, 3600.0);
}
