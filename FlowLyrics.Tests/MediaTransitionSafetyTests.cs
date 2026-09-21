using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
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

	[Theory]
	[InlineData("metadata-conflict")]
	[InlineData("old-matcher")]
	[InlineData("expired")]
	[InlineData("malformed")]
	public async Task SpeculativeMiss_DoesNotDeleteDurableCache(string reason)
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-speculation-" + Guid.NewGuid().ToString("N"));
		try
		{
			using var handler = new NoNetwork();
			using LyricsService service = new(directory, handler);
			TrackInfo track = new("Current", "Artist", "Album", TimeSpan.FromSeconds(200), SourceAppUserModelId: "Spotify");
			var store = Read<LyricsCacheStore>(service, "_cacheStore");
			await store.WriteAsync(track, new() { MatcherVersion = reason == "old-matcher" ? 1 : 8,
				CacheKind = "Positive", LrclibId = 1, LrclibTrackName = "Current", LrclibArtistName = "Artist",
				LrclibDuration = reason == "metadata-conflict" ? 240 : 200,
				SyncedLyrics = "[00:01.00]Line", Source = "LRCLIB", SavedAtUtc = DateTimeOffset.UtcNow,
				ExpiresAtUtc = reason == "expired" ? DateTimeOffset.UtcNow.AddMinutes(-1) : null }, default);
			string path = store.GetPath(track);
			if (reason == "malformed") await File.WriteAllTextAsync(path, "{broken");
			string before = await File.ReadAllTextAsync(path);
			Assert.Null(await service.TryGetCachedLyricsAsync(track));
			Assert.Equal(before, await File.ReadAllTextAsync(path));
			Assert.Equal(0, handler.Calls);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task SpeculativeManualSelection_DoesNotMigrateLegacyKey()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-manual-read-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			TrackInfo track = new("Title", "Artist", "Album", TimeSpan.FromSeconds(200));
			var store = new LyricsOverrideStore(directory);
			string original = JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, ManualLyricsSelection>
				{ [track.CacheKey] = new() { LrclibId = 17, SelectedManually = true } });
			await File.WriteAllTextAsync(store.Path, original);
			Assert.Equal(17, (await store.GetAsync(track, readOnly: true))!.LrclibId);
			Assert.Equal(original, await File.ReadAllTextAsync(store.Path));
			Assert.Equal(17, (await store.GetAsync(track))!.LrclibId);
			Assert.Contains(track.StableIdentityKey, JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ManualLyricsSelection>>(
				await File.ReadAllTextAsync(store.Path))!.Keys);
		}
		finally { Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task SpotifyEnrichment_DoesNotBlockReads_OrApplyPreviousTrackCredit()
	{
		using var enricher = new BlockingEnricher();
		using var provider = new WindowsMediaSessionProvider(enricher);
		MethodInfo method = typeof(WindowsMediaSessionProvider).GetMethod("GetEnrichmentWithoutWaiting", BindingFlags.Instance | BindingFlags.NonPublic)!;
		MediaTrackMetadata first = new("First", "Artist", "Album", TimeSpan.FromSeconds(200));
		MediaTrackMetadata second = first with { TitleRaw = "Second" };
		MediaTrackMetadata ReadMetadata(MediaTrackMetadata raw) => (MediaTrackMetadata)method.Invoke(provider, ["Spotify", raw])!;
		try
		{
			Assert.Same(first, await Task.Run(() => ReadMetadata(first)).WaitAsync(TimeSpan.FromSeconds(3)));
			Assert.True(enricher.Entered.Wait(TimeSpan.FromSeconds(3)));
			Assert.Same(second, await Task.Run(() => ReadMetadata(second)).WaitAsync(TimeSpan.FromSeconds(3)));
			Task pending = Read<Task>(provider, "_enrichmentTask");
			enricher.Release.Set();
			await pending.WaitAsync(TimeSpan.FromSeconds(3));
			var result = ReadMetadata(second);
			Assert.Null(result.EnrichedArtistCredit);
			Assert.Equal("Artist", result.ArtistRaw);
			await Read<Task>(provider, "_enrichmentTask").WaitAsync(TimeSpan.FromSeconds(3));
			Assert.Equal("Artist, Guest", ReadMetadata(second).EnrichedArtistCredit);
		}
		finally
		{
			enricher.Release.Set();
			if (Read<Task?>(provider, "_enrichmentTask") is Task pending) await pending.WaitAsync(TimeSpan.FromSeconds(3));
		}
	}

	private sealed class BlockingEnricher : IArtistCreditEnricher
	{
		public readonly ManualResetEventSlim Entered = new();
		public readonly ManualResetEventSlim Release = new();
		public ArtistCreditEnrichment? TryGet(string sourceId, MediaTrackMetadata metadata)
		{
			Entered.Set(); Release.Wait(TimeSpan.FromSeconds(10));
			return new(metadata.TitleRaw, "Artist, Guest", SpotifyArtistCredit.Source);
		}
		public void Dispose() { }
	}

	private sealed class NoNetwork : HttpMessageHandler
	{
		public int Calls;
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{ Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)); }
	}
}
