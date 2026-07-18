namespace FlowLyrics.Models;

public sealed class SavedColorPalette
{
	public int FormatVersion { get; set; } = 1;

	public string Name { get; set; } = "My Palette";

	public string CurrentTextColor { get; set; } = "#FFFFFFFF";

	public string NextTextColor { get; set; } = "#FFE3E7EF";

	public string OutlineColor { get; set; } = "#FF05070A";

	public string ShadowColor { get; set; } = "#E6000000";

	public string BackgroundColor { get; set; } = "#FF111318";

	public string BorderColor { get; set; } = "#66FFFFFF";

	public string UiColor { get; set; } = "#FFFF6B2C";
}
