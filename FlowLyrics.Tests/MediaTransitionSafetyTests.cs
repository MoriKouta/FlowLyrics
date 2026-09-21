using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;
using static FlowLyrics.Tests.TransitionPerformanceTests;

namespace FlowLyrics.Tests;

public sealed class MediaTransitionSafetyTests
{
	[Fact]
	public async Task PendingRetainsSession_RequiresConsistentReads_AndDisappearanceGrace()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		var provider = new TransitionProvider();
		using MediaSessionService service = new(provider, () => now);
		TrackInfo first = new("A", "Artist", "Album", TimeSpan.FromSeconds(180));
		provider.Set(first);
		Assert.Equal(MediaMetadataState.PendingMetadata, (await service.GetUpdateAsync()).State);
		now = now.AddMilliseconds(120);
		Assert.Equal(MediaMetadataState.Stable, (await service.GetUpdateAsync()).State);
		provider.Sessions = provider.Sessions.Select(s => s with { Metadata = s.Metadata with { EnrichedArtistCredit = "Artist, Guest" } }).ToArray();
		Assert.Equal(MediaMetadataState.Stable, (await service.GetUpdateAsync()).State);
		provider.Set(first with { Title = "" });
		Assert.Equal(MediaMetadataState.PendingMetadata, (await service.GetUpdateAsync()).State);
		Assert.True(await service.TryTogglePlayPauseAsync());
		Assert.True((await service.GetSessionsAsync()).Single().IsSelectedByFlowLyrics);
		provider.Set(first with { Title = "B" });
		Assert.Equal("B", (await service.GetUpdateAsync()).Snapshot!.Track.Title);
		now = now.AddMilliseconds(120);
		provider.Set(first with { Title = "B", Album = "New album", Duration = TimeSpan.FromSeconds(240) });
		Assert.Equal(MediaMetadataState.PendingMetadata, (await service.GetUpdateAsync()).State);
		now = now.AddMilliseconds(120);
		var stable = await service.GetUpdateAsync();
		Assert.Equal(MediaMetadataState.Stable, stable.State);
		Assert.Equal("New album", stable.Snapshot!.Track.Album);
		provider.Sessions = [];
		Assert.Equal(MediaMetadataState.PendingMetadata, (await service.GetUpdateAsync()).State);
		now = now.AddMilliseconds(399);
		Assert.Equal(MediaMetadataState.PendingMetadata, (await service.GetUpdateAsync()).State);
		now = now.AddMilliseconds(2);
		Assert.Equal(MediaMetadataState.NoSession, (await service.GetUpdateAsync()).State);
		Assert.False(await service.TryTogglePlayPauseAsync());
	}

	[Fact]
	public async Task ParsedHotCache_UsesNoDiskOrNetwork_AndClearInvalidatesIt()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-hot-" + Guid.NewGuid().ToString("N"));
		try
		{
			using var handler = new NoNetwork();
			using LyricsService service = new(directory, handler);
			TrackInfo track = new("Cached", "Artist", "Album", TimeSpan.FromSeconds(200), SourceAppUserModelId: "Spotify");
			var store = Read<LyricsCacheStore>(service, "_cacheStore");
			await store.WriteAsync(track, new() { MatcherVersion = 8, CacheKind = "Positive", LrclibId = 1,
				LrclibTrackName = "Cached", LrclibArtistName = "Artist", LrclibDuration = 200,
				SyncedLyrics = "[00:01.00]Line", Source = "LRCLIB", SavedAtUtc = DateTimeOffset.UtcNow }, default);
			var first = await service.TryGetCachedLyricsAsync(track);
			Assert.NotNull(first!.Lyrics);
			// A locked disk file proves a hot read does not attempt deserialization.
			using (var locked = File.Open(store.GetPath(track), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
			{
				var second = await service.GetLyricsAsync(track, false, default, () => throw new Exception("Unexpected searching state"));
				Assert.Same(first.Lyrics, second.Lyrics);
			}
			Assert.Equal(0, handler.Calls);
			await service.ClearTrackCacheAsync(track);
			Assert.Null(await service.TryGetCachedLyricsAsync(track));
			Assert.Equal(0, handler.Calls);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public void SpeculativeIdentity_DistinguishesSourceDurationAndTransientFields()
	{
		TrackInfo track = new("Title", "Artist", "Album", TimeSpan.FromSeconds(200), SourceAppUserModelId: "Spotify");
		string key = LyricsService.LookupIdentity(track);
		Assert.NotEqual(key, LyricsService.LookupIdentity(track with { SourceAppUserModelId = "chrome" }));
		Assert.NotEqual(key, LyricsService.LookupIdentity(track with { Duration = TimeSpan.FromSeconds(200.1) }));
		Assert.NotEqual(key, LyricsService.LookupIdentity(track with { Artist = "Previous artist" }));
		Assert.NotEqual(key, LyricsService.LookupIdentity(track with { Title = "Previous title" }));
	}

	private sealed class NoNetwork : HttpMessageHandler
	{
		public int Calls;
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{ Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)); }
	}
}
