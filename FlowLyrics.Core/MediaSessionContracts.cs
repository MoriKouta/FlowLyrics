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
	IReadOnlyList<SearchMetadataCandidate>? SearchAlternates = null)
{
	public bool HasTitle => !string.IsNullOrWhiteSpace(TitleRaw);
}

public sealed record MediaPlaybackCapabilities(
	bool CanPlay,
	bool CanPause,
	bool CanTogglePlayPause,
	bool CanNext,
	bool CanPrevious,
	bool CanSeek);

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
}
