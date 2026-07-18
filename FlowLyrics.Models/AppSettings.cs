using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FlowLyrics.Services;

namespace FlowLyrics.Models;

public sealed class AppSettings
{
	public int SettingsSchemaVersion { get; set; } = 13;

	public string Language { get; set; } = "en-US";

	public double? WindowLeft { get; set; }

	public double? WindowTop { get; set; }

	public double WindowWidth { get; set; } = 760.0;

	public double WindowHeight { get; set; } = 520.0;

	public string FontFamily { get; set; } = "Yu Gothic UI";

	public double FontSize { get; set; } = 38.0;

	public double MinimumFontSize { get; set; } = 8.0;

	public string CurrentTextColor { get; set; } = "#FFFFFFFF";

	public string NextTextColor { get; set; } = "#FFE3E7EF";

	public string TextColorMode { get; set; } = "Fixed";

	public int RandomPaletteSeed { get; set; }

	public string OutlineColor { get; set; } = "#FF05070A";

	public double OutlineThickness { get; set; }

	public string ShadowColor { get; set; } = "#E6000000";

	public double ShadowDepth { get; set; }

	public double PreviousLineOpacity { get; set; } = 0.34;

	public double NextLineOpacity { get; set; } = 0.68;

	public double InactiveFontScale { get; set; } = 0.82;

	public string BackgroundColor { get; set; } = "#FF111318";

	public string UiColor { get; set; } = "#FFFF6B2C";

	public bool ReverseColors { get; set; }

	public List<SavedColorPalette> SavedColorPalettes { get; set; } = new List<SavedColorPalette>();

	public double BackgroundOpacity { get; set; }

	public double OverlayOpacity { get; set; } = 1.0;

	public double CornerRadius { get; set; } = 22.0;

	public double PanelPadding { get; set; } = 24.0;

	public string BorderColor { get; set; } = "#66FFFFFF";

	public double BorderThickness { get; set; } = 1.0;

	public bool ShowPanelBorder { get; set; } = true;

	public string TextAlignment { get; set; } = "Left";

	public int DisplayLines { get; set; } = 7;

	public string CurrentLinePosition { get; set; } = "Center";

	public double LineSpacing { get; set; } = 5.0;

	public bool WrapLongLines { get; set; } = true;

	public bool AutoFitText { get; set; } = true;

	public int MaximumWrapLines { get; set; } = 4;

	public bool ShowUnlockedBadge { get; set; }

	public bool ShowTrackInfo { get; set; } = true;

	public bool ShowPlaybackControls { get; set; } = true;

	public bool ShowProgressBar { get; set; } = true;

	public bool AlwaysOnTop { get; set; } = true;

	public bool HideWhenPaused { get; set; }

	public bool StartWithWindows { get; set; }

	public bool ShortcutsEnabled { get; set; } = true;

	public bool PauseEyeAnimation { get; set; }

	public bool LockOnStartup { get; set; }

	public bool IsLocked { get; set; }

	public bool ShowStatusWhenIdle { get; set; } = true;

	public bool EnablePlainLyricsFallback { get; set; } = true;

	public bool PlainLyricsAutoScroll { get; set; } = true;

	public int GlobalLyricsOffsetMs { get; set; }

	public Dictionary<string, int> TrackOffsetsMs { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

	public AppSettings Clone()
	{
		return JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this)) ?? new AppSettings();
	}

	public void Normalize()
	{
		SettingsSchemaVersion = Math.Max(13, SettingsSchemaVersion);
		Language = LocalizationService.NormalizeLanguage(Language);
		WindowWidth = Math.Clamp(WindowWidth, 120.0, 3840.0);
		WindowHeight = Math.Clamp(WindowHeight, 40.0, 1200.0);
		FontSize = Math.Clamp(FontSize, 14.0, 128.0);
		MinimumFontSize = Math.Clamp(MinimumFontSize, 4.0, Math.Min(72.0, FontSize));
		OutlineThickness = Math.Clamp(OutlineThickness, 0.0, 8.0);
		ShadowDepth = Math.Clamp(ShadowDepth, 0.0, 12.0);
		PreviousLineOpacity = Math.Clamp(PreviousLineOpacity, 0.08, 1.0);
		NextLineOpacity = Math.Clamp(NextLineOpacity, 0.08, 1.0);
		InactiveFontScale = Math.Clamp(InactiveFontScale, 0.55, 1.0);
		BackgroundOpacity = Math.Clamp(BackgroundOpacity, 0.0, 1.0);
		OverlayOpacity = Math.Clamp(OverlayOpacity, 0.15, 1.0);
		CornerRadius = Math.Clamp(CornerRadius, 0.0, 40.0);
		PanelPadding = Math.Clamp(PanelPadding, 4.0, 60.0);
		BorderThickness = Math.Clamp(BorderThickness, 0.0, 5.0);
		DisplayLines = Math.Clamp(DisplayLines, 1, 12);
		LineSpacing = Math.Clamp(LineSpacing, 0.0, 20.0);
		WrapLongLines = true;
		AutoFitText = true;
		MaximumWrapLines = Math.Clamp(MaximumWrapLines, 1, 8);
		TextColorMode = "Fixed";
		ShowUnlockedBadge = false;
		string textAlignment = TextAlignment;
		bool flag = ((textAlignment == "Left" || textAlignment == "Right") ? true : false);
		TextAlignment = (flag ? TextAlignment : "Center");
		textAlignment = CurrentLinePosition;
		flag = ((textAlignment == "Top" || textAlignment == "Bottom") ? true : false);
		CurrentLinePosition = (flag ? CurrentLinePosition : "Center");
		GlobalLyricsOffsetMs = Math.Clamp(GlobalLyricsOffsetMs, -5000, 5000);
		if (TrackOffsetsMs == null)
		{
			Dictionary<string, int> dictionary = (TrackOffsetsMs = new Dictionary<string, int>(StringComparer.Ordinal));
		}
		SavedColorPalettes ??= new List<SavedColorPalette>();
		SavedColorPalettes = SavedColorPalettes
			.Where((SavedColorPalette palette) => palette != null && !string.IsNullOrWhiteSpace(palette.Name))
			.GroupBy((SavedColorPalette palette) => palette.Name.Trim(), StringComparer.OrdinalIgnoreCase)
			.Select((IGrouping<string, SavedColorPalette> group) => group.Last())
			.Take(100)
			.ToList();
	}

}
