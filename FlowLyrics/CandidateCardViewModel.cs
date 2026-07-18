using System.Windows.Media;
using FlowLyrics.Models;

namespace FlowLyrics;

public sealed class CandidateCardViewModel
{
	public required LyricsCandidate Candidate { get; init; }

	public string Title { get; init; } = string.Empty;

	public string Artist { get; init; } = string.Empty;

	public string Album { get; init; } = string.Empty;

	public string Quality { get; init; } = string.Empty;

	public Brush QualityBrush { get; init; } = Brushes.Orange;

	public string Summary { get; init; } = string.Empty;

	public string Matches { get; init; } = string.Empty;

	public string Mismatches { get; init; } = string.Empty;

	public bool CanUse { get; init; }

	public string DisabledReason { get; init; } = string.Empty;

	public string PreviewLabel { get; init; } = string.Empty;

	public string UseLabel { get; init; } = string.Empty;

	public string OpenLabel { get; init; } = string.Empty;
}
