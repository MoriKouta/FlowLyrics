using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public static class PersonalSyncIdentity
{
	public static PersonalSyncContext Create(PlaybackSnapshot snapshot, LyricsLookupResult lookup)
	{
		TrackInfo track = snapshot.Track;
		string originalTitle = string.IsNullOrWhiteSpace(track.OriginalMediaTitle) ? track.Title : track.OriginalMediaTitle.Trim();
		string originalArtist = string.IsNullOrWhiteSpace(track.OriginalMediaArtist) ? track.Artist : track.OriginalMediaArtist.Trim();
		string sourceId = snapshot.SourceAppUserModelId?.Trim() ?? string.Empty;
		string sourceName = string.IsNullOrWhiteSpace(snapshot.SourceDisplayName) ? "Media Session" : snapshot.SourceDisplayName.Trim();
		string videoId = TryExtractYouTubeVideoId(originalTitle) ?? string.Empty;
		string sourceSeed = string.Join("|",
			Normalize(sourceId.Length > 0 ? sourceId : sourceName),
			videoId.Length > 0 ? "youtube:" + videoId : "media",
			Normalize(originalTitle),
			Normalize(originalArtist),
			Math.Max(0, (int)Math.Round(track.Duration.TotalSeconds)));

		return new PersonalSyncContext(
			new PersonalSyncTrackIdentity
			{
				StableTrackKey = track.StableIdentityKey,
				Title = track.Title.Trim(),
				Artist = track.Artist.Trim(),
				Album = track.Album.Trim(),
				DurationSeconds = Math.Max(0.0, track.Duration.TotalSeconds)
			},
			new PersonalSyncSourceIdentity
			{
				StableSourceKey = "source:" + Hash(sourceSeed),
				Source = sourceName,
				SourceAppUserModelId = sourceId,
				OriginalMediaTitle = originalTitle,
				OriginalMediaArtist = originalArtist,
				DurationSeconds = Math.Max(0.0, track.Duration.TotalSeconds),
				YouTubeVideoId = videoId.Length == 0 ? null : videoId
			},
			CreateLyricsIdentity(track, lookup));
	}

	private static PersonalSyncLyricsIdentity CreateLyricsIdentity(TrackInfo track, LyricsLookupResult lookup)
	{
		if (lookup.LrclibRecord != null)
		{
			return new PersonalSyncLyricsIdentity
			{
				Key = "lrclib:" + lookup.LrclibRecord.Id,
				Kind = "LRCLIB",
				LrclibId = lookup.LrclibRecord.Id,
				DisplayName = "LRCLIB #" + lookup.LrclibRecord.Id
			};
		}
		if (!string.IsNullOrWhiteSpace(lookup.LocalLrcPath))
		{
			string fullPath;
			try { fullPath = Path.GetFullPath(lookup.LocalLrcPath); }
			catch { fullPath = lookup.LocalLrcPath.Trim(); }
			return new PersonalSyncLyricsIdentity
			{
				Key = "local:" + Hash(fullPath.ToLowerInvariant()),
				Kind = "LocalLrc",
				DisplayName = Path.GetFileName(fullPath)
			};
		}

		string source = lookup.Lyrics?.Source?.Trim() ?? "lyrics";
		string contentSignature = lookup.Lyrics == null
			? string.Empty
			: string.Join("\n", lookup.Lyrics.Lines.Select(line =>
				$"{line.Time.TotalMilliseconds:0}|{line.Text}")
				.Append(lookup.Lyrics.PlainLyrics ?? string.Empty));
		return new PersonalSyncLyricsIdentity
		{
			Key = "lyrics:" + Hash(track.StableIdentityKey + "|" + source + "|" + contentSignature),
			Kind = "Lyrics",
			DisplayName = source
		};
	}

	private static string? TryExtractYouTubeVideoId(string value)
	{
		Match match = Regex.Match(value ?? string.Empty,
			@"(?:youtube\.com/watch\?v=|youtu\.be/)(?<id>[A-Za-z0-9_-]{11})",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		return match.Success ? match.Groups["id"].Value : null;
	}

	private static string Normalize(string? value) => Regex.Replace(
		(value ?? string.Empty).Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant(), @"\s+", " ");

	private static string Hash(string value) => Convert.ToHexString(
		SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
