using System.Collections.Generic;

namespace FlowLyrics.Models;

public static class ColorPalettes
{
	public static readonly IReadOnlyList<CuratedColorPalette> Themes = new CuratedColorPalette[10]
	{
		new CuratedColorPalette("Signal", "#FFFFF4E6", "#FFFFA169", "#FF11100F", "#FF000000", "#FF000000", "#66FFFFFF", "#FFFF6B2C"),
		new CuratedColorPalette("Lagoon", "#FF56D9F2", "#FFFFA47D", "#FF0B1820", "#FF031015", "#FF031015", "#6656D9F2", "#FF38C9E5"),
		new CuratedColorPalette("Cherry", "#FFFF6FAE", "#FFFFD166", "#FF24111D", "#FF10070D", "#FF10070D", "#66FF6FAE", "#FFFF5A9F"),
		new CuratedColorPalette("Mint", "#FF6EE7B7", "#FFFF8B78", "#FF0D201A", "#FF04100D", "#FF04100D", "#666EE7B7", "#FF53DCA4"),
		new CuratedColorPalette("Cobalt", "#FF8AB4FF", "#FFFFE56A", "#FF101936", "#FF060A18", "#FF060A18", "#668AB4FF", "#FF749EFF"),
		new CuratedColorPalette("Violet", "#FFD5A1FF", "#FFFF874F", "#FF1D1326", "#FF0B0710", "#FF0B0710", "#66D5A1FF", "#FFC886FF"),
		new CuratedColorPalette("Paper", "#FF1B1917", "#FFFF6B2C", "#FFF3E7D3", "#FFFFF8EC", "#66000000", "#FF1B1917", "#FFFF6B2C"),
		new CuratedColorPalette("Acid", "#FFC9F564", "#FF69C7FF", "#FF14190E", "#FF080B05", "#FF080B05", "#66C9F564", "#FFA8D93D"),
		new CuratedColorPalette("Sunset", "#FFFFB38A", "#FFF27A9D", "#FF27161D", "#FF10080C", "#FF10080C", "#66FFB38A", "#FFFF835F"),
		new CuratedColorPalette("Slate", "#FFE8EDF3", "#FF8EA6BE", "#FF12171C", "#FF050709", "#FF050709", "#668EA6BE", "#FF7F9DB8")
	};

	public static CuratedColorPalette GetTheme(int seed)
	{
		int index = (int)((uint)seed % (uint)Themes.Count);
		return Themes[index];
	}

	public static string GetAccent(int seed)
	{
		CuratedColorPalette theme = GetTheme(seed / 2);
		if ((seed & 1) != 0)
		{
			return theme.Secondary;
		}
		return theme.Primary;
	}
}
