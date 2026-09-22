using System;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

/// <summary>Best-effort player commands; observed player state remains authoritative.</summary>
public sealed class PlaybackCommandCoordinator : IDisposable
{
	private readonly MediaSessionService _media;
	private readonly Func<DateTimeOffset> _utcNow;
	private readonly AppLogger? _logger;
	private readonly StopAfterTrackReservation _reservation = new();
	private CancellationTokenSource _reservationCancellation = new();
	private MediaSessionUpdate? _latest;
	private int _revision;
	private bool _arming;
	public event EventHandler? Changed;
	public event EventHandler? CommandFailed;
	public bool IsArmed => _reservation.IsArmed;
	public bool IsArming => _arming;
	public bool RepeatBusy { get; private set; }
	public MediaRepeatMode? RepeatMode => _latest?.Session?.RepeatMode;
	public bool CanRepeat => _latest?.Session?.Capabilities.CanRepeat == true && RepeatMode.HasValue;
	public bool CanArm => _latest is { State: MediaMetadataState.Stable, Session: { } session }
		&& session.Metadata.Duration > TimeSpan.Zero && session.HasTimeline && (session.Capabilities.CanPause || session.Capabilities.CanStop);
	public Task PendingStop { get; private set; } = Task.CompletedTask;

	public PlaybackCommandCoordinator(MediaSessionService media, AppLogger? logger = null, Func<DateTimeOffset>? utcNow = null)
	{
		_media = media; _logger = logger; _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
		_media.PlaybackNavigationRequested += NavigationRequested;
	}
	private void NavigationRequested(object? sender, EventArgs e) => Cancel();

	public void Observe(MediaSessionUpdate update)
	{
		_latest = update;
		if (update.State == MediaMetadataState.NoSession) Cancel();
		MediaSessionInfo? stop = _reservation.Observe(update.Session, _utcNow());
		if (stop != null) PendingStop = StopAsync(stop, _reservationCancellation.Token);
		Changed?.Invoke(this, EventArgs.Empty);
	}

	public void Cancel()
	{
		_revision++;
		_reservation.Cancel();
		_reservationCancellation.Cancel(); _reservationCancellation.Dispose(); _reservationCancellation = new();
		Changed?.Invoke(this, EventArgs.Empty);
	}

	public static MediaRepeatMode NextRepeat(MediaRepeatMode current) => current switch
	{ MediaRepeatMode.None => MediaRepeatMode.List, MediaRepeatMode.List => MediaRepeatMode.Track, _ => MediaRepeatMode.None };

	public async Task<bool> CycleRepeatAsync()
	{
		if (!CanRepeat || RepeatBusy || _arming || _latest?.Session is not { } session) return false;
		Cancel(); RepeatBusy = true; Changed?.Invoke(this, EventArgs.Empty);
		try
		{
			var mode = NextRepeat(session.RepeatMode!.Value);
			bool accepted = await _media.TrySetRepeatAsync(session.SessionId, mode);
			Log("Repeat" + mode, accepted); return accepted;
		}
		catch (Exception) { return false; }
		finally { RepeatBusy = false; Changed?.Invoke(this, EventArgs.Empty); }
	}

	public async Task<bool> ToggleStopAfterTrackAsync()
	{
		if (IsArmed || _arming) { Cancel(); return true; }
		if (!CanArm || RepeatBusy || _latest?.Session is not { } original) return false;
		Cancel(); int revision = _revision;
		CancellationToken token = _reservationCancellation.Token;
		_arming = true; Changed?.Invoke(this, EventArgs.Empty);
		try
		{
			if (original.RepeatMode is MediaRepeatMode.List or MediaRepeatMode.Track)
			{
				bool accepted = await _media.TrySetRepeatAsync(original.SessionId, MediaRepeatMode.None, token);
				Log("RepeatNoneForStopAfterTrack", accepted);
				if (!accepted) return false;
				for (int attempt = 0; attempt < 10; attempt++)
				{
					await Task.Delay(80, token);
					Observe(await _media.GetUpdateAsync(token));
					if (_latest is { State: MediaMetadataState.Stable, Session.RepeatMode: MediaRepeatMode.None }) break;
				}
			}
			if (revision != _revision || !CanArm || _latest?.Session is not { } current
				|| current.SessionId != original.SessionId || StopAfterTrackReservation.TrackIdentity(current) != StopAfterTrackReservation.TrackIdentity(original)) return false;
			bool armed = _reservation.Arm(current); Log("StopAfterTrackArm", armed); return armed;
		}
		catch (Exception) { return false; }
		finally { _arming = false; Changed?.Invoke(this, EventArgs.Empty); }
	}

	private async Task StopAsync(MediaSessionInfo target, CancellationToken token)
	{
		try
		{
			bool accepted = await _media.TryPauseOrStopAsync(target.SessionId, token, StopAfterTrackReservation.TrackIdentity(target));
			Log("StopAfterTrackPauseOrStop", accepted);
			if (!accepted) CommandFailed?.Invoke(this, EventArgs.Empty);
		}
		catch (OperationCanceledException) { }
		catch (Exception) { Log("StopAfterTrackPauseOrStop", false); CommandFailed?.Invoke(this, EventArgs.Empty); }
	}

	private void Log(string action, bool accepted) { if (_logger != null) _ = _logger.WriteAsync("MEDIA COMMAND action=" + action + " accepted=" + accepted); }
	public void Dispose()
	{
		_media.PlaybackNavigationRequested -= NavigationRequested;
		_reservationCancellation.Cancel(); _reservationCancellation.Dispose();
	}
}
