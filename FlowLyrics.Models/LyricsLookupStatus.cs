namespace FlowLyrics.Models;

public enum LyricsLookupStatus
{
	None,
	LrclibAuto,
	LrclibManual,
	LocalLrc,
	Cache,
	CandidatesFound,
	NoLyrics
}
