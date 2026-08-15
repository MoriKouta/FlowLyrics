namespace FlowLyrics.Models;

public enum LyricsLookupStatus
{
	None,
	LrclibAuto,
	LrclibBestMatch,
	LrclibManual,
	LocalLrc,
	Cache,
	CandidatesFound,
	NoLyrics
}
