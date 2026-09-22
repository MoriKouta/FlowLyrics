using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class PlaybackCommandTests
{
	[Fact]
	public async Task RepeatCycle_UsesObservedState_AndRejectsFailureOrUnsupported()
	{
		using Fixture f = new();
		foreach (MediaRepeatMode target in new[] { MediaRepeatMode.List, MediaRepeatMode.Track, MediaRepeatMode.None })
		{
			var before = f.Commands.RepeatMode;
			Assert.True(await f.Commands.CycleRepeatAsync());
			Assert.Equal(target, f.Provider.RequestedRepeat);
			Assert.Equal(before, f.Commands.RepeatMode); // No optimistic success state.
			f.Observe(); Assert.Equal(target, f.Commands.RepeatMode);
		}
		f.Provider.Current = f.Provider.Current with { RepeatMode = MediaRepeatMode.Track }; f.Observe();
		Assert.Equal(MediaRepeatMode.Track, f.Commands.RepeatMode);
		f.Provider.AcceptRepeat = false;
		Assert.False(await f.Commands.CycleRepeatAsync()); Assert.Equal(MediaRepeatMode.Track, f.Commands.RepeatMode);
		f.Provider.Current = f.Provider.Current with { Capabilities = f.Provider.Current.Capabilities with { CanRepeat = false } }; f.Observe();
		Assert.False(f.Commands.CanRepeat); Assert.False(await f.Commands.CycleRepeatAsync());
	}

	[Theory]
	[InlineData(true, true, 1, 0)]
	[InlineData(false, true, 0, 1)]
	[InlineData(true, false, 1, 1)]
	public async Task NaturalEnd_StopsOnce_WithPauseOrStopFallback(bool canPause, bool acceptsPause, int pauses, int stops)
	{
		using Fixture f = new();
		f.Provider.AcceptPause = acceptsPause;
		f.Provider.Current = f.Provider.Current with { Capabilities = f.Provider.Current.Capabilities with { CanPause = canPause } }; f.Observe();
		Assert.False(f.Commands.IsArmed); Assert.True(await f.Commands.ToggleStopAfterTrackAsync());
		for (int i = 1; i <= 5; i++) f.Advance(i, 5 + i);
		await f.Commands.PendingStop;
		Assert.False(f.Commands.IsArmed); Assert.Equal(pauses, f.Provider.Pauses); Assert.Equal(stops, f.Provider.Stops);
		f.Advance(6, 10); await f.Commands.PendingStop;
		Assert.Equal(pauses, f.Provider.Pauses); Assert.Equal(stops, f.Provider.Stops);
	}

	[Theory]
	[InlineData("next")]
	[InlineData("previous")]
	[InlineData("seek")]
	[InlineData("external-next")]
	[InlineData("external-seek")]
	[InlineData("source")]
	[InlineData("repeat")]
	public async Task ManualNavigationOrConflict_CancelsWithoutPausing(string action)
	{
		using Fixture f = new(); Assert.True(await f.Commands.ToggleStopAfterTrackAsync());
		f.Advance(1, 6); f.Advance(2, 7); f.Advance(3, 8); f.Advance(4, 9);
		switch (action)
		{
			case "next": await f.Media.TrySkipNextAsync(); break;
			case "previous": await f.Media.TrySkipPreviousAsync(); break;
			case "seek": await f.Media.TrySeekAsync(TimeSpan.FromSeconds(1)); break;
			case "external-next": f.Advance(4.2, .2, "B", timelineSeconds: 4); break;
			case "external-seek": f.Advance(4.2, 1); break;
			case "source": f.Provider.Current = f.Provider.Current with { SessionId = "other" }; f.Observe(); break;
			case "repeat": f.Provider.Current = f.Provider.Current with { RepeatMode = MediaRepeatMode.Track }; f.Observe(); break;
		}
		Assert.False(f.Commands.IsArmed); Assert.Equal(0, f.Provider.Pauses + f.Provider.Stops);
	}

	[Fact]
	public async Task NaturalTransitionFallback_RequiresEndAndFreshNewTimelineEvidence()
	{
		using Fixture f = new(); Assert.True(await f.Commands.ToggleStopAfterTrackAsync());
		f.Advance(1, 6); f.Advance(2, 7); f.Advance(3, 8); f.Advance(4.5, 9.5);
		f.Advance(5.2, .2, "B", timelineSeconds: 5);
		await f.Commands.PendingStop;
		Assert.Equal(1, f.Provider.Pauses); Assert.False(f.Commands.IsArmed);
	}

	[Theory]
	[InlineData(4.8)]
	[InlineData(0)]
	public async Task ExternalNextNearEndOrStaleTimeline_DoesNotTriggerFallback(double newTimeline)
	{
		using Fixture f = new(); Assert.True(await f.Commands.ToggleStopAfterTrackAsync());
		f.Advance(1, 6); f.Advance(2, 7); f.Advance(3, 8); f.Advance(4.5, 9.5);
		f.Advance(5.2, .4, "B", timelineSeconds: newTimeline);
		await f.Commands.PendingStop;
		Assert.Equal(0, f.Provider.Pauses); Assert.False(f.Commands.IsArmed);
	}

	[Fact]
	public async Task StopReservation_ConfirmsRepeatOff_AndRefusesRejectedOff()
	{
		using Fixture f = new();
		await f.Media.GetUpdateAsync(); f.Now = f.Now.AddSeconds(.2); await f.Media.GetUpdateAsync();
		f.Provider.Current = f.Provider.Current with { RepeatMode = MediaRepeatMode.Track }; f.Observe();
		Assert.True(await f.Commands.ToggleStopAfterTrackAsync()); Assert.True(f.Commands.IsArmed);
		Assert.Equal(MediaRepeatMode.None, f.Provider.RequestedRepeat);
		f.Commands.Cancel(); f.Provider.AcceptRepeat = false;
		f.Provider.Current = f.Provider.Current with { RepeatMode = MediaRepeatMode.List }; f.Observe();
		Assert.False(await f.Commands.ToggleStopAfterTrackAsync()); Assert.False(f.Commands.IsArmed);
	}

	[Fact]
	public async Task ChangedTargetBeforeCommand_DoesNotPauseAnotherTrack()
	{
		using Fixture f = new();
		string identity = StopAfterTrackReservation.TrackIdentity(f.Provider.Current);
		f.Provider.Current = f.Provider.Current with { Metadata = f.Provider.Current.Metadata with { TitleRaw = "New" } };
		Assert.False(await f.Media.TryPauseOrStopAsync("session", expectedTrack: identity));
		Assert.Equal(0, f.Provider.Pauses);
	}

	[Fact]
	public async Task TrackChangesDuringRejectedPause_DoesNotStopTheWrongTrack()
	{
		using Fixture f = new(); f.Provider.AcceptPause = false;
		string identity = StopAfterTrackReservation.TrackIdentity(f.Provider.Current);
		f.Provider.OnPause = () => f.Provider.Current = f.Provider.Current with { Metadata = f.Provider.Current.Metadata with { TitleRaw = "New" } };
		Assert.False(await f.Media.TryPauseOrStopAsync("session", expectedTrack: identity));
		Assert.Equal(1, f.Provider.Pauses); Assert.Equal(0, f.Provider.Stops);
	}

	private sealed class Fixture : IDisposable
	{
		public readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		public DateTimeOffset Now;
		public readonly Provider Provider = new();
		public readonly MediaSessionService Media;
		public readonly PlaybackCommandCoordinator Commands;
		public Fixture()
		{
			Now = Start;
			Provider.Current = new() { SessionId = "session", SourceAppUserModelId = "player", IsCurrentSession = true,
				Metadata = new("A", "Artist", "Album", TimeSpan.FromSeconds(10)), Position = TimeSpan.FromSeconds(5),
				CapturedAtUtc = Now, TimelineUpdatedAtUtc = Now, HasTimeline = true, PlaybackState = MediaPlaybackState.Playing,
				Capabilities = new(true, true, true, true, true, true, true, true), RepeatMode = MediaRepeatMode.None };
			Media = new(Provider, () => Now); Commands = new(Media, utcNow: () => Now); Observe();
		}
		public void Observe() => Commands.Observe(new(MediaMetadataState.Stable, Session: Provider.Current));
		public void Advance(double seconds, double position, string title = "A", double? timelineSeconds = null)
		{
			Now = Start.AddSeconds(seconds);
			Provider.Current = Provider.Current with { CapturedAtUtc = Now, TimelineUpdatedAtUtc = Start.AddSeconds(timelineSeconds ?? seconds),
				Position = TimeSpan.FromSeconds(position), Metadata = Provider.Current.Metadata with { TitleRaw = title } };
			Observe();
		}
		public void Dispose() { Commands.Dispose(); Media.Dispose(); }
	}

	internal sealed class Provider : IMediaSessionProvider
	{
		public MediaSessionInfo Current = new();
		public int Pauses, Stops;
		public MediaRepeatMode? RequestedRepeat;
		public bool AcceptRepeat = true, AcceptPause = true;
		public Action? OnPause;
		public event EventHandler? SessionsChanged { add { } remove { } }
		public Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MediaSessionInfo>>([Current]);
		public Task<bool> TrySetRepeatAsync(string id, MediaRepeatMode mode, CancellationToken cancellationToken = default)
		{ RequestedRepeat = mode; if (AcceptRepeat) Current = Current with { RepeatMode = mode }; return Task.FromResult(AcceptRepeat); }
		public Task<bool> TryPauseAsync(string id, CancellationToken cancellationToken = default) { Pauses++; OnPause?.Invoke(); return Task.FromResult(AcceptPause); }
		public Task<bool> TryStopAsync(string id, CancellationToken cancellationToken = default) { Stops++; return Task.FromResult(true); }
		public Task<bool> TryTogglePlayPauseAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<bool> TrySkipNextAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<bool> TrySkipPreviousAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<bool> TrySeekAsync(string id, TimeSpan position, CancellationToken cancellationToken = default) => Task.FromResult(true);
		public void Dispose() { }
	}
}
