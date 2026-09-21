using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class SpotifyEnrichmentTests
{
	private const string Title = "TOKYO WATASHI COLLECTION";
	private const string Raw = "鈴代紗弓";
	private static readonly string[] Artists = { Raw, "蟹沢萌子", "夏吉ゆうこ", "長谷川里桃", "中川梨花" };
	private static string Credit => string.Join(", ", Artists);
	private static MediaTrackMetadata Metadata(string title = Title, string artist = Raw) =>
		new(title, artist, "Album", TimeSpan.FromSeconds(261), OriginalTitleRaw: title, OriginalArtistRaw: artist,
			OriginalAlbumRaw: "Album", OriginalAlbumArtistRaw: "Various Artists", OriginalSubtitleRaw: "",
			OriginalGenresRaw: Array.Empty<string>(), OriginalTrackNumberRaw: 2);
	private static ArtistCreditEnrichment? Observed(string title = Title, string process = "Spotify", bool current = true) =>
		SpotifyArtistCredit.Validate(process, current, Title, Raw, title, "再生中：" + Credit + " の " + title, Artists);
	private static TrackInfo Track(MediaTrackMetadata metadata) => new(metadata.TitleRaw, metadata.ArtistRaw, metadata.AlbumRaw,
		metadata.Duration, SearchAlternates: metadata.SearchAlternates, OriginalMediaTitle: metadata.OriginalTitleRaw,
		OriginalMediaArtist: metadata.OriginalArtistRaw, EnrichedArtistCredit: metadata.EnrichedArtistCredit,
		EnrichmentSource: metadata.EnrichmentSource);

	[Fact]
	public void SuccessfulEnrichment_AddsCreditAndCandidateWithoutChangingRawOrPersistentIdentity()
	{
		MediaTrackMetadata raw = Metadata();
		MediaTrackMetadata enriched = SpotifyArtistCredit.Apply(raw, Observed());
		TrackInfo track = Track(enriched);
		Assert.Equal(Raw, enriched.ArtistRaw);
		Assert.Equal(Raw, track.RawArtist);
		Assert.Equal(Raw, track.Artist);
		Assert.Equal(Credit, track.DisplayArtist);
		Assert.Equal(SpotifyArtistCredit.Source, track.EnrichmentSource);
		Assert.Equal(Track(raw).CacheKey, track.CacheKey);
		Assert.Equal(Track(raw).StableIdentityKey, track.StableIdentityKey);
		Assert.Contains(MetadataNormalizer.BuildCandidates(track.Title, track.Artist, track.Album), item => item.Artist == Raw);
		Assert.Contains(track.SearchAlternates!, item => item.Artist == Credit && item.CanEstablishIdentity);
		Assert.True(LyricsMatcher.Evaluate(track, MatchingSafetyTests.Record(Title, Credit)).AutoEligible);
		Assert.True(LyricsMatcher.Evaluate(track, MatchingSafetyTests.Record(Title, Raw)).AutoEligible);
		Assert.Equal(Raw, track.OriginalMediaArtist);
	}

	[Theory]
	[InlineData("Different song", "Spotify", true)]
	[InlineData(Title, "chrome", true)]
	[InlineData(Title, "Spotify", false)]
	public void UntrustedEvidence_IsRejectedAndFallsBack(string title, string process, bool current)
	{
		Assert.Null(Observed(title, process, current));
		MediaTrackMetadata raw = Metadata();
		Assert.Same(raw, SpotifyArtistCredit.Apply(raw, Observed(title, process, current)));
		Assert.Equal(Raw, Track(raw).DisplayArtist);
		Assert.Null(raw.EnrichmentSource);
	}

	[Fact]
	public void TitleMustStillMatchWhenTheAsynchronousResultIsApplied()
	{
		MediaTrackMetadata changed = Metadata("Next song");
		Assert.Same(changed, SpotifyArtistCredit.Apply(changed, Observed()));
		Assert.Same(changed, SpotifyArtistCredit.Apply(changed, null));
		Assert.NotNull(Observed("tokyo watashi collection"));
	}

	[Theory]
	[InlineData("Earth, Wind & Fire")]
	[InlineData("Simon & Garfunkel")]
	[InlineData("AC/DC")]
	public void StructuredLinks_PreservePunctuationWithinWholeArtistNames(string wholeArtist)
	{
		string[] links = { wholeArtist, "Guest Artist" };
		string credit = string.Join(", ", links);
		ArtistCreditEnrichment? evidence = SpotifyArtistCredit.Validate("Spotify", true, Title, wholeArtist,
			Title, "再生中：" + credit + " の " + Title, links);
		Assert.NotNull(evidence);
		Assert.Equal(credit, Track(SpotifyArtistCredit.Apply(Metadata(artist: wholeArtist), evidence)).DisplayArtist);
	}

	[Fact]
	public void MissingRawArtistOrIncompleteLinksCannotEstablishEnrichment()
	{
		Assert.Null(SpotifyArtistCredit.Validate("Spotify", true, Title, "Other Artist", Title,
			"再生中：" + Credit + " の " + Title, Artists));
		Assert.Null(SpotifyArtistCredit.Validate("Spotify", true, Title, Raw, Title,
			"再生中：" + Credit + " の " + Title, Artists.Take(4).ToArray()));
		Assert.Null(SpotifyArtistCredit.Validate("Spotify", true, Title, Raw, Title, "Search results", Artists));
	}

	[Fact]
	public void GsmtcAlreadyExposesCredits_UiaIsNotRequested()
	{
		Assert.True(SpotifyArtistCredit.ShouldProbe("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", Metadata()));
		Assert.False(SpotifyArtistCredit.ShouldProbe("chrome", Metadata()));
		Assert.False(SpotifyArtistCredit.ShouldProbe("Spotify", Metadata(artist: Credit)));
		Assert.False(SpotifyArtistCredit.ShouldProbe("Spotify", Metadata() with { OriginalSubtitleRaw = Credit }));
		Assert.False(SpotifyArtistCredit.ShouldProbe("Spotify", Metadata() with { OriginalAlbumArtistRaw = Credit }));
	}

	[Fact]
	public void Diagnostics_SeparatesCompleteRawFieldsAndDisplayCredit()
	{
		MediaTrackMetadata metadata = SpotifyArtistCredit.Apply(Metadata(), Observed());
		var method = typeof(MediaSessionDiagnosticsWindow).GetMethod("BuildSessionDiagnostics", BindingFlags.NonPublic | BindingFlags.Static)!;
		string text = (string)method.Invoke(null, new object[] { new MediaSessionInfo { Metadata = metadata } })!;
		Assert.Contains("RAW TITLE: " + Title, text);
		Assert.Contains("RAW ARTIST: " + Raw, text);
		Assert.Contains("RAW ALBUM ARTIST: Various Artists", text);
		Assert.Contains("RAW SUBTITLE: —", text);
		Assert.Contains("RAW GENRES: —", text);
		Assert.Contains("RAW TRACK NUMBER: 2", text);
		Assert.Contains("DISPLAY ARTIST: " + Credit, text);
		Assert.Contains("DISPLAY TITLE: " + Title, text);
		Assert.Contains("NORMALIZED TITLE: " + Title, text);
		Assert.Contains("NORMALIZED ARTIST: " + Raw, text);
		Assert.Contains("TITLE ALIASES: " + Title, text);
		Assert.Contains("ARTIST SEARCH CANDIDATES: " + Raw, text);
		Assert.Contains(Credit, text);
		Assert.Contains("EDITION SIGNATURE: Standard", text);
		Assert.Contains("SPOTIFY WINDOW STATE:", text);
		Assert.Contains("ENRICHMENT SOURCE: Spotify UI Automation", text);
	}

	[Fact]
	public async Task BackgroundFailure_IsOptionalAndDoesNotEscapeMediaPolling()
	{
		TaskCompletionSource invoked = new(TaskCreationOptions.RunContinuationsAsynchronously);
		using var enricher = new SpotifyUiAutomationEnricher((_, _) => { invoked.SetResult(); throw new InvalidOperationException(); });
		Assert.Null(enricher.TryGet("Spotify", Metadata()));
		await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Null(enricher.TryGet("Spotify", Metadata()));
	}

	[Fact]
	public async Task SlowProvider_DoesNotBlockOrSpawnExtraCallsAndStaleTrackIsRejected()
	{
		TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
		using ManualResetEventSlim release = new();
		int calls = 0;
		using var enricher = new SpotifyUiAutomationEnricher((_, _) =>
		{
			Interlocked.Increment(ref calls); started.TrySetResult(); release.Wait(TimeSpan.FromSeconds(5)); return Observed();
		});
		try
		{
			Assert.Null(enricher.TryGet("Spotify", Metadata()));
			await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
			for (int index = 0; index < 10; index++) Assert.Null(enricher.TryGet("Spotify", Metadata("Next song")));
			Assert.Equal(1, Volatile.Read(ref calls));
		}
		finally { release.Set(); }
		Assert.Null(enricher.TryGet("Spotify", Metadata("Next song")));
	}
}
