using System.Collections.Generic;

namespace FlowLyrics.Models;

public sealed record LyricsResult(IReadOnlyList<LyricLine> Lines, string? PlainLyrics, string Source, bool IsInstrumental = false, bool IsEstimatedTiming = false)
{
	public bool HasSyncedLyrics => Lines.Count > 0;

	public bool HasPlainLyrics => !string.IsNullOrWhiteSpace(PlainLyrics);
}
