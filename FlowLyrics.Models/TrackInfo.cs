using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlowLyrics.Core;

namespace FlowLyrics.Models;

public sealed record TrackInfo(
	string Title,
	string Artist,
	string Album,
	TimeSpan Duration,
	string? LegacyProviderTrackId = null,
	IReadOnlyList<SearchMetadataCandidate>? SearchAlternates = null,
	string? OriginalMediaTitle = null,
	string? OriginalMediaArtist = null,
	string? OriginalMediaAlbum = null,
	string? SourceAppUserModelId = null,
	string? EnrichedArtistCredit = null,
	string? EnrichmentSource = null)
{
	public string RawTitle => OriginalMediaTitle ?? Title;
	public string RawArtist => OriginalMediaArtist ?? Artist;
	public string RawAlbum => OriginalMediaAlbum ?? Album;
	public string CanonicalTitle => Title;
	public string CanonicalArtist => Artist;
	public string CanonicalAlbum => Album;
	public string DisplayArtist => EnrichedArtistCredit ?? Artist;
	public string DisplayTitle => MetadataNormalizer.TryReleaseTitle(Title, out string title, out _) ? title : Title;
	public IReadOnlyList<string> TitleAliases => MetadataNormalizer.TitleAliases(Title);
	public ArtistIdentity ArtistIdentities => ArtistIdentity.Parse(Artist);
	public string ReleaseContext => MetadataNormalizer.TryReleaseTitle(Title, out _, out string work) ? work : string.Empty;
	// Recording comparisons belong to LyricsMatcher (aliases, editions, source-aware
	// duration). Persistent identities intentionally retain their historical format.
	public string StableSourceMetadataKey => StableIdentityKey;

	public string CacheKey
	{
		get
		{
			InlineArray4<object> buffer = default(InlineArray4<object>);
			buffer[0] = Title.Trim();
			buffer[1] = Artist.Trim();
			buffer[2] = Album.Trim();
			buffer[3] = Math.Max(0, (int)Math.Round(Duration.TotalSeconds));
			return string.Join("|", (ReadOnlySpan<object?>)buffer);
		}
	}

	public string StableIdentityKey
	{
		get
		{
			InlineArray4<object> buffer = default(InlineArray4<object>);
			buffer[0] = NormalizeIdentityPart(Title);
			buffer[1] = NormalizeIdentityPart(Artist);
			buffer[2] = NormalizeIdentityPart(Album);
			buffer[3] = Math.Max(0, (int)Math.Round(Duration.TotalSeconds));
			string s = string.Join("|", (ReadOnlySpan<object?>)buffer);
			return "metadata:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
		}
	}

	public IEnumerable<string> LegacyIdentityKeys
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(LegacyProviderTrackId))
			{
				string value = LegacyProviderTrackId.Trim();
				yield return value.StartsWith("spotify:track:", StringComparison.OrdinalIgnoreCase)
					? value.ToLowerInvariant()
					: "spotify:track:" + value.ToLowerInvariant();
			}
		}
	}

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(DisplayArtist))
			{
				return DisplayTitle + " — " + DisplayArtist;
			}
			return DisplayTitle;
		}
	}

	private static string NormalizeIdentityPart(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant(), "\\s+", " ");
	}
}
