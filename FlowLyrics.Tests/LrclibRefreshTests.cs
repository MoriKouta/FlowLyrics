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
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class LrclibRefreshTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-refresh-" + Guid.NewGuid().ToString("N"));
	private static TrackInfo Track => new("Song", "Artist", "Album", TimeSpan.FromSeconds(240));
	private static LrclibRecord Record => new() { Id = 123, TrackName = "Song", ArtistName = "Artist", AlbumName = "Album", Duration = 240, SyncedLyrics = "[00:01.00]test lyrics" };

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ForceRefresh_BypassesPositiveOrNegativeLookupCaches(bool negative)
	{
		using Handler handler = new() { Empty = negative };
		using LyricsService service = new(_directory, handler);
		var first = await service.GetLyricsAsync(Track, false, CancellationToken.None);
		int requests = handler.Urls.Count;
		Assert.True((await service.GetLyricsAsync(Track, false, CancellationToken.None)).LoadedFromCache);
		Assert.Equal(requests, handler.Urls.Count);
		var refreshed = await service.GetLyricsAsync(Track, true, CancellationToken.None);
		Assert.False(refreshed.LoadedFromCache);
		Assert.Equal(first.Status, refreshed.Status); // Unchanged server response is still valid.
		Assert.True(handler.Urls.Count > requests);
		Assert.All(handler.Urls, url => Assert.DoesNotContain("timestamp", url));
	}

	[Fact]
	public async Task CandidateSearch_NormalCacheAndExplicitBypass()
	{
		using Handler handler = new();
		using LyricsService service = new(_directory, handler);
		LyricsSearchRequest request = new("Song", "Artist", "Album", "");
		await service.SearchCandidatesAsync(Track, request, CancellationToken.None);
		int requests = handler.Urls.Count;
		await service.SearchCandidatesAsync(Track, request, CancellationToken.None);
		Assert.Equal(requests, handler.Urls.Count);
		await service.SearchCandidatesAsync(Track, request, CancellationToken.None, bypassRequestCache: true);
		Assert.True(handler.Urls.Count > requests);
		requests = handler.Urls.Count;
		await service.ClearTrackCacheAsync(Track);
		await service.SearchCandidatesAsync(Track, request, CancellationToken.None);
		Assert.True(handler.Urls.Count > requests);
	}

	[Fact]
	public async Task DirectId_PreviewsRefreshesAndSavesManualSelectionWithoutSearchOrAutomaticEligibility()
	{
		using Handler handler = new();
		using LyricsService service = new(_directory, handler);
		Assert.Equal(123, (await service.GetRecordByIdAsync(123, CancellationToken.None))!.Id);
		await service.GetRecordByIdAsync(123, CancellationToken.None);
		Assert.Single(handler.Urls);
		await service.GetRecordByIdAsync(123, CancellationToken.None, bypassRequestCache: true);
		Assert.Equal(2, handler.Urls.Count);
		TrackInfo anotherTitle = Track with { Title = "Manual override" };
		Assert.False(LyricsMatcher.Evaluate(anotherTitle, Record).AutoEligible);
		var applied = await service.ApplyManualSelectionAsync(anotherTitle, 123, CancellationToken.None);
		Assert.True(applied.SelectedManually);
		Assert.Equal(123, (await new LyricsOverrideStore(_directory).GetAsync(anotherTitle))!.LrclibId);
		await service.GetLyricsAsync(anotherTitle, true, CancellationToken.None);
		Assert.Equal(3, handler.Urls.Count);
		Assert.All(handler.Urls, url => Assert.Equal("/api/get/123", url));
	}

	[Fact]
	public async Task DirectId_InvalidAndMissingIdsDoNotFallBackToSearch()
	{
		using Handler handler = new();
		using LyricsService service = new(_directory, handler);
		Assert.Null(await service.GetRecordByIdAsync(0, CancellationToken.None, true));
		Assert.Empty(handler.Urls);
		var error = await Assert.ThrowsAsync<LyricsServiceException>(() => service.GetRecordByIdAsync(999, CancellationToken.None, true));
		Assert.Equal(LyricsErrorKind.NotFound, error.Kind);
		Assert.Equal("/api/get/999", Assert.Single(handler.Urls));
	}

	[Fact]
	public async Task ClearingOneTrack_DoesNotFlushUnrelatedRequests()
	{
		using Handler handler = new();
		using LyricsService service = new(_directory, handler);
		TrackInfo other = Track with { Title = "Unrelated" };
		LyricsSearchRequest request = new(other.Title, other.Artist, other.Album, "");
		await service.SearchCandidatesAsync(other, request, CancellationToken.None);
		await service.GetLyricsAsync(Track, false, CancellationToken.None);
		await service.ClearTrackCacheAsync(Track);
		int count = handler.Urls.Count;
		await service.SearchCandidatesAsync(other, request, CancellationToken.None);
		Assert.Equal(count, handler.Urls.Count);
	}

	internal sealed class Handler : HttpMessageHandler
	{
		public List<string> Urls { get; } = new();
		public bool Empty { get; set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			string url = request.RequestUri!.PathAndQuery;
			Urls.Add(url);
			if (url == "/api/get/999" || (Empty && !url.StartsWith("/api/search")))
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
			object body = url.StartsWith("/api/search") ? (Empty ? Array.Empty<LrclibRecord>() : new[] { Record }) : Record;
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") });
		}
	}

	public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
