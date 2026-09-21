using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class LyricsSearchSafetyTests
{
	[Fact]
	public async Task LateSpotifyEnrichment_RechecksNegativeCacheAndSearchesTheAdditionalCredit()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-spotify-credit-" + Guid.NewGuid().ToString("N"));
		try
		{
			const string title = "TOKYO WATASHI COLLECTION";
			const string rawArtist = "鈴代紗弓";
			MediaTrackMetadata metadata = new(title, rawArtist, "Album", TimeSpan.FromSeconds(261), OriginalArtistRaw: rawArtist);
			TrackInfo original = new(title, rawArtist, "Album", metadata.Duration, OriginalMediaArtist: rawArtist);
			string credit = MatchingSafetyTests.Actors;
			using FakeLrclib handler = new(query => query.Contains("artist_name=" + credit)
				? new[] { MatchingSafetyTests.Record(title, credit) } : Array.Empty<LrclibRecord>());
			using LyricsService service = new(directory, handler);
			Assert.Equal(LyricsLookupStatus.NoLyrics, (await service.GetLyricsAsync(original, false, CancellationToken.None)).Status);
			Assert.True((await service.GetLyricsAsync(original, false, CancellationToken.None)).LoadedFromCache);
			MediaTrackMetadata enriched = SpotifyArtistCredit.Apply(metadata, new(title, credit, SpotifyArtistCredit.Source));
			TrackInfo track = original with { SearchAlternates = enriched.SearchAlternates,
				EnrichedArtistCredit = enriched.EnrichedArtistCredit, EnrichmentSource = enriched.EnrichmentSource };
			LyricsLookupResult found = await service.GetLyricsAsync(track, false, CancellationToken.None);
			Assert.NotNull(found.Lyrics);
			Assert.False(found.LoadedFromCache);
			Assert.Contains(handler.Queries, query => query.Contains("artist_name=" + credit));
			Assert.Contains(handler.Queries, query => query.Contains("artist_name=" + rawArtist + "&"));
			Assert.Equal(rawArtist, track.RawArtist);
			Assert.True((await service.GetLyricsAsync(track, false, CancellationToken.None)).LoadedFromCache);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task MatcherUpgrade_PreservesExplicitManualSelections()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-manual-" + Guid.NewGuid().ToString("N"));
		try
		{
			TrackInfo track = new(MatchingSafetyTests.Song, MatchingSafetyTests.Actors, "", TimeSpan.FromSeconds(261));
			LyricsCacheStore cache = new(Path.Combine(directory, "dev-cache", "old-build", "lyrics-cache"));
			await cache.WriteAsync(track, new LyricsCacheEntry
			{
				MatcherVersion = 6, SelectionMode = "Manual", LrclibId = 9,
				LrclibTrackName = "Different title", LrclibArtistName = "Different artist", LrclibDuration = 236,
				SyncedLyrics = "[00:01.00]ユーザー選択"
			}, CancellationToken.None);
			await new LyricsOverrideStore(directory).SetAsync(track, 9);
			using FakeLrclib handler = new(_ => throw new InvalidOperationException("Manual cache should not require network"));
			using LyricsService service = new(directory, handler);
			LyricsLookupResult result = await service.GetLyricsAsync(track, false, CancellationToken.None);
			Assert.NotNull(result.Lyrics);
			Assert.Equal(9, result.LrclibRecord!.Id);
			Assert.Empty(handler.Queries);
			Assert.NotNull(await new LyricsOverrideStore(directory).GetAsync(track));
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task UnsafeSearchAndOldBestMatch_DoNotAutomaticallyDisplayLyrics()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-safety-" + Guid.NewGuid().ToString("N"));
		try
		{
			TrackInfo track = new(MatchingSafetyTests.Song, MatchingSafetyTests.Actors, "Album", TimeSpan.FromSeconds(261));
			string previousCache = Path.Combine(directory, "dev-cache", "old-build", "lyrics-cache");
			LyricsCacheStore cache = new(previousCache);
			await cache.WriteAsync(track, new LyricsCacheEntry
			{
				MatcherVersion = 6, SelectionMode = "BestMatch", LrclibId = 1,
				LrclibTrackName = "春風に乗って", LrclibArtistName = MatchingSafetyTests.Actors,
				LrclibDuration = 236, SyncedLyrics = "[00:01.00]別曲"
			}, CancellationToken.None);
			using FakeLrclib handler = new(_ => new[] { MatchingSafetyTests.Record("春風に乗って", duration: 236) });
			using LyricsService service = new(directory, handler);
			LyricsLookupResult result = await service.GetLyricsAsync(track, false, CancellationToken.None);
			Assert.Equal(LyricsLookupStatus.NoLyrics, result.Status);
			Assert.Null(result.Lyrics);
			Assert.NotEmpty(handler.Queries);
			Assert.True(File.Exists(cache.GetPath(track)));
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task Search_UsesRomanizedAliasAndRetainsVideoSourceForPersonalSync()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-alias-" + Guid.NewGuid().ToString("N"));
		try
		{
			const string raw = "【Ado】アイ・アイ・ア（AiAiA）";
			RepairedProviderMetadata metadata = ProviderMetadataRepair.Repair("chrome", raw, "Ado", "");
			TrackInfo track = new(metadata.Title, metadata.Artist, "", TimeSpan.FromSeconds(280), SearchAlternates: metadata.SearchAlternates,
				OriginalMediaTitle: raw, OriginalMediaArtist: "Ado", OriginalMediaAlbum: "", SourceAppUserModelId: "chrome");
			using FakeLrclib handler = new(query => query.Contains("track_name=AiAiA") ? new[] { MatchingSafetyTests.Record("AiAiA", "Ado", 261) } : Array.Empty<LrclibRecord>());
			using LyricsService service = new(directory, handler);
			LyricsLookupResult result = await service.GetLyricsAsync(track, false, CancellationToken.None);
			Assert.NotNull(result.Lyrics);
			Assert.Contains(handler.Queries, query => query.Contains("track_name=AiAiA"));
			Assert.True(LyricsMatcher.Evaluate(track, result.LrclibRecord!).UsesVideoDurationTolerance);
			PlaybackSnapshot snapshot = new(track, TimeSpan.Zero, true, DateTimeOffset.UtcNow, SourceAppUserModelId: "chrome", SourceDisplayName: "Google Chrome");
			PersonalSyncContext sync = PersonalSyncIdentity.Create(snapshot, result);
			Assert.Equal(raw, sync.Source.OriginalMediaTitle);
			Assert.Equal(280, sync.Source.DurationSeconds);
			Assert.Equal("chrome", sync.Source.SourceAppUserModelId);
			int requests = handler.Queries.Count;
			Assert.True((await service.GetLyricsAsync(track, false, CancellationToken.None)).LoadedFromCache);
			Assert.Equal(requests, handler.Queries.Count);
			// A video match cannot be reused under the audio source's stricter duration rule.
			LyricsLookupResult audio = await service.GetLyricsAsync(track with { SourceAppUserModelId = "spotify" }, false, CancellationToken.None);
			Assert.Equal(LyricsLookupStatus.NoLyrics, audio.Status);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Search_UsesReleaseAndCvCandidates(bool withCv)
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-credit-" + Guid.NewGuid().ToString("N"));
		try
		{
			TrackInfo track = new(MatchingSafetyTests.Song + " - TVアニメ「シャインポスト」", withCv ? "TINGS: " + MatchingSafetyTests.Characters : MatchingSafetyTests.Actors, "Single", TimeSpan.FromSeconds(261));
			using FakeLrclib handler = new(query => query.Contains("track_name=" + MatchingSafetyTests.Song + "&") && (!withCv || query.Contains("artist_name=TINGS"))
				? new[] { MatchingSafetyTests.Record(artist: "TINGS: " + MatchingSafetyTests.Characters) } : Array.Empty<LrclibRecord>());
			using LyricsService service = new(directory, handler);
			Assert.NotNull((await service.GetLyricsAsync(track, false, CancellationToken.None)).Lyrics);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	private sealed class FakeLrclib(Func<string, LrclibRecord[]> search) : HttpMessageHandler
	{
		public List<string> Queries { get; } = new();
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			string query = Uri.UnescapeDataString(request.RequestUri!.Query);
			Queries.Add(query);
			if (request.RequestUri.AbsolutePath.StartsWith("/api/get")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(search(query)), Encoding.UTF8, "application/json") });
		}
	}
}
