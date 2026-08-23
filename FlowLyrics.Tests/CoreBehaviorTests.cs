using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class CoreBehaviorTests
{
	[Fact]
	public void MetadataNormalizer_PreservesRawAndRemovesOnlySafeSearchNoise()
	{
		IReadOnlyList<SearchMetadataCandidate> candidates = MetadataNormalizer.BuildCandidates(
			"  Magnetic (Official Audio)  ",
			"ILLIT",
			"SUPER REAL ME");

		Assert.Equal("Magnetic (Official Audio)", candidates[0].Title);
		Assert.Equal("Magnetic", candidates[1].Title);
		Assert.Equal("Song (Live)", MetadataNormalizer.NormalizeForSearch("Song (Live)", "Artist", "Album").Title);
		Assert.Equal("Song (Remix)", MetadataNormalizer.NormalizeForSearch("Song (Remix)", "Artist", "Album").Title);
	}

	[Fact]
	public void ProviderMetadataRepair_SplitsAppleMusicArtistAndAlbum()
	{
		RepairedProviderMetadata metadata = ProviderMetadataRepair.Repair(
			"AppleInc.AppleMusicWin_nzyj5cx40ttqa!App",
			"ライラック",
			"Mrs. GREEN APPLE — ライラック - Single",
			string.Empty);

		Assert.Equal("ライラック", metadata.Title);
		Assert.Equal("Mrs. GREEN APPLE", metadata.Artist);
		Assert.Equal("ライラック - Single", metadata.Album);
	}

	[Fact]
	public void ProviderMetadataRepair_RemovesAppleAlbumFromArtistEvenWhenAlbumFieldsArePopulated()
	{
		RepairedProviderMetadata metadata = ProviderMetadataRepair.Repair(
			"AppleInc.AppleMusicWin_nzyj5cx40ttqa!App",
			"ライラック",
			"Mrs. GREEN APPLE — ライラック - Single",
			"ライラック - Single",
			"Mrs. GREEN APPLE — ライラック - Single");

		Assert.Equal("Mrs. GREEN APPLE", metadata.Artist);
		Assert.Equal("ライラック - Single", metadata.Album);
	}

	[Fact]
	public void ProviderMetadataRepair_ExtractsJapaneseQuotedYouTubeTitle()
	{
		RepairedProviderMetadata metadata = ProviderMetadataRepair.Repair(
			"MSEdge",
			"YOASOBI「怪物」Offcial Music Video (YOASOBI - Monster)",
			"YOASOBI",
			string.Empty);

		Assert.Equal("怪物", metadata.Title);
		Assert.Equal("YOASOBI", metadata.Artist);
	}

	[Fact]
	public void ProviderMetadataRepair_UsesYouTubeSlashCreditInsteadOfChannelName()
	{
		RepairedProviderMetadata metadata = ProviderMetadataRepair.Repair(
			"Microsoft.MicrosoftEdge.Stable_8wekyb3d8bbwe!MSEDGE",
			"ビビデバ / 星街すいせい(official)",
			"Suisei Channel",
			string.Empty);

		Assert.Equal("ビビデバ", metadata.Title);
		Assert.Equal("星街すいせい", metadata.Artist);
	}

	[Fact]
	public void ProviderMetadataRepair_LeavesAmbiguousBrowserMetadataAlone()
	{
		RepairedProviderMetadata metadata = ProviderMetadataRepair.Repair(
			"chrome",
			"Episode 12 - Interview with an Artist",
			"Example Podcast",
			"Season 2");

		Assert.Equal("Episode 12 - Interview with an Artist", metadata.Title);
		Assert.Equal("Example Podcast", metadata.Artist);
		Assert.Equal("Season 2", metadata.Album);
	}

	[Theory]
	[InlineData("[MV] Creepy Nuts - バレる！", "Creepy Nuts", "バレる!", "Creepy Nuts")]
	[InlineData("音乃瀬奏 - You＆合図 (Official MV)", "KANADE Ch. 音乃瀬奏 - ReGLOSS", "You&合図", "音乃瀬奏")]
	[InlineData("Chinozo 'グッバイ宣言' feat.FloweR", "Chinozo", "グッバイ宣言", "Chinozo")]
	[InlineData("ATEEZ(에이티즈) - 'BAD' Official MV", "KQ ENTERTAINMENT", "BAD", "ATEEZ")]
	[InlineData("ILLIT (아일릿) ‘It’s Me’ Official MV", "HYBE LABELS", "It’s Me", "ILLIT")]
	[InlineData("Hearts2Hearts 하츠투하츠 'FOCUS' MV", "SMTOWN", "FOCUS", "Hearts2Hearts")]
	public void ProviderMetadataRepair_RecognizesCommonYouTubeMusicConventions(
		string rawTitle,
		string channel,
		string expectedTitle,
		string expectedArtist)
	{
		RepairedProviderMetadata metadata = ProviderMetadataRepair.Repair("MSEdge", rawTitle, channel, string.Empty);

		Assert.Equal(expectedTitle, metadata.Title);
		Assert.Equal(expectedArtist, metadata.Artist);
	}

	[Fact]
	public void ProviderMetadataRepair_RetainsFastAlternatesForLanguageAliasesAndAmbiguousCredits()
	{
		RepairedProviderMetadata aliases = ProviderMetadataRepair.Repair(
			"MSEdge",
			"Hearts2Hearts 하츠투하츠 'FOCUS' MV",
			"SMTOWN",
			string.Empty);
		RepairedProviderMetadata slash = ProviderMetadataRepair.Repair(
			"MSEdge",
			"テトリス / 重音テトSV",
			"柊マグネタイト",
			string.Empty);

		Assert.Contains(aliases.SearchAlternates!, candidate => candidate.Title == "FOCUS" && candidate.Artist == "하츠투하츠");
		Assert.Equal("テトリス", slash.Title);
		Assert.Equal("柊マグネタイト", slash.Artist);
		Assert.Contains(slash.SearchAlternates!, candidate => candidate.Title == "テトリス" && candidate.Artist == "重音テトSV");
	}

	[Fact]
	public void TrackIdentity_IsPlayerIndependentButVersionSensitive()
	{
		TrackInfo spotify = new("Magnetic", "ILLIT", "SUPER REAL ME", TimeSpan.FromSeconds(160), "spotify-id");
		TrackInfo apple = new("Magnetic", "ILLIT", "SUPER REAL ME", TimeSpan.FromSeconds(160), "apple-id");
		TrackInfo remix = new("Magnetic (Remix)", "ILLIT", "SUPER REAL ME", TimeSpan.FromSeconds(160));
		TrackInfo longer = new("Magnetic", "ILLIT", "SUPER REAL ME", TimeSpan.FromSeconds(175));

		Assert.Equal(spotify.StableIdentityKey, apple.StableIdentityKey);
		Assert.NotEqual(spotify.StableIdentityKey, remix.StableIdentityKey);
		Assert.NotEqual(spotify.StableIdentityKey, longer.StableIdentityKey);
	}

	[Fact]
	public void LrclibQueryBuilder_EncodesValuesExactlyOnce()
	{
		MethodInfo method = typeof(LyricsService).GetMethod("BuildQuery", BindingFlags.NonPublic | BindingFlags.Static)!;
		Dictionary<string, string> values = new()
		{
			["track_name"] = "A&B + C?#% / ' \" 日本語",
			["artist_name"] = "aespa"
		};
		string query = (string)method.Invoke(null, new object[] { values })!;

		Assert.Contains("track_name=A%26B%20%2B%20C%3F%23%25%20%2F%20%27%20%22%20", query);
		Assert.Contains("artist_name=aespa", query);
		Assert.DoesNotContain("%2526", query);
	}

	[Fact]
	public async Task MediaSelection_UsesPlayingThenPreferredAndExcludesIgnored()
	{
		FakeMediaSessionProvider provider = new();
		provider.Sessions = new[]
		{
			Session("spotify", "Spotify", MediaPlaybackState.Paused, current: true),
			Session("vlc", "VLC", MediaPlaybackState.Playing, current: false)
		};
		using MediaSessionService service = new(provider);

		IReadOnlyList<MediaSessionInfo> automatic = await service.GetSessionsAsync();
		Assert.Equal("vlc", automatic.Single(item => item.IsSelectedByFlowLyrics).SourceAppUserModelId);

		service.ConfigureSelection("spotify", Array.Empty<string>());
		IReadOnlyList<MediaSessionInfo> preferred = await service.GetSessionsAsync();
		Assert.Equal("spotify", preferred.Single(item => item.IsSelectedByFlowLyrics).SourceAppUserModelId);

		service.ConfigureSelection("spotify", new[] { "spotify" });
		IReadOnlyList<MediaSessionInfo> ignored = await service.GetSessionsAsync();
		Assert.True(ignored.Single(item => item.SourceAppUserModelId == "spotify").IsIgnored);
		Assert.Equal("vlc", ignored.Single(item => item.IsSelectedByFlowLyrics).SourceAppUserModelId);
	}

	[Fact]
	public async Task Snapshot_PropagatesProviderCapabilitiesAfterMetadataStabilizes()
	{
		FakeMediaSessionProvider provider = new();
		provider.Sessions = new[]
		{
			Session("browser", "Google Chrome", MediaPlaybackState.Playing, current: true) with
			{
				Capabilities = new MediaPlaybackCapabilities(true, true, true, false, false, false)
			}
		};
		using MediaSessionService service = new(provider);

		Assert.Null(await service.GetSnapshotAsync());
		await Task.Delay(700);
		PlaybackSnapshot? snapshot = await service.GetSnapshotAsync();

		Assert.NotNull(snapshot);
		Assert.True(snapshot!.CanTogglePlayPause);
		Assert.False(snapshot.CanSkipNext);
		Assert.False(snapshot.CanSeek);
		Assert.Equal("Google Chrome", snapshot.SourceDisplayName);
	}

	[Fact]
	public async Task AppleStyleTimeline_RemainsMonotonicAndKeepsSeekInteractive()
	{
		FakeMediaSessionProvider provider = new();
		provider.Sessions = new[]
		{
			Session("applemusic", "Apple Music", MediaPlaybackState.Playing, current: true) with
			{
				Position = TimeSpan.FromSeconds(10),
				HasTimeline = true,
				TimelineUpdatedAtUtc = DateTimeOffset.UtcNow,
				Capabilities = new MediaPlaybackCapabilities(true, true, true, true, true, false)
			}
		};
		using MediaSessionService service = new(provider);

		Assert.Null(await service.GetSnapshotAsync());
		await Task.Delay(700);
		PlaybackSnapshot stable = (await service.GetSnapshotAsync())!;
		Assert.True(stable.CanSeek);

		provider.Sessions = new[]
		{
			provider.Sessions[0] with
			{
				Position = TimeSpan.FromSeconds(8),
				TimelineUpdatedAtUtc = DateTimeOffset.UtcNow,
				CapturedAtUtc = DateTimeOffset.UtcNow
			}
		};
		PlaybackSnapshot jittered = (await service.GetSnapshotAsync())!;
		Assert.True(jittered.Position >= stable.Position);

		Assert.True(await service.TrySeekAsync(TimeSpan.FromSeconds(60)));
		PlaybackSnapshot afterSeek = (await service.GetSnapshotAsync())!;
		Assert.True(afterSeek.Position >= TimeSpan.FromSeconds(59.5));
	}

	[Fact]
	public void BestEffortLyrics_UsesTheHighestScoringUsableCandidate()
	{
		LyricsCandidate lower = new()
		{
			Record = new LrclibRecord { Id = 1, SyncedLyrics = "[00:00.00]lower" },
			Score = 70
		};
		LyricsCandidate higher = new()
		{
			Record = new LrclibRecord { Id = 2, PlainLyrics = "higher" },
			Score = 88
		};

		LyricsCandidate? selected = LyricsMatcher.SelectBestEffortCandidate(new[] { lower, higher });

		Assert.NotNull(selected);
		Assert.Equal(2, selected!.Record.Id);
	}

	[Fact]
	public void LyricsMatcher_UsesProviderLanguageAliasAsAnExactIdentity()
	{
		TrackInfo track = new(
			"FOCUS",
			"Hearts2Hearts",
			string.Empty,
			TimeSpan.FromSeconds(189),
			SearchAlternates: new[] { new SearchMetadataCandidate("FOCUS", "하츠투하츠", string.Empty) });
		LrclibRecord record = new()
		{
			Id = 42,
			TrackName = "FOCUS",
			ArtistName = "하츠투하츠",
			Duration = 189,
			SyncedLyrics = "[00:00.00]lyrics"
		};

		LyricsCandidate evaluated = LyricsMatcher.Evaluate(track, record);

		Assert.True(evaluated.AutoEligible);
		Assert.False(evaluated.ArtistMatchIsCrossScript);
	}

	[Fact]
	public void VolumeSessionMatcher_FollowsTheSelectedPlayerIdentity()
	{
		MethodInfo matcher = typeof(SystemVolumeService).GetMethod("IsSourceSessionIdentity", BindingFlags.NonPublic | BindingFlags.Static)!;
		bool apple = (bool)matcher.Invoke(null, new object?[] { "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", "AMPMediaPlayer", "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", null, null, null, null })!;
		bool appleHelper = (bool)matcher.Invoke(null, new object?[] { "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", "AMPMediaPlayer", null, null, null, null, null })!;
		bool chrome = (bool)matcher.Invoke(null, new object?[] { "chrome.exe", "chrome", null, null, null, null, null })!;
		bool mismatch = (bool)matcher.Invoke(null, new object?[] { "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", "Spotify", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", null, null, null, null })!;

		Assert.True(apple);
		Assert.True(appleHelper);
		Assert.True(chrome);
		Assert.False(mismatch);
	}

	[Fact]
	public void SeekTargets_PreferTrackRelativeTicksBeforeNonZeroTimelineOrigin()
	{
		MethodInfo builder = typeof(WindowsMediaSessionProvider).GetMethod("BuildSeekTargetTicks", BindingFlags.NonPublic | BindingFlags.Static)!;
		long[] targets = (long[])builder.Invoke(null, new object[]
		{
			TimeSpan.FromSeconds(60),
			TimeSpan.FromMinutes(10),
			TimeSpan.FromMinutes(10),
			TimeSpan.FromMinutes(20)
		})!;

		Assert.Equal(TimeSpan.FromSeconds(60).Ticks, targets[0]);
		Assert.Contains(TimeSpan.FromMinutes(11).Ticks, targets);
	}

	[Fact]
	public void UiAutomationSeekMatcher_RejectsAmbiguousVolumeRange()
	{
		Type matcherType = typeof(WindowsMediaSessionProvider).Assembly.GetType("FlowLyrics.Services.MediaPlayerUiAutomation")!;
		MethodInfo scorer = matcherType.GetMethod("ScoreCandidate", BindingFlags.NonPublic | BindingFlags.Static)!;
		double volumeScore = (double)scorer.Invoke(null, new object?[]
		{
			"Volume", "volumeSlider", "Slider", 0d, 100d, 50d, 110d, 24d, 90d, 180d
		})!;
		double seekScore = (double)scorer.Invoke(null, new object?[]
		{
			"Playback position", "progressSlider", "Slider", 0d, 180d, 90d, 420d, 18d, 90d, 180d
		})!;

		Assert.True(double.IsNegativeInfinity(volumeScore));
		Assert.True(seekScore > 0d);
	}

	[Fact]
	public void LyricsOnlyMode_PreservesUnderlyingComponentChoices()
	{
		AppSettings settings = new()
		{
			LyricsOnlyMode = true,
			ShowPanelBorder = false,
			ShowTrackInfo = true,
			ShowPlaybackControls = false,
			ShowProgressBar = true
		};

		AppSettings clone = settings.Clone();

		Assert.True(clone.LyricsOnlyMode);
		Assert.False(clone.ShowPanelBorder);
		Assert.True(clone.ShowTrackInfo);
		Assert.False(clone.ShowPlaybackControls);
		Assert.True(clone.ShowProgressBar);
	}

	[Fact]
	public void PersonalSync_NoProfileLeavesPlaybackTimeUnchanged()
	{
		TimeSpan playback = TimeSpan.FromSeconds(42.75);

		TimeSpan mapped = PersonalSyncMapper.MapPlaybackToLyrics(playback, null);

		Assert.Equal(playback, mapped);
	}

	[Theory]
	[InlineData(30.0, 1.2, 28.8)]
	[InlineData(30.0, -0.8, 30.8)]
	[InlineData(100.0, 42.0, 58.0)]
	[InlineData(100.0, -38.0, 138.0)]
	[InlineData(0.2, 1.0, 0.0)]
	public void PersonalSync_OffsetUsesDisplayedDelayConvention(double playback, double offset, double expected)
	{
		PersonalSyncProfile profile = new() { Mode = PersonalSyncMode.Offset, OffsetSeconds = offset };

		double mapped = PersonalSyncMapper.MapPlaybackToLyrics(TimeSpan.FromSeconds(playback), profile).TotalSeconds;

		Assert.Equal(expected, mapped, precision: 6);
	}

	[Fact]
	public void PersonalSync_AdvancedMappingIsSeekIndependent()
	{
		PersonalSyncProfile profile = new()
		{
			Mode = PersonalSyncMode.Advanced,
			Anchors = new()
			{
				new PersonalSyncAnchor { PlaybackSeconds = 10, LyricsSeconds = 8 },
				new PersonalSyncAnchor { PlaybackSeconds = 20, LyricsSeconds = 19 }
			}
		};

		Assert.Equal(21, PersonalSyncMapper.MapPlaybackToLyrics(TimeSpan.FromSeconds(22), profile).TotalSeconds, 6);
		Assert.Equal(10, PersonalSyncMapper.MapPlaybackToLyrics(TimeSpan.FromSeconds(12), profile).TotalSeconds, 6);
		Assert.Equal(24, PersonalSyncMapper.MapPlaybackToLyrics(TimeSpan.FromSeconds(25), profile).TotalSeconds, 6);
	}

	[Fact]
	public void PersonalSync_FutureSyncPointDoesNotChangeEarlierPlayback()
	{
		PersonalSyncProfile profile = new()
		{
			Mode = PersonalSyncMode.Advanced,
			OffsetSeconds = 1.0,
			Anchors = new() { new PersonalSyncAnchor { PlaybackSeconds = 30, LyricsSeconds = 20 } }
		};

		Assert.Equal(9, PersonalSyncMapper.MapPlaybackToLyrics(10, profile), 6);
		Assert.Equal(20, PersonalSyncMapper.MapPlaybackToLyrics(30, profile), 6);
	}

	[Fact]
	public void PersonalSync_HoldFreezesThenAccumulatesThePause()
	{
		PersonalSyncProfile profile = new()
		{
			Mode = PersonalSyncMode.Advanced,
			Segments = new()
			{
				new PersonalSyncSegment
				{
					Type = PersonalSyncSegmentType.Hold,
					PlaybackStartSeconds = 10,
					PlaybackEndSeconds = 15,
					LyricsTimeSeconds = 10
				}
			}
		};

		Assert.Equal(10, PersonalSyncMapper.MapPlaybackToLyrics(TimeSpan.FromSeconds(12), profile).TotalSeconds, 6);
		Assert.Equal(12, PersonalSyncMapper.MapPlaybackToLyrics(TimeSpan.FromSeconds(17), profile).TotalSeconds, 6);
	}

	[Fact]
	public async Task PersonalSyncStore_PrefersSourceProfileThenTrackFallback()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-personal-sync-" + Guid.NewGuid().ToString("N"));
		try
		{
			PersonalSyncStore store = new(directory);
			PersonalSyncContext spotify = PersonalSyncTestContext("spotify", 100);
			PersonalSyncContext apple = PersonalSyncTestContext("apple", 100);
			PersonalSyncProfile global = PersonalSyncTestProfile(spotify, PersonalSyncScope.Track, 0.7);
			PersonalSyncProfile exact = PersonalSyncTestProfile(spotify, PersonalSyncScope.Source, 1.4);
			await store.UpsertAsync(global);
			await store.UpsertAsync(exact);

			PersonalSyncResolution spotifyResolution = await store.ResolveAsync(spotify);
			PersonalSyncResolution appleResolution = await store.ResolveAsync(apple);

			Assert.Equal(1.4, spotifyResolution.Profile!.OffsetSeconds, 6);
			Assert.Equal(0.7, appleResolution.Profile!.OffsetSeconds, 6);
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public async Task PersonalSyncStore_DoesNotApplyProfileForDifferentLyrics()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-personal-sync-" + Guid.NewGuid().ToString("N"));
		try
		{
			PersonalSyncStore store = new(directory);
			PersonalSyncContext firstLyrics = PersonalSyncTestContext("spotify", 100);
			PersonalSyncContext replacementLyrics = PersonalSyncTestContext("spotify", 200);
			await store.UpsertAsync(PersonalSyncTestProfile(firstLyrics, PersonalSyncScope.Source, 1.0));

			PersonalSyncResolution resolution = await store.ResolveAsync(replacementLyrics);

			Assert.Null(resolution.Profile);
			Assert.True(resolution.HasProfileForDifferentLyrics);
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public async Task PersonalSyncStore_RoundTripsWithoutTouchingLyricsTimestamps()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-personal-sync-" + Guid.NewGuid().ToString("N"));
		List<LyricLine> original = new()
		{
			new LyricLine(TimeSpan.FromSeconds(3), "one"),
			new LyricLine(TimeSpan.FromSeconds(8), "two")
		};
		try
		{
			PersonalSyncContext context = PersonalSyncTestContext("youtube", 300);
			PersonalSyncProfile profile = PersonalSyncTestProfile(context, PersonalSyncScope.Source, 2.5);
			profile.Mode = PersonalSyncMode.Advanced;
			profile.Anchors.Add(new PersonalSyncAnchor { PlaybackSeconds = 10, LyricsSeconds = 8 });
			await new PersonalSyncStore(directory).UpsertAsync(profile);

			PersonalSyncResolution loaded = await new PersonalSyncStore(directory).ResolveAsync(context);

			Assert.NotNull(loaded.Profile);
			Assert.Single(loaded.Profile!.Anchors);
			Assert.Equal(3, original[0].Time.TotalSeconds);
			Assert.Equal(8, original[1].Time.TotalSeconds);
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public async Task PersonalSyncStore_ResetDeletesOnlyTheProfile()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-personal-sync-" + Guid.NewGuid().ToString("N"));
		string lyricsDirectory = Path.Combine(directory, "lyrics-cache");
		string sentinel = Path.Combine(lyricsDirectory, "untouched.lrc");
		Directory.CreateDirectory(lyricsDirectory);
		await File.WriteAllTextAsync(sentinel, "[00:01.00]untouched");
		try
		{
			PersonalSyncStore store = new(directory);
			PersonalSyncContext context = PersonalSyncTestContext("vlc", 400);
			PersonalSyncProfile profile = await store.UpsertAsync(PersonalSyncTestProfile(context, PersonalSyncScope.Source, 1.2));
			profile.Mode = PersonalSyncMode.None;

			await store.UpsertAsync(profile);

			Assert.Empty(await store.ListAsync());
			Assert.True(File.Exists(sentinel));
			Assert.Equal("[00:01.00]untouched", await File.ReadAllTextAsync(sentinel));
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public void PersonalSyncIdentity_SeparatesRawSourceMetadataForTheSameTrack()
	{
		LyricsLookupResult lookup = new()
		{
			LrclibRecord = new LrclibRecord { Id = 500 }
		};
		TrackInfo firstTrack = new("Song", "Artist", "Album", TimeSpan.FromSeconds(180),
			OriginalMediaTitle: "Artist - Song (Official Video)", OriginalMediaArtist: "Channel A");
		TrackInfo secondTrack = new("Song", "Artist", "Album", TimeSpan.FromSeconds(180),
			OriginalMediaTitle: "Song — live session", OriginalMediaArtist: "Channel B");
		PlaybackSnapshot first = new(firstTrack, TimeSpan.Zero, false, DateTimeOffset.UtcNow,
			SourceAppUserModelId: "MSEdge", SourceDisplayName: "Microsoft Edge");
		PlaybackSnapshot second = new(secondTrack, TimeSpan.Zero, false, DateTimeOffset.UtcNow,
			SourceAppUserModelId: "MSEdge", SourceDisplayName: "Microsoft Edge");

		PersonalSyncContext firstContext = PersonalSyncIdentity.Create(first, lookup);
		PersonalSyncContext secondContext = PersonalSyncIdentity.Create(second, lookup);

		Assert.Equal(firstContext.Track.StableTrackKey, secondContext.Track.StableTrackKey);
		Assert.NotEqual(firstContext.Source.StableSourceKey, secondContext.Source.StableSourceKey);
	}

	[Fact]
	public void PersonalSyncIdentity_ChangesWhenNonLrclibLyricsContentChanges()
	{
		TrackInfo track = new("Song", "Artist", "Album", TimeSpan.FromSeconds(180));
		PlaybackSnapshot snapshot = new(track, TimeSpan.Zero, false, DateTimeOffset.UtcNow,
			SourceAppUserModelId: "vlc", SourceDisplayName: "VLC");
		LyricsLookupResult first = new()
		{
			Lyrics = new LyricsResult(new[] { new LyricLine(TimeSpan.FromSeconds(1), "first") }, null, "embedded")
		};
		LyricsLookupResult replacement = new()
		{
			Lyrics = new LyricsResult(new[] { new LyricLine(TimeSpan.FromSeconds(1), "replacement") }, null, "embedded")
		};

		Assert.NotEqual(
			PersonalSyncIdentity.Create(snapshot, first).Lyrics.Key,
			PersonalSyncIdentity.Create(snapshot, replacement).Lyrics.Key);
	}

	[Fact]
	public void GlowSettings_NormalizeAndEveryCuratedPaletteProvidesGlowColor()
	{
		AppSettings settings = new() { GlowStrength = 999, GlowOpacity = -1 };
		settings.Normalize();

		Assert.Equal(40.0, settings.GlowStrength);
		Assert.Equal(0.0, settings.GlowOpacity);
		Assert.All(ColorPalettes.Themes, palette => Assert.StartsWith("#", palette.Glow));
	}

	[Fact]
	public async Task PersonalSyncStore_DoesNotPersistAnEmptyProfile()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-sync-empty-" + Guid.NewGuid().ToString("N"));
		try
		{
			PersonalSyncContext context = PersonalSyncTestContext("spotify", 10);
			PersonalSyncProfile profile = PersonalSyncTestProfile(context, PersonalSyncScope.Source, 0);
			profile.Mode = PersonalSyncMode.None;
			PersonalSyncStore store = new(directory);

			await store.UpsertAsync(profile);

			Assert.False(File.Exists(store.FilePath));
			Assert.Empty(await store.ListAsync());
		}
		finally
		{
			if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public async Task CacheStore_MigratesLegacyMetadataPathWithoutDeletingIt()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			TrackInfo track = new("Magnetic", "ILLIT", "SUPER REAL ME", TimeSpan.FromSeconds(160));
			LyricsCacheEntry legacy = new()
			{
				TrackKey = track.CacheKey,
				CacheKind = "Positive",
				Source = "LOCAL LRC",
				SyncedLyrics = "[00:00.00]test",
				MatcherVersion = 5
			};
			string legacyPath = Path.Combine(directory, Hash(track.CacheKey) + ".json");
			await File.WriteAllTextAsync(legacyPath, JsonSerializer.Serialize(legacy));
			LyricsCacheStore store = new(directory);

			LyricsCacheEntry? read = await store.ReadAsync(track, CancellationToken.None);

			Assert.NotNull(read);
			Assert.True(File.Exists(store.GetPath(track)));
			Assert.True(File.Exists(legacyPath));
			Assert.Equal(track.StableIdentityKey, read!.TrackKey);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	private static MediaSessionInfo Session(string sourceId, string displayName, MediaPlaybackState state, bool current)
	{
		return new MediaSessionInfo
		{
			SessionId = sourceId + "|1",
			SourceAppUserModelId = sourceId,
			DisplaySourceName = displayName,
			Metadata = new MediaTrackMetadata("Track", "Artist", "Album", TimeSpan.FromMinutes(3)),
			Position = TimeSpan.FromSeconds(10),
			PlaybackState = state,
			Capabilities = new MediaPlaybackCapabilities(true, true, true, true, true, true),
			IsCurrentSession = current,
			LastActivityUtc = DateTimeOffset.UtcNow,
			CapturedAtUtc = DateTimeOffset.UtcNow
		};
	}

	private static PersonalSyncContext PersonalSyncTestContext(string source, int lrclibId)
	{
		return new PersonalSyncContext(
			new PersonalSyncTrackIdentity
			{
				StableTrackKey = "track:one",
				Title = "Track",
				Artist = "Artist",
				DurationSeconds = 180
			},
			new PersonalSyncSourceIdentity
			{
				StableSourceKey = "source:" + source,
				Source = source,
				SourceAppUserModelId = source
			},
			new PersonalSyncLyricsIdentity
			{
				Key = "lrclib:" + lrclibId,
				Kind = "LRCLIB",
				LrclibId = lrclibId,
				DisplayName = "LRCLIB #" + lrclibId
			});
	}

	private static PersonalSyncProfile PersonalSyncTestProfile(PersonalSyncContext context, PersonalSyncScope scope, double offset)
	{
		return new PersonalSyncProfile
		{
			Track = context.Track,
			Source = context.Source,
			Lyrics = context.Lyrics,
			Scope = scope,
			Mode = PersonalSyncMode.Offset,
			OffsetSeconds = offset
		};
	}

	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

	private sealed class FakeMediaSessionProvider : IMediaSessionProvider
	{
		public IReadOnlyList<MediaSessionInfo> Sessions { get; set; } = Array.Empty<MediaSessionInfo>();

		public event EventHandler? SessionsChanged;

		public Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sessions);

		public Task<bool> TryTogglePlayPauseAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<bool> TrySkipNextAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<bool> TrySkipPreviousAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public Task<bool> TrySeekAsync(string sessionId, TimeSpan position, CancellationToken cancellationToken = default) => Task.FromResult(true);

		public void Dispose()
		{
		}

		public void RaiseChanged() => SessionsChanged?.Invoke(this, EventArgs.Empty);
	}
}
