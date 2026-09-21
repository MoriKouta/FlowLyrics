using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class MetadataIdentityTests
{
	[Theory]
	[InlineData("【Ado】アイ・アイ・ア（AiAiA）", "Ado", "アイ・アイ・ア", "AiAiA")]
	[InlineData("【Ado】AiAiA", "Ado", "AiAiA", null)]
	[InlineData("【Some Artist】Song Name", "Some Artist", "Song Name", null)]
	[InlineData("日本語の曲 (English Name)", "Artist", "日本語の曲", "English Name")]
	public void BrowserTitles_UseOnlyObservedCreditsAndLanguageAliases(string rawTitle, string artist, string title, string? alias)
	{
		RepairedProviderMetadata repaired = ProviderMetadataRepair.Repair("chrome", rawTitle, artist, "");
		TrackInfo track = new(repaired.Title, repaired.Artist, repaired.Album, TimeSpan.FromSeconds(261),
			SearchAlternates: repaired.SearchAlternates, OriginalMediaTitle: rawTitle, OriginalMediaArtist: artist, SourceAppUserModelId: "chrome");
		Assert.Equal(title, track.DisplayTitle);
		Assert.Equal(artist, track.DisplayArtist);
		Assert.Equal(rawTitle, track.OriginalMediaTitle);
		Assert.Equal(artist, track.OriginalMediaArtist);
		if (alias != null)
		{
			Assert.Contains(track.SearchAlternates!, item => item.Title == alias);
			Assert.True(LyricsMatcher.Evaluate(track, MatchingSafetyTests.Record(alias, artist)).AutoEligible);
		}
		else Assert.DoesNotContain(track.SearchAlternates ?? Array.Empty<SearchMetadataCandidate>(), item => item.Title == "アイ・アイ・ア");
	}

	[Theory]
	[InlineData("Live")]
	[InlineData("Remix")]
	[InlineData("Acoustic")]
	[InlineData("Instrumental")]
	[InlineData("TV Size")]
	[InlineData("2026 Remaster")]
	[InlineData("feat. Someone")]
	public void Parentheses_DoNotTurnVersionsOrCreditsIntoAliases(string suffix)
	{
		string title = "日本語の曲 (" + suffix + ")";
		Assert.False(MetadataNormalizer.TryBilingualTitle(title, out _, out _));
		Assert.Equal(title, MetadataNormalizer.NormalizeForSearch(title, "Artist", "").Title);
		Assert.Equal(title, ProviderMetadataRepair.Repair("chrome", title, "Artist", "").Title);
		Assert.DoesNotContain(suffix, LyricsMatcher.ExtractTitleAliases(title));
		Assert.DoesNotContain("日本語の曲", LyricsMatcher.ExtractTitleAliases(title));
	}

	[Theory]
	[InlineData("MV")]
	[InlineData("Official")]
	[InlineData("Lyrics")]
	[InlineData("Live")]
	public void ProductionBrackets_AreNotArtistCredits(string marker)
	{
		RepairedProviderMetadata repaired = ProviderMetadataRepair.Repair("chrome", "【" + marker + "】Song", "Artist", "");
		Assert.Equal("Artist", repaired.Artist);
		Assert.DoesNotContain(repaired.SearchAlternates ?? Array.Empty<SearchMetadataCandidate>(), item => item.Artist == marker);
		if (marker == "Live") Assert.Contains("Live", repaired.Title);
	}

	[Fact]
	public void UnprovedBracketsAndSameScriptSubtitlesRemainIntact()
	{
		Assert.Equal("【Someone Else】Song", ProviderMetadataRepair.Repair("chrome", "【Someone Else】Song", "Artist", "").Title);
		Assert.Equal("【Ado】AiAiA", ProviderMetadataRepair.Repair("spotify", "【Ado】AiAiA", "Ado", "").Title);
		Assert.False(MetadataNormalizer.TryBilingualTitle("Song Name (English Name)", out _, out _));
		Assert.DoesNotContain("Song", LyricsMatcher.ExtractTitleAliases("Song - Another Song"));
		Assert.DoesNotContain("Song", LyricsMatcher.ExtractTitleAliases("Song (Unrelated subtitle)"));
	}

	[Fact]
	public void BrowserRoleAmbiguity_RequiresTwoSignalsAndNeverInventsArtist()
	{
		string album = "一歩前ノセカイ (TVアニメ「シャインポスト」)";
		RepairedProviderMetadata repaired = ProviderMetadataRepair.Repair("chrome", "シャインポスト", MatchingSafetyTests.Song, album);
		SearchMetadataCandidate candidate = Assert.Single(repaired.SearchAlternates!);
		Assert.Equal("シャインポスト", repaired.Title);
		Assert.Equal(MatchingSafetyTests.Song, repaired.Artist);
		Assert.Equal(MatchingSafetyTests.Song, candidate.Title);
		Assert.Empty(candidate.Artist);
		Assert.False(candidate.CanEstablishIdentity);
		RepairedProviderMetadata knownCredit = ProviderMetadataRepair.Repair("chrome", "シャインポスト", MatchingSafetyTests.Song, album, MatchingSafetyTests.Actors);
		TrackInfo track = new(knownCredit.Title, knownCredit.Artist, knownCredit.Album, TimeSpan.FromSeconds(261), SearchAlternates: knownCredit.SearchAlternates);
		Assert.True(LyricsMatcher.Evaluate(track, MatchingSafetyTests.Record()).AutoEligible);
		RepairedProviderMetadata weak = ProviderMetadataRepair.Repair("chrome", "Different work", MatchingSafetyTests.Song, album);
		Assert.DoesNotContain(weak.SearchAlternates ?? Array.Empty<SearchMetadataCandidate>(), item => item.Evidence.Contains("album base title"));
	}

	[Theory]
	[InlineData(MatchingSafetyTests.Actors)]
	[InlineData("Earth, Wind & Fire")]
	[InlineData("AC/DC")]
	[InlineData("Simon & Garfunkel")]
	[InlineData("Artist A")]
	public void ArtistDisplay_RetainsWholeCreditThroughMatching(string rawArtist)
	{
		RepairedProviderMetadata repaired = ProviderMetadataRepair.Repair("spotify", "Song", rawArtist, "Album");
		TrackInfo track = new(repaired.Title, repaired.Artist, repaired.Album, TimeSpan.FromSeconds(261), OriginalMediaArtist: rawArtist);
		ArtistIdentity identity = ArtistIdentity.Parse(track.Artist);
		_ = MetadataNormalizer.BuildCandidates(track.Title, track.Artist, track.Album);
		_ = LyricsMatcher.Evaluate(track, MatchingSafetyTests.Record("Song", "Artist A, Artist B"));
		Assert.Equal(rawArtist, identity.WholeArtist);
		Assert.Equal(rawArtist, track.DisplayArtist);
		Assert.Equal(rawArtist, track.RawArtist);
		Assert.Equal(rawArtist, track.OriginalMediaArtist);
		Assert.Equal(rawArtist, ProviderMetadataRepair.Repair("chrome", rawArtist + " 'Song' Official MV", rawArtist, "Album").Artist);
	}

	[Theory]
	[InlineData("Earth, Wind & Fire", "Earth")]
	[InlineData("AC/DC", "AC")]
	[InlineData("Simon & Garfunkel", "Simon")]
	public void Matching_DoesNotReduceWholeBandsToOneToken(string whole, string member)
	{
		Assert.False(ArtistIdentity.Parse(whole).StronglyMatches(ArtistIdentity.Parse(member)));
		Assert.False(ArtistIdentity.Parse(member).StronglyMatches(ArtistIdentity.Parse(whole)));
	}

	[Fact]
	public void BrowserDisplay_DoesNotDropAdditionalProviderCredits()
	{
		const string credit = "Artist A, Artist B, Artist C";
		Assert.Equal(credit, ProviderMetadataRepair.Repair("chrome", "Artist A - Song (Official MV)", credit, "").Artist);
		Assert.False(ArtistIdentity.Parse("Character (CV.Actor), Someone Else").StronglyMatches(ArtistIdentity.Parse("Actor")));
		Assert.False(ArtistIdentity.Parse("Someone Else, Character (CV.Actor)").StronglyMatches(ArtistIdentity.Parse("Actor")));
		Assert.False(ArtistIdentity.Parse("Someone Else, Character (CV.Actor)").StronglyMatches(ArtistIdentity.Parse("Someone Else")));
	}

	[Fact]
	public async Task MediaSnapshot_PreservesProviderRawMetadataAndSource()
	{
		const string title = "  【Ado】アイ・アイ・ア（AiAiA）  ";
		const string artist = " Ado ";
		RepairedProviderMetadata repaired = ProviderMetadataRepair.Repair("chrome", title, artist, " Album ");
		using MediaSessionService sessions = new(new TestProvider(new(repaired.Title, repaired.Artist, repaired.Album,
			TimeSpan.FromSeconds(261), repaired.SearchAlternates, title, artist, " Album ")));
		Assert.Null(await sessions.GetSnapshotAsync());
		await Task.Delay(700);
		TrackInfo track = (await sessions.GetSnapshotAsync())!.Track;
		Assert.Equal(title, track.OriginalMediaTitle);
		Assert.Equal(artist, track.RawArtist);
		Assert.Equal("Ado", track.DisplayArtist);
		Assert.Equal(" Album ", track.OriginalMediaAlbum);
		Assert.Equal("chrome", track.SourceAppUserModelId);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void SettingsBaml_CurrentTrackShowsFullCreditWithBoundedWrapping(bool enriched)
	{
		Exception? failure = null;
		Thread thread = new(() =>
		{
			string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-settings-ui-" + Guid.NewGuid().ToString("N"));
			try
			{
				using LyricsService lyrics = new(directory);
				using MediaSessionService sessions = new(new TestProvider(new("Song", "Artist", "", TimeSpan.FromSeconds(261))));
				PersonalSyncStore sync = new(directory);
				TrackInfo track = new(MatchingSafetyTests.Song, MatchingSafetyTests.Actors, "Album", TimeSpan.FromSeconds(261));
				if (enriched) track = track with { Artist = "鈴代紗弓", OriginalMediaArtist = "鈴代紗弓" };
				SettingsWindow window = new(new AppSettings(), directory, lyrics, sessions, sync,
					() => null, () => track, () => null, () => null, () => Task.CompletedTask);
				try
				{
					typeof(SettingsWindow).GetMethod("RefreshLyricsTab", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
					TextBlock Read(string name) => (TextBlock)typeof(SettingsWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
					TextBlock title = Read("CurrentTrackTitleText"), artist = Read("CurrentTrackArtistText");
					if (enriched)
					{
						Assert.Equal("鈴代紗弓", artist.Text);
						track = track with { EnrichedArtistCredit = MatchingSafetyTests.Actors };
						window.RefreshCurrentTrack();
					}
					Assert.Equal(MatchingSafetyTests.Song, title.Text);
					Assert.Equal(MatchingSafetyTests.Actors, artist.Text);
					Assert.Equal(TextWrapping.Wrap, artist.TextWrapping);
					Assert.Equal(TextTrimming.CharacterEllipsis, artist.TextTrimming);
					Assert.Equal(44, artist.MaxHeight);
				}
				finally { window.Close(); }
			}
			catch (Exception ex) { failure = ex; }
			finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
		});
		thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
		Assert.Null(failure);
	}

	private sealed class TestProvider(MediaTrackMetadata metadata) : IMediaSessionProvider
	{
		public event EventHandler? SessionsChanged { add { } remove { } }
		public Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<MediaSessionInfo>>(new[] { new MediaSessionInfo
			{
				SessionId = "browser-test", SourceAppUserModelId = "chrome", DisplaySourceName = "Google Chrome",
				Metadata = metadata, PlaybackState = MediaPlaybackState.Playing, IsCurrentSession = true
			} });
		public Task<bool> TryTogglePlayPauseAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
		public Task<bool> TrySkipNextAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
		public Task<bool> TrySkipPreviousAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
		public Task<bool> TrySeekAsync(string sessionId, TimeSpan position, CancellationToken cancellationToken = default) => Task.FromResult(false);
		public void Dispose() { }
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void CurrentTrackHeader_SeparatesCreditsAndWrapsWithinTwoLines(bool enriched)
	{
		Exception? failure = null;
		Thread thread = new(() =>
		{
			try
			{
				StackPanel panel = new() { Width = 280 };
				TextBlock title = new() { Name = "TrackTitleText", TextTrimming = TextTrimming.CharacterEllipsis };
				panel.Children.Add(title);
				TextBlock artist = CurrentTrackHeader.Attach(panel, title);
				Assert.Same(artist, CurrentTrackHeader.Attach(panel, title));
				TrackInfo track = new(MatchingSafetyTests.Song, MatchingSafetyTests.Actors, "", TimeSpan.FromSeconds(261));
				if (enriched) track = track with { Artist = "鈴代紗弓", OriginalMediaArtist = "鈴代紗弓", EnrichedArtistCredit = MatchingSafetyTests.Actors };
				CurrentTrackHeader.Update(title, artist, track, "Idle");
				panel.Measure(new Size(280, 200)); panel.Arrange(new Rect(0, 0, 280, 200));
				Assert.Equal(MatchingSafetyTests.Song, title.Text);
				Assert.Equal(MatchingSafetyTests.Actors, artist.Text);
				Assert.Equal(TextWrapping.Wrap, artist.TextWrapping);
				Assert.True(artist.ActualHeight <= 32);
				Assert.True(artist.ActualWidth <= 280);
				panel.Width = 400; panel.Measure(new Size(400, 200)); panel.Arrange(new Rect(0, 0, 400, 200));
				Assert.True(artist.ActualWidth > 280);
				CurrentTrackHeader.Update(title, artist, null, "Idle");
				Assert.Equal(Visibility.Collapsed, artist.Visibility);
			}
			catch (Exception ex) { failure = ex; }
		});
		thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
		Assert.Null(failure);
	}
}
