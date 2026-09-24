using System;

namespace FlowLyrics.Models;

public sealed record PlaybackSnapshot(
	TrackInfo Track,
	TimeSpan Position,
	bool IsPlaying,
	DateTimeOffset CapturedAtUtc,
	bool CanTogglePlayPause = true,
	bool CanSkipPrevious = true,
	bool CanSkipNext = true,
	bool CanSeek = true,
	bool CanPlay = true,
	bool CanPause = true,
	string SessionId = "",
	string SourceAppUserModelId = "",
	string SourceDisplayName = "Media Session",
	bool CanStop = false,
	bool CanRepeat = false,
	FlowLyrics.Core.MediaRepeatMode? RepeatMode = null,
	long TimelineRevision = 0,
	FlowLyrics.Core.MediaTimelineChange TimelineChange = FlowLyrics.Core.MediaTimelineChange.None,
	bool CanShuffle = false,
	bool? ShuffleActive = null)
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
