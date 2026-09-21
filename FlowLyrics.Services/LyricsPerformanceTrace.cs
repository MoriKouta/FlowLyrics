using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace FlowLyrics.Services;

/// <summary>Buffer timings in memory; logging never precedes first paint.</summary>
public sealed class LyricsPerformanceTrace
{
	private readonly Stopwatch _clock = Stopwatch.StartNew();
	private readonly ConcurrentQueue<string> _events = new();
	private int _flushed;
	private double _firstRenderMs;
	public LyricsPerformanceTrace() => Mark("MEDIA_CHANGED");
	public void Mark(string stage)
	{
		if (Volatile.Read(ref _flushed) >= 2) return;
		_events.Enqueue($"{DateTimeOffset.UtcNow:O} {stage} elapsedMs={_clock.Elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture)}");
	}
	public void FirstRender(AppLogger logger)
	{
		if (Interlocked.CompareExchange(ref _flushed, 1, 0) != 0) return;
		Mark("FIRST_LYRICS_RENDER");
		_firstRenderMs = _clock.Elapsed.TotalMilliseconds;
	}
	public void FullUiUpdated(AppLogger logger)
	{
		Mark("FULL_UI_UPDATED");
		if (Interlocked.CompareExchange(ref _flushed, 2, 1) != 1) return;
		string log = "lyrics-performance TrackChangeToFirstLyricsMs=" + _firstRenderMs.ToString("F2", CultureInfo.InvariantCulture)
			+ Environment.NewLine + string.Join(Environment.NewLine, _events);
		_ = Task.Run(() => logger.WriteAsync(log));
	}
}
