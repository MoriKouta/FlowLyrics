namespace FlowLyrics.Models;

public enum LyricsErrorKind
{
	NotFound,
	Network,
	Timeout,
	RateLimited,
	Json,
	CacheDelete,
	ManualSelectionSave,
	LrclibId,
	NoSyncedLyrics,
	NoSpotifyTrack
}
