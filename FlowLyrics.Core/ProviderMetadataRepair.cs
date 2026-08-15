using System;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowLyrics.Core;

public sealed record RepairedProviderMetadata(string Title, string Artist, string Album);

/// <summary>
/// Repairs known provider-specific GSMTC metadata mistakes before the values are
/// displayed, cached, or sent to a lyrics provider. Heuristics are deliberately
/// source-scoped and require an unambiguous title pattern.
/// </summary>
public static partial class ProviderMetadataRepair
{
	public static RepairedProviderMetadata Repair(
		string? sourceAppUserModelId,
		string? title,
		string? artist,
		string? album,
		string? albumArtist = null)
	{
		string repairedTitle = MetadataNormalizer.NormalizeWhitespace(title);
		string repairedArtist = MetadataNormalizer.NormalizeWhitespace(artist);
		string repairedAlbum = MetadataNormalizer.NormalizeWhitespace(album);
		string repairedAlbumArtist = MetadataNormalizer.NormalizeWhitespace(albumArtist);

		if (string.IsNullOrWhiteSpace(repairedArtist) && !string.IsNullOrWhiteSpace(repairedAlbumArtist))
		{
			repairedArtist = repairedAlbumArtist;
		}

		RepairedProviderMetadata metadata = new(repairedTitle, repairedArtist, repairedAlbum);
		if (MediaSourceClassifier.IsAppleMusic(sourceAppUserModelId))
		{
			metadata = RepairAppleMusic(metadata, repairedAlbumArtist);
		}
		if (MediaSourceClassifier.IsBrowser(sourceAppUserModelId))
		{
			metadata = RepairBrowser(metadata);
		}
		return metadata;
	}

	private static RepairedProviderMetadata RepairAppleMusic(RepairedProviderMetadata metadata, string albumArtist)
	{
		if (!string.IsNullOrWhiteSpace(metadata.Album)) return metadata;

		Match combined = AppleCombinedArtistAlbumRegex().Match(metadata.Artist);
		if (!combined.Success) return metadata;

		string parsedArtist = MetadataNormalizer.NormalizeWhitespace(combined.Groups["artist"].Value);
		string parsedAlbum = MetadataNormalizer.NormalizeWhitespace(combined.Groups["album"].Value);
		if (string.IsNullOrWhiteSpace(parsedArtist) || string.IsNullOrWhiteSpace(parsedAlbum)) return metadata;

		return metadata with
		{
			Artist = string.IsNullOrWhiteSpace(albumArtist) ? parsedArtist : albumArtist,
			Album = parsedAlbum
		};
	}

	private static RepairedProviderMetadata RepairBrowser(RepairedProviderMetadata metadata)
	{
		if (string.IsNullOrWhiteSpace(metadata.Title)) return metadata;

		bool hasProductionMarker = ProductionMarkerRegex().IsMatch(metadata.Title);
		Match quoted = QuotedTitleRegex().Match(metadata.Title);
		if (quoted.Success)
		{
			string prefix = TrimTitlePunctuation(quoted.Groups["artist"].Value);
			string quotedTitle = TrimTitlePunctuation(quoted.Groups["title"].Value);
			bool prefixMatchesArtist = LooksLikeSameArtist(prefix, metadata.Artist);
			if (!string.IsNullOrWhiteSpace(quotedTitle)
				&& (hasProductionMarker || prefixMatchesArtist || IsGenericChannelName(metadata.Artist)))
			{
				string repairedArtist = metadata.Artist;
				if (!string.IsNullOrWhiteSpace(prefix)
					&& (string.IsNullOrWhiteSpace(repairedArtist) || prefixMatchesArtist || IsGenericChannelName(repairedArtist)))
				{
					repairedArtist = prefix;
				}
				return metadata with { Title = quotedTitle, Artist = repairedArtist };
			}
		}

		string cleanedTitle = TrimTitlePunctuation(ProductionSuffixRegex().Replace(metadata.Title, string.Empty));
		if (string.IsNullOrWhiteSpace(cleanedTitle)) cleanedTitle = metadata.Title;

		Match slash = SlashCreditRegex().Match(cleanedTitle);
		if (slash.Success)
		{
			string left = TrimTitlePunctuation(slash.Groups["left"].Value);
			string right = TrimTitlePunctuation(slash.Groups["right"].Value);
			if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
			{
				if (LooksLikeSameArtist(right, metadata.Artist))
				{
					return metadata with { Title = left, Artist = right };
				}
				if (LooksLikeSameArtist(left, metadata.Artist))
				{
					return metadata with { Title = right, Artist = left };
				}
				if (hasProductionMarker || IsGenericChannelName(metadata.Artist))
				{
					// Japanese official channels commonly publish "title / credited artist".
					return metadata with { Title = left, Artist = right };
				}
			}
		}

		Match dash = DashCreditRegex().Match(cleanedTitle);
		if (dash.Success)
		{
			string left = TrimTitlePunctuation(dash.Groups["left"].Value);
			string right = TrimTitlePunctuation(dash.Groups["right"].Value);
			if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
			{
				if (LooksLikeSameArtist(left, metadata.Artist))
				{
					return metadata with { Title = right, Artist = left };
				}
				if (LooksLikeSameArtist(right, metadata.Artist))
				{
					return metadata with { Title = left, Artist = right };
				}
				if (hasProductionMarker && IsGenericChannelName(metadata.Artist))
				{
					return metadata with { Title = right, Artist = left };
				}
			}
		}

		return metadata with { Title = cleanedTitle };
	}

