namespace FlowLyrics.Models;

public sealed record LyricsSearchRequest(string Title, string Artist, string Album, string Keyword);
