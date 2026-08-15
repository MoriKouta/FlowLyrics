using System;
using System.Collections.Generic;

namespace FlowLyrics.Models;

public enum PersonalSyncMode
{
	None,
	Offset,
	Advanced
}

public enum PersonalSyncScope
{
	Source,
	Track
}

public enum PersonalSyncSegmentType
{
	Normal,
	Hold
}

public sealed class PersonalSyncTrackIdentity
{
	public string StableTrackKey { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Artist { get; set; } = string.Empty;
	public string Album { get; set; } = string.Empty;
	public double DurationSeconds { get; set; }
}

public sealed class PersonalSyncSourceIdentity
{
	public string StableSourceKey { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public string SourceAppUserModelId { get; set; } = string.Empty;
	public string OriginalMediaTitle { get; set; } = string.Empty;
	public string OriginalMediaArtist { get; set; } = string.Empty;
	public double DurationSeconds { get; set; }
	public string? YouTubeVideoId { get; set; }
}

public sealed class PersonalSyncLyricsIdentity
{
	public string Key { get; set; } = string.Empty;
	public string Kind { get; set; } = string.Empty;
	public int? LrclibId { get; set; }
	public string DisplayName { get; set; } = string.Empty;
}

public sealed class PersonalSyncAnchor
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public double PlaybackSeconds { get; set; }
	public double LyricsSeconds { get; set; }
}

public sealed class PersonalSyncSegment
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public PersonalSyncSegmentType Type { get; set; } = PersonalSyncSegmentType.Normal;
	public double PlaybackStartSeconds { get; set; }
	public double PlaybackEndSeconds { get; set; }
	public double LyricsTimeSeconds { get; set; }
}

public sealed class PersonalSyncProfile
{
	public const int CurrentSchemaVersion = 1;

	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public Guid Id { get; set; } = Guid.NewGuid();
	public PersonalSyncMode Mode { get; set; } = PersonalSyncMode.Offset;
	public PersonalSyncScope Scope { get; set; } = PersonalSyncScope.Source;
	public PersonalSyncTrackIdentity Track { get; set; } = new();
	public PersonalSyncSourceIdentity Source { get; set; } = new();
	public PersonalSyncLyricsIdentity Lyrics { get; set; } = new();
	public double OffsetSeconds { get; set; }
	public List<PersonalSyncAnchor> Anchors { get; set; } = new();
	public List<PersonalSyncSegment> Segments { get; set; } = new();
	public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

	public PersonalSyncProfile Clone()
	{
		return new PersonalSyncProfile
		{
			SchemaVersion = SchemaVersion,
			Id = Id,
			Mode = Mode,
			Scope = Scope,
			Track = new PersonalSyncTrackIdentity
			{
				StableTrackKey = Track.StableTrackKey,
				Title = Track.Title,
				Artist = Track.Artist,
				Album = Track.Album,
				DurationSeconds = Track.DurationSeconds
			},
			Source = new PersonalSyncSourceIdentity
			{
				StableSourceKey = Source.StableSourceKey,
				Source = Source.Source,
				SourceAppUserModelId = Source.SourceAppUserModelId,
				OriginalMediaTitle = Source.OriginalMediaTitle,
				OriginalMediaArtist = Source.OriginalMediaArtist,
				DurationSeconds = Source.DurationSeconds,
				YouTubeVideoId = Source.YouTubeVideoId
			},
			Lyrics = new PersonalSyncLyricsIdentity
			{
				Key = Lyrics.Key,
				Kind = Lyrics.Kind,
				LrclibId = Lyrics.LrclibId,
				DisplayName = Lyrics.DisplayName
			},
			OffsetSeconds = OffsetSeconds,
			Anchors = Anchors.ConvertAll(anchor => new PersonalSyncAnchor
			{
				Id = anchor.Id,
				PlaybackSeconds = anchor.PlaybackSeconds,
				LyricsSeconds = anchor.LyricsSeconds
			}),
			Segments = Segments.ConvertAll(segment => new PersonalSyncSegment
			{
				Id = segment.Id,
				Type = segment.Type,
				PlaybackStartSeconds = segment.PlaybackStartSeconds,
				PlaybackEndSeconds = segment.PlaybackEndSeconds,
				LyricsTimeSeconds = segment.LyricsTimeSeconds
			}),
			UpdatedAtUtc = UpdatedAtUtc
		};
	}
}

public sealed record PersonalSyncContext(
	PersonalSyncTrackIdentity Track,
	PersonalSyncSourceIdentity Source,
	PersonalSyncLyricsIdentity Lyrics);

public sealed record PersonalSyncResolution(
	PersonalSyncProfile? Profile,
	bool HasProfileForDifferentLyrics = false);

public sealed record PersonalSyncDiagnosticSnapshot(
	string ProfileId,
	string Mode,
	string Scope,
	string TrackKey,
	string SourceKey,
	string LyricsKey,
	double OffsetSeconds,
	int AnchorCount,
	int HoldCount,
	bool LyricsMismatch,
	double PlaybackSeconds,
	double MappedLyricsSeconds,
	string ActiveSegment = "Normal");
