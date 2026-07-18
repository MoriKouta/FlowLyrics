using System;
using System.Collections.Generic;

namespace FlowLyrics.Models;

public sealed class LyricsSearchProgress
{
	public int CompletedQueries { get; init; }

	public int CandidateCount { get; init; }

	public string Stage { get; init; } = string.Empty;

	public IReadOnlyList<LyricsCandidate> Candidates { get; init; } = Array.Empty<LyricsCandidate>();
}
