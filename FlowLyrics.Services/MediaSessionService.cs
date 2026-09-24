using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

/// <summary>
/// Platform-neutral selection, stabilization and playback-command coordinator.
/// </summary>
public sealed class MediaSessionService : IDisposable
{
	private static readonly TimeSpan PreferredReturnStability = TimeSpan.FromMilliseconds(850);

	private static readonly TimeSpan MetadataStability = TimeSpan.FromMilliseconds(110);
	private static readonly TimeSpan FreshTimelineAge = TimeSpan.FromSeconds(1.5);
	private static readonly TimeSpan MinimumExternalSeekJump = TimeSpan.FromSeconds(5);
	private long _timelineRevision;
	private MediaTimelineChange _timelineChange;
	private bool _timelineHasStableMetadata;
	private DateTimeOffset? _missingSessionSince;
	private long _metadataRevision;
	private readonly Func<DateTimeOffset> _utcNow;

	private readonly object _gate = new();

	private readonly IMediaSessionProvider _provider;

	private readonly HashSet<string> _ignoredSourceIds = new(StringComparer.OrdinalIgnoreCase);

	private string _preferredSourceId = string.Empty;

	private string _selectedSessionId = string.Empty;

	private string _candidateSessionId = string.Empty;

	private DateTimeOffset _candidateSinceUtc = DateTimeOffset.MinValue;

	private string _pendingMetadataIdentity = string.Empty;

	private DateTimeOffset _pendingMetadataSinceUtc = DateTimeOffset.MinValue;

	private string _stableMetadataIdentity = string.Empty;

	private string _timelineIdentity = string.Empty;

	private TimeSpan _stableTimelinePosition;

	private DateTimeOffset _stableTimelineCapturedAtUtc = DateTimeOffset.MinValue;

	private bool _stableTimelineWasPlaying;

	private DateTimeOffset _lastProviderTimelineUpdatedAtUtc = DateTimeOffset.MinValue;

	private DateTimeOffset _pendingSeekUntilUtc = DateTimeOffset.MinValue;

	private double? _pendingBackwardOffsetSeconds;

	private DateTimeOffset _pendingBackwardOffsetSinceUtc = DateTimeOffset.MinValue;

	private bool _disposed;

	public MediaSessionService()
		: this(new WindowsMediaSessionProvider())
	{
	}

	public MediaSessionService(IMediaSessionProvider provider, Func<DateTimeOffset>? utcNow = null)
	{
		_utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		_provider.SessionsChanged += Provider_SessionsChanged;
	}

	public event EventHandler? SessionsChanged;
	public event EventHandler? PlaybackNavigationRequested;

