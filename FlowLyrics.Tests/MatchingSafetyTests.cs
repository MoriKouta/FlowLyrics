using System;
using System.Linq;
using System.Reflection;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class MatchingSafetyTests
{
	[Theory]
	[InlineData("Song - From \"Movie\" Soundtrack")]
	[InlineData("Song (from \"Movie\" Soundtrack)")]
	public void ExplicitSoundtrackRelease_ProvidesAliasWithoutGenericSuffixStripping(string title)
	{
		Assert.True(MetadataNormalizer.TryReleaseTitle(title, out string primary, out string work));
		Assert.Equal("Song", primary); Assert.Equal("Movie", work);
		Assert.True(LyricsMatcher.Evaluate(new(title, "Artist", "Single", TimeSpan.FromSeconds(240)), Record("Song", "Artist", 240)).AutoEligible);
		Assert.False(MetadataNormalizer.TryReleaseTitle("Theme From Jurassic Park", out _, out _));
		Assert.False(MetadataNormalizer.TryReleaseTitle("Song - Other Title", out _, out _));
		Assert.DoesNotContain("Song", MetadataNormalizer.TitleAliases("Song (feat. Artist)"));
	}

	[Theory]
	[InlineData("Character(CV. Actor)")]
	[InlineData("Character(CV: Actor)")]
	[InlineData("Character（CV. Actor）")]
	[InlineData("Character（CV：Actor）")]
	[InlineData("Character(CV Actor)")]
	public void CreditPunctuationVariants_RetainWholeCreditAndIdentifyActor(string credit)
	{
		ArtistIdentity identity = ArtistIdentity.Parse(credit);
		Assert.Equal("Character", Assert.Single(identity.CharacterCredits).Character);
		Assert.Equal("Actor", identity.CharacterCredits[0].VoiceActor);
		Assert.True(identity.StronglyMatches(ArtistIdentity.Parse("Actor")));
		Assert.NotEmpty(identity.WholeArtist);
	}
	internal const string Song = "一歩前ノセカイ";
	internal const string Actors = "鈴代紗弓, 蟹沢萌子, 夏吉ゆうこ, 長谷川里桃, 中川梨花";
	internal const string Characters = "青天国春(CV.鈴代紗弓)、玉城杏夏(CV.蟹沢萌子)、聖舞理王(CV.夏吉ゆうこ)、祇園寺雪音(CV.長谷川里桃)、伊藤紅葉(CV.中川梨花)";
	internal static LrclibRecord Record(string title = Song, string artist = Actors, double duration = 261, int id = 1) =>
		new() { Id = id, TrackName = title, ArtistName = artist, AlbumName = "Another release", Duration = duration, SyncedLyrics = "[00:01.00]歌詞" };

	[Theory]
	[InlineData("一歩前ノセカイ - TVアニメ「シャインポスト」", "一歩前ノセカイ (TVアニメ「シャインポスト」)")]
	[InlineData("一歩前ノセカイ", "SHINEPOST Character Song Collection (TVアニメ「シャインポスト」)")]
	public void ReleaseVariants_MatchRecordingWithoutRequiringAlbum(string title, string album)
	{
		TrackInfo track = new(title, Actors, album, TimeSpan.FromSeconds(261));
		Assert.True(LyricsMatcher.Evaluate(track, Record(artist: "TINGS: " + Characters)).AutoEligible);
		Assert.Equal(Song, track.DisplayTitle);
		Assert.Contains(MetadataNormalizer.BuildCandidates(title, Actors, album), candidate => candidate.Title == Song);
		Assert.Equal(title, track.Title);
	}

	[Fact]
	public void CharacterCredits_ConnectExplicitActorsAndGroupButNotUnknownUnits()
	{
		ArtistIdentity credit = ArtistIdentity.Parse("TINGS: " + Characters);
		Assert.Contains(credit.CharacterCredits, item => item.Character == "玉城杏夏" && item.VoiceActor == "蟹沢萌子");
		Assert.True(credit.StronglyMatches(ArtistIdentity.Parse(Actors)));
		Assert.True(credit.StronglyMatches(ArtistIdentity.Parse("TINGS")));
		Assert.False(ArtistIdentity.Parse("TINGS").StronglyMatches(ArtistIdentity.Parse(Actors)));
		Assert.False(credit.StronglyMatches(ArtistIdentity.Parse("蟹沢萌子")));
		Assert.Contains(MetadataNormalizer.BuildCandidates(Song, "TINGS: " + Characters, "Album"), item => item.Artist == "TINGS");
		Assert.False(LyricsMatcher.Evaluate(new(Song, Actors, "", TimeSpan.FromSeconds(261)), Record(artist: "TINGS")).AutoEligible);
	}

	[Theory]
	[InlineData("spotify", "春風に乗って", Actors, 236, false)]
	[InlineData("spotify", Song, Actors, 236, false)]
	[InlineData("applemusic", Song, Actors, 236, false)]
	[InlineData("", Song, Actors, 236, false)]
	[InlineData("chrome", "春風に乗って", Actors, 236, false)]
	[InlineData("chrome", Song, "Other Artist", 236, false)]
	[InlineData("chrome", Song, Actors, 236, true)]
	[InlineData("chrome", Song, Actors, 100, false)]
	[InlineData("chrome", Song, Actors, 290, false)]
	public void Duration_UsesSourceAndCannotRescueWrongIdentity(string source, string title, string artist, double duration, bool expected)
	{
		TrackInfo track = new(Song, Actors, "Album", TimeSpan.FromSeconds(261), SourceAppUserModelId: source);
		LyricsCandidate evaluated = LyricsMatcher.Evaluate(track, Record(title, artist, duration));
		Assert.Equal(expected, evaluated.AutoEligible);
		Assert.Equal(expected, evaluated.UsesVideoDurationTolerance);
		Assert.Equal(expected, LyricsMatcher.SelectBestEffortCandidate(new[] { evaluated }) != null);
	}

	[Theory]
	[InlineData("Live")]
	[InlineData("Remix")]
	[InlineData("Acoustic")]
	[InlineData("Piano Version")]
	[InlineData("Instrumental")]
	[InlineData("Karaoke")]
	[InlineData("Off Vocal")]
	[InlineData("Radio Edit")]
	[InlineData("Single Version")]
	[InlineData("Extended Version")]
	[InlineData("Sped Up")]
	[InlineData("Slowed Down")]
	[InlineData("Nightcore")]
	[InlineData("2026 Remaster")]
	[InlineData("Re-recorded")]
	[InlineData("Taylor's Version")]
	[InlineData("Clean")]
	[InlineData("Explicit")]
	[InlineData("TV Size")]
	[InlineData("TV Version")]
	[InlineData("Japanese Version")]
	[InlineData("English Ver.")]
	[InlineData("Chinese Version")]
	[InlineData("Korean Version")]
	public void EditionConflict_IsNeverAutomaticEvenWithVideoDurationTolerance(string edition)
	{
		TrackInfo track = new("Song", "Artist", "", TimeSpan.FromSeconds(261), SourceAppUserModelId: "chrome");
		LrclibRecord record = Record("Song (" + edition + ")", "Artist", 240);
		Assert.False(LyricsMatcher.Evaluate(track, record).AutoEligible);
		Assert.False(LyricsMatcher.Evaluate(track with { Title = record.TrackName!, SourceAppUserModelId = "spotify" }, Record("Song", "Artist")).AutoEligible);
	}

	[Theory]
	[InlineData("Japanese Version", "English Version")]
	[InlineData("Piano Ver.", "Acoustic Ver.")]
	[InlineData("Single Version", "Extended Version")]
	[InlineData("Alice Remix", "Bob Remix")]
	[InlineData("2020 Remaster", "2026 Remaster")]
	[InlineData("Taylor's Version", "Original Version")]
	public void StructuredEditions_DoNotCollapseToGenericVersion(string first, string second)
	{
		TrackInfo track = new("Song (" + first + ")", "Artist", "", TimeSpan.FromSeconds(261));
		Assert.True(LyricsMatcher.Evaluate(track, Record(track.Title, "Artist")).AutoEligible);
		LyricsCandidate different = LyricsMatcher.Evaluate(track, Record("Song (" + second + ")", "Artist"));
		Assert.Contains("Version", different.MismatchedFields);
		Assert.False(different.AutoEligible);
	}

	[Fact]
	public void NestedEditions_AndRawProviderEditionSurviveAliases()
	{
		string title = "Song (feat. Artist) (Taylor's Version) (From The Vault)";
		Assert.True(RecordingEdition.Signature(title).Count >= 2);
		Assert.False(RecordingEdition.Signature(title).SetEquals(RecordingEdition.Signature("Song (Taylor's Version)")));
		Assert.False(RecordingEdition.Signature("Song (Remix (Alice))").SetEquals(RecordingEdition.Signature("Song (Remix (Bob))")));
		TrackInfo track = new("Song", "Artist", "", TimeSpan.FromSeconds(261),
			SearchAlternates: new[] { new SearchMetadataCandidate("Song", "Artist", "") }, OriginalMediaTitle: "Song (Live)");
		Assert.False(LyricsMatcher.Evaluate(track, Record("Song", "Artist")).AutoEligible);
	}

	[Fact]
	public void NestedNamedVersions_RetainIdentityAlongsideInnerLiveMarker()
	{
		var first = RecordingEdition.Signature("Song (First Version (Live))");
		var second = RecordingEdition.Signature("Song (Second Version (Live))");
		Assert.Contains("live", first);
		Assert.Contains("named-version:firstversion", first);
		Assert.False(first.SetEquals(second));
	}

	[Theory]
	[InlineData("Alice Remix")]
	[InlineData("Taylor's Version")]
	public void BrowserCredit_DoesNotBecomePartOfEditionIdentity(string edition)
	{
		string title = "Song (" + edition + ")";
		string raw = "Artist - " + title;
		RepairedProviderMetadata metadata = ProviderMetadataRepair.Repair("chrome", raw, "Artist", "");
		TrackInfo track = new(metadata.Title, metadata.Artist, "", TimeSpan.FromSeconds(261),
			SearchAlternates: metadata.SearchAlternates, OriginalMediaTitle: raw, SourceAppUserModelId: "chrome");
		Assert.True(LyricsMatcher.Evaluate(track, Record(title, "Artist")).AutoEligible);
	}

	[Fact]
	public void NoSafeCandidate_ReturnsNoAutomaticSelection()
	{
		TrackInfo track = new(Song, Actors, "Album", TimeSpan.FromSeconds(261));
		LrclibRecord[] records = { Record("春風に乗って", Actors, 236), Record(Song, "Other", 261, 2) };
		Assert.Null(LyricsMatcher.SelectSafeAutomaticCandidate(track, records));
		Assert.Null(LyricsMatcher.SelectBestEffortCandidate(LyricsMatcher.RankCandidates(track, records)));
		Assert.False(LyricsMatcher.Evaluate(track, withInstrumental()).AutoEligible);
		LrclibRecord withInstrumental() => new() { Id = 3, TrackName = Song, ArtistName = Actors, Duration = 261, Instrumental = true };
	}

	[Fact]
	public void CrossScriptArtist_RequiresExplicitEvidence()
	{
		TrackInfo track = new("Song", "Latin Artist", "", TimeSpan.FromSeconds(261));
		LrclibRecord record = Record("Song", "日本語の別アーティスト");
		Assert.False(LyricsMatcher.Evaluate(track, record).AutoEligible);
		Assert.True(LyricsMatcher.Evaluate(track with { SearchAlternates = new[] { new SearchMetadataCandidate("Song", record.ArtistName!, "") } }, record).AutoEligible);
		Assert.False(LyricsMatcher.Evaluate(track with { SearchAlternates = new[] { new SearchMetadataCandidate("Song", record.ArtistName!, "") { CanEstablishIdentity = false } } }, record).AutoEligible);
	}

	[Fact]
	public void Cache_OldMatcherAndUnsafeBestMatchAreRejectedButLocalRemains()
	{
		TrackInfo track = new(Song, Actors, "", TimeSpan.FromSeconds(261));
		int version = (int)typeof(LyricsService).GetField("CurrentMatcherVersion", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
		MethodInfo validate = typeof(LyricsService).GetMethod("IsValidPositiveCache", BindingFlags.NonPublic | BindingFlags.Static)!;
		bool Valid(LyricsCacheEntry entry) => (bool)validate.Invoke(null, new object[] { track, entry })!;
		LyricsCacheEntry cache = new() { LrclibId = 1, LrclibTrackName = Song, LrclibArtistName = Actors, LrclibDuration = 261, SyncedLyrics = "[00:01.00]歌詞", SelectionMode = "BestMatch", MatcherVersion = version };
		Assert.True(version > 6);
		Assert.True(Valid(cache));
		cache.LrclibTrackName = "春風に乗って";
		Assert.False(Valid(cache));
		cache.LrclibTrackName = Song;
		cache.MatcherVersion = 6;
		Assert.False(Valid(cache));
		cache.Source = "LOCAL LRC";
		Assert.True(Valid(cache));
	}
}
