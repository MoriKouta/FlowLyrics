using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlowLyrics.Controls;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class GlowOverlayTests
{
	[Theory]
	[InlineData(TextAlignment.Left, false)]
	[InlineData(TextAlignment.Center, false)]
	[InlineData(TextAlignment.Right, false)]
	[InlineData(TextAlignment.Left, true)]
	[InlineData(TextAlignment.Center, true)]
	[InlineData(TextAlignment.Right, true)]
	public void SiblingGlow_BleedsPastEveryViewportEdge_WithoutChangingLayout(TextAlignment alignment, bool longLine)
	{
		Sta(() =>
		{
			var texts = Enumerable.Range(0, 4).Select(_ => new OutlinedText
			{
				Text = longLine ? "光の広がりを切らずに表示 Keep every word and every line in exactly the same place" : "Glow",
				FontSize = 52, Height = longLine ? 180 : 100, StrokeThickness = 0, ShadowDepth = 0,
				TextAlignment = alignment, GlowColor = Colors.OrangeRed, GlowOpacity = 1
			}).ToArray();
			StackPanel stack = new(); foreach (var text in texts) stack.Children.Add(text);
			ScrollViewer scroll = new() { Content = stack, ClipToBounds = true, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
			LyricGlowOverlay glow = new(scroll, () => texts);
			Grid host = new() { Margin = new Thickness(48) }; host.Children.Add(glow); host.Children.Add(scroll);
			Window window = new() { Width = 430, Height = 320, Content = host, Background = Brushes.Black,
				WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true,
				ShowActivated = false, Topmost = true, Left = 100, Top = 100 };
			try
			{
				window.Show(); Pump();
				foreach (double offset in new[] { texts[0].GlyphGeometry!.Bounds.Top + 2,
					texts[2].TranslatePoint(new Point(0, texts[2].GlyphGeometry!.Bounds.Bottom), stack).Y - scroll.ViewportHeight - 2, 10000.0 })
				{
					scroll.ScrollToVerticalOffset(offset); Pump();
					var original = texts.Select(text => (text.GlyphGeometry!.ToString(CultureInfo.InvariantCulture), text.RenderedFontSize,
						text.DesiredSize, text.TranslatePoint(new Point(), window))).ToArray();
					byte[]? unlit = null;
					foreach (double radius in new[] { 0.0, 10, 20, 30, 40 })
					{
						foreach (var text in texts) text.GlowRadius = radius;
						Pump(); glow.Refresh(); Pump();
						Assert.False(glow.IsDescendantOf(scroll)); Assert.False(glow.IsHitTestVisible);
						Assert.Equal(new Size(0, 0), glow.DesiredSize);
						for (int i = 0; i < texts.Length; i++)
						{
							Assert.Equal(original[i], (texts[i].GlyphGeometry!.ToString(CultureInfo.InvariantCulture), texts[i].RenderedFontSize,
								texts[i].DesiredSize, texts[i].TranslatePoint(new Point(), window)));
							Assert.Equal(1, VisualTreeHelper.GetChildrenCount(texts[i]));
						}
						RenderTargetBitmap bitmap = new(430, 320, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
						byte[] pixels = new byte[430 * 320 * 4]; bitmap.CopyPixels(pixels, 430 * 4, 0);
						if (radius == 0) unlit = pixels;
						else if (offset != 10000)
						{
							Assert.True(Enumerable.Range(0, 430 * 320).Any(p =>
								(p % 430 < 48 || p % 430 >= 382 || p / 430 < 48 || p / 430 >= 272)
								&& pixels[p * 4 + 2] > unlit![p * 4 + 2] + 1), $"Halo must reach outside the text viewport: offset={offset}, radius={radius}.");
						}
						string name = $"glow-overlay-{alignment}-{longLine}-{offset}-{radius}";
						UiUxRuntimeTests.Capture(window, name);
						CaptureNative(window, name);
					}
				}
				window.Width += 80; Pump(); glow.Refresh();
				Assert.Equal(scroll.ActualWidth, glow.ActualWidth);
			}
			finally { window.Close(); }
		});
	}

	internal static void CaptureNative(Window window, string name)
	{
		string? directory = Environment.GetEnvironmentVariable("FLOWLYRICS_NATIVE_CAPTURE_DIR");
		if (string.IsNullOrEmpty(directory)) return;
		Directory.CreateDirectory(directory);
		bool topmost = window.Topmost;
		Window? backdrop = null;
		Window? owner = window.Owner;
		try
		{
			if (window.AllowsTransparency)
			{
				// Native capture must never include another application's content through
				// the overlay or its outer bleed. Keep an opaque owned backdrop underneath.
				backdrop = new Window { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
					Background = Brushes.Black, ShowInTaskbar = false, ShowActivated = false, Topmost = true,
					Left = window.Left, Top = window.Top, Width = window.ActualWidth, Height = window.ActualHeight };
				backdrop.Show(); window.Owner = backdrop;
			}
			window.Topmost = false; window.Topmost = true; window.Activate(); window.UpdateLayout(); Pump();
			// Keep dispatching while the compositor presents the window. Blocking the UI
			// thread here can capture only the unpainted native window background.
			var frame = new System.Windows.Threading.DispatcherFrame();
			var present = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
			present.Tick += (_, _) => { present.Stop(); frame.Continue = false; };
			present.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
			FrameworkElement surface = window.WindowStyle == WindowStyle.None ? window : (FrameworkElement)window.Content;
			Point origin = surface.PointToScreen(new Point()); DpiScale dpi = VisualTreeHelper.GetDpi(surface);
			using System.Drawing.Bitmap bitmap = new((int)(surface.ActualWidth * dpi.DpiScaleX), (int)(surface.ActualHeight * dpi.DpiScaleY));
			using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
				graphics.CopyFromScreen((int)origin.X, (int)origin.Y, 0, 0, bitmap.Size);
			bitmap.Save(Path.Combine(directory, name + ".png"));
		}
		finally { window.Owner = owner; backdrop?.Close(); window.Topmost = topmost; }
	}
}
