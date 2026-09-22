using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;
using static FlowLyrics.Tests.PersonalSyncRuntimeTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class PersonalSyncTransitionTests
{
	private static PersonalSyncContext Context(string title, int id) => PersonalSyncIdentity.Create(
		new(new TrackInfo(title, "Artist", "Album", TimeSpan.FromSeconds(120)), TimeSpan.Zero, false, DateTimeOffset.UtcNow),
		new() { LrclibRecord = new() { Id = id } });
	private static readonly LyricLine[] Lines = [new(TimeSpan.FromSeconds(20), "Line one"), new(TimeSpan.FromSeconds(40), "Line two")];
	private static PersonalSyncProfile Profile(PersonalSyncContext context, double offset) => new()
	{ Track = context.Track, Source = context.Source, Lyrics = context.Lyrics, Mode = PersonalSyncMode.Offset, OffsetSeconds = offset };

	[Fact]
	public async Task DelayedTrack_KeepsWindowAndConfirmedEdits_DiscardsHoldAndDrag()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-editor-transition-" + Guid.NewGuid().ToString("N"));
		try
		{
			PersonalSyncStore store = new(directory);
			var a = Context("Track A", 101); var b = Context("Track B", 102);
			await store.UpsertAsync(Profile(b, -3));
			Sta(() =>
			{
				var profile = Profile(a, 5); profile.Mode = PersonalSyncMode.Advanced;
				profile.Anchors.Add(new() { PlaybackSeconds = 70, LyricsSeconds = 60 });
				PersonalSyncWindow editor = new(store, a, profile, Lines, () => 0, () => TimeSpan.FromSeconds(36), "ja-JP");
				editor.ShowActivated = false; editor.Show(); Pump();
				try
				{
					editor.Left = 80; editor.Top = 90; editor.Width = 920; editor.Height = 740;
					Read<CheckBox>(editor, "_followNowBox").IsChecked = false;
					Invoke(editor, "Nudge", .5);
					Invoke(editor, "Hold_Click", editor, new RoutedEventArgs());
					Assert.NotNull(Read<double?>(editor, "_pendingHoldStart"));
					var confirmed = Read<PersonalSyncProfile>(editor, "_profile").Clone();
					Set("_railEditBefore", confirmed); Set("_railEditChanged", true);
					Read<PersonalSyncProfile>(editor, "_profile").Anchors[0].PlaybackSeconds = 99;
					editor.SuspendTrack("SEARCHING..."); Pump();
					Assert.True(editor.IsVisible); Assert.False(editor.IsTrackReady);
					Assert.Equal("SEARCHING...", Read<TextBlock>(editor, "_waitingText").Text);
					Assert.False(Read<Grid>(editor, "_editingSurface").IsVisible);
					Assert.False(Read<Grid>(editor, "_offsetNudges").IsEnabled);
					Invoke(editor, "Nudge", 50.0); // Pending state cannot modify the departing track.
					Assert.Equal(5.5, Read<PersonalSyncProfile>(editor, "_profile").OffsetSeconds);
					Task populate = editor.UpdateTrackAsync(b, [new(TimeSpan.FromSeconds(9), "New lyric")]);
					WaitUntil(() => populate.IsCompleted); populate.GetAwaiter().GetResult();
					Assert.True(editor.IsVisible); Assert.True(editor.IsTrackReady);
					Assert.Equal((80d, 90d, 920d, 740d), (editor.Left, editor.Top, editor.Width, editor.Height));
					Assert.False(Read<CheckBox>(editor, "_followNowBox").IsChecked);
					Assert.Contains("Track B", Read<TextBlock>(editor, "_trackText").Text);
					Assert.Equal(-3, Read<PersonalSyncProfile>(editor, "_profile").OffsetSeconds);
					Assert.Empty(Read<PersonalSyncProfile>(editor, "_profile").Anchors);
					Assert.Null(Read<double?>(editor, "_pendingHoldStart"));
					Assert.Single(Read<ListBox>(editor, "_lyricsList").Items);
					Assert.Equal(-1, Read<ListBox>(editor, "_lyricsList").SelectedIndex);
					Assert.False(Read<Button>(editor, "_undoButton").IsEnabled);
					editor.SuspendTrack("NO SYNCED LYRICS"); Pump(); Assert.True(editor.IsVisible);
					editor.SuspendTrack("WAITING FOR PLAYER"); Pump(); Assert.True(editor.IsVisible);
				}
				finally { editor.Close(); WaitUntil(() => !editor.IsVisible); }
				void Set(string field, object value) => typeof(PersonalSyncWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor, value);
			});
			var saved = (await new PersonalSyncStore(directory).ResolveAsync(a)).Profile!;
			Assert.Equal(5.5, saved.OffsetSeconds); Assert.Equal(70.5, Assert.Single(saved.Anchors).PlaybackSeconds);
			Assert.Empty(saved.Segments);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public void RapidChanges_OnlyPopulateLatestTrack_InspectorAlignRemainsGlobal()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-editor-race-" + Guid.NewGuid().ToString("N"));
		try
		{
			Sta(() =>
			{
				PersonalSyncWindow editor = new(new(directory), Context("A", 1), null, Lines, () => 0, () => TimeSpan.FromSeconds(36), "en-US");
				editor.ShowActivated = false; editor.Show(); Pump();
				try
				{
					editor.SuspendTrack("LYRICS NOT READY");
					Task b = editor.UpdateTrackAsync(Context("B", 2), Lines);
					editor.SuspendTrack("LYRICS NOT READY");
					Task c = editor.UpdateTrackAsync(Context("C", 3), Lines);
					WaitUntil(() => b.IsCompleted && c.IsCompleted);
					Assert.StartsWith("C", Read<TextBlock>(editor, "_trackText").Text);
					Read<ListBox>(editor, "_lyricsList").SelectedIndex = 0; Pump();
					Button align = Read<Button>(editor, "_matchButton"); Assert.NotNull(align.Parent);
					Assert.True(Read<Grid>(editor, "_lyricNudges").IsVisible);
					align.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					var profile = Read<PersonalSyncProfile>(editor, "_profile");
					Assert.Equal(16, profile.OffsetSeconds); Assert.Empty(profile.Anchors);
					Assert.Equal(4, PersonalSyncMapper.MapPlaybackToLyrics(20, profile));
					Assert.Equal(20, PersonalSyncMapper.MapPlaybackToLyrics(36, profile));
				}
				finally { editor.Close(); WaitUntil(() => !editor.IsVisible); }
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task FailedResetWrite_ReloadsDisk_AndCanBeRetried()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-sync-retry-" + Guid.NewGuid().ToString("N"));
		try
		{
			PersonalSyncStore store = new(directory); var context = Context("A", 1);
			var profile = await store.UpsertAsync(Profile(context, 5)); profile.Mode = PersonalSyncMode.None;
			using (FileStream locked = new(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				Exception? error = await Record.ExceptionAsync(() => store.UpsertAsync(profile));
				Assert.True(error is IOException or UnauthorizedAccessException);
			}
			Assert.NotNull((await store.ResolveAsync(context)).Profile);
			await store.UpsertAsync(profile);
			Assert.Null((await new PersonalSyncStore(directory).ResolveAsync(context)).Profile);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
