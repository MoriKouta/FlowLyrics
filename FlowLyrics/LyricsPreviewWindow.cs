using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using FlowLyrics.Models;
using FlowLyrics.Services;

namespace FlowLyrics;

public class LyricsPreviewWindow : Window, IComponentConnector
{
	private readonly string _language;

	internal TextBlock TitleText;

	internal TextBlock ArtistText;

	internal TextBlock AlbumText;

	internal TextBlock DurationText;

	internal TextBox LyricsText;

	internal Button CloseButton;

	private bool _contentLoaded;

	public LyricsPreviewWindow(TrackInfo track, LyricsCandidate candidate, string language, string accentColor)
	{
		InitializeComponent();
		ApplyAccentColor(accentColor);
		_language = language;
		FontFamily englishDotFont = (FontFamily)base.Resources["DotFont"];
		base.Resources["DotFont"] = LocalizedUiFont.Resolve(_language, englishDotFont);
		LrclibRecord record = candidate.Record;
		base.Title = "Lyrics preview";
		CloseButton.Content = T("Close");
		TitleText.Text = record.TrackName ?? T("Unknown title");
		ArtistText.Text = record.ArtistName ?? T("Unknown artist");
		AlbumText.Text = record.AlbumName ?? T("Unknown album");
		string value = (candidate.DurationDifferenceSeconds.HasValue ? $"{candidate.DurationDifferenceSeconds.Value:+0.0;-0.0;0.0} s" : "--");
		DurationText.Text = $"LRCLIB #{record.Id}  ·  {FormatDuration(record.Duration)}  ·  {T("Spotify duration difference")}: {value}";
		if (record.Instrumental)
		{
			LyricsText.Text = T("Instrumental");
		}
		else if (!string.IsNullOrWhiteSpace(record.SyncedLyrics))
		{
			LyricsText.Text = string.Join(Environment.NewLine, from line in LrcParser.Parse(record.SyncedLyrics)
				select line.Text);
		}
		else
		{
			LyricsText.Text = record.PlainLyrics ?? T("No lyrics");
		}
	}

	private string T(string key)
	{
		return LocalizationService.Translate(_language, key);
	}

	private void ApplyAccentColor(string value)
	{
		try
		{
			Color color = (Color)ColorConverter.ConvertFromString(value.Trim());
			base.Resources["Orange"] = new SolidColorBrush(color);
		}
		catch
		{
		}
	}

	private static string FormatDuration(double seconds)
	{
		if (!(seconds <= 0.0))
		{
			return TimeSpan.FromSeconds(seconds).ToString("m\\:ss");
		}
		return "--:--";
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.10.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/FlowLyrics;component/flowlyrics/lyricspreviewwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.10.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			TitleText = (TextBlock)target;
			break;
		case 2:
			ArtistText = (TextBlock)target;
			break;
		case 3:
			AlbumText = (TextBlock)target;
			break;
		case 4:
			DurationText = (TextBlock)target;
			break;
		case 5:
			LyricsText = (TextBox)target;
			break;
		case 6:
			CloseButton = (Button)target;
			CloseButton.Click += Close_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
