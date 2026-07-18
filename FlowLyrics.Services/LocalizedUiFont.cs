using System.Windows.Media;

namespace FlowLyrics.Services;

internal static class LocalizedUiFont
{
	public static FontFamily Resolve(string? language, FontFamily englishDotFont)
	{
		return LocalizationService.NormalizeLanguage(language) switch
		{
			"en-US" => englishDotFont, 
			"ja-JP" => new FontFamily("Yu Gothic UI, Meiryo UI, Segoe UI"), 
			"zh-CN" => new FontFamily("Microsoft YaHei UI, Segoe UI"), 
			"zh-TW" => new FontFamily("Microsoft JhengHei UI, Segoe UI"), 
			"ko-KR" => new FontFamily("Malgun Gothic, Segoe UI"), 
			_ => new FontFamily("Segoe UI"), 
		};
	}
}
