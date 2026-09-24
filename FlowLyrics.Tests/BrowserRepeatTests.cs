using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class BrowserRepeatTests
{
	[Fact]
	public async Task ContinuousBoundary_SeeksThenPlays_Once()
	{
		using var f = new Fixture(); await f.Start(); f.Approach();
		Assert.Empty(f.Provider.Commands);
		await f.Deadline();
		Assert.Equal(new[] { "seek", "play" }, f.Provider.Commands); Assert.True(f.Repeat.Enabled);
	}

	[Theory]
	[InlineData("native")]
	[InlineData("duration")]
	[InlineData("seek")]
	[InlineData("play")]
	[InlineData("timeline")]
	[InlineData("audio")]
	public void IneligibleSources_NeverOfferFallback(string reason)
	{
		using var f = new Fixture(); var s = f.Provider.Current;
		s = reason switch
		{
			"native" => s with { Capabilities = s.Capabilities with { CanRepeat = true } },
			"duration" => s with { Metadata = s.Metadata with { Duration = TimeSpan.Zero } },
			"seek" => s with { Capabilities = s.Capabilities with { CanSeek = false } },
			"play" => s with { Capabilities = s.Capabilities with { CanPlay = false } },
			"timeline" => s with { HasTimeline = false },
			_ => s with { SourceAppUserModelId = "Spotify" }
		};
		Assert.False(f.Repeat.Enable(s)); Assert.Empty(f.Provider.Commands);
		Assert.Equal(reason == "native" ? "Native" : "Unsupported", BrowserTrackRepeat.Capability(s));
	}

	[Theory]
	[InlineData("external-seek")]
	[InlineData("natural-loop")]
	[InlineData("track")]
	[InlineData("session")]
	[InlineData("paused")]
	[InlineData("pending")]
	[InlineData("dispose")]
	public async Task ChangedStateAtDeadline_DoesNotRestartOldPlayback(string reason)
	{
		using var f = new Fixture(); await f.Start(); f.Approach();
		f.Sample(9.88, reason is "external-seek" or "natural-loop" ? .1 : 9.88, observe: false);
		if (reason == "track") f.Provider.Current = f.Provider.Current with { Metadata = f.Provider.Current.Metadata with { TitleRaw = "Next" } };
		if (reason == "session") f.Provider.Current = f.Provider.Current with { SessionId = "other-browser" };
		if (reason == "paused") f.Provider.Current = f.Provider.Current with { PlaybackState = MediaPlaybackState.Paused };
		if (reason == "pending") f.Repeat.Observe(new(MediaMetadataState.PendingMetadata, Session: f.Provider.Current));
		if (reason == "dispose") f.Repeat.Dispose();
		f.Release.TrySetResult(); await f.Repeat.Pending;
		Assert.Empty(f.Provider.Commands);
	}

	[Fact]
	public async Task ObservedNaturalWrap_CancelsPendingSeek_AndKeepsRepeatEnabled()
	{
		using var f = new Fixture(); await f.Start(); f.Approach();
		f.Sample(10.1, .1); f.Release.TrySetResult(); await f.Repeat.Pending;
		Assert.Empty(f.Provider.Commands); Assert.True(f.Repeat.Enabled);
	}

	[Theory]
	[InlineData("seek")]
	[InlineData("play")]
	[InlineData("wrong-track-after-seek")]
	public async Task RejectionOrTargetChange_DisablesFallback(string stage)
	{
		using var f = new Fixture(); await f.Start(); f.Approach();
		f.Provider.Fail = stage; await f.Deadline();
		Assert.False(f.Repeat.Enabled);
		if (stage == "seek") { Assert.Equal(new[] { "seek" }, f.Provider.Commands); Assert.False(f.Repeat.Available(f.Provider.Current)); }
		if (stage == "wrong-track-after-seek") Assert.DoesNotContain("play", f.Provider.Commands);
	}

	[Theory]
	[InlineData("seek")]
	[InlineData("next")]
	public async Task ManualNavigation_DisarmsCoordinatorFallback(string action)
	{
		using var f = new Fixture(); await f.Start();
		using var commands = new PlaybackCommandCoordinator(f.Media, utcNow: () => f.Now);
		commands.Observe(new(MediaMetadataState.Stable, Session: f.Provider.Current));
		Assert.True(commands.CanRepeat); Assert.True(commands.IsFallbackRepeat);
		Assert.True(await commands.CycleRepeatAsync()); Assert.Equal(MediaRepeatMode.Track, commands.RepeatMode);
		if (action == "seek") await f.Media.TrySeekAsync(TimeSpan.FromSeconds(2)); else await f.Media.TrySkipNextAsync();
		Assert.Equal(MediaRepeatMode.None, commands.RepeatMode);
		Assert.DoesNotContain("play", f.Provider.Commands);
	}

	private sealed class Fixture : IDisposable
	{
		public readonly DateTimeOffset StartTime = new(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
		public DateTimeOffset Now;
		public Provider Provider = new();
		public MediaSessionService Media;
		public BrowserTrackRepeat Repeat;
		public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public Fixture()
		{
			Now = StartTime.AddSeconds(5); Provider.Current = Provider.Current with { CapturedAtUtc = Now, TimelineUpdatedAtUtc = Now };
			Media = new(Provider, () => Now); Repeat = new(Media, () => Now, (_, token) => Release.Task.WaitAsync(token));
		}
		public async Task Start() { await Media.GetUpdateAsync(); Now = Now.AddSeconds(.15); await Media.GetUpdateAsync(); Assert.True(Repeat.Enable(Provider.Current)); }
		public void Approach() { Sample(6, 6); Sample(7, 7); Sample(8, 8); Sample(9, 9); Assert.False(Repeat.Pending.IsCompleted); }
		public void Sample(double time, double position, bool observe = true)
		{
			Now = StartTime.AddSeconds(time); Provider.Current = Provider.Current with { Position = TimeSpan.FromSeconds(position), CapturedAtUtc = Now, TimelineUpdatedAtUtc = Now };
			if (observe) Repeat.Observe(new(MediaMetadataState.Stable, Session: Provider.Current));
		}
		public async Task Deadline() { Sample(9.88, 9.88, false); Release.TrySetResult(); await Repeat.Pending; }
		public void Dispose() { Repeat.Dispose(); Media.Dispose(); }
	}
	private sealed class Provider : IMediaSessionProvider
	{
		public MediaSessionInfo Current = new() { SessionId = "browser", SourceAppUserModelId = "chrome.exe", IsCurrentSession = true,
			Metadata = new("Video", "Artist", "", TimeSpan.FromSeconds(10)), Position = TimeSpan.FromSeconds(5), HasTimeline = true,
			PlaybackState = MediaPlaybackState.Playing, Capabilities = new(true, true, true, true, true, true) };
		public List<string> Commands = new(); public string Fail = "";
		public event EventHandler? SessionsChanged { add { } remove { } }
		public Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MediaSessionInfo>>([Current]);
		public Task<bool> TrySeekAsync(string id, TimeSpan position, CancellationToken cancellationToken = default)
		{
			Commands.Add("seek"); if (Fail == "seek") return Task.FromResult(false);
			Current = Current with { Position = position };
			if (Fail == "wrong-track-after-seek") Current = Current with { Metadata = Current.Metadata with { TitleRaw = "Other" } };
			return Task.FromResult(true);
		}
		public Task<bool> TrySeekNativeAsync(string id, TimeSpan position, CancellationToken cancellationToken = default) => TrySeekAsync(id, position, cancellationToken);
		public Task<bool> TryPlayAsync(string id, CancellationToken cancellationToken = default) { Commands.Add("play"); return Task.FromResult(Fail != "play"); }
		public Task<bool> TrySkipNextAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<bool> TrySkipPreviousAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<bool> TryTogglePlayPauseAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public void Dispose() { }
	}
}
