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

	private static readonly TimeSpan MetadataStability = TimeSpan.FromMilliseconds(650);

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

	public MediaSessionService(IMediaSessionProvider provider)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
		_provider.SessionsChanged += Provider_SessionsChanged;
	}

	public event EventHandler? SessionsChanged;

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
		DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
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
		IReadOnlyList<MediaSessionInfo> sessions = await GetSessionsAsync(cancellationToken);
		MediaSessionInfo? selected = sessions.FirstOrDefault(session => session.IsSelectedByFlowLyrics);
		if (selected == null || !selected.Metadata.HasTitle) return null;

		TrackInfo track = new(
			selected.Metadata.TitleRaw,
			selected.Metadata.ArtistRaw,
			selected.Metadata.AlbumRaw,
			selected.Metadata.Duration,
			SearchAlternates: selected.Metadata.SearchAlternates,
			OriginalMediaTitle: selected.Metadata.OriginalTitleRaw,
			OriginalMediaArtist: selected.Metadata.OriginalArtistRaw);
		DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
		if (!IsMetadataStable(track, selected.SessionId, nowUtc)) return null;

		MediaPlaybackCapabilities capabilities = selected.Capabilities;
		(TimeSpan stablePosition, DateTimeOffset stableCapturedAtUtc) = StabilizeTimeline(selected, track, nowUtc);
		return new PlaybackSnapshot(
			track,
			stablePosition,
			selected.IsPlaying,
			stableCapturedAtUtc,
			capabilities.CanTogglePlayPause || capabilities.CanPlay || capabilities.CanPause,
			capabilities.CanPrevious,
			capabilities.CanNext,
			capabilities.CanSeek || (selected.HasTimeline && track.Duration > TimeSpan.Zero),
			capabilities.CanPlay,
			capabilities.CanPause,
			selected.SessionId,
			selected.SourceAppUserModelId,
			selected.DisplaySourceName);
	}

	public async Task<bool> TryTogglePlayPauseAsync(CancellationToken cancellationToken = default)
	{
		string sessionId = GetSelectedSessionId();
		return !string.IsNullOrEmpty(sessionId) && await _provider.TryTogglePlayPauseAsync(sessionId, cancellationToken);
	}

	public async Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default)
	{
		string sessionId = GetSelectedSessionId();
		return !string.IsNullOrEmpty(sessionId) && await _provider.TrySkipNextAsync(sessionId, cancellationToken);
	}

	public async Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default)
	{
		string sessionId = GetSelectedSessionId();
		return !string.IsNullOrEmpty(sessionId) && await _provider.TrySkipPreviousAsync(sessionId, cancellationToken);
	}

	public async Task<bool> TrySeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
	{
		string sessionId = GetSelectedSessionId();
		if (string.IsNullOrEmpty(sessionId) || !await _provider.TrySeekAsync(sessionId, position, cancellationToken)) return false;
		DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
		lock (_gate)
		{
			if (string.Equals(sessionId, _selectedSessionId, StringComparison.Ordinal))
			{
				_stableTimelinePosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
				_stableTimelineCapturedAtUtc = nowUtc;
				_pendingSeekUntilUtc = nowUtc + TimeSpan.FromSeconds(2.5);
				_pendingBackwardOffsetSeconds = null;
				_pendingBackwardOffsetSinceUtc = DateTimeOffset.MinValue;
			}
		}
		return true;
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
			_selectedSessionId = string.Empty;
			_candidateSessionId = string.Empty;
			ResetMetadataStabilityLocked();
			return string.Empty;
		}

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
		string identity = sessionId + "|" + track.CacheKey;
		lock (_gate)
		{
			if (string.Equals(identity, _stableMetadataIdentity, StringComparison.Ordinal)) return true;
			if (!string.Equals(identity, _pendingMetadataIdentity, StringComparison.Ordinal))
			{
				_pendingMetadataIdentity = identity;
				_pendingMetadataSinceUtc = nowUtc;
				return false;
			}
			if (nowUtc - _pendingMetadataSinceUtc < MetadataStability) return false;
			_stableMetadataIdentity = identity;
			return true;
		}
	}

	private (TimeSpan Position, DateTimeOffset CapturedAtUtc) StabilizeTimeline(
		MediaSessionInfo session,
		TrackInfo track,
		DateTimeOffset nowUtc)
	{
		string identity = session.SessionId + "|" + track.Title.Trim().ToLowerInvariant()
			+ "|" + track.Artist.Trim().ToLowerInvariant()
			+ "|" + track.Album.Trim().ToLowerInvariant();
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
				_pendingSeekUntilUtc = DateTimeOffset.MinValue;
				_pendingBackwardOffsetSeconds = null;
				_pendingBackwardOffsetSinceUtc = DateTimeOffset.MinValue;
				return (_stableTimelinePosition, _stableTimelineCapturedAtUtc);
			}

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
			return (_stableTimelinePosition, _stableTimelineCapturedAtUtc);
		}
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

	private static bool IsHealthy(MediaSessionInfo? session) => session != null && session.Metadata.HasTitle;

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
		_pendingSeekUntilUtc = DateTimeOffset.MinValue;
		ClearPendingBackwardCorrectionLocked();
	}

	private string GetSelectedSessionId()
	{
		lock (_gate) return _selectedSessionId;
	}

	private void Provider_SessionsChanged(object? sender, EventArgs e)
	{
		SessionsChanged?.Invoke(this, EventArgs.Empty);
	}
}