	private static bool LooksLikeSameArtist(string first, string second)
	{
		string left = CanonicalizeArtist(first);
		string right = CanonicalizeArtist(second);
		return left.Length >= 2 && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsGenericChannelName(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return true;
		return ChannelSuffixRegex().IsMatch(value)
			|| string.Equals(value.Trim(), "YouTube", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(value.Trim(), "YouTube Music", StringComparison.OrdinalIgnoreCase);
	}

	private static string CanonicalizeArtist(string value)
	{
		string normalized = MetadataNormalizer.NormalizeWhitespace(value);
		normalized = ChannelSuffixRegex().Replace(normalized, string.Empty);
		normalized = normalized.Normalize(NormalizationForm.FormKC);
		return ArtistPunctuationRegex().Replace(normalized, string.Empty).ToLowerInvariant();
	}

	private static string TrimTitlePunctuation(string value) =>
		MetadataNormalizer.NormalizeWhitespace(value).Trim(' ', '-', '–', '—', '|', '/', '／', ':', '：');

	[GeneratedRegex(@"^(?<artist>.+?)\s+[—–]\s+(?<album>.+)$", RegexOptions.CultureInvariant)]
	private static partial Regex AppleCombinedArtistAlbumRegex();

	[GeneratedRegex(@"^(?<artist>.*?)\s*[「『](?<title>[^」』]{1,160})[」』](?<tail>.*)$", RegexOptions.CultureInvariant)]
	private static partial Regex QuotedTitleRegex();

	[GeneratedRegex(@"^(?<left>.+?)\s+[/／]\s+(?<right>.+)$", RegexOptions.CultureInvariant)]
	private static partial Regex SlashCreditRegex();

	[GeneratedRegex(@"^(?<left>.+?)\s+(?:-|–|—)\s+(?<right>.+)$", RegexOptions.CultureInvariant)]
	private static partial Regex DashCreditRegex();

	[GeneratedRegex(@"\b(?:offi?cial|music\s+video|lyric\s+video|lyrics?|mv|pv)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ProductionMarkerRegex();

	[GeneratedRegex(@"\s*(?:[-|]\s*)?(?:[\(\[（【]\s*)?(?:(?:offi?cial)(?:\s+(?:music\s+)?video|\s+audio|\s+mv)?|(?:music|lyric)\s+video|lyrics?|mv|pv)\s*(?:[\)\]）】])?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ProductionSuffixRegex();

	[GeneratedRegex(@"\s*(?:[-–—]\s*)?(?:(?:offi?cial\s+)?(?:channel|topic)|vevo)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex ChannelSuffixRegex();

	[GeneratedRegex(@"[\s\p{P}\p{S}]+", RegexOptions.CultureInvariant)]
	private static partial Regex ArtistPunctuationRegex();
}
