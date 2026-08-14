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
		lock (_gate)
		{
			_preferredSourceId = preferredSourceId?.Trim() ?? string.Empty;
			_ignoredSourceIds.Clear();
			if (ignoredSourceIds != null)
			{
				foreach (string sourceId in ignoredSourceIds.Where(id => !string.IsNullOrWhiteSpace(id)))
				{
					_ignoredSourceIds.Add(sourceId.Trim());
				}
			}
			if (_ignoredSourceIds.Contains(_preferredSourceId)) _preferredSourceId = string.Empty;
			_candidateSessionId = string.Empty;
			_candidateSinceUtc = DateTimeOffset.MinValue;
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
			selected.Metadata.Duration);
		if (!IsMetadataStable(track, selected.SessionId, DateTimeOffset.UtcNow)) return null;

		MediaPlaybackCapabilities capabilities = selected.Capabilities;
		return new PlaybackSnapshot(
			track,
			selected.Position,
			selected.IsPlaying,
			selected.CapturedAtUtc,
			capabilities.CanTogglePlayPause || capabilities.CanPlay || capabilities.CanPause,
			capabilities.CanPrevious,
			capabilities.CanNext,
			capabilities.CanSeek,
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
		return !string.IsNullOrEmpty(sessionId) && await _provider.TrySeekAsync(sessionId, position, cancellationToken);
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
