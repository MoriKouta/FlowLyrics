using System;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class TimelineLoopTests
{
	[Theory]
	[InlineData(MediaRepeatMode.Track)]
	[InlineData(MediaRepeatMode.List)]
	[InlineData(MediaRepeatMode.None)]
	public async Task FreshSameTrackEndToStart_IsAcceptedImmediately(MediaRepeatMode repeat)
	{
		using Fixture f = new(179.7, repeat);
		await f.Stabilize();
		f.Sample(0.2);
		var loop = (await f.Media.GetSnapshotAsync())!;
		Assert.Equal(0.2, loop.Position.TotalSeconds, 3);
		Assert.Equal(MediaTimelineChange.Wrap, loop.TimelineChange);
		Assert.Equal(1, loop.TimelineRevision);
		Assert.Equal(loop.TimelineRevision, (await f.Media.GetSnapshotAsync())!.TimelineRevision);
	}

	[Fact]
	public async Task FreshLargeExternalBackwardSeek_IsAcceptedImmediately()
	{
		using Fixture f = new(150);
		await f.Stabilize(); f.Sample(20);
		var snapshot = (await f.Media.GetSnapshotAsync())!;
		Assert.Equal(20, snapshot.Position.TotalSeconds, 3);
		Assert.Equal(MediaTimelineChange.Seek, snapshot.TimelineChange);
	}

	[Fact]
	public async Task FlowLyricsBackwardSeek_UsesTheSameClockAndAcceptsConfirmation()
	{
		using Fixture f = new(150);
		await f.Stabilize();
		Assert.True(await f.Media.TrySeekAsync(TimeSpan.FromSeconds(20)));
		f.Sample(20.2);
		Assert.InRange((await f.Media.GetSnapshotAsync())!.Position.TotalSeconds, 20, 20.5);
	}

	[Theory]
	[InlineData(50, 49.6, true)]
	[InlineData(50, 48, true)]
	[InlineData(179.7, 0.2, false)]
	public async Task SmallCorrectionsAndStaleWrap_KeepAntiJitter(double previous, double incoming, bool fresh)
	{
		using Fixture f = new(previous);
		await f.Stabilize(); f.Sample(incoming, fresh);
		Assert.True((await f.Media.GetSnapshotAsync())!.Position.TotalSeconds >= previous);
	}

	[Fact]
	public async Task RepeatSettingAlone_DoesNotChangePosition()
	{
		using Fixture f = new(60);
		await f.Stabilize();
		Assert.True(await f.Media.TrySetRepeatAsync("session", MediaRepeatMode.Track));
		Assert.InRange((await f.Media.GetSnapshotAsync())!.Position.TotalSeconds, 60, 61);
		Assert.Equal(0, (await f.Media.GetSnapshotAsync())!.TimelineRevision);
	}

	[Theory]
	[InlineData("session")]
	[InlineData("track")]
	[InlineData("paused")]
	[InlineData("seek-pending")]
	public async Task ChangedIdentityOrNonPlayingOrPendingSeek_IsNotARepeatBoundary(string reason)
	{
		using Fixture f = new(179.7);
		await f.Stabilize();
		if (reason == "seek-pending") await f.Media.TrySeekAsync(TimeSpan.FromSeconds(20));
		f.Sample(0.2);
		f.Provider.Current = reason switch
		{
			"session" => f.Provider.Current with { SessionId = "different" },
			"track" => f.Provider.Current with { Metadata = f.Provider.Current.Metadata with { TitleRaw = "Next" } },
			"paused" => f.Provider.Current with { PlaybackState = MediaPlaybackState.Paused },
			_ => f.Provider.Current
		};
		Assert.NotEqual(MediaTimelineChange.Wrap, (await f.Media.GetUpdateAsync()).Snapshot!.TimelineChange);
	}

	internal sealed class Fixture : IDisposable
	{
		public DateTimeOffset Now = new(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
		public PlaybackCommandTests.Provider Provider = new();
		public MediaSessionService Media;
		public Fixture(double position, MediaRepeatMode repeat = MediaRepeatMode.None)
		{
			Provider.Current = new() { SessionId = "session", IsCurrentSession = true, SourceAppUserModelId = "player",
				Metadata = new("Track", "Artist", "Album", TimeSpan.FromSeconds(180)), Position = TimeSpan.FromSeconds(position),
				CapturedAtUtc = Now, TimelineUpdatedAtUtc = Now, HasTimeline = true, PlaybackState = MediaPlaybackState.Playing,
				Capabilities = new(true, true, true, true, true, true, true, true), RepeatMode = repeat };
			Media = new(Provider, () => Now);
		}
		public async Task Stabilize() { await Media.GetUpdateAsync(); Now = Now.AddMilliseconds(120); Assert.NotNull(await Media.GetSnapshotAsync()); }
		public void Sample(double seconds, bool fresh = true)
		{
			Now = Now.AddMilliseconds(200);
			Provider.Current = Provider.Current with { Position = TimeSpan.FromSeconds(seconds), CapturedAtUtc = Now,
				TimelineUpdatedAtUtc = fresh ? Now : Provider.Current.TimelineUpdatedAtUtc };
		}
		public void Dispose() => Media.Dispose();
	}
}
