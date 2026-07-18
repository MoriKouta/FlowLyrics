using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;
using Windows.Media.Control;

namespace FlowLyrics.Services;

public sealed class MediaSessionService
{
	private GlobalSystemMediaTransportControlsSessionManager? _manager;

	private DateTimeOffset _lastInitializationAttempt = DateTimeOffset.MinValue;

	public async Task<PlaybackSnapshot?> GetSpotifySnapshotAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		await EnsureManagerAsync(cancellationToken);
		if ((object)_manager == null)
		{
			return null;
		}
		GlobalSystemMediaTransportControlsSession spotifySession = await GetSpotifySessionAsync(cancellationToken);
		if ((object)spotifySession == null)
		{
			return null;
		}
		try
		{
			GlobalSystemMediaTransportControlsSessionMediaProperties globalSystemMediaTransportControlsSessionMediaProperties = await spotifySession.TryGetMediaPropertiesAsync();
			cancellationToken.ThrowIfCancellationRequested();
			string text = globalSystemMediaTransportControlsSessionMediaProperties?.Title?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			string artist = globalSystemMediaTransportControlsSessionMediaProperties?.Artist?.Trim() ?? string.Empty;
			string album = globalSystemMediaTransportControlsSessionMediaProperties?.AlbumTitle?.Trim() ?? string.Empty;
			GlobalSystemMediaTransportControlsSessionTimelineProperties timelineProperties = spotifySession.GetTimelineProperties();
			GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = spotifySession.GetPlaybackInfo();
			bool flag = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
			GlobalSystemMediaTransportControlsSessionPlaybackControls controls = playbackInfo.Controls;
			TimeSpan timeSpan = timelineProperties.EndTime - timelineProperties.StartTime;
			if (timeSpan <= TimeSpan.Zero)
			{
				timeSpan = timelineProperties.MaxSeekTime - timelineProperties.MinSeekTime;
			}
			if (timeSpan < TimeSpan.Zero)
			{
				timeSpan = TimeSpan.Zero;
			}
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			TimeSpan timeSpan2 = timelineProperties.Position;
			if (flag && timelineProperties.LastUpdatedTime != default(DateTimeOffset))
			{
				TimeSpan timeSpan3 = utcNow - timelineProperties.LastUpdatedTime;
				if (timeSpan3 > TimeSpan.Zero && timeSpan3 < TimeSpan.FromHours(1))
				{
					timeSpan2 += timeSpan3;
				}
			}
			if (timeSpan2 < TimeSpan.Zero)
			{
				timeSpan2 = TimeSpan.Zero;
			}
			if (timeSpan > TimeSpan.Zero && timeSpan2 > timeSpan)
			{
				timeSpan2 = timeSpan;
			}
			return new PlaybackSnapshot(new TrackInfo(text, artist, album, timeSpan), timeSpan2, flag, utcNow, controls.IsPlayPauseToggleEnabled || controls.IsPlayEnabled || controls.IsPauseEnabled, controls.IsPreviousEnabled, controls.IsNextEnabled);
		}
		catch
		{
			return null;
		}
	}

	public async Task<bool> TryTogglePlayPauseAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = await GetSpotifySessionAsync(cancellationToken);
		if ((object)globalSystemMediaTransportControlsSession == null)
		{
			return false;
		}
		cancellationToken.ThrowIfCancellationRequested();
		return await globalSystemMediaTransportControlsSession.TryTogglePlayPauseAsync();
	}

	public async Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = await GetSpotifySessionAsync(cancellationToken);
		if ((object)globalSystemMediaTransportControlsSession == null)
		{
			return false;
		}
		cancellationToken.ThrowIfCancellationRequested();
		return await globalSystemMediaTransportControlsSession.TrySkipNextAsync();
	}

	public async Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = await GetSpotifySessionAsync(cancellationToken);
		if ((object)globalSystemMediaTransportControlsSession == null)
		{
			return false;
		}
		cancellationToken.ThrowIfCancellationRequested();
		return await globalSystemMediaTransportControlsSession.TrySkipPreviousAsync();
	}

	public async Task<bool> TrySeekAsync(TimeSpan position, CancellationToken cancellationToken = default(CancellationToken))
	{
		GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = await GetSpotifySessionAsync(cancellationToken);
		if ((object)globalSystemMediaTransportControlsSession == null)
		{
			return false;
		}
		cancellationToken.ThrowIfCancellationRequested();
		long requestedPlaybackPosition = Math.Max(0L, position.Ticks);
		return await globalSystemMediaTransportControlsSession.TryChangePlaybackPositionAsync(requestedPlaybackPosition);
	}

	private async Task<GlobalSystemMediaTransportControlsSession?> GetSpotifySessionAsync(CancellationToken cancellationToken)
	{
		await EnsureManagerAsync(cancellationToken);
		if ((object)_manager == null)
		{
			return null;
		}
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			GlobalSystemMediaTransportControlsSession globalSystemMediaTransportControlsSession = _manager.GetSessions().FirstOrDefault((GlobalSystemMediaTransportControlsSession session) => IsSpotifySession(session.SourceAppUserModelId));
			if ((object)globalSystemMediaTransportControlsSession == null)
			{
				GlobalSystemMediaTransportControlsSession currentSession = _manager.GetCurrentSession();
				if ((object)currentSession != null && IsSpotifySession(currentSession.SourceAppUserModelId))
				{
					globalSystemMediaTransportControlsSession = currentSession;
				}
			}
			return globalSystemMediaTransportControlsSession;
		}
		catch
		{
			_manager = null;
			return null;
		}
	}

	private async Task EnsureManagerAsync(CancellationToken cancellationToken)
	{
		if ((object)_manager != null)
		{
			return;
		}
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		if (utcNow - _lastInitializationAttempt < TimeSpan.FromSeconds(5L))
		{
			return;
		}
		_lastInitializationAttempt = utcNow;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			_manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
		}
		catch
		{
			_manager = null;
		}
	}

	private static bool IsSpotifySession(string? sourceAppUserModelId)
	{
		if (!string.IsNullOrWhiteSpace(sourceAppUserModelId))
		{
			return sourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
