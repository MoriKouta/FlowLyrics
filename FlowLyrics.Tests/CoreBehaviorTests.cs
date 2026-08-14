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
		await service.GetSessionsAsync();
		await Task.Delay(900);
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
