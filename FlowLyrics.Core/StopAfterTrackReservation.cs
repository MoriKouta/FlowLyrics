using System;

namespace FlowLyrics.Core;

/// <summary>One-shot, observation-based natural-end detection. Never changes audio volume.</summary>
public sealed class StopAfterTrackReservation
{
	private MediaSessionInfo? _previous;
	private string _sessionId = "";
	private string _track = "";
	private double _continuousSeconds;
	public bool IsArmed => _sessionId.Length > 0;

	public static string TrackIdentity(MediaSessionInfo session) => string.Join("\0", session.Metadata.TitleRaw,
		session.Metadata.ArtistRaw, session.Metadata.AlbumRaw, session.Metadata.Duration.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));

	public bool Arm(MediaSessionInfo session)
	{
		Cancel();
		if (!session.Metadata.HasTitle || session.Metadata.Duration <= TimeSpan.Zero || !session.HasTimeline
			|| (!session.Capabilities.CanPause && !session.Capabilities.CanStop)
			|| session.RepeatMode is MediaRepeatMode.List or MediaRepeatMode.Track) return false;
		_sessionId = session.SessionId; _track = TrackIdentity(session); _previous = session;
		return IsArmed;
	}

	public void Cancel() { _sessionId = _track = ""; _previous = null; _continuousSeconds = 0; }

	// A returned session identifies a single pause/stop request, including a boundary
	// fallback. GSMTC exposes no transition reason; ambiguous jumps are not enough.
	public MediaSessionInfo? Observe(MediaSessionInfo? current, DateTimeOffset now)
	{
		if (!IsArmed || _previous == null) return null;
		var previous = _previous;
		double elapsed = (now - previous.CapturedAtUtc).TotalSeconds;
		if (current == null || !current.Metadata.HasTitle || current.PlaybackState == MediaPlaybackState.Changing)
		{
			if (elapsed > 1.5) Cancel();
			return null;
		}
		if (current.SessionId != _sessionId || current.RepeatMode is MediaRepeatMode.List or MediaRepeatMode.Track
			|| elapsed < 0 || elapsed > 1.5 || !current.HasTimeline) { Cancel(); return null; }
		bool changed = TrackIdentity(current) != _track;
		double duration = previous.Metadata.Duration.TotalSeconds;
		DateTimeOffset expectedEnd = previous.CapturedAtUtc.AddSeconds(duration - previous.Position.TotalSeconds);
		if (changed)
		{
			bool natural = _continuousSeconds >= 3 && previous.IsPlaying && previous.Position.TotalSeconds >= duration - 1.0
				&& now >= expectedEnd && current.IsPlaying && current.Position.TotalSeconds <= 2
				&& current.TimelineUpdatedAtUtc >= expectedEnd && current.TimelineUpdatedAtUtc <= now;
			Cancel();
			return natural ? current : null;
		}
		double advanced = (current.Position - previous.Position).TotalSeconds;
		if (advanced < -.2 || advanced > elapsed + .75) { Cancel(); return null; } // External seek/jump.
		if (previous.IsPlaying && current.IsPlaying && elapsed > 0 && advanced >= Math.Max(0, elapsed - .75))
			_continuousSeconds += elapsed;
		else if (!current.IsPlaying && current.Position.TotalSeconds < duration - .1) _continuousSeconds = 0;
		bool ended = _continuousSeconds >= 3 && previous.IsPlaying && current.Position.TotalSeconds >= duration
			&& current.PlaybackState is MediaPlaybackState.Playing or MediaPlaybackState.Paused or MediaPlaybackState.Stopped;
		_previous = current;
		if (!ended) return null;
		Cancel();
		return current.IsPlaying ? current : null; // Already paused/stopped: consume without toggling playback.
	}
}
