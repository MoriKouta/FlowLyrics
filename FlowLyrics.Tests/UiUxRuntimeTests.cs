using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using FlowLyrics.Controls;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class UiUxRuntimeTests
{

	[Fact]
	public void Settings_CurrentTrackKeepsDetailsVisibleAndLocalRowCompactWithSingleSyncRoute()
	{
		string directory = Temp();
		try
		{
			Sta(() =>
			{
				using LyricsService lyrics = new(directory, new LrclibRefreshTests.Handler());
				using MediaSessionService media = new(new EmptyProvider());
				TrackInfo track = new("Timing study", "Artist A, Artist B, Artist C", "Album", TimeSpan.FromSeconds(240));
				LyricsLookupResult lookup = new() { Status = LyricsLookupStatus.LrclibAuto, Lyrics = new([new(TimeSpan.FromSeconds(1), "Line")], null, "LRCLIB") };
				var snapshot = new PlaybackSnapshot(track, TimeSpan.Zero, true, DateTimeOffset.UtcNow);
				SettingsWindow window = new(new() { Language = "ja-JP" }, directory, lyrics, media, new(directory), () => snapshot,
					() => track, () => lookup, () => null, () => System.Threading.Tasks.Task.CompletedTask);
				try
				{
					window.ShowActivated = false; window.Show(); Pump();
					int opened = 0; window.PersonalSyncRequested += (_, _) => opened++;
					Button route = Read<Button>(window, "_openSyncButton");
					Assert.True(route.IsEnabled);
					route.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Assert.Equal(1, opened);
					StackPanel trackPanel = Read<StackPanel>(window, "CurrentTrackPanel");
					Assert.DoesNotContain(Logical<CheckBox>((DependencyObject)trackPanel.Parent), c => c.Name == "UseLocalLrcToggle");
					Assert.DoesNotContain(Logical<Button>((DependencyObject)trackPanel.Parent), c => c.Name == "CurrentTrackSyncButton");
					Assert.Empty(Logical<Expander>(trackPanel));
					Assert.True(Read<TextBlock>(window, "LrclibArtistText").IsVisible);
					DockPanel local = Logical<DockPanel>((DependencyObject)trackPanel.Parent).Single(c => c.Name == "CurrentTrackLocalLrc");
					Assert.InRange(local.ActualHeight, 20, 60);
					Capture(window, "settings-current-track");
					var tabs = Read<TabControl>(window, "SettingsTabs");
					var scroll = (ScrollViewer)((TabItem)tabs.SelectedItem).Content;
					scroll.ScrollToBottom(); Pump(); Capture(window, "settings-local-sync");
					lookup = new() { Status = LyricsLookupStatus.NoLyrics }; window.RefreshCurrentTrack(); Pump();
					Assert.False(route.IsEnabled);
				}
				finally { window.Close(); }
				MediaSessionDiagnosticsWindow diagnostics = new(media, () => snapshot, () => null, "ja-JP");
				try { diagnostics.ShowActivated = false; diagnostics.Show(); Pump(); Capture(diagnostics, "diagnostics"); }
				finally { diagnostics.Close(); }
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public void StrongGlow_BleedsOutsideTextLayoutAndLeavesForegroundUnfiltered()
	{
		Sta(() =>
		{
			OutlinedText lyric = new() { Text = "Soft glow", FontSize = 48, Fill = Brushes.White, StrokeThickness = 0, ShadowDepth = 0,
				GlowColor = Colors.OrangeRed, GlowOpacity = 1, GlowRadius = 40 };
			ScrollViewer viewport = new() { Content = lyric, ClipToBounds = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
			LyricGlowOverlay overlay = new(viewport, () => [lyric]);
			Grid host = new() { Margin = new Thickness(48) }; host.Children.Add(overlay); host.Children.Add(viewport);
			Window window = new() { Width = 480, Height = 300, Background = Brushes.Black, Content = host, ShowActivated = false };
			try
			{
				window.Show(); Pump();
				Assert.Null(lyric.Effect);
				overlay.Refresh();
				var rear = (DrawingVisual)VisualTreeHelper.GetChild(overlay, 0);
				var front = (DrawingVisual)VisualTreeHelper.GetChild(lyric, 0);
				Assert.Equal(KernelType.Gaussian, Assert.IsType<BlurEffect>(rear.Effect).KernelType);
				Assert.Null(front.Effect);
				Assert.True(front.ContentBounds.Left < 15);
				Assert.False(lyric.ClipToBounds);
				Assert.Null(rear.Clip);
				Capture(window, "glow-strong");
				lyric.GlowOpacity = 0; Pump();
				overlay.Refresh(); Assert.Equal(0, VisualTreeHelper.GetChildrenCount(overlay));
			}
			finally { window.Close(); }
		});
	}

	internal static void Capture(Window window, string name, double dpi = 96)
	{
		string? output = Environment.GetEnvironmentVariable("FLOWLYRICS_UI_CAPTURE_DIR");
		if (string.IsNullOrWhiteSpace(output)) return;
		Directory.CreateDirectory(output);
		window.UpdateLayout();
		int width = (int)Math.Ceiling(window.ActualWidth * dpi / 96), height = (int)Math.Ceiling(window.ActualHeight * dpi / 96);
		RenderTargetBitmap bitmap = new(width, height, dpi, dpi, PixelFormats.Pbgra32);
		bitmap.Render(window);
		// Composite transparent overlay captures on a known background. A PNG viewer
		// that ignores alpha otherwise shows false colored contours in the glow.
		DrawingVisual backdrop = new();
		using (DrawingContext drawing = backdrop.RenderOpen())
		{
			Rect bounds = new(0, 0, window.ActualWidth, window.ActualHeight);
			drawing.DrawRectangle(Brushes.Black, null, bounds);
			drawing.DrawImage(bitmap, bounds);
		}
		bitmap = new(width, height, dpi, dpi, PixelFormats.Pbgra32);
		bitmap.Render(backdrop);
		PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using FileStream stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
	}
	private static string Temp() => Path.Combine(Path.GetTempPath(), "FlowLyrics-uiux-" + Guid.NewGuid().ToString("N"));
}
