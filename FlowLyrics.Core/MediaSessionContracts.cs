using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlowLyrics.Core;

public enum MediaPlaybackState
{
	Unknown,
	Closed,
	Opened,
	Changing,
	Stopped,
	Playing,
	Paused
}

public sealed record MediaTrackMetadata(
	string TitleRaw,
	string ArtistRaw,
	string AlbumRaw,
	TimeSpan Duration,
	IReadOnlyList<SearchMetadataCandidate>? SearchAlternates = null,
	string OriginalTitleRaw = "",
	string OriginalArtistRaw = "",
	string? OriginalAlbumRaw = null,
	string? OriginalAlbumArtistRaw = null,
	string? OriginalSubtitleRaw = null,
	IReadOnlyList<string>? OriginalGenresRaw = null,
	int? OriginalTrackNumberRaw = null,
	string? EnrichedArtistCredit = null,
	string? EnrichmentSource = null,
	string? SpotifyWindowState = null)
{
	public bool HasTitle => !string.IsNullOrWhiteSpace(TitleRaw);
	public string DisplayArtist => EnrichedArtistCredit ?? ArtistRaw;
}

public enum MediaMetadataState { NoSession, PendingMetadata, Stable }
public enum MediaRepeatMode { None, List, Track }
public enum MediaTimelineChange { None, Wrap, Seek }

// Separate metadata changes from frequent position/playback notifications.
public sealed class MediaMetadataChangedEventArgs(string sessionId) : EventArgs
{
	public string SessionId { get; } = sessionId;
}

public sealed record MediaPlaybackCapabilities(
	bool CanPlay,
	bool CanPause,
	bool CanTogglePlayPause,
	bool CanNext,
	bool CanPrevious,
	bool CanSeek,
	bool CanStop = false,
	bool CanRepeat = false,
	bool CanShuffle = false);

public sealed record MediaSessionInfo
{
	public string SessionId { get; init; } = string.Empty;

	public string SourceAppUserModelId { get; init; } = string.Empty;

	public string DisplaySourceName { get; init; } = "Media Session";

	public MediaTrackMetadata Metadata { get; init; } = new(string.Empty, string.Empty, string.Empty, TimeSpan.Zero);

	public TimeSpan Position { get; init; }

	public DateTimeOffset TimelineUpdatedAtUtc { get; init; }

	public bool HasTimeline { get; init; }

	public MediaPlaybackState PlaybackState { get; init; }
	public MediaRepeatMode? RepeatMode { get; init; }
	public bool? ShuffleActive { get; init; }

	public MediaPlaybackCapabilities Capabilities { get; init; } = new(false, false, false, false, false, false);

	public bool IsCurrentSession { get; init; }

	public bool IsSelectedByFlowLyrics { get; init; }

	public bool IsIgnored { get; init; }

	public DateTimeOffset LastActivityUtc { get; init; }

	public DateTimeOffset CapturedAtUtc { get; init; }

	public bool IsPlaying => PlaybackState == MediaPlaybackState.Playing;
}

public interface IMediaSessionProvider : IDisposable
{
	event EventHandler? SessionsChanged;

	Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default);

	Task<bool> TryTogglePlayPauseAsync(string sessionId, CancellationToken cancellationToken = default);

	Task<bool> TrySkipNextAsync(string sessionId, CancellationToken cancellationToken = default);

	Task<bool> TrySkipPreviousAsync(string sessionId, CancellationToken cancellationToken = default);

	Task<bool> TrySeekAsync(string sessionId, TimeSpan position, CancellationToken cancellationToken = default);
	Task<bool> TryPauseAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
	Task<bool> TryStopAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
	Task<bool> TrySetRepeatAsync(string sessionId, MediaRepeatMode mode, CancellationToken cancellationToken = default) => Task.FromResult(false);
	Task<bool> TrySetShuffleAsync(string sessionId, bool active, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
