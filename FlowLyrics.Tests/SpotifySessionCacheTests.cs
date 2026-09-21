using System;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class SpotifySessionCacheTests
{
	private static MediaTrackMetadata Track => new("Song (Single Version)", "Artist A", "Album", TimeSpan.FromSeconds(240),
		OriginalTitleRaw: "Song (Single Version)", OriginalArtistRaw: "Artist A", OriginalAlbumRaw: "Album");

	[Fact]
	public async Task ConfirmedCredit_SurvivesMinimizeAndRefreshesWhenVisible()
	{
		SpotifyWindowState state = SpotifyWindowState.Visible;
		DateTimeOffset now = DateTimeOffset.UtcNow;
		string credit = "Artist A, Artist B";
		int calls = 0;
		using var enricher = new SpotifyUiAutomationEnricher((_, metadata) =>
		{
			Interlocked.Increment(ref calls);
			return new(metadata.TitleRaw, credit, SpotifyArtistCredit.Source);
		}, _ => state, () => now);
		ArtistCreditEnrichment live = await Observe(enricher, Track);
		Assert.Equal(credit, live.Credit);
		Assert.Equal(SpotifyArtistCredit.Source, live.Source);
		state = SpotifyWindowState.Minimized; now = now.AddMinutes(10);
		for (int i = 0; i < 30; i++)
		{
			ArtistCreditEnrichment cached = Assert.IsType<ArtistCreditEnrichment>(enricher.TryGet("Spotify", Track));
			Assert.Equal(credit, cached.Credit);
			Assert.Equal("Spotify UI Automation (cached)", cached.Source);
		}
		Assert.Equal("MINIMIZED", enricher.WindowState);
		Assert.Equal(1, Volatile.Read(ref calls));
		credit = "Artist A, Artist B, Artist C";
		state = SpotifyWindowState.Visible; now = now.AddSeconds(2);
		await Observe(enricher, Track, credit);
		Assert.Equal(2, Volatile.Read(ref calls));
	}

	[Fact]
	public void MinimizedWithoutSessionCache_FallsBackWithoutScanning()
	{
		using var enricher = new SpotifyUiAutomationEnricher((_, _) => throw new Exception("Must not scan"), _ => SpotifyWindowState.Minimized);
		Assert.Null(enricher.TryGet("Spotify", Track));
		Assert.Equal("Artist A", SpotifyArtistCredit.Apply(Track, null).DisplayArtist);
	}

	[Theory]
	[InlineData("title")]
	[InlineData("album")]
	[InlineData("duration")]
	[InlineData("artist")]
	[InlineData("edition")]
	[InlineData("source")]
	public async Task MinimizedCache_RequiresCompleteRawTrackIdentity(string difference)
	{
		SpotifyWindowState state = SpotifyWindowState.Visible;
		DateTimeOffset now = DateTimeOffset.UtcNow;
		using var enricher = new SpotifyUiAutomationEnricher((_, metadata) => new(metadata.TitleRaw, "Artist A, Artist B", SpotifyArtistCredit.Source), _ => state, () => now);
		await Observe(enricher, Track);
		state = SpotifyWindowState.Minimized; now = now.AddSeconds(2);
		MediaTrackMetadata next = difference switch
		{
			"title" => Track with { OriginalTitleRaw = "Next song", TitleRaw = "Next song" },
			"album" => Track with { OriginalAlbumRaw = "Other album" },
			"duration" => Track with { Duration = TimeSpan.FromSeconds(265) },
			"artist" => Track with { OriginalArtistRaw = "Another artist" },
			"edition" => Track with { OriginalTitleRaw = "Song (Live Version)" },
			_ => Track
		};
		Assert.Null(enricher.TryGet(difference == "source" ? "Spotify.exe" : "Spotify", next));
		Assert.Equal("Artist A, Artist B", enricher.TryGet("Spotify", Track)!.Credit);
	}

	private static async Task<ArtistCreditEnrichment> Observe(SpotifyUiAutomationEnricher enricher, MediaTrackMetadata metadata, string? expected = null)
	{
		for (int i = 0; i < 100; i++)
		{
			ArtistCreditEnrichment? result = enricher.TryGet("Spotify", metadata);
			if (result != null && (expected == null || result.Credit == expected)) return result;
			await Task.Delay(10);
		}
		throw new TimeoutException("Background enrichment did not complete.");
	}
}
