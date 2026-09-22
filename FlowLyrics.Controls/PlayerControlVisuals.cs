using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowLyrics.Services;

namespace FlowLyrics.Controls;

internal static class PlayerControlVisuals
{
	public const double ButtonSize = 30;
	public const double ButtonSpacing = 2;
	public const double BorderWidth = 1.25;
	public const double IconFontSize = 16.5;
	public const double NeutralOpacity = .72;
	public const double HoverOpacity = 1;
	public const double DotDiameter = 1.8;
	public const double DotPitch = 2.55;
	public const double EllipsisDotDiameter = 2;
	public const double EllipsisPitch = 6;

	public static void Size(Button button)
	{
		button.Width = button.Height = ButtonSize;
		button.Margin = new Thickness(ButtonSpacing, 0, ButtonSpacing, 0);
		button.Padding = new Thickness(0);
		button.BorderThickness = new Thickness(BorderWidth);
		button.UseLayoutRounding = true;
		button.SnapsToDevicePixels = true;
	}

	public static TextBlock Letter(string letter)
	{
		var icon = new TextBlock { Text = letter, FontFamily = LocalizedUiFont.EnglishDotFont,
			FontSize = IconFontSize, LineHeight = IconFontSize, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
			TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
			RenderTransform = new TranslateTransform(1.2, 0), IsHitTestVisible = false };
		LocalizedUiFont.Technical(icon);
		TextOptions.SetTextFormattingMode(icon, TextFormattingMode.Display);
		return icon;
	}

	public static void IconState(Button button, UIElement icon, bool active, double neutral = NeutralOpacity)
		=> icon.Opacity = active || (button.IsEnabled && button.IsMouseOver) ? HoverOpacity : neutral;

	public static void TrackHover(Button button, Action refresh)
	{
		button.MouseEnter += (_, _) => refresh(); button.MouseLeave += (_, _) => refresh();
		button.IsEnabledChanged += (_, _) => refresh();
	}
}
