using System;

namespace FlowLyrics.Core;

public static class MediaSourceClassifier
{
	public static string GetDisplayName(string? sourceAppUserModelId)
	{
		string value = sourceAppUserModelId?.Trim() ?? string.Empty;
		if (Contains(value, "spotify")) return "Spotify";
		if (ContainsAny(value, "applemusic", "apple.music", "itunes")) return "Apple Music";
		if (Contains(value, "tidal")) return "TIDAL";
		if (ContainsAny(value, "videolan", "vlc")) return "VLC";
		if (ContainsAny(value, "msedge", "microsoftedge")) return "Microsoft Edge";
		if (ContainsAny(value, "chrome", "chromium")) return "Google Chrome";
		if (Contains(value, "firefox")) return "Firefox";
		return "Media Session";
	}

	public static bool IsBrowser(string? sourceAppUserModelId)
	{
		string value = sourceAppUserModelId ?? string.Empty;
		return ContainsAny(value, "msedge", "microsoftedge", "chrome", "chromium", "firefox");
	}

	public static bool IsAppleMusic(string? sourceAppUserModelId)
	{
		string value = sourceAppUserModelId ?? string.Empty;
		return ContainsAny(value, "applemusic", "apple.music", "itunes");
	}

	private static bool Contains(string value, string token) =>
		value.Contains(token, StringComparison.OrdinalIgnoreCase);

	private static bool ContainsAny(string value, params string[] tokens)
	{
		foreach (string token in tokens)
		{
			if (Contains(value, token)) return true;
		}
		return false;
	}
}
