using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

public sealed class PersistenceFailureTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-save-failure-" + Guid.NewGuid().ToString("N"));

	[Theory]
	[InlineData("delete-id")]
	[InlineData("delete-source")]
	[InlineData("delete-track")]
	[InlineData("update")]
	public async Task FailedProfileWrite_ReloadsSavedState_BeforeAnotherEdit(string operation)
	{
		PersonalSyncStore store = new(_directory);
		PersonalSyncProfile saved = await store.UpsertAsync(new()
		{
			Track = new() { StableTrackKey = "track-a" }, Source = new() { StableSourceKey = "source" },
			Lyrics = new() { Key = "lyrics" }, OffsetSeconds = 2,
			Scope = operation == "delete-track" ? PersonalSyncScope.Track : PersonalSyncScope.Source
		});
		int notifications = 0; store.ProfilesChanged += (_, _) => notifications++;
		using (FileStream blocked = new(store.FilePath + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
		{
			await Assert.ThrowsAsync<IOException>(async () =>
			{
				if (operation == "delete-id") await store.DeleteAsync(saved.Id);
				else if (operation == "update") { var edit = saved.Clone(); edit.OffsetSeconds = 20; await store.UpsertAsync(edit); }
				else await store.DeleteForContextAsync(new(saved.Track, saved.Source, saved.Lyrics), operation == "delete-source");
			});
		}
		Assert.Equal(0, notifications);
		Assert.Equal(2, Assert.Single(await store.ListAsync()).OffsetSeconds);
		var other = saved.Clone(); other.Id = Guid.NewGuid(); other.Track.StableTrackKey = "track-b";
		await store.UpsertAsync(other);
		var onDisk = await new PersonalSyncStore(_directory).ListAsync();
		Assert.Equal(2, onDisk.Count);
		Assert.Equal(2, onDisk.Single(profile => profile.Id == saved.Id).OffsetSeconds);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FailedManualSelectionWrite_DoesNotBecomeTheCurrentOrNextSavedChoice(bool remove)
	{
		LyricsOverrideStore store = new(_directory);
		TrackInfo track = new("A", "Artist", "Album", TimeSpan.FromSeconds(200));
		await store.SetAsync(track, 1);
		using (FileStream blocked = new(store.Path + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
		{
			await Assert.ThrowsAsync<IOException>(async () =>
			{
				if (remove) await store.RemoveAsync(track);
				else await store.SetAsync(track, 2);
			});
		}
		Assert.Equal(1, (await store.GetAsync(track))?.LrclibId);
		await store.SetAsync(track with { Title = "B" }, 3);
		Assert.Equal(1, (await new LyricsOverrideStore(_directory).GetAsync(track))?.LrclibId);
	}

	public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
