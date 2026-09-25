using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowLyrics.Controls;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class ContinuationRuntimeTests
{
	[Theory]
	[InlineData(200, TextAlignment.Left)]
	[InlineData(340, TextAlignment.Center)]
	[InlineData(600, TextAlignment.Right)]
	public void GlowStrength_DoesNotChangeGlyphGeometryWrapOrPosition(double width, TextAlignment alignment)
	{
		Sta(() =>
		{
			OutlinedText text = new() { Text = "文字のサイズを保つ Soft light across a long lyric", FontSize = 48,
				TextAlignment = alignment, GlowColor = Colors.OrangeRed, GlowOpacity = 1, MaximumLines = 3, Height = 170 };
			Window window = new() { Width = width + 80, Height = 290, Background = Brushes.Black, Content = new Border { Padding = new Thickness(24), Child = text }, ShowActivated = false };
			try
			{
				window.Show(); Pump();
				string? geometry = null; Size? desired = null; Rect? bounds = null; Point? position = null;
				foreach (double radius in new[] { 0.0, 10, 20, 30, 40 })
				{
					text.GlowRadius = radius; window.UpdateLayout(); Pump();
					DrawingVisual front = (DrawingVisual)VisualTreeHelper.GetChild(text, 0);
					string actual = string.Join("|", front.Drawing.Children.OfType<GeometryDrawing>().Select(g => g.Geometry.ToString(CultureInfo.InvariantCulture)));
					Assert.NotEmpty(actual);
					geometry ??= actual; desired ??= text.DesiredSize; bounds ??= front.ContentBounds; position ??= text.TranslatePoint(new Point(), window);
					Assert.Equal(geometry, actual); Assert.Equal(desired.Value, text.DesiredSize);
					Assert.Equal(bounds.Value, front.ContentBounds); Assert.Equal(position.Value, text.TranslatePoint(new Point(), window));
					Assert.Null(front.Effect);
					UiUxRuntimeTests.Capture(window, $"glow-{width}-{radius}");
				}
			}
			finally { window.Close(); }
		});
	}

	[Fact]
	public void UnifiedTimeline_DragCreatesAnchorSeekUsesPlaybackAndKeyboardUndoRestoresIt()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-timeline-" + Guid.NewGuid().ToString("N"));
		try
		{
			Sta(() =>
			{
				TrackInfo track = new("Timing study", "Artist", "", TimeSpan.FromSeconds(120));
				var context = PersonalSyncIdentity.Create(new(track, TimeSpan.Zero, true, DateTimeOffset.UtcNow), new() { LrclibRecord = new() { Id = 123 } });
				LyricLine[] lines = [new(TimeSpan.FromSeconds(10), "First"), new(TimeSpan.FromSeconds(20), "Second"), new(TimeSpan.FromSeconds(30), "Third")];
				PersonalSyncWindow window = new(new(directory), context, null, lines, () => 1, () => TimeSpan.FromSeconds(35), "en-US") { ShowActivated = false };
				try
				{
					window.Show(); Pump();
					TextBlock target = Read<TextBlock>(window, "_nowText");
					Assert.True(target.AllowDrop);
					typeof(PersonalSyncWindow).GetField("_dragLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(window, 1);
					var dropConstructor = typeof(DragEventArgs).GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Single();
					DragEventArgs Drop(string token) => (DragEventArgs)dropConstructor.Invoke([new DataObject("FlowLyrics.SyncLyric", token), DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, target, new Point()]);
					var wrong = Drop("another-window"); wrong.RoutedEvent = DragDrop.PreviewDropEvent; target.RaiseEvent(wrong);
					Assert.Equal(DragDropEffects.None, wrong.Effects);
					Assert.Empty(Read<PersonalSyncProfile>(window, "_profile").Anchors);
					var over = Drop(Read<string>(window, "_dragToken")); over.RoutedEvent = DragDrop.PreviewDragOverEvent; target.RaiseEvent(over);
					Assert.Equal(DragDropEffects.Move, over.Effects);
					var valid = Drop(Read<string>(window, "_dragToken")); valid.RoutedEvent = DragDrop.PreviewDropEvent; target.RaiseEvent(valid);
					var profile = Read<PersonalSyncProfile>(window, "_profile");
					Assert.Empty(profile.Anchors);
					Assert.Equal(15, profile.OffsetSeconds);
					Assert.Equal(TimeSpan.FromSeconds(20), lines[1].Time);
					TimeSpan? seek = null; window.SeekRequested += (_, time) => seek = time;
					Invoke(window, "SeekToLyric", 2); Assert.Equal(TimeSpan.FromSeconds(45), seek);
					Assert.True(ApplicationCommands.Undo.CanExecute(null, window));
					ApplicationCommands.Undo.Execute(null, window); Assert.Empty(Read<PersonalSyncProfile>(window, "_profile").Anchors);
					ApplicationCommands.Redo.Execute(null, window); Assert.Equal(15, Read<PersonalSyncProfile>(window, "_profile").OffsetSeconds);
					Invoke(window, "AlignDraggedLyric", -1, 20.0); Assert.Empty(Read<PersonalSyncProfile>(window, "_profile").Anchors);
					Invoke(window, "Hold_Click", window, new RoutedEventArgs());
					Invoke(window, "AlignDraggedLyric", 2, 36.0); Assert.Empty(Read<PersonalSyncProfile>(window, "_profile").Anchors);
				}
				finally { window.Close(); PersonalSyncRuntimeTests.WaitUntil(() => !window.IsVisible); }
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
