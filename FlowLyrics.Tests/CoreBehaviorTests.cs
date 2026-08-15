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
	[InlineData("[MV] Creepy Nuts - バレる！", "Creepy Nuts", "バレる！", "Creepy Nuts")]
	[InlineData("音乃瀬奏 - You＆合図 (Official MV)", "KANADE Ch. 音乃瀬奏 - ReGLOSS", "You＆合図", "音乃瀬奏")]
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
