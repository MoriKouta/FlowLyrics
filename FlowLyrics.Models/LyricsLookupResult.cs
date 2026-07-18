using System;
using System.Collections.Generic;

namespace FlowLyrics.Models;

public sealed class LyricsLookupResult
{
	public LyricsResult? Lyrics { get; init; }

	public LyricsLookupStatus Status { get; init; }

	public LrclibRecord? LrclibRecord { get; init; }

	public bool LoadedFromCache { get; init; }

	public bool SelectedManually { get; init; }

	public string? LocalLrcPath { get; init; }

	public IReadOnlyList<LyricsCandidate> Candidates { get; init; } = Array.Empty<LyricsCandidate>();
}
