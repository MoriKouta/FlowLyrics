using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowLyrics.Core;

public sealed record ArtistCreditEnrichment(string Title, string Credit, string Source);

public interface IArtistCreditEnricher : IDisposable
{
	ArtistCreditEnrichment? TryGet(string sourceId, MediaTrackMetadata metadata);
	string WindowState => "UNKNOWN";
}

/// <summary>Pure validation of observed Spotify now-playing links; no UIA or inferred collaborators.</summary>
public static class SpotifyArtistCredit
{
	public const string Source = "Spotify UI Automation";

	public static bool ShouldProbe(string sourceId, MediaTrackMetadata metadata)
	{
		if (!MediaSourceClassifier.IsSpotify(sourceId) || !metadata.HasTitle
			|| string.IsNullOrWhiteSpace(metadata.OriginalArtistRaw)) return false;
		if (ArtistIdentity.Parse(metadata.OriginalArtistRaw).PerformerTokens.Count > 1) return false;
		string raw = MetadataNormalizer.ComparisonKey(metadata.OriginalArtistRaw);
		// If GSMTC already exposes a fuller credit, leave interpretation to the
		// provider instead of introducing a second, potentially conflicting source.
		return !new[] { metadata.ArtistRaw, metadata.OriginalAlbumArtistRaw, metadata.OriginalSubtitleRaw,
			metadata.OriginalAlbumRaw }.Concat(metadata.OriginalGenresRaw ?? Array.Empty<string>())
			.Any(value => !string.IsNullOrWhiteSpace(value)
				&& MetadataNormalizer.ComparisonKey(value) != raw
				&& ArtistIdentity.Parse(value).PerformerTokens.Contains(raw));
	}

	public static ArtistCreditEnrichment? Validate(string processName, bool isNowPlayingRegion,
		string expectedTitle, string rawArtist, string uiTitle, string regionLabel, IReadOnlyList<string> artistLinks)
	{
		if (!string.Equals(processName, "Spotify", StringComparison.OrdinalIgnoreCase) || !isNowPlayingRegion
			|| string.IsNullOrWhiteSpace(expectedTitle)
			|| MetadataNormalizer.ComparisonKey(expectedTitle) != MetadataNormalizer.ComparisonKey(uiTitle)
			|| artistLinks.Count == 0 || artistLinks.Any(string.IsNullOrWhiteSpace)) return null;
		// A link is one complete artist name, even if it includes commas, & or /.
		string rawKey = MetadataNormalizer.ComparisonKey(rawArtist);
		if (rawKey.Length == 0 || !artistLinks.Any(artist => MetadataNormalizer.ComparisonKey(artist) == rawKey)) return null;
		string credit = string.Join(", ", artistLinks);
		string expectedJapanese = "再生中：" + credit + " の " + uiTitle;
		string expectedEnglish = "Now playing: " + uiTitle + " by " + credit;
		string label = MetadataNormalizer.NormalizeWhitespace(regionLabel);
		if (!string.Equals(label, MetadataNormalizer.NormalizeWhitespace(expectedJapanese), StringComparison.Ordinal)
			&& !string.Equals(label, MetadataNormalizer.NormalizeWhitespace(expectedEnglish), StringComparison.OrdinalIgnoreCase)) return null;
		return new(uiTitle, credit, Source);
	}

	public static MediaTrackMetadata Apply(MediaTrackMetadata metadata, ArtistCreditEnrichment? enrichment)
	{
		if (enrichment == null || string.IsNullOrWhiteSpace(enrichment.Credit)
			|| MetadataNormalizer.ComparisonKey(metadata.TitleRaw) != MetadataNormalizer.ComparisonKey(enrichment.Title)) return metadata;
		List<SearchMetadataCandidate> candidates = (metadata.SearchAlternates ?? Array.Empty<SearchMetadataCandidate>()).ToList();
		if (!candidates.Any(item => item.Title == metadata.TitleRaw && item.Artist == enrichment.Credit))
			candidates.Add(new(metadata.TitleRaw, enrichment.Credit, metadata.AlbumRaw) { Evidence = enrichment.Source });
		return metadata with { EnrichedArtistCredit = enrichment.Credit, EnrichmentSource = enrichment.Source, SearchAlternates = candidates };
	}
}
