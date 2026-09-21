using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class PersonalSyncRuntimeTests
{
	[Fact]
	public async Task LoadedHoldWorkflow_LinksResumeEditingAndPersistsBeforeClose()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-hold-runtime-" + Guid.NewGuid().ToString("N"));
		PersonalSyncStore store = new(directory);
		TrackInfo track = new("BAD", "ATEEZ", "", TimeSpan.FromSeconds(240), SourceAppUserModelId: "chrome");
		PersonalSyncContext context = PersonalSyncIdentity.Create(new(track, TimeSpan.Zero, true, DateTimeOffset.UtcNow, SourceAppUserModelId: "chrome"), new() { LrclibRecord = new() { Id = 123 } });
		LyricLine[] lines = [new(TimeSpan.FromSeconds(10), "VICTORIA"), new(TimeSpan.FromSeconds(20), "She's so bad")];
		try
		{
			Sta(() =>
			{
				TimeSpan position = TimeSpan.FromSeconds(12);
				PersonalSyncWindow window = new(store, context, null, lines, () => 0, () => position, "en-US");
				window.ShowActivated = false; window.Show(); Pump();
				try
				{
					Button hold = Read<Button>(window, "_holdButton");
					hold.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Assert.Equal(12, Read<double?>(window, "_pendingHoldStart"));
					Read<ListBox>(window, "_lyricsList").SelectedIndex = 1;
					position = TimeSpan.FromSeconds(32);
					Invoke(window, "RefreshLiveUi");
					Assert.False(hold.IsVisible);
					Read<Button>(window, "_matchButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					PersonalSyncProfile profile = Read<PersonalSyncProfile>(window, "_profile");
					PersonalSyncSegment segment = Assert.Single(profile.Segments);
					Assert.Equal(10, segment.LyricsTimeSeconds); Assert.Equal(32, segment.PlaybackEndSeconds);
					Assert.Equal(segment.ResumeAnchorId, Assert.Single(profile.Anchors).Id);
					Assert.Equal(20, profile.Anchors[0].LyricsSeconds);
					Assert.Equal(10, PersonalSyncMapper.MapPlaybackToLyrics(25, profile));
					Assert.Equal(20, PersonalSyncMapper.MapPlaybackToLyrics(32, profile));
					Type field = typeof(PersonalSyncWindow).GetNestedType("TimeField", BindingFlags.NonPublic)!;
					Invoke(window, "AdjustSelectedPoint", Enum.Parse(field, "B"), 0.5);
					Invoke(window, "AdjustSelectedPoint", Enum.Parse(field, "B"), -0.1);
					profile = Read<PersonalSyncProfile>(window, "_profile");
					Assert.Equal(32.4, profile.Segments[0].PlaybackEndSeconds); Assert.Equal(32.4, profile.Anchors[0].PlaybackSeconds);
					Read<Button>(window, "_undoButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Assert.Equal(32.5, Read<PersonalSyncProfile>(window, "_profile").Anchors[0].PlaybackSeconds);
					Read<Button>(window, "_redoButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Assert.Equal(32.4, Read<PersonalSyncProfile>(window, "_profile").Anchors[0].PlaybackSeconds);
					window.Close();
					WaitUntil(() => !window.IsVisible);
					Assert.True(File.Exists(store.FilePath));
					Assert.False(Read<bool>(window, "_dirty"));
				}
				finally { window.Close(); }
			});
			PersonalSyncProfile saved = (await new PersonalSyncStore(directory).ResolveAsync(context)).Profile!;
			Assert.Equal(32.4, Assert.Single(saved.Anchors).PlaybackSeconds);
			PersonalSyncContext otherLyrics = context with { Lyrics = new() { Key = "lrclib:999", LrclibId = 999 } };
			Assert.Null((await store.ResolveAsync(otherLyrics)).Profile);
			Sta(() =>
			{
				PersonalSyncManagerWindow history = new(store, "en-US", saved.Id);
				try
				{
					history.ShowActivated = false; history.Show(); Pump();
					WaitUntil(() => Read<ListBox>(history, "_list").Items.Count == 1);
					Assert.Contains("BAD", Read<TextBlock>(history, "_details").Text);
					Assert.NotEmpty(Read<Canvas>(history, "_timeline").Children.Cast<UIElement>());
					Read<TextBox>(history, "_searchBox").Text = "No matching track";
					Assert.Empty(Read<ListBox>(history, "_list").Items);
					Read<TextBox>(history, "_searchBox").Text = "ATEEZ";
					Assert.Single(Read<ListBox>(history, "_list").Items);
				}
				finally { history.Close(); }
			});
			Sta(() =>
			{
				PersonalSyncWindow reopened = new(store, context, saved, lines, () => 1, () => TimeSpan.FromSeconds(32.4), "en-US");
				reopened.ShowActivated = false; reopened.Show(); Pump();
				Assert.Equal(32.4, Read<PersonalSyncProfile>(reopened, "_profile").Anchors.Single().PlaybackSeconds);
				Invoke(reopened, "Reset_Click", reopened, new RoutedEventArgs());
				reopened.Close(); WaitUntil(() => !reopened.IsVisible);
			});
			Assert.Empty(await store.ListAsync());
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public void OpenAndCloseWithoutEdits_DoesNotSaveAnEmptyProfile()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-sync-noop-" + Guid.NewGuid().ToString("N"));
		Sta(() =>
		{
			PersonalSyncStore store = new(directory);
			PersonalSyncWindow window = new(store, new(new(), new(), new()), null, Array.Empty<LyricLine>(), () => null, () => TimeSpan.Zero, "en-US");
			window.ShowActivated = false; window.Show(); Pump(); window.Close();
			Assert.False(File.Exists(store.FilePath));
		});
	}

	internal static void WaitUntil(Func<bool> ready)
	{
		Stopwatch timeout = Stopwatch.StartNew();
		while (!ready() && timeout.Elapsed < TimeSpan.FromSeconds(5)) { Pump(); Thread.Sleep(10); }
		Assert.True(ready(), "Dispatcher operation did not finish.");
	}
}
