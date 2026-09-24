using System;
using System.Collections.Generic;
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

	public static bool AlignGlobally(PersonalSyncProfile profile, double lyric, double playback)
	{
		if (!double.IsFinite(lyric) || !double.IsFinite(playback) || lyric < 0 || playback < 0) return false;
		double desired = Math.Clamp(playback - lyric, -3600, 3600);
		// Repair only the selected line, on an explicit Align action. A schema-v2
		// anchor's origin cannot safely be inferred during profile loading.
		double[] occurrences = PlaybackOccurrences(lyric, profile).ToArray();
		var conflicts = profile.Mode == PersonalSyncMode.Advanced ? profile.Anchors.Where(anchor =>
			Math.Abs(anchor.LyricsSeconds - lyric) < .0001
			&& occurrences.Any(time => time < anchor.PlaybackSeconds - .001)
			&& !profile.Segments.Any(segment => segment.ResumeAnchorId == anchor.Id
				|| (!segment.ResumeAnchorId.HasValue && Math.Abs(segment.PlaybackEndSeconds - anchor.PlaybackSeconds) < .15)))
			.ToArray() : Array.Empty<PersonalSyncAnchor>();
		double delta = desired - profile.OffsetSeconds;
		if (conflicts.Length == 0 && Math.Abs(delta) < .0001 && profile.Mode != PersonalSyncMode.None) return false;
		foreach (var anchor in conflicts) profile.Anchors.Remove(anchor);
		ShiftWholeTrack(profile, delta);
		return true;
	}

	public static double? PlaybackForLyric(double lyric, PersonalSyncProfile profile) => PlaybackOccurrences(lyric, profile).Cast<double?>().FirstOrDefault();

	private static IEnumerable<double> PlaybackOccurrences(double lyric, PersonalSyncProfile profile)
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
			if (Math.Abs(mapped - lyric) < .0001) { yield return start; continue; }
			double probe = double.IsInfinity(end) ? start + 1 : (start + end) / 2;
			if (PersonalSyncMapper.DescribeActiveSegment(probe, profile).StartsWith("Hold", StringComparison.Ordinal)) continue;
			// Account for the mapper's clamp at the beginning of a positive offset.
			double sample = Math.Max(probe, start + Math.Abs(profile.OffsetSeconds) + 1);
			if (sample >= end) sample = probe;
			double target = sample + lyric - PersonalSyncMapper.MapPlaybackToLyrics(sample, profile);
			if (target >= start && target < end && Math.Abs(PersonalSyncMapper.MapPlaybackToLyrics(target, profile) - lyric) < .001) yield return target;
		}
	}
}
