using System;
using System.Collections.Generic;

namespace FlowLyrics.Models;

public sealed class LyricsCandidate
{
	public required LrclibRecord Record { get; init; }

	public int Score { get; init; }

	public double? DurationDifferenceSeconds { get; init; }

	public bool AutoEligible { get; init; }

	public bool ArtistMatchIsCrossScript { get; init; }

	public bool LyricsScriptMismatch { get; init; }

	public IReadOnlyList<string> MatchedFields { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> MismatchedFields { get; init; } = Array.Empty<string>();

	public IReadOnlyList<string> RejectionReasons { get; init; } = Array.Empty<string>();

	public string QualityKey { get; init; } = "Needs review";
}
