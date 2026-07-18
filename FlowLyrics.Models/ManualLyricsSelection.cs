using System;

namespace FlowLyrics.Models;

public sealed class ManualLyricsSelection
{
	public string Source { get; set; } = "lrclib";

	public int LrclibId { get; set; }

	public bool SelectedManually { get; set; } = true;

	public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
