using System;

namespace FlowLyrics.Models;

public sealed record PlaybackSnapshot(TrackInfo Track, TimeSpan Position, bool IsPlaying, DateTimeOffset CapturedAtUtc, bool CanTogglePlayPause = true, bool CanSkipPrevious = true, bool CanSkipNext = true)
{
	public TimeSpan EstimatedPosition(DateTimeOffset nowUtc)
	{
		TimeSpan position = Position;
		if (IsPlaying)
		{
			TimeSpan timeSpan = nowUtc - CapturedAtUtc;
			if (timeSpan > TimeSpan.Zero && timeSpan < TimeSpan.FromSeconds(10L))
			{
				position += timeSpan;
			}
		}
		if (position < TimeSpan.Zero)
		{
			return TimeSpan.Zero;
		}
		if (Track.Duration > TimeSpan.Zero && position > Track.Duration)
		{
			return Track.Duration;
		}
		return position;
	}
}
