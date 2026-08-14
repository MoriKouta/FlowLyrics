using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using Windows.Media.Control;

namespace FlowLyrics.Services;

/// <summary>
/// Windows-specific GSMTC adapter. The rest of FlowLyrics consumes only the
/// platform-neutral contracts in FlowLyrics.Core.
/// </summary>
public sealed class WindowsMediaSessionProvider : IMediaSessionProvider
{
	private readonly object _gate = new();

	private readonly SemaphoreSlim _initializationGate = new(1, 1);

	private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _sessionsById = new(StringComparer.Ordinal);

	private readonly HashSet<GlobalSystemMediaTransportControlsSession> _subscribedSessions = new(SessionReferenceComparer.Instance);

	private readonly Dictionary<string, DateTimeOffset> _lastActivityById = new(StringComparer.Ordinal);

	private GlobalSystemMediaTransportControlsSessionManager? _manager;

	private DateTimeOffset _lastInitializationAttempt = DateTimeOffset.MinValue;

	private bool _disposed;

	public event EventHandler? SessionsChanged;

	public async Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default)
	{
		GlobalSystemMediaTransportControlsSessionManager? manager = await EnsureManagerAsync(cancellationToken);
		if (manager == null)
		{
			return Array.Empty<MediaSessionInfo>();
		}

		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions = manager.GetSessions().ToArray();
			GlobalSystemMediaTransportControlsSession? currentSession = manager.GetCurrentSession();
			SynchronizeSessionSubscriptions(sessions);

			List<MediaSessionInfo> result = new(sessions.Count);
			foreach (GlobalSystemMediaTransportControlsSession session in sessions)
			{
				cancellationToken.ThrowIfCancellationRequested();
				MediaSessionInfo? info = await CreateSessionInfoAsync(session, currentSession, cancellationToken);
				if (info != null)
				{
					result.Add(info);
				}
			}
			return result;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("Media Session enumeration failed: " + ex.GetType().Name + ": " + ex.Message);
			ResetManager();
			return Array.Empty<MediaSessionInfo>();
		}
	}

	public async Task<bool> TryTogglePlayPauseAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		GlobalSystemMediaTransportControlsSession? session = await ResolveSessionAsync(sessionId, cancellationToken);
		if (session == null) return false;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			return await session.TryTogglePlayPauseAsync();
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex)
		{
			LogCommandFailure("toggle play/pause", ex);
			return false;
		}
	}

	public async Task<bool> TrySkipNextAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		GlobalSystemMediaTransportControlsSession? session = await ResolveSessionAsync(sessionId, cancellationToken);
		if (session == null) return false;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			return await session.TrySkipNextAsync();
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex)
		{
			LogCommandFailure("skip next", ex);
			return false;
		}
	}

	public async Task<bool> TrySkipPreviousAsync(string sessionId, CancellationToken cancellationToken = default)
	{
		GlobalSystemMediaTransportControlsSession? session = await ResolveSessionAsync(sessionId, cancellationToken);
		if (session == null) return false;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			return await session.TrySkipPreviousAsync();
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex)
		{
			LogCommandFailure("skip previous", ex);
			return false;
		}
	}

	public async Task<bool> TrySeekAsync(string sessionId, TimeSpan position, CancellationToken cancellationToken = default)
	{
		GlobalSystemMediaTransportControlsSession? session = await ResolveSessionAsync(sessionId, cancellationToken);
		if (session == null) return false;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			return await session.TryChangePlaybackPositionAsync(Math.Max(0L, position.Ticks));
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex)
		{
			LogCommandFailure("seek", ex);
			return false;
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (_disposed) return;
			_disposed = true;
			UnsubscribeManagerLocked();
			foreach (GlobalSystemMediaTransportControlsSession session in _subscribedSessions.ToArray())
			{
				UnsubscribeSession(session);
			}
			_subscribedSessions.Clear();
			_sessionsById.Clear();
			_lastActivityById.Clear();
		}
		_initializationGate.Dispose();
	}

	private async Task<GlobalSystemMediaTransportControlsSessionManager?> EnsureManagerAsync(CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			if (_disposed) return null;
			if (_manager != null) return _manager;
			if (DateTimeOffset.UtcNow - _lastInitializationAttempt < TimeSpan.FromSeconds(5)) return null;
		}

		await _initializationGate.WaitAsync(cancellationToken);
		try
		{
			lock (_gate)
			{
				if (_disposed) return null;
				if (_manager != null) return _manager;
				if (DateTimeOffset.UtcNow - _lastInitializationAttempt < TimeSpan.FromSeconds(5)) return null;
				_lastInitializationAttempt = DateTimeOffset.UtcNow;
			}

			GlobalSystemMediaTransportControlsSessionManager manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
			cancellationToken.ThrowIfCancellationRequested();
			lock (_gate)
			{
				if (_disposed) return null;
				_manager = manager;
				_manager.SessionsChanged += Manager_SessionsChanged;
				_manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
				return _manager;
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("Media Session manager initialization failed: " + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
		finally
		{
			_initializationGate.Release();
		}
	}

	private async Task<MediaSessionInfo?> CreateSessionInfoAsync(
		GlobalSystemMediaTransportControlsSession session,
		GlobalSystemMediaTransportControlsSession? currentSession,
		CancellationToken cancellationToken)
	{
		try
		{
			GlobalSystemMediaTransportControlsSessionMediaProperties properties = await session.TryGetMediaPropertiesAsync();
			cancellationToken.ThrowIfCancellationRequested();
			GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = session.GetTimelineProperties();
			GlobalSystemMediaTransportControlsSessionPlaybackInfo playback = session.GetPlaybackInfo();
			GlobalSystemMediaTransportControlsSessionPlaybackControls controls = playback.Controls;
			DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
			TimeSpan duration = timeline.EndTime - timeline.StartTime;
			if (duration <= TimeSpan.Zero) duration = timeline.MaxSeekTime - timeline.MinSeekTime;
			if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

			TimeSpan position = timeline.Position;
			if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
				&& timeline.LastUpdatedTime != default)
			{
				TimeSpan elapsed = capturedAt - timeline.LastUpdatedTime;
				if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromHours(1)) position += elapsed;
			}
			if (position < TimeSpan.Zero) position = TimeSpan.Zero;
			if (duration > TimeSpan.Zero && position > duration) position = duration;

			string sourceId = session.SourceAppUserModelId?.Trim() ?? string.Empty;
			string sessionId = CreateSessionId(session, sourceId);
			DateTimeOffset lastActivity;
			lock (_gate)
			{
				if (!_lastActivityById.TryGetValue(sessionId, out lastActivity))
				{
					lastActivity = timeline.LastUpdatedTime == default ? capturedAt : timeline.LastUpdatedTime;
					_lastActivityById[sessionId] = lastActivity;
				}
				_sessionsById[sessionId] = session;
			}

			return new MediaSessionInfo
			{
				SessionId = sessionId,
				SourceAppUserModelId = sourceId,
				DisplaySourceName = MediaSourceClassifier.GetDisplayName(sourceId),
				Metadata = new MediaTrackMetadata(
					properties?.Title?.Trim() ?? string.Empty,
					properties?.Artist?.Trim() ?? string.Empty,
					properties?.AlbumTitle?.Trim() ?? string.Empty,
					duration),
				Position = position,
				TimelineUpdatedAtUtc = timeline.LastUpdatedTime,
				HasTimeline = duration > TimeSpan.Zero || position > TimeSpan.Zero,
				PlaybackState = MapPlaybackState(playback.PlaybackStatus),
				Capabilities = new MediaPlaybackCapabilities(
					controls.IsPlayEnabled,
					controls.IsPauseEnabled,
					controls.IsPlayPauseToggleEnabled,
					controls.IsNextEnabled,
					controls.IsPreviousEnabled,
					controls.IsPlaybackPositionEnabled),
				IsCurrentSession = ReferenceEquals(session, currentSession),
				LastActivityUtc = lastActivity,
				CapturedAtUtc = capturedAt
			};
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("Media Session metadata read failed for " + (session.SourceAppUserModelId ?? "unknown") + ": " + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}

	private async Task<GlobalSystemMediaTransportControlsSession?> ResolveSessionAsync(string sessionId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(sessionId)) return null;
		lock (_gate)
		{
			if (_sessionsById.TryGetValue(sessionId, out GlobalSystemMediaTransportControlsSession? existing)) return existing;
		}
		await GetSessionsAsync(cancellationToken);
		lock (_gate)
		{
			return _sessionsById.TryGetValue(sessionId, out GlobalSystemMediaTransportControlsSession? session) ? session : null;
		}
	}

	private void SynchronizeSessionSubscriptions(IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions)
	{
		lock (_gate)
		{
			if (_disposed) return;
			HashSet<GlobalSystemMediaTransportControlsSession> active = new(sessions, SessionReferenceComparer.Instance);
			foreach (GlobalSystemMediaTransportControlsSession removed in _subscribedSessions.Where(session => !active.Contains(session)).ToArray())
			{
				UnsubscribeSession(removed);
				_subscribedSessions.Remove(removed);
			}
			foreach (GlobalSystemMediaTransportControlsSession added in active.Where(session => !_subscribedSessions.Contains(session)))
			{
				SubscribeSession(added);
				_subscribedSessions.Add(added);
			}
			HashSet<string> activeIds = active.Select(session => CreateSessionId(session, session.SourceAppUserModelId ?? string.Empty)).ToHashSet(StringComparer.Ordinal);
			foreach (string staleId in _sessionsById.Keys.Where(id => !activeIds.Contains(id)).ToArray())
			{
				_sessionsById.Remove(staleId);
				_lastActivityById.Remove(staleId);
			}
		}
	}

	private void SubscribeSession(GlobalSystemMediaTransportControlsSession session)
	{
		session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
		session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
		session.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
	}

	private void UnsubscribeSession(GlobalSystemMediaTransportControlsSession session)
	{
		session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
		session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
		session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
	}

	private void Manager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
	{
		OnProviderChanged(null);
	}

	private void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
	{
		OnProviderChanged(null);
	}

	private void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
	{
		OnProviderChanged(sender);
	}

	private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
	{
		OnProviderChanged(sender);
	}

	private void Session_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
	{
		OnProviderChanged(sender);
	}

	private void OnProviderChanged(GlobalSystemMediaTransportControlsSession? sender)
	{
		if (sender != null)
		{
			string id = CreateSessionId(sender, sender.SourceAppUserModelId ?? string.Empty);
			lock (_gate) _lastActivityById[id] = DateTimeOffset.UtcNow;
		}
		SessionsChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ResetManager()
	{
		lock (_gate)
		{
			UnsubscribeManagerLocked();
			foreach (GlobalSystemMediaTransportControlsSession session in _subscribedSessions.ToArray()) UnsubscribeSession(session);
			_subscribedSessions.Clear();
			_sessionsById.Clear();
			_manager = null;
		}
	}

	private void UnsubscribeManagerLocked()
	{
		if (_manager == null) return;
		_manager.SessionsChanged -= Manager_SessionsChanged;
		_manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
	}

	private static string CreateSessionId(GlobalSystemMediaTransportControlsSession session, string sourceId)
	{
		return (string.IsNullOrWhiteSpace(sourceId) ? "media-session" : sourceId.Trim())
			+ "|" + RuntimeHelpers.GetHashCode(session).ToString("x8", CultureInfo.InvariantCulture);
	}

	private static MediaPlaybackState MapPlaybackState(GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
	{
		return status switch
		{
			GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed => MediaPlaybackState.Closed,
			GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => MediaPlaybackState.Opened,
			GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => MediaPlaybackState.Changing,
			GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => MediaPlaybackState.Stopped,
			GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackState.Playing,
			GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackState.Paused,
			_ => MediaPlaybackState.Unknown
		};
	}

	private static void LogCommandFailure(string command, Exception ex)
	{
		System.Diagnostics.Debug.WriteLine("Media Session " + command + " failed: " + ex.GetType().Name + ": " + ex.Message);
	}

	private sealed class SessionReferenceComparer : IEqualityComparer<GlobalSystemMediaTransportControlsSession>
	{
		public static readonly SessionReferenceComparer Instance = new();

		public bool Equals(GlobalSystemMediaTransportControlsSession? x, GlobalSystemMediaTransportControlsSession? y) => ReferenceEquals(x, y);

		public int GetHashCode(GlobalSystemMediaTransportControlsSession obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
