using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlowLyrics.Controls;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class PersonalSyncEditorV2Tests
{
	[Theory]
	[InlineData("ja-JP", 760)]
	[InlineData("ja-JP", 900)]
	[InlineData("en-US", 1120)]
	public void FixedRows_HoverAndSelectionKeepGeometry_RepeatedAlignmentShiftsGlobally(string language, int width)
	{
		string directory = Temp();
		try
		{
			Sta(() =>
			{
				double now = 61.1;
				LyricLine[] lines = [new(TimeSpan.FromSeconds(58.2), "君だってさっきのカフェの"), new(TimeSpan.FromSeconds(70), "A second lyric to align"), new(TimeSpan.FromSeconds(80), "Third")];
				PersonalSyncWindow window = new(new(directory), Context(), null, lines, () => 0, () => TimeSpan.FromSeconds(now), language) { Width = width, Height = 680, ShowActivated = false };
				try
				{
					window.Show(); Pump();
					var list = Read<ListBox>(window, "_lyricsList");
					var first = (ListBoxItem)list.Items[0]; var second = (ListBoxItem)list.Items[1];
					double height = first.ActualHeight, nextY = second.TranslatePoint(new Point(), list).Y;
					Assert.InRange(height, 32, 46);
					var actions = Logical<StackPanel>(second).Single(panel => panel.Children.OfType<Button>().Any());
					Assert.Single(actions.Children.OfType<Button>());
					Assert.Null(second.ContextMenu);
					second.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent }); Pump();
					Assert.Equal(Visibility.Visible, actions.Visibility);
					Assert.Equal(height, first.ActualHeight); Assert.Equal(nextY, second.TranslatePoint(new Point(), list).Y);
					second.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
					Assert.Equal(Visibility.Hidden, actions.Visibility);
					Assert.Empty(Logical<Expander>(window));
					Assert.Equal(width >= 1080, Read<Border>(window, "_inspector").IsVisible);
					int previews = 0; window.PreviewChanged += (_, _) => previews++;
					Button Align(ListBoxItem item) => Logical<Button>(item).Single(b => b.Name == "AlignLyricButton");
					Align(first).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
					var profile = Read<PersonalSyncProfile>(window, "_profile");
					Assert.Equal(2.9, profile.OffsetSeconds, 6); Assert.Empty(profile.Anchors);
					Assert.Contains("+2.9", Read<TextBlock>(window, "_offsetText").Text);
					Assert.Equal(58.2, PersonalSyncMapper.MapPlaybackToLyrics(61.1, profile), 6);
					double before = PersonalSyncMapper.MapPlaybackToLyrics(60, profile);
					now = 75;
					Align(second).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
					Assert.Equal(5, profile.OffsetSeconds, 6);
					Assert.Equal(70, PersonalSyncMapper.MapPlaybackToLyrics(75, profile), 6);
					Assert.Equal(before - 2.1, PersonalSyncMapper.MapPlaybackToLyrics(60, profile), 6);
					Assert.Empty(profile.Anchors); Assert.True(previews >= 2);
					Assert.Equal(height, first.ActualHeight); Assert.Equal(nextY, second.TranslatePoint(new Point(), list).Y);
					UiUxRuntimeTests.Capture(window, "sync-v2-" + language + "-" + width);
					Invoke(window, "Undo"); Assert.Empty(Read<PersonalSyncProfile>(window, "_profile").Anchors);
					Invoke(window, "Redo"); Assert.Equal(5, Read<PersonalSyncProfile>(window, "_profile").OffsetSeconds);
				}
				finally { window.Close(); PersonalSyncRuntimeTests.WaitUntil(() => !window.IsVisible); }
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task RailRange_DragUpdatesLinkedResume_OneUndo_CancelDeleteAndReopen()
	{
		string directory = Temp(); var context = Context(); PersonalSyncStore store = new(directory);
		LyricLine[] lines = [new(TimeSpan.FromSeconds(10), "Hold this lyric"), new(TimeSpan.FromSeconds(30), "Resume this lyric")];
		try
		{
			Sta(() =>
			{
				PersonalSyncWindow window = new(store, context, null, lines, () => 0, () => TimeSpan.FromSeconds(20), "en-US") { ShowActivated = false };
				window.Show(); Pump();
				try
				{
					Read<Button>(window, "_holdButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					var profile = Read<PersonalSyncProfile>(window, "_profile");
					var hold = Assert.Single(profile.Segments); var anchor = Assert.Single(profile.Anchors);
					Assert.Equal(hold.ResumeAnchorId, anchor.Id); Assert.Equal(10, PersonalSyncMapper.MapPlaybackToLyrics(22, profile));
					var rail = Read<PersonalSyncRail>(window, "_rail");
					int previews = 0; window.PreviewChanged += (_, _) => previews++;
					rail.BeginEdit(new(hold.Id, SyncRailField.HoldEnd, hold.PlaybackEndSeconds));
					rail.MoveInteraction(35); rail.MoveInteraction(40);
					Assert.Equal(40, hold.PlaybackEndSeconds); Assert.Equal(40, anchor.PlaybackSeconds);
					Assert.True(previews >= 2); rail.EndInteraction();
					Assert.True(Read<CheckBox>(window, "_followNowBox").IsChecked); // Drag suspends, never disables follow.
					Invoke(window, "Undo"); Assert.Equal(25, Assert.Single(Read<PersonalSyncProfile>(window, "_profile").Segments).PlaybackEndSeconds);
					Invoke(window, "Redo"); profile = Read<PersonalSyncProfile>(window, "_profile"); hold = profile.Segments.Single();
					rail.BeginEdit(new(hold.Id, SyncRailField.HoldStart, hold.PlaybackStartSeconds)); rail.MoveInteraction(100);
					Assert.True(hold.PlaybackStartSeconds < hold.PlaybackEndSeconds);
					rail.EndInteraction(cancel: true); profile = Read<PersonalSyncProfile>(window, "_profile"); hold = profile.Segments.Single();
					Assert.Equal(20, hold.PlaybackStartSeconds);
					rail.BeginEdit(new(hold.Id, SyncRailField.HoldEnd, hold.PlaybackEndSeconds)); rail.MoveInteraction(0); rail.EndInteraction();
					Assert.True(hold.PlaybackEndSeconds > hold.PlaybackStartSeconds);
					Assert.Equal(hold.PlaybackEndSeconds, profile.Anchors.Single().PlaybackSeconds);
					Invoke(window, "Undo");
					profile = Read<PersonalSyncProfile>(window, "_profile"); hold = profile.Segments.Single(); anchor = profile.Anchors.Single();
					rail.BeginEdit(new(anchor.Id, SyncRailField.Anchor, anchor.PlaybackSeconds)); rail.MoveInteraction(45); rail.EndInteraction();
					Assert.Equal(45, hold.PlaybackEndSeconds); Assert.Equal(45, anchor.PlaybackSeconds);
					Invoke(window, "Undo");
					Invoke(window, "SelectPoint", hold.Id);
					Invoke(window, "DeleteSelectedPoint_Click", window, new RoutedEventArgs());
					Assert.Empty(Read<PersonalSyncProfile>(window, "_profile").Segments); Assert.Empty(Read<PersonalSyncProfile>(window, "_profile").Anchors);
					Invoke(window, "Undo"); Pump(); UiUxRuntimeTests.Capture(window, "sync-v2-range");
					profile = Read<PersonalSyncProfile>(window, "_profile"); hold = profile.Segments.Single();
					rail.BeginEdit(new(hold.Id, SyncRailField.HoldEnd, hold.PlaybackEndSeconds)); rail.MoveInteraction(50);
					Assert.Equal(50, hold.PlaybackEndSeconds); // Closing must discard this uncommitted preview.
				}
				finally { window.Close(); PersonalSyncRuntimeTests.WaitUntil(() => !window.IsVisible); }
			});
			var saved = (await new PersonalSyncStore(directory).ResolveAsync(context)).Profile!;
			Assert.Equal(40, saved.Segments.Single().PlaybackEndSeconds);
			Assert.Equal(saved.Segments.Single().ResumeAnchorId, saved.Anchors.Single().Id);
			Assert.Equal(40, saved.Anchors.Single().PlaybackSeconds);
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public void RailSeek_LinearMappingPreviewThenRelease_DisabledAndUnknownDuration()
	{
		Sta(() =>
		{
			PersonalSyncRail rail = new(); rail.SetTimeline(200, new(), []);
			Assert.Equal(0, PersonalSyncRail.TimeAt(18, 236, 200));
			Assert.Equal(100, PersonalSyncRail.TimeAt(118, 236, 200));
			Assert.Equal(200, PersonalSyncRail.TimeAt(218, 236, 200));
			int calls = 0; double target = -1; rail.SeekRequested += time => { calls++; target = time; };
			rail.BeginSeek(0); rail.MoveInteraction(100); Assert.Equal(0, calls); Assert.Equal(100, rail.PreviewSeconds);
			rail.MoveInteraction(220); rail.EndInteraction(); Assert.Equal(1, calls); Assert.Equal(200, target); Assert.Null(rail.PreviewSeconds);
			rail.BeginSeek(50); rail.EndInteraction(true); Assert.Equal(1, calls);
			rail.CanSeek = false; rail.BeginSeek(70); rail.EndInteraction(); Assert.Equal(1, calls);
			rail.CanSeek = true; rail.SetTimeline(0, new(), []); rail.BeginSeek(10); rail.EndInteraction(); Assert.Equal(1, calls);
		});
	}

	[Fact]
	public void LegacyHold_WithoutResumeIdStillMovesTogetherWithItsResumePoint()
	{
		string directory = Temp();
		try
		{
			Sta(() =>
			{
				var context = Context();
				var profile = new PersonalSyncProfile { Track = context.Track, Source = context.Source, Lyrics = context.Lyrics, Mode = PersonalSyncMode.Advanced };
				profile.Segments.Add(new() { Type = PersonalSyncSegmentType.Hold, PlaybackStartSeconds = 10, PlaybackEndSeconds = 30, LyricsTimeSeconds = 5 });
				profile.Anchors.Add(new() { PlaybackSeconds = 30, LyricsSeconds = 20 });
				PersonalSyncWindow window = new(new(directory), context, profile, [new(TimeSpan.FromSeconds(5), "Held"), new(TimeSpan.FromSeconds(20), "Resume")], () => 0, () => TimeSpan.FromSeconds(25), "en-US") { ShowActivated = false };
				try
				{
					window.Show(); Pump();
					var rail = Read<PersonalSyncRail>(window, "_rail");
					rail.BeginEdit(new(profile.Anchors[0].Id, SyncRailField.Anchor, 30)); rail.MoveInteraction(35); rail.EndInteraction();
					var changed = Read<PersonalSyncProfile>(window, "_profile");
					Assert.Equal(35, changed.Segments[0].PlaybackEndSeconds);
					Assert.Equal(changed.Anchors[0].Id, changed.Segments[0].ResumeAnchorId);
					Assert.Equal(5, PersonalSyncMapper.MapPlaybackToLyrics(25, changed));
					Assert.Equal(20, PersonalSyncMapper.MapPlaybackToLyrics(35, changed));
				}
				finally { window.Close(); PersonalSyncRuntimeTests.WaitUntil(() => !window.IsVisible); }
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Theory]
	[InlineData(760)]
	[InlineData(1120)]
	public void ContextActions_KeepRowsSimple_ResyncAndResumeOnlyOnSelectedTarget(int width)
	{
		string directory = Temp();
		try
		{
			Sta(() =>
			{
				double now = 36;
				LyricLine[] lines = [new(TimeSpan.FromSeconds(20), "A"), new(TimeSpan.FromSeconds(24), "B"), new(TimeSpan.FromSeconds(28), "C")];
				var window = new PersonalSyncWindow(new(directory), Context(), null, lines, () => 0, () => TimeSpan.FromSeconds(now), "ja-JP") { Width = width, ShowActivated = false };
				try
				{
					window.Show(); Pump();
					var list = Read<ListBox>(window, "_lyricsList");
					Assert.All(list.Items.Cast<ListBoxItem>(), row => { Assert.Null(row.ContextMenu); Assert.Single(Logical<Button>(row)); });
					Assert.DoesNotContain(Logical<Button>(window), b => Equals(b.Content, "⋯"));
					Assert.False(Read<Button>(window, "_resumeButton").IsVisible);
					Assert.False(Read<Button>(window, "_deletePointButton").IsVisible);
					if (width < 1080) Logical<Button>(window).Single(b => b.Name == "TimingDetailsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Assert.True(Read<Border>(window, "_inspector").IsVisible);
					Assert.Equal(width < 1080 ? 2 : 1, Grid.GetRow(Read<Border>(window, "_inspector")));
					Read<Button>(window, "_resyncButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					var profile = Read<PersonalSyncProfile>(window, "_profile");
					Assert.Single(profile.Anchors); Assert.Equal(0, profile.OffsetSeconds);
					Assert.False(Read<Button>(window, "_resyncButton").IsVisible);
					Assert.True(Read<Button>(window, "_deletePointButton").IsVisible);
					now = 40;
					Read<Button>(window, "_holdButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Assert.True(Read<Button>(window, "_resumeButton").IsVisible);
					list.SelectedIndex = 2;
					Assert.True(Read<Button>(window, "_resumeButton").IsVisible);
					Read<Button>(window, "_resumeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					var hold = profile.Segments.Single();
					Assert.Equal(28, profile.Anchors.Single(a => a.Id == hold.ResumeAnchorId).LyricsSeconds);
					Pump(); UiUxRuntimeTests.Capture(window, "sync-context-" + width);
					GlowOverlayTests.CaptureNative(window, "sync-context-" + width);
				}
				finally { window.Close(); PersonalSyncRuntimeTests.WaitUntil(() => !window.IsVisible); }
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	private static string Temp() => Path.Combine(Path.GetTempPath(), "FlowLyrics-sync-v2-" + Guid.NewGuid().ToString("N"));
	private static PersonalSyncContext Context() => PersonalSyncIdentity.Create(new(new("Timing study", "Artist A, Artist B", "Album", TimeSpan.FromSeconds(150)), TimeSpan.Zero, true, DateTimeOffset.UtcNow), new() { LrclibRecord = new() { Id = 123 } });
}