	public void ConfigureSelection(string? preferredSourceId, IEnumerable<string>? ignoredSourceIds)
	{
		string preferred = preferredSourceId?.Trim() ?? string.Empty;
		HashSet<string> ignored = new(
			ignoredSourceIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()) ?? Array.Empty<string>(),
			StringComparer.OrdinalIgnoreCase);
		if (ignored.Contains(preferred)) preferred = string.Empty;
		bool changed;
		lock (_gate)
		{
			changed = !string.Equals(_preferredSourceId, preferred, StringComparison.OrdinalIgnoreCase)
				|| !_ignoredSourceIds.SetEquals(ignored);
			if (!changed) return;
			_preferredSourceId = preferred;
			_ignoredSourceIds.Clear();
			_ignoredSourceIds.UnionWith(ignored);
			_selectedSessionId = string.Empty;
			_candidateSessionId = string.Empty;
			_candidateSinceUtc = DateTimeOffset.MinValue;
			ResetMetadataStabilityLocked();
		}
		SessionsChanged?.Invoke(this, EventArgs.Empty);
	}

	public async Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default)
	{
		IReadOnlyList<MediaSessionInfo> rawSessions = await _provider.GetSessionsAsync(cancellationToken);
		DateTimeOffset nowUtc = _utcNow();
		string selectedId;
		HashSet<string> ignored;
		lock (_gate)
		{
			ignored = new HashSet<string>(_ignoredSourceIds, StringComparer.OrdinalIgnoreCase);
			selectedId = SelectSessionLocked(rawSessions, ignored, nowUtc);
		}

		return rawSessions.Select(session => session with
		{
			IsIgnored = ignored.Contains(session.SourceAppUserModelId),
			IsSelectedByFlowLyrics = string.Equals(session.SessionId, selectedId, StringComparison.Ordinal)
		}).ToArray();
	}

	public async Task<PlaybackSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
	{
		MediaSessionUpdate update = await GetUpdateAsync(cancellationToken);
		return update.State == MediaMetadataState.Stable ? update.Snapshot : null;
	}

	public async Task<MediaSessionUpdate> GetUpdateAsync(CancellationToken cancellationToken = default)
	{
		long revision;
		lock (_gate) revision = _metadataRevision;
		IReadOnlyList<MediaSessionInfo> sessions = await GetSessionsAsync(cancellationToken);
		MediaSessionInfo? selected = sessions.FirstOrDefault(session => session.IsSelectedByFlowLyrics);
		if (selected == null)
			return new(GetSelectedSessionId().Length == 0 ? MediaMetadataState.NoSession : MediaMetadataState.PendingMetadata);
		if (!selected.Metadata.HasTitle || selected.PlaybackState == MediaPlaybackState.Changing)
		{
			lock (_gate) ResetMetadataStabilityLocked();
			return new(MediaMetadataState.PendingMetadata, Session: selected);
		}

		TrackInfo track = new(
			selected.Metadata.TitleRaw,
			selected.Metadata.ArtistRaw,
			selected.Metadata.AlbumRaw,
			selected.Metadata.Duration,
			SearchAlternates: selected.Metadata.SearchAlternates,
			OriginalMediaTitle: selected.Metadata.OriginalTitleRaw,
			OriginalMediaArtist: selected.Metadata.OriginalArtistRaw,
			OriginalMediaAlbum: selected.Metadata.OriginalAlbumRaw,
			SourceAppUserModelId: selected.SourceAppUserModelId,
			EnrichedArtistCredit: selected.Metadata.EnrichedArtistCredit,
			EnrichmentSource: selected.Metadata.EnrichmentSource);
		DateTimeOffset nowUtc = _utcNow();
		bool stable;
		lock (_gate)
		{
			if (revision != _metadataRevision) return new(MediaMetadataState.PendingMetadata);
			stable = IsMetadataStable(track, selected.SessionId, nowUtc);
		}

		MediaPlaybackCapabilities capabilities = selected.Capabilities;
		var timeline = StabilizeTimeline(selected, track, nowUtc, stable);
		PlaybackSnapshot snapshot = new(
			track,
			timeline.Position,
			selected.IsPlaying,
			timeline.CapturedAtUtc,
			capabilities.CanTogglePlayPause || capabilities.CanPlay || capabilities.CanPause,
			capabilities.CanPrevious,
			capabilities.CanNext,
			capabilities.CanSeek || (selected.HasTimeline && track.Duration > TimeSpan.Zero),
			capabilities.CanPlay,
			capabilities.CanPause,
			selected.SessionId,
			selected.SourceAppUserModelId,
			selected.DisplaySourceName,
			capabilities.CanStop, capabilities.CanRepeat, selected.RepeatMode, timeline.Revision, timeline.Change,
			capabilities.CanShuffle, selected.ShuffleActive);
		return new(stable ? MediaMetadataState.Stable : MediaMetadataState.PendingMetadata, snapshot, selected);
	}

	public async Task<bool> TrySetRepeatAsync(string expectedSessionId, MediaRepeatMode mode, CancellationToken cancellationToken = default)
	{
		var selected = (await GetSessionsAsync(cancellationToken)).FirstOrDefault(session => session.IsSelectedByFlowLyrics);
		cancellationToken.ThrowIfCancellationRequested();
		return selected?.SessionId == expectedSessionId && selected.Capabilities.CanRepeat
			&& await _provider.TrySetRepeatAsync(expectedSessionId, mode, cancellationToken);
	}

	public async Task<bool> TrySetShuffleAsync(string expectedSessionId, bool active, CancellationToken cancellationToken = default)
	{
		var selected = (await GetSessionsAsync(cancellationToken)).FirstOrDefault(session => session.IsSelectedByFlowLyrics);
		cancellationToken.ThrowIfCancellationRequested();
		return selected?.SessionId == expectedSessionId && selected.Capabilities.CanShuffle && selected.ShuffleActive.HasValue
			&& await _provider.TrySetShuffleAsync(expectedSessionId, active, cancellationToken);
	}

	public async Task<bool> TryPauseOrStopAsync(string expectedSessionId, CancellationToken cancellationToken = default, string? expectedTrack = null)
	{
		var selected = (await GetSessionsAsync(cancellationToken)).FirstOrDefault(session => session.IsSelectedByFlowLyrics);
		cancellationToken.ThrowIfCancellationRequested();
		if (selected?.SessionId != expectedSessionId || (expectedTrack != null && StopAfterTrackReservation.TrackIdentity(selected) != expectedTrack)) return false;
		if (selected.Capabilities.CanPause && await _provider.TryPauseAsync(expectedSessionId, cancellationToken)) return true;
		cancellationToken.ThrowIfCancellationRequested();
		selected = (await GetSessionsAsync(cancellationToken)).FirstOrDefault(session => session.IsSelectedByFlowLyrics);
		cancellationToken.ThrowIfCancellationRequested();
		return selected?.SessionId == expectedSessionId && selected.Capabilities.CanStop
			&& (expectedTrack == null || StopAfterTrackReservation.TrackIdentity(selected) == expectedTrack)
			&& await _provider.TryStopAsync(expectedSessionId, cancellationToken);
	}

	public async Task<bool> TryTogglePlayPauseAsync(CancellationToken cancellationToken = default)
	{
		string sessionId = GetSelectedSessionId();
		return !string.IsNullOrEmpty(sessionId) && await _provider.TryTogglePlayPauseAsync(sessionId, cancellationToken);
	}

	public async Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default)
	{
		PlaybackNavigationRequested?.Invoke(this, EventArgs.Empty);
		string sessionId = GetSelectedSessionId();
		return !string.IsNullOrEmpty(sessionId) && await _provider.TrySkipNextAsync(sessionId, cancellationToken);
	}

	public async Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default)
	{
		PlaybackNavigationRequested?.Invoke(this, EventArgs.Empty);
		string sessionId = GetSelectedSessionId();
		return !string.IsNullOrEmpty(sessionId) && await _provider.TrySkipPreviousAsync(sessionId, cancellationToken);
	}

	public async Task<bool> TrySeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
	{
		PlaybackNavigationRequested?.Invoke(this, EventArgs.Empty);
		string sessionId = GetSelectedSessionId();
		if (string.IsNullOrEmpty(sessionId) || !await _provider.TrySeekAsync(sessionId, position, cancellationToken)) return false;
		RecordSeek(sessionId, position);
		return true;
	}

	// Automatic repeat never invokes UI Automation or navigates a different track.
	public async Task<bool> TryRestartTrackAsync(MediaSessionInfo expected, CancellationToken cancellationToken = default)
	{
		bool Same(MediaSessionInfo? s) => s != null && s.SessionId == expected.SessionId && BrowserTrackRepeat.Eligible(s)
			&& StopAfterTrackReservation.TrackIdentity(s) == StopAfterTrackReservation.TrackIdentity(expected);
		var selected = (await GetSessionsAsync(cancellationToken)).FirstOrDefault(s => s.IsSelectedByFlowLyrics);
		cancellationToken.ThrowIfCancellationRequested();
		if (!Same(selected)) return false;
		if (selected!.Position.TotalSeconds < 1) return true;
		if ((selected.Metadata.Duration - selected.Position).TotalSeconds > .2) return false;
		if (!await _provider.TrySeekNativeAsync(expected.SessionId, TimeSpan.Zero, cancellationToken)) return false;
		for (int attempt = 0; attempt < 8; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			selected = (await GetSessionsAsync(cancellationToken)).FirstOrDefault(s => s.IsSelectedByFlowLyrics);
			if (!Same(selected)) return false;
			if (selected!.Position.TotalSeconds < 1)
			{
				RecordSeek(expected.SessionId, selected.Position);
				return await _provider.TryPlayAsync(expected.SessionId, cancellationToken);
			}
			await Task.Delay(60, cancellationToken);
		}
		return false;
	}

	private void RecordSeek(string sessionId, TimeSpan position)
	{
		DateTimeOffset nowUtc = _utcNow();
		lock (_gate)
		{
			if (string.Equals(sessionId, _selectedSessionId, StringComparison.Ordinal))
			{
				_stableTimelinePosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
				_stableTimelineCapturedAtUtc = nowUtc;
				_pendingSeekUntilUtc = nowUtc + TimeSpan.FromSeconds(2.5);
				_timelineRevision++;
				_timelineChange = MediaTimelineChange.Seek;
				_pendingBackwardOffsetSeconds = null;
				_pendingBackwardOffsetSinceUtc = DateTimeOffset.MinValue;
			}
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (_disposed) return;
			_disposed = true;
		}
		_provider.SessionsChanged -= Provider_SessionsChanged;
		_provider.Dispose();
	}

	private string SelectSessionLocked(
		IReadOnlyList<MediaSessionInfo> allSessions,
		HashSet<string> ignored,
		DateTimeOffset nowUtc)
	{
		MediaSessionInfo[] eligible = allSessions
			.Where(session => !ignored.Contains(session.SourceAppUserModelId))
			.ToArray();
		MediaSessionInfo? currentlySelected = eligible.FirstOrDefault(session =>
			string.Equals(session.SessionId, _selectedSessionId, StringComparison.Ordinal));

		MediaSessionInfo? desired;
		if (!string.IsNullOrWhiteSpace(_preferredSourceId))
		{
			MediaSessionInfo[] preferred = eligible
				.Where(session => string.Equals(session.SourceAppUserModelId, _preferredSourceId, StringComparison.OrdinalIgnoreCase))
				.ToArray();
			desired = preferred.FirstOrDefault(session => string.Equals(session.SessionId, _selectedSessionId, StringComparison.Ordinal))
				?? ChooseBest(preferred);
			if (desired == null)
			{
				desired = IsHealthy(currentlySelected) ? currentlySelected : ChooseBest(eligible);
			}
		}
		else
		{
			desired = IsHealthy(currentlySelected) ? currentlySelected : ChooseBest(eligible);
		}

		if (desired == null)
		{
			if (_selectedSessionId.Length > 0)
			{
				_missingSessionSince ??= nowUtc;
				ResetMetadataStabilityLocked();
				if (nowUtc - _missingSessionSince.Value < TimeSpan.FromMilliseconds(400)) return _selectedSessionId;
			}
			_selectedSessionId = string.Empty;
			_candidateSessionId = string.Empty;
			ResetMetadataStabilityLocked();
			return string.Empty;
		}
		_missingSessionSince = null;

		if (string.Equals(desired.SessionId, _selectedSessionId, StringComparison.Ordinal))
		{
			_candidateSessionId = string.Empty;
			return _selectedSessionId;
		}

		// A vanished or blacklisted source falls back immediately. Returning to a
		// preferred source waits briefly so transient GSMTC sessions cannot flap.
		if (currentlySelected == null)
		{
			CommitSelectionLocked(desired.SessionId);
			return _selectedSessionId;
		}

		if (!string.Equals(_candidateSessionId, desired.SessionId, StringComparison.Ordinal))
		{
			_candidateSessionId = desired.SessionId;
			_candidateSinceUtc = nowUtc;
			return _selectedSessionId;
		}
		if (nowUtc - _candidateSinceUtc >= PreferredReturnStability)
		{
			CommitSelectionLocked(desired.SessionId);
		}
		return _selectedSessionId;
	}

	private bool IsMetadataStable(TrackInfo track, string sessionId, DateTimeOffset nowUtc)
	{
		// Late enrichment adds evidence; it is not a new raw recording transition.
		string identity = sessionId + "|" + LyricsService.LookupIdentity(track with { EnrichedArtistCredit = null, SearchAlternates = null });
		lock (_gate)
		{
			if (string.Equals(identity, _stableMetadataIdentity, StringComparison.Ordinal)) return true;
			if (!string.Equals(identity, _pendingMetadataIdentity, StringComparison.Ordinal))
			{
				_stableMetadataIdentity = string.Empty;
				_pendingMetadataIdentity = identity;
				_pendingMetadataSinceUtc = nowUtc;
				return false;
			}
			if (nowUtc - _pendingMetadataSinceUtc < MetadataStability) return false;
			_stableMetadataIdentity = identity;
			return true;
		}
	}

	private (TimeSpan Position, DateTimeOffset CapturedAtUtc, long Revision, MediaTimelineChange Change) StabilizeTimeline(
		MediaSessionInfo session,
		TrackInfo track,
		DateTimeOffset nowUtc,
		bool metadataStable)
	{
		string identity = session.SessionId + "|" + track.StableIdentityKey;
		TimeSpan providerPosition = ClampPosition(session.Position, track.Duration);
		lock (_gate)
		{
			if (!string.Equals(identity, _timelineIdentity, StringComparison.Ordinal)
				|| _stableTimelineCapturedAtUtc == DateTimeOffset.MinValue)
			{
				_timelineIdentity = identity;
				_stableTimelinePosition = providerPosition;
				_stableTimelineCapturedAtUtc = nowUtc;
				_stableTimelineWasPlaying = session.IsPlaying;
				_lastProviderTimelineUpdatedAtUtc = session.TimelineUpdatedAtUtc;
				_timelineHasStableMetadata = metadataStable;
				_timelineChange = MediaTimelineChange.None;
				_pendingSeekUntilUtc = DateTimeOffset.MinValue;
				_pendingBackwardOffsetSeconds = null;
				_pendingBackwardOffsetSinceUtc = DateTimeOffset.MinValue;
				return (_stableTimelinePosition, _stableTimelineCapturedAtUtc, _timelineRevision, _timelineChange);
			}
			_timelineHasStableMetadata |= metadataStable;

			TimeSpan expected = _stableTimelinePosition;
			TimeSpan elapsed = nowUtc - _stableTimelineCapturedAtUtc;
			if (_stableTimelineWasPlaying && elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromSeconds(10))
			{
				expected += elapsed;
			}
			expected = ClampPosition(expected, track.Duration);
			double deltaSeconds = (providerPosition - expected).TotalSeconds;
			TimeSpan result;

			if (nowUtc < _pendingSeekUntilUtc)
			{
				if (Math.Abs(deltaSeconds) <= 1.25)
				{
					_pendingSeekUntilUtc = DateTimeOffset.MinValue;
					result = session.IsPlaying && providerPosition < expected ? expected : providerPosition;
				}
				else
				{
					result = expected;
				}
			}
			else if (_timelineHasStableMetadata && IsFreshTimelineLocked(session, nowUtc)
				&& Math.Abs(deltaSeconds) >= MinimumExternalSeekJump.TotalSeconds)
			{
				// Repeat state alone is not evidence of a new cycle. The provider must
				// publish a fresh, large position discontinuity for the same identity.
				double edgeSeconds = Math.Clamp(track.Duration.TotalSeconds * 0.02, 0.5, 3.0);
				bool wrapped = _stableTimelineWasPlaying && session.IsPlaying && track.Duration > TimeSpan.Zero
					&& track.Duration.TotalSeconds - _stableTimelinePosition.TotalSeconds <= edgeSeconds
					&& providerPosition.TotalSeconds <= edgeSeconds
					&& elapsed <= FreshTimelineAge + FreshTimelineAge;
				_timelineRevision++;
				_timelineChange = wrapped ? MediaTimelineChange.Wrap : MediaTimelineChange.Seek;
				result = providerPosition;
				ClearPendingBackwardCorrectionLocked();
			}
			else if (session.IsPlaying)
			{
				if (deltaSeconds >= 1.5)
				{
					// A forward jump cannot cause lyric-line oscillation, so accept it at once.
					result = providerPosition;
					ClearPendingBackwardCorrectionLocked();
				}
				else if (deltaSeconds >= 0.0)
				{
					result = expected + TimeSpan.FromSeconds(Math.Min(deltaSeconds, 0.12));
					ClearPendingBackwardCorrectionLocked();
				}
				else if (deltaSeconds > -1.0 || !IsStableBackwardCorrectionLocked(deltaSeconds, nowUtc))
				{
					// GSMTC providers (notably Apple Music) often publish a slightly older
					// position after a newer one. Keep playback monotonic until a real
					// backward jump remains consistent for several polls.
					result = expected;
				}
				else
				{
					result = providerPosition;
					ClearPendingBackwardCorrectionLocked();
				}
			}
			else
			{
				double pausedDeltaSeconds = (providerPosition - _stableTimelinePosition).TotalSeconds;
				if (pausedDeltaSeconds >= 0.0)
				{
					result = providerPosition;
					ClearPendingBackwardCorrectionLocked();
				}
				else if (pausedDeltaSeconds > -1.0 || !IsStableBackwardCorrectionLocked(pausedDeltaSeconds, nowUtc))
				{
					result = _stableTimelinePosition;
				}
				else
				{
					result = providerPosition;
					ClearPendingBackwardCorrectionLocked();
				}
			}

			_stableTimelinePosition = ClampPosition(result, track.Duration);
			_stableTimelineCapturedAtUtc = nowUtc;
			_stableTimelineWasPlaying = session.IsPlaying;
			if (session.TimelineUpdatedAtUtc > _lastProviderTimelineUpdatedAtUtc)
			{
				_lastProviderTimelineUpdatedAtUtc = session.TimelineUpdatedAtUtc;
			}
			return (_stableTimelinePosition, _stableTimelineCapturedAtUtc, _timelineRevision, _timelineChange);
		}
	}

	private bool IsFreshTimelineLocked(MediaSessionInfo session, DateTimeOffset nowUtc)
	{
		TimeSpan age = nowUtc - session.TimelineUpdatedAtUtc;
		return session.HasTimeline && _lastProviderTimelineUpdatedAtUtc != default
			&& session.TimelineUpdatedAtUtc > _lastProviderTimelineUpdatedAtUtc
			&& age >= TimeSpan.Zero && age <= FreshTimelineAge;
	}

	private bool IsStableBackwardCorrectionLocked(double offsetSeconds, DateTimeOffset nowUtc)
	{
		if (!_pendingBackwardOffsetSeconds.HasValue
			|| Math.Abs(_pendingBackwardOffsetSeconds.Value - offsetSeconds) > 0.8)
		{
			_pendingBackwardOffsetSeconds = offsetSeconds;
			_pendingBackwardOffsetSinceUtc = nowUtc;
			return false;
		}
		return nowUtc - _pendingBackwardOffsetSinceUtc >= TimeSpan.FromSeconds(1.8);
	}

	private void ClearPendingBackwardCorrectionLocked()
	{
		_pendingBackwardOffsetSeconds = null;
		_pendingBackwardOffsetSinceUtc = DateTimeOffset.MinValue;
	}

	private static TimeSpan ClampPosition(TimeSpan position, TimeSpan duration)
	{
		if (position < TimeSpan.Zero) return TimeSpan.Zero;
		if (duration > TimeSpan.Zero && position > duration) return duration;
		return position;
	}

	private static MediaSessionInfo? ChooseBest(IEnumerable<MediaSessionInfo> sessions)
	{
		return sessions
			.OrderByDescending(session => session.IsPlaying)
			.ThenByDescending(session => session.IsCurrentSession)
			.ThenByDescending(session => session.Metadata.HasTitle)
			.ThenByDescending(session => session.LastActivityUtc)
			.FirstOrDefault();
	}

	private static bool IsHealthy(MediaSessionInfo? session) => session != null;

	private void CommitSelectionLocked(string sessionId)
	{
		if (!string.Equals(_selectedSessionId, sessionId, StringComparison.Ordinal)) ResetMetadataStabilityLocked();
		_selectedSessionId = sessionId;
		_candidateSessionId = string.Empty;
		_candidateSinceUtc = DateTimeOffset.MinValue;
	}

	private void ResetMetadataStabilityLocked()
	{
		_pendingMetadataIdentity = string.Empty;
		_pendingMetadataSinceUtc = DateTimeOffset.MinValue;
		_stableMetadataIdentity = string.Empty;
		ResetTimelineLocked();
	}

	private void ResetTimelineLocked()
	{
		_timelineIdentity = string.Empty;
		_stableTimelinePosition = TimeSpan.Zero;
		_stableTimelineCapturedAtUtc = DateTimeOffset.MinValue;
		_stableTimelineWasPlaying = false;
		_lastProviderTimelineUpdatedAtUtc = DateTimeOffset.MinValue;
		_timelineHasStableMetadata = false;
		_timelineChange = MediaTimelineChange.None;
		_pendingSeekUntilUtc = DateTimeOffset.MinValue;
		ClearPendingBackwardCorrectionLocked();
	}

	private string GetSelectedSessionId()
	{
		lock (_gate) return _selectedSessionId;
	}

	private void Provider_SessionsChanged(object? sender, EventArgs e)
	{
		if (e is MediaMetadataChangedEventArgs change)
		{
			lock (_gate)
			{
				if (_selectedSessionId.Length > 0 && change.SessionId != _selectedSessionId) return;
				_metadataRevision++;
				_pendingMetadataIdentity = _stableMetadataIdentity = string.Empty;
			}
		}
		SessionsChanged?.Invoke(this, e);
	}
}
