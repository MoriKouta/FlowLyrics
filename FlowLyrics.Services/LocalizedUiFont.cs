using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace FlowLyrics.Services;

internal static class LocalizedUiFont
{
	public static FontFamily EnglishDotFont { get; } = new FontFamily(new System.Uri("pack://application:,,,/"), "./FlowLyrics;component/assets/fonts/#Flow Dots, Segoe UI, Yu Gothic UI, Microsoft YaHei UI, Malgun Gothic");

	public static void Apply(Window window, string? language, FontFamily englishDotFont)
	{
		window.FontFamily = Resolve(language, englishDotFont);
		window.Resources["DotFont"] = window.FontFamily;
		BindTechnicalFonts(window, englishDotFont);
	}

	private static void BindTechnicalFonts(DependencyObject node, FontFamily englishDotFont)
	{
		// UI technical text follows the selected language. Preserve user-selected lyric
		// fonts and the single-letter transport icons, which are graphic symbols.
		if (node is FrameworkElement element && node is not TextBlock { Text: "R" or "S" }
			&& node.GetValue(TextElement.FontFamilyProperty) is FontFamily font
			&& (font.Equals(englishDotFont) || font.Source.Contains("Flow Dots", System.StringComparison.Ordinal)))
			element.SetResourceReference(TextElement.FontFamilyProperty, "DotFont");
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
