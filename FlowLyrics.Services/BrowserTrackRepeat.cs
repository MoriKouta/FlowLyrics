using System;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

/// <summary>One-track browser repeat. All state is local; native repeat always wins.</summary>
public sealed class BrowserTrackRepeat : IDisposable
{
	private const double BoundaryLeadSeconds = .12;
	private const double RequiredContinuousSeconds = 2;
	private readonly MediaSessionService _media;
	private readonly Func<DateTimeOffset> _now;
	private readonly Func<TimeSpan, CancellationToken, Task> _delay;
	private CancellationTokenSource? _scheduled;
	private MediaSessionInfo? _previous;
	private string? _target, _failedTarget;
	private double _continuous;
	private DateTimeOffset? _due;
	private bool _executing, _disposed;
	private int _scheduleRevision;
	public bool Enabled => _target != null;
	public Task Pending { get; private set; } = Task.CompletedTask;
	public event EventHandler? Changed;
	public event EventHandler? Failed;

	public BrowserTrackRepeat(MediaSessionService media, Func<DateTimeOffset>? now = null,
		Func<TimeSpan, CancellationToken, Task>? delay = null)
	{ _media = media; _now = now ?? (() => DateTimeOffset.UtcNow); _delay = delay ?? Task.Delay; }

	public static bool Eligible(MediaSessionInfo? session) => session is { HasTimeline: true, Metadata.HasTitle: true }
		&& MediaSourceClassifier.IsBrowser(session.SourceAppUserModelId) && !session.Capabilities.CanRepeat
		&& session.Capabilities.CanSeek && session.Capabilities.CanPlay && session.Metadata.Duration.TotalSeconds > 0;
	public static string Capability(MediaSessionInfo? session) => session?.Capabilities.CanRepeat == true ? "Native" : Eligible(session) ? "Fallback" : "Unsupported";
	private static string Key(MediaSessionInfo session) => session.SessionId + "\0" + StopAfterTrackReservation.TrackIdentity(session);
	public bool Available(MediaSessionInfo? session) => !_disposed && Eligible(session) && Key(session!) != _failedTarget;

	public bool Enable(MediaSessionInfo session)
	{
		Cancel();
		if (!Available(session)) return false;
		_target = Key(session); _previous = session; Changed?.Invoke(this, EventArgs.Empty); return true;
	}

	public void Cancel()
	{
		CancelSchedule(); _target = null; _previous = null; _continuous = 0;
		Changed?.Invoke(this, EventArgs.Empty);
	}
	private void CancelSchedule() { _scheduleRevision++; _scheduled?.Cancel(); _scheduled?.Dispose(); _scheduled = null; _due = null; _executing = false; }

	public void Observe(MediaSessionUpdate update)
	{
		var current = update.Session;
		if (current != null && Key(current) != _failedTarget) _failedTarget = null;
		if (!Enabled) return;
		if (update.State != MediaMetadataState.Stable || !Available(current) || Key(current!) != _target) { Cancel(); return; }
		if (_executing) return;
		var previous = _previous; _previous = current;
		if (previous == null) return;
		double elapsed = (current!.CapturedAtUtc - previous.CapturedAtUtc).TotalSeconds;
		double advanced = (current.Position - previous.Position).TotalSeconds;
		// A natural browser loop is already a new cycle. Never add a second seek.
		bool wrapped = previous.Position.TotalSeconds > previous.Metadata.Duration.TotalSeconds - 1 && current.Position.TotalSeconds < 1;
		if (wrapped || !previous.IsPlaying || !current.IsPlaying || elapsed < 0 || elapsed > 1.5 || Math.Abs(advanced - elapsed) > .35)
		{ CancelSchedule(); _continuous = 0; return; }
		_continuous += elapsed;
		double remaining = current.Metadata.Duration.TotalSeconds - current.Position.TotalSeconds;
		if (_continuous < RequiredContinuousSeconds || remaining > 1.5) return;
		DateTimeOffset due = current.CapturedAtUtc.AddSeconds(Math.Max(0, remaining - BoundaryLeadSeconds));
		if (_due.HasValue && Math.Abs((_due.Value - due).TotalSeconds) < .05) return;
		CancelSchedule(); _due = due; _scheduled = new();
		Pending = RestartAtBoundaryAsync(current, due, _scheduleRevision, _scheduled.Token);
	}

	private async Task RestartAtBoundaryAsync(MediaSessionInfo expected, DateTimeOffset due, int revision, CancellationToken token)
	{
		try
		{
			TimeSpan wait = due - _now();
			if (wait > TimeSpan.Zero) await _delay(wait, token);
			token.ThrowIfCancellationRequested();
			_executing = true;
			// Refresh at the deadline: polling-time predictions alone cannot authorize a seek.
			var update = await _media.GetUpdateAsync(token);
			var fresh = update.Session;
			if (update.State != MediaMetadataState.Stable || !Available(fresh) || Key(fresh!) != _target) { Cancel(); return; }
			double predicted = expected.Position.TotalSeconds + (_now() - expected.CapturedAtUtc).TotalSeconds;
			double remaining = fresh!.Metadata.Duration.TotalSeconds - fresh.Position.TotalSeconds;
			if (fresh.Position.TotalSeconds < 1) return; // Player's own loop won the race.
			if (Math.Abs(fresh.Position.TotalSeconds - predicted) > .35 || remaining > .2 || _now() - due > TimeSpan.FromMilliseconds(350)) return;
			if (!fresh.IsPlaying && fresh.Position < fresh.Metadata.Duration) return;
			bool accepted = await _media.TryRestartTrackAsync(fresh, token);
			token.ThrowIfCancellationRequested();
			if (!accepted) { _failedTarget = Key(fresh); Cancel(); Failed?.Invoke(this, EventArgs.Empty); }
		}
		catch (OperationCanceledException) { }
		catch (Exception) { _failedTarget = Key(expected); Cancel(); Failed?.Invoke(this, EventArgs.Empty); }
		finally { if (revision == _scheduleRevision) { _executing = false; _due = null; _continuous = 0; _previous = null; } }
	}
	public void Dispose() { _disposed = true; Cancel(); }
}
