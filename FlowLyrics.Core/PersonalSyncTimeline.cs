using System;
using System.Linq;
using FlowLyrics.Models;

namespace FlowLyrics.Core;

/// <summary>Playback coordinates for lyric-side editing; never changes source timestamps.</summary>
public static class PersonalSyncTimeline
{
	public static void ShiftWholeTrack(PersonalSyncProfile profile, double delta)
	{
		if (!double.IsFinite(delta)) return;
		if (profile.Mode == PersonalSyncMode.None) profile.Mode = PersonalSyncMode.Offset;
		double offset = Math.Clamp(profile.OffsetSeconds + delta, -3600, 3600);
		double applied = offset - profile.OffsetSeconds;
		profile.OffsetSeconds = offset;
		foreach (var anchor in profile.Anchors) anchor.PlaybackSeconds += applied;
		foreach (var hold in profile.Segments)
		{ hold.PlaybackStartSeconds += applied; hold.PlaybackEndSeconds += applied; }
	}

	public static double? PlaybackForLyric(double lyric, PersonalSyncProfile profile)
	{
		// Each boundary starts a linear or held segment. A skipped lyric has no
		// playback coordinate; do not invent a seek position for it.
		double[] boundaries = new[] { 0.0 }.Concat(profile.Anchors.Select(a => a.PlaybackSeconds))
			.Concat(profile.Segments.SelectMany(h => new[] { h.PlaybackStartSeconds, h.PlaybackEndSeconds }))
			.Where(t => double.IsFinite(t) && t >= 0).Distinct().Order().ToArray();
		// If an edit repeats a lyric, its latest occurrence is the editor's target.
		for (int i = boundaries.Length - 1; i >= 0; i--)
		{
			double start = boundaries[i], end = i + 1 < boundaries.Length ? boundaries[i + 1] : double.PositiveInfinity;
			double mapped = PersonalSyncMapper.MapPlaybackToLyrics(start, profile);
			if (Math.Abs(mapped - lyric) < .0001) return start;
			double probe = double.IsInfinity(end) ? start + 1 : (start + end) / 2;
			if (PersonalSyncMapper.DescribeActiveSegment(probe, profile).StartsWith("Hold", StringComparison.Ordinal)) continue;
			// Account for the mapper's clamp at the beginning of a positive offset.
			double sample = Math.Max(probe, start + Math.Abs(profile.OffsetSeconds) + 1);
			if (sample >= end) sample = probe;
			double target = sample + lyric - PersonalSyncMapper.MapPlaybackToLyrics(sample, profile);
			if (target >= start && target < end && Math.Abs(PersonalSyncMapper.MapPlaybackToLyrics(target, profile) - lyric) < .001) return target;
		}
		return null;
	}
}
