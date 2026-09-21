using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class ContinuationBehaviorTests
{
	[Fact]
	public void TimelineCoordinates_RespectOffsetAnchorsHoldsAndSkippedLyrics()
	{
		PersonalSyncProfile profile = new() { Mode = PersonalSyncMode.Offset, OffsetSeconds = 5 };
		Assert.Equal(15, PersonalSyncTimeline.PlaybackForLyric(10, profile));
		profile.Mode = PersonalSyncMode.Advanced;
		profile.Segments.Add(new() { Type = PersonalSyncSegmentType.Hold, PlaybackStartSeconds = 20, PlaybackEndSeconds = 30, LyricsTimeSeconds = 10 });
		profile.Anchors.Add(new() { PlaybackSeconds = 30, LyricsSeconds = 25 });
		Assert.Equal(20, PersonalSyncTimeline.PlaybackForLyric(10, profile));
		Assert.Null(PersonalSyncTimeline.PlaybackForLyric(22, profile));
		Assert.Equal(35, PersonalSyncTimeline.PlaybackForLyric(30, profile));
	}

	[Fact]
	public void AudioAudit_RecordsActualWriteAndNeverChangesItsResultOrThrowsFromLogger()
	{
		var write = typeof(SystemVolumeService).GetMethod("WriteAudio", BindingFlags.NonPublic | BindingFlags.Static)!;
		int writes = 0; List<string> messages = new();
		Func<int> call = () => { writes++; return -1; };
		Assert.Equal(-1, write.Invoke(null, [call, (Action<string>)messages.Add, "Spotify", "SetMute", "true", "UserVolumeButton"]));
		Assert.Equal(1, writes);
		string log = Assert.Single(messages);
		Assert.Contains("AUDIO WRITE at=", log); Assert.Contains("source=Spotify action=SetMute value=true reason=UserVolumeButton", log);
		Assert.Equal(-1, write.Invoke(null, [call, (Action<string>)(_ => throw new IOException()), "Spotify", "SetMasterVolume", "0.72", "UserVolumeSlider"]));
		Assert.Equal(2, writes);
	}

	[Fact]
	public async Task SpotifySuccess_ReusesCreditForThirtySecondsAndLogsOnlyRealScans()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow; int scans = 0;
		List<string> audit = new();
		MediaTrackMetadata track = new("Song", "Artist A", "Album", TimeSpan.FromSeconds(240), OriginalTitleRaw: "Song", OriginalArtistRaw: "Artist A", OriginalAlbumRaw: "Album");
		using var enricher = new SpotifyUiAutomationEnricher((_, _) => { Interlocked.Increment(ref scans); return new("Song", "Artist A, Artist B", SpotifyArtistCredit.Source); },
			_ => SpotifyWindowState.Visible, () => now, message => { lock (audit) audit.Add(message); });
		ArtistCreditEnrichment? result = null;
		for (int i = 0; i < 100 && result == null; i++) { result = enricher.TryGet("Spotify", track); await Task.Delay(5); }
		Assert.NotNull(result);
		now = now.AddSeconds(20);
		for (int i = 0; i < 100; i++) Assert.NotNull(enricher.TryGet("Spotify", track));
		Assert.Equal(1, scans);
		lock (audit)
		{
			Assert.Single(audit, s => s.StartsWith("UIA scan-start"));
			Assert.Contains(audit, s => s.Contains("elapsedMs=") && s.Contains("appCpuMs=") && s.Contains("appWorkingSetMiB="));
		}
	}

	[Fact]
	public async Task SafeIdentityQueries_PrecedeWeakHintsAndStopWithoutBroadSearch()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-fast-search-" + Guid.NewGuid().ToString("N"));
		try
		{
			SearchMetadataCandidate[] hints = Enumerable.Range(0, 14).Select(i => new SearchMetadataCandidate("Hint " + i, "Artist", "") { CanEstablishIdentity = false })
				.Append(new("Preferred title", "Artist", "")).ToArray();
			TrackInfo track = new("Source title", "Artist", "Album", TimeSpan.FromSeconds(240), SearchAlternates: hints);
			using var handler = new FastHandler(); using LyricsService service = new(directory, handler);
			Assert.NotNull((await service.GetLyricsAsync(track, false, CancellationToken.None)).Lyrics);
			Assert.Equal(4, handler.Urls.Count); // exact get + full fields + primary identity + evidenced alias
			Assert.DoesNotContain(handler.Urls, s => s.Contains("Hint") || (s.Contains("search") && !s.Contains("artist_name")));
			string log = await File.ReadAllTextAsync(Path.Combine(directory, "logs", "flowlyrics.log"));
			Assert.Contains("search start stage=", log); Assert.Contains("search response stage=", log);
			Assert.Contains("search safe-candidate", log); Assert.Contains("firstSafeMs=", log); Assert.Contains("lookup complete", log);
			Assert.True((await service.GetLyricsAsync(track, false, CancellationToken.None)).LoadedFromCache);
			Assert.Equal(4, handler.Urls.Count);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	private sealed class FastHandler : HttpMessageHandler
	{
		public List<string> Urls { get; } = new();
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			string url = Uri.UnescapeDataString(request.RequestUri!.PathAndQuery); Urls.Add(url);
			if (url.StartsWith("/api/get")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
			LrclibRecord[] body = url.Contains("Preferred title") ? [new() { Id = 123, TrackName = "Preferred title", ArtistName = "Artist", Duration = 240, SyncedLyrics = "[00:01.00]Line" }] : [];
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") });
		}
	}
}
