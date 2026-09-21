using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class LocalLrcPreferenceTests
{
	[Fact]
	public async Task ToggleOff_PreservesOlderCacheOnlyLocalImports()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-local-cache-" + Guid.NewGuid().ToString("N"));
		TrackInfo track = new("Song", "Artist", "Album", TimeSpan.FromSeconds(240));
		try
		{
			using LyricsService service = new(directory, new LrclibRefreshTests.Handler());
			LyricsCacheStore oldCache = RuntimeSettingsTests.Read<LyricsCacheStore>(service, "_cacheStore");
			await oldCache.WriteAsync(track, new LyricsCacheEntry { Source = "LOCAL LRC", SelectionMode = "Local", SyncedLyrics = "[00:01.00]Saved local words", CacheKind = "Positive" }, default);
			await service.SetLocalLrcEnabledAsync(track, false);
			Assert.NotEqual(LyricsLookupStatus.LocalLrc, (await service.GetLyricsAsync(track, false, default)).Status);
			await service.SetLocalLrcEnabledAsync(track, true);
			var restored = await service.GetLyricsAsync(track, false, default);
			Assert.Equal(LyricsLookupStatus.LocalLrc, restored.Status);
			Assert.Equal("Saved local words", Assert.Single(restored.Lyrics!.Lines).Text);
			await service.ImportManualLrcAsync(track, "[00:02.00]Updated local words");
			await service.ApplyManualSelectionAsync(track, 123, default);
			await service.SetLocalLrcEnabledAsync(track, true);
			Assert.Equal("Updated local words", Assert.Single((await service.GetLyricsAsync(track, false, default)).Lyrics!.Lines).Text);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task Toggle_RetainsFileAndManualSelectionAcrossRestartAndDoesNotLeakToOtherTracks()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-local-toggle-" + Guid.NewGuid().ToString("N"));
		TrackInfo track = new("Song", "Artist", "Album", TimeSpan.FromSeconds(240));
		try
		{
			string file;
			using (LyricsService service = new(directory, new LrclibRefreshTests.Handler()))
			{
				await service.ApplyManualSelectionAsync(track, 123, CancellationToken.None);
				string source = Path.Combine(directory, "source.lrc");
				await File.WriteAllTextAsync(source, "[00:01.00]Local fixture");
				file = await service.ImportLocalLrcFileAsync(track, source);
				Assert.True(await service.IsLocalLrcEnabledAsync(track));
				Assert.Equal(LyricsLookupStatus.LocalLrc, (await service.GetLyricsAsync(track, false, default)).Status);
				await service.SetLocalLrcEnabledAsync(track, false);
				Assert.True(File.Exists(file));
				Assert.True((await service.GetLyricsAsync(track, false, default)).SelectedManually);
				Assert.True(await service.IsLocalLrcEnabledAsync(track with { Title = "Other" }));
			}
			using LyricsService reopened = new(directory, new LrclibRefreshTests.Handler());
			Assert.False(await reopened.IsLocalLrcEnabledAsync(track));
			await reopened.SetLocalLrcEnabledAsync(track, true);
			Assert.Equal(LyricsLookupStatus.LocalLrc, (await reopened.GetLyricsAsync(track, false, default)).Status);
			await reopened.ApplyManualSelectionAsync(track, 123, default);
			Assert.False(await reopened.IsLocalLrcEnabledAsync(track));
			Assert.True(File.Exists(file));
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task LegacyManualSelection_RemainsPreferredUntilUserEnablesLocalFile()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-local-legacy-" + Guid.NewGuid().ToString("N"));
		TrackInfo track = new("Song", "Artist", "Album", TimeSpan.FromSeconds(240));
		try
		{
			using LyricsService service = new(directory, new LrclibRefreshTests.Handler());
			await File.WriteAllTextAsync(Path.Combine(service.LyricsDirectory, "Artist - Song.lrc"), "[00:01.00]Local fixture");
			await new LyricsOverrideStore(directory).SetAsync(track, 123);
			Assert.True((await service.GetLyricsAsync(track, false, default)).SelectedManually);
			await service.SetLocalLrcEnabledAsync(track, true);
			Assert.Equal(LyricsLookupStatus.LocalLrc, (await service.GetLyricsAsync(track, false, default)).Status);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
