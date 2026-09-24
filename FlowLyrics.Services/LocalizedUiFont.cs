using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace FlowLyrics.Services;

internal static class LocalizedUiFont
{
	private static readonly DependencyProperty TechnicalFontProperty = DependencyProperty.RegisterAttached("TechnicalFont", typeof(bool), typeof(LocalizedUiFont), new PropertyMetadata(false));
	public static FontFamily EnglishDotFont { get; } = new FontFamily(new System.Uri("pack://application:,,,/"), "./FlowLyrics;component/assets/fonts/#Flow Dots, Segoe UI, Yu Gothic UI, Microsoft YaHei UI, Malgun Gothic");
	public static FontFamily MetadataFont { get; } = new("Segoe UI, Yu Gothic UI, Microsoft YaHei UI, Malgun Gothic");

	public static void Apply(Window window, string? language, FontFamily englishDotFont)
	{
		window.FontFamily = Resolve(language, englishDotFont);
		window.Resources["DotFont"] = EnglishDotFont;
		window.Resources["EnglishDotFont"] = EnglishDotFont;
		window.Resources["UiFont"] = window.FontFamily;
		Style tooltip = new(typeof(ToolTip));
		tooltip.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(32, 30, 33))));
		tooltip.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(240, 237, 240))));
		tooltip.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(89, 83, 91))));
		tooltip.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
		tooltip.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
		tooltip.Setters.Add(new Setter(Control.FontFamilyProperty, window.FontFamily));
		window.Resources[typeof(ToolTip)] = tooltip;
		BindTechnicalFonts(window, englishDotFont);
	}

	public static void Technical(FrameworkElement element)
	{
		element.SetValue(TechnicalFontProperty, true);
		element.SetResourceReference(TextElement.FontFamilyProperty, "DotFont");
	}

	public static void Heading(TextBlock element, string english)
	{
		element.Text = english.ToUpperInvariant();
		element.Tag = "VisualHeading";
		Technical(element);
	}

	public static TextBlock Heading(string english, double size = 15, Brush? foreground = null)
	{
		TextBlock heading = new() { FontSize = size, FontWeight = FontWeights.SemiBold, Foreground = foreground ?? Brushes.White, TextWrapping = TextWrapping.Wrap };
		Heading(heading, english); return heading;
	}

	private static void BindTechnicalFonts(DependencyObject node, FontFamily englishDotFont)
	{
		// Existing BAML assigns DotFont broadly. Only explicitly marked technical text
		// keeps it; translated controls use UiFont. Never inspect or change lyric fonts.
		if (node is FrameworkElement technical && (bool)node.GetValue(TechnicalFontProperty))
			technical.SetResourceReference(TextElement.FontFamilyProperty, "DotFont");
		else if (node is FrameworkElement element && node is not TextBlock { Text: "R" or "S" }
			&& node.GetValue(TextElement.FontFamilyProperty) is FontFamily font
			&& (font.Equals(englishDotFont) || font.Source.Contains("Flow Dots", System.StringComparison.Ordinal)))
			element.SetResourceReference(TextElement.FontFamilyProperty, "UiFont");
		foreach (object child in LogicalTreeHelper.GetChildren(node))
			if (child is DependencyObject dependency) BindTechnicalFonts(dependency, englishDotFont);
	}

	public static FontFamily Resolve(string? language, FontFamily englishDotFont)
	{
		return LocalizationService.NormalizeLanguage(language) switch
		{
			"en-US" => englishDotFont.Source.Contains("Flow Dots", System.StringComparison.Ordinal) ? EnglishDotFont : englishDotFont,
			"ja-JP" => new FontFamily("Yu Gothic UI, Meiryo UI, Segoe UI"), 
			"zh-CN" => new FontFamily("Microsoft YaHei UI, Segoe UI"), 
			"zh-TW" => new FontFamily("Microsoft JhengHei UI, Segoe UI"), 
			"ko-KR" => new FontFamily("Malgun Gothic, Segoe UI"), 
			_ => new FontFamily("Segoe UI"), 
		};
	}
}
