using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class IdentityPersistenceTests
{
	[Fact]
	public async Task EnrichmentAndReleaseAliases_PreserveLegacySelectionsCacheAndSyncWithoutMergingReleases()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-identity-retention-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			TrackInfo single = new("Song - From \"Movie\" Soundtrack", "Artist A", "Single", TimeSpan.FromSeconds(240), LegacyProviderTrackId: "legacy-id");
			TrackInfo album = single with { Title = "Song", Album = "Album" };
			TrackInfo enriched = single with { EnrichedArtistCredit = "Artist A, Artist B", SearchAlternates = new[] { new SearchMetadataCandidate("Song", "Artist A, Artist B", "Single") } };
			Assert.Equal(single.CacheKey, enriched.CacheKey);
			Assert.Equal(single.StableIdentityKey, enriched.StableSourceMetadataKey);
			Assert.NotEqual(single.StableIdentityKey, album.StableIdentityKey);
			Assert.Equal("Song", enriched.DisplayTitle);
			Assert.Equal("Movie", enriched.ReleaseContext);
			// Historical raw cache keys still migrate through the existing fallback.
			await File.WriteAllTextAsync(Path.Combine(directory, "manual-selections.json"), JsonSerializer.Serialize(new Dictionary<string, ManualLyricsSelection>
			{
				[single.CacheKey] = new() { LrclibId = 123, SelectedManually = true },
				[album.StableIdentityKey] = new() { LrclibId = 456, SelectedManually = true }
			}));
			LyricsOverrideStore selections = new(directory);
			Assert.Equal(123, (await selections.GetAsync(enriched))!.LrclibId);
			Assert.Equal(456, (await selections.GetAsync(album))!.LrclibId);
			Assert.Contains("spotify:track:legacy-id", enriched.LegacyIdentityKeys);
			LyricsCacheStore cache = new(Path.Combine(directory, "cache"));
			await cache.WriteAsync(single, new() { LrclibId = 123, SelectionMode = "Manual", SyncedLyrics = "[00:01.00]line" }, CancellationToken.None);
			await cache.WriteAsync(album, new() { LrclibId = 456, SelectionMode = "Manual", SyncedLyrics = "[00:01.00]line" }, CancellationToken.None);
			Assert.Equal(123, (await cache.ReadAsync(enriched, CancellationToken.None))!.LrclibId);
			Assert.Equal(456, (await cache.ReadAsync(album, CancellationToken.None))!.LrclibId);
			PersonalSyncContext Context(TrackInfo track) => PersonalSyncIdentity.Create(new(track, TimeSpan.Zero, false, DateTimeOffset.UtcNow, SourceAppUserModelId: "Spotify"), new() { LrclibRecord = new() { Id = 123 } });
			PersonalSyncContext before = Context(single), after = Context(enriched);
			Assert.Equal(before.Source.StableSourceKey, after.Source.StableSourceKey);
			PersonalSyncStore store = new(directory);
			var profile = await store.UpsertAsync(new() { Track = before.Track, Source = before.Source, Lyrics = before.Lyrics, OffsetSeconds = 0.5 });
			Assert.Equal(profile.Id, (await new PersonalSyncStore(directory).ResolveAsync(after)).Profile!.Id);
			Assert.Null((await store.ResolveAsync(Context(album))).Profile);
			Assert.Single(await store.ListAsync()); // Looking up another release never deletes the old profile.
		}
		finally { Directory.Delete(directory, true); }
	}
}
