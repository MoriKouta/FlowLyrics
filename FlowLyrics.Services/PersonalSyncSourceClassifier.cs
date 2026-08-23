using System;
using System.Linq;
using System.Text.RegularExpressions;
using FlowLyrics.Core;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public sealed record PersonalSyncSourceDescription(string Provider, string ContextLabel, bool IsInferred);

/// <summary>
/// Creates a user-facing source label without weakening the stable source identity.
/// Browsers do not always expose a page URL through GSMTC, so inferred providers are
/// deliberately marked as such instead of being presented as certain.
/// </summary>
public static class PersonalSyncSourceClassifier
{
	private static readonly Regex YouTubeTitleMarker = new(
		@"(?:\bofficial\s*(?:music\s*)?(?:video|mv|audio)\b|【\s*(?:mv|pv|official)\s*】|\[(?:mv|official)\])",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	public static PersonalSyncSourceDescription Classify(PlaybackSnapshot snapshot, string? youtubeVideoId = null)
	{
		string source = string.IsNullOrWhiteSpace(snapshot.SourceDisplayName)
			? MediaSourceClassifier.GetDisplayName(snapshot.SourceAppUserModelId)
			: snapshot.SourceDisplayName.Trim();
		if (!MediaSourceClassifier.IsBrowser(snapshot.SourceAppUserModelId))
		{
			string provider = MediaSourceClassifier.GetDisplayName(snapshot.SourceAppUserModelId);
			if (string.Equals(provider, "Media Session", StringComparison.OrdinalIgnoreCase)) provider = source;
			return new PersonalSyncSourceDescription(provider, source, false);
		}

		TrackInfo track = snapshot.Track;
		string rawTitle = track.OriginalMediaTitle ?? track.Title;
		string rawArtist = track.OriginalMediaArtist ?? track.Artist;
		bool exactYouTube = !string.IsNullOrWhiteSpace(youtubeVideoId)
			|| ContainsYouTubeAddress(rawTitle)
			|| ContainsYouTubeAddress(rawArtist)
			|| string.Equals(rawArtist.Trim(), "YouTube", StringComparison.OrdinalIgnoreCase);
		bool inferredYouTube = !exactYouTube && (
			(track.SearchAlternates?.Any(candidate => !candidate.IsEmpty) ?? false)
			|| YouTubeTitleMarker.IsMatch(rawTitle));
		if (exactYouTube || inferredYouTube)
		{
			return new PersonalSyncSourceDescription("YouTube", $"YouTube · {source}", inferredYouTube);
		}
		return new PersonalSyncSourceDescription("Browser media", $"Browser media · {source}", false);
	}

	private static bool ContainsYouTubeAddress(string? value) =>
		(value ?? string.Empty).Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
		|| (value ?? string.Empty).Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
}
