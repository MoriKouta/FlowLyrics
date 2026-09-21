using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FlowLyrics.Models;

namespace FlowLyrics;

/// <summary>Applied after BAML load as well as to editable XAML layouts.</summary>
public static class CurrentTrackHeader
{
	public static TextBlock Attach(StackPanel panel, TextBlock title)
	{
		TextBlock artist = panel.Children.OfType<TextBlock>().FirstOrDefault(child => child.Name == "TrackArtistText")
			?? new TextBlock { Name = "TrackArtistText" };
		if (!panel.Children.Contains(artist)) panel.Children.Insert(panel.Children.IndexOf(title) + 1, artist);
		artist.FontSize = 12;
		artist.FontWeight = FontWeights.Normal;
		artist.Margin = new Thickness(0, 2, 16, 0);
		ConfigureArtist(artist, 16);
		artist.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(TextBlock.Foreground)) { Source = title });
		artist.Visibility = Visibility.Collapsed;
		return artist;
	}

	public static void ConfigureArtist(TextBlock artist, double lineHeight)
	{
		artist.TextWrapping = TextWrapping.Wrap;
		artist.TextTrimming = TextTrimming.CharacterEllipsis;
		artist.LineHeight = lineHeight;
		artist.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
		artist.MaxHeight = lineHeight * 2;
	}

	public static void Update(TextBlock title, TextBlock artist, TrackInfo? track, string idleText)
	{
		title.Text = track?.DisplayTitle ?? idleText;
		artist.Text = track?.DisplayArtist ?? string.Empty;
		artist.ToolTip = artist.Text;
		artist.Visibility = string.IsNullOrWhiteSpace(artist.Text) ? Visibility.Collapsed : Visibility.Visible;
	}
}
