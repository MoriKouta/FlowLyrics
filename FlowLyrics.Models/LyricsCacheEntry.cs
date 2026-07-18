using System;
using System.Collections.Generic;

namespace FlowLyrics.Models;

public sealed class LyricsCacheEntry
{
	public string TrackKey { get; set; } = string.Empty;

	public string? SyncedLyrics { get; set; }

	public string? PlainLyrics { get; set; }

	public string Source { get; set; } = "LRCLIB";

	public bool IsInstrumental { get; set; }

	public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;

	public string CacheKind { get; set; } = "Positive";

	public DateTimeOffset? ExpiresAtUtc { get; set; }

	public int? LrclibId { get; set; }

	public string? LrclibTrackName { get; set; }

	public string? LrclibArtistName { get; set; }

	public string? LrclibAlbumName { get; set; }

	public double LrclibDuration { get; set; }

	public string SelectionMode { get; set; } = "Auto";

	public List<int> CandidateIds { get; set; } = new List<int>();

	public int MatcherVersion { get; set; }
}
