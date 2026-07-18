using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlowLyrics.Models;
using FlowLyrics.Services;

namespace FlowLyrics;

public class CandidateSearchWindow : Window, IComponentConnector, IStyleConnector
{
	private readonly TrackInfo _track;

	private readonly LyricsService _lyricsService;

	private readonly string _language;

	private readonly bool _plainFallbackEnabled;

	private readonly FontFamily _englishDotFont;

	private readonly DispatcherTimer _loadingTimer;

	private readonly Stopwatch _loadingStopwatch = new Stopwatch();

	private readonly IReadOnlyList<Ellipse> _loadingDots;

	private string _accentColor;

	private bool _reverseColors;

	private CancellationTokenSource? _searchCancellation;

	private int _searchGeneration;

	private bool _isSearching;

	private Button? _titleOnlyButton;

	internal TextBlock TitleLabel;

	internal TextBox TitleBox;

	internal TextBlock ArtistLabel;

	internal TextBox ArtistBox;

	internal TextBlock AlbumLabel;

	internal TextBox AlbumBox;

	internal TextBlock KeywordLabel;

	internal TextBox KeywordBox;

	internal TextBlock StatusText;

	internal StackPanel LoadingDotLine;

	internal Button SearchButton;

	internal ItemsControl ResultsList;

	internal Button CloseButton;

	private bool _contentLoaded;

	public LyricsLookupResult? SelectedResult { get; private set; }

	public event EventHandler? SelectionApplied;

	public CandidateSearchWindow(TrackInfo track, LyricsService lyricsService, string language, bool plainFallbackEnabled, string accentColor, bool reverseColors)
	{
		InitializeComponent();
		_englishDotFont = (FontFamily)base.Resources["DotFont"];
		_track = track;
		_lyricsService = lyricsService;
		_language = LocalizationService.NormalizeLanguage(language);
		_plainFallbackEnabled = plainFallbackEnabled;
		_accentColor = accentColor;
		_reverseColors = reverseColors;
		SetAppearance(accentColor, reverseColors);
		_loadingDots = LoadingDotLine.Children.OfType<Ellipse>().ToArray();
		_loadingTimer = new DispatcherTimer(DispatcherPriority.Render)
		{
			Interval = TimeSpan.FromMilliseconds(40L)
		};
		_loadingTimer.Tick += delegate
		{
			UpdateLoadingDots();
		};
		TitleBox.Text = track.Title;
		ArtistBox.Text = track.Artist;
		AlbumBox.Text = track.Album;
		ApplyLanguage();
		InitializeSearchActions();
		InitializeContributionFooter();
		base.Loaded += async delegate
		{
			await SearchAsync(titleOnly: false);
		};
		base.Closed += delegate
		{
			_loadingTimer.Stop();
			_searchCancellation?.Cancel();
			_searchCancellation?.Dispose();
		};
	}

	private string T(string key)
	{
		return LocalizationService.Translate(_language, key);
	}

	public void SetAccentColor(string value)
	{
		SetAppearance(value, _reverseColors);
	}

	public void SetAppearance(string accentColor, bool reverseColors)
	{
		try
		{
			Color color = (Color)ColorConverter.ConvertFromString(accentColor.Trim());
			_accentColor = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
			_reverseColors = reverseColors;
			bool darkTheme = !reverseColors;
			base.Resources["Orange"] = new SolidColorBrush(color);
			base.Resources["WindowBackground"] = new SolidColorBrush(darkTheme
				? Color.FromRgb(26, 24, 27)
				: Color.FromRgb(229, 231, 228));
			base.Resources["Panel"] = new SolidColorBrush(darkTheme
				? Color.FromRgb(34, 32, 35)
				: Color.FromRgb(242, 243, 241));
			base.Resources["Paper"] = new SolidColorBrush(darkTheme
				? Color.FromRgb(224, 221, 223)
				: Color.FromRgb(29, 32, 30));
			base.Resources["Muted"] = new SolidColorBrush(darkTheme
				? Color.FromRgb(188, 183, 186)
				: Color.FromRgb(58, 63, 60));
			base.Resources["Control"] = new SolidColorBrush(darkTheme
				? Color.FromRgb(48, 45, 49)
				: Color.FromRgb(220, 223, 220));
			base.Resources["ControlBorder"] = new SolidColorBrush(darkTheme
				? Color.FromRgb(88, 83, 88)
				: Color.FromRgb(174, 180, 175));
			base.Resources["Input"] = new SolidColorBrush(darkTheme
				? Color.FromRgb(42, 39, 43)
				: Color.FromRgb(250, 250, 248));
		}
		catch
		{
		}
	}

	private void ApplyLanguage()
	{
		base.Resources["DotFont"] = LocalizedUiFont.Resolve(_language, _englishDotFont);
		base.Title = "Choose from LRCLIB";
		TitleLabel.Text = "Title";
		ArtistLabel.Text = "Artist";
		AlbumLabel.Text = "Album";
		KeywordLabel.Text = "Keyword";
		SearchButton.Content = T("Search LRCLIB");
		CloseButton.Content = T("Close");
	}

	private async void Search_Click(object sender, RoutedEventArgs e)
	{
		await SearchAsync(titleOnly: false);
	}

	private void InitializeSearchActions()
	{
		if (SearchButton.Parent is not Grid actions)
		{
			return;
		}
		actions.Children.Remove(SearchButton);
		StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal };
		_titleOnlyButton = new Button
		{
			Content = "TITLE ONLY",
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			ToolTip = "Search by title only"
		};
		_titleOnlyButton.Click += async delegate { await SearchAsync(titleOnly: true); };
		buttons.Children.Add(_titleOnlyButton);
		buttons.Children.Add(SearchButton);
		Grid.SetColumn(buttons, 1);
		actions.Children.Add(buttons);
	}

	private void InitializeContributionFooter()
	{
		if (base.Content is not Grid root)
		{
			return;
		}
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		Grid.SetRow(CloseButton, 4);
		Border panel = new Border
		{
			Margin = new Thickness(2.0, 5.0, 2.0, 2.0),
			Padding = new Thickness(0.0),
			Background = System.Windows.Media.Brushes.Transparent,
			BorderThickness = new Thickness(0.0)
		};
		TextBlock message = new TextBlock
		{
			TextWrapping = TextWrapping.Wrap,
			VerticalAlignment = VerticalAlignment.Center,
			FontSize = 10.0,
			Opacity = 0.72
		};
		message.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
		message.Inlines.Add(new Run(T("Can’t find the lyrics? Contribute them to LRCLIB and become the first person to share them.")) + "  ");
		Hyperlink lrclib = new Hyperlink(new Run("LRCLIB")) { FontFamily = _englishDotFont, FontSize = 9.0, TextDecorations = null };
		lrclib.SetResourceReference(TextElement.ForegroundProperty, "Orange");
		lrclib.Click += delegate { OpenUrl(new Uri("https://lrclib.net/")); };
		message.Inlines.Add(lrclib);
		message.Inlines.Add(new Run("  ·  "));
		Hyperlink lrcget = new Hyperlink(new Run("LRCGET")) { FontFamily = _englishDotFont, FontSize = 9.0, TextDecorations = null };
		lrcget.SetResourceReference(TextElement.ForegroundProperty, "Orange");
		lrcget.Click += delegate { OpenUrl(new Uri("https://github.com/tranxuanthang/lrcget")); };
		message.Inlines.Add(lrcget);
		panel.Child = message;
		Grid.SetRow(panel, 3);
		root.Children.Add(panel);
	}

	private void UpdateLoadingDots()
	{
		if (_loadingDots.Count == 0)
		{
			return;
		}
		double num = _loadingStopwatch.Elapsed.TotalMilliseconds % 1850.0 / 1850.0;
		if (num < 0.8)
		{
			double num2 = EaseInOutCubic(num / 0.8);
			for (int i = 0; i < _loadingDots.Count; i++)
			{
				double num3 = (double)i / Math.Max(1.0, (double)_loadingDots.Count - 1.0);
				double num4 = SmoothStep(num3 - 0.075, num3 + 0.015, num2);
				double num5 = Math.Exp(0.0 - Math.Pow((num3 - num2) / 0.075, 2.0));
				_loadingDots[i].Opacity = Math.Clamp(0.14 + num4 * 0.7 + num5 * 0.16, 0.14, 1.0);
			}
			return;
		}
		double num6 = 1.0 - EaseInOutCubic((num - 0.8) / 0.2);
		foreach (Ellipse loadingDot in _loadingDots)
		{
			loadingDot.Opacity = 0.14 + num6 * 0.86;
		}
	}

	private static double EaseInOutCubic(double value)
	{
		double num = Math.Clamp(value, 0.0, 1.0);
		if (!(num < 0.5))
		{
			return 1.0 - Math.Pow(-2.0 * num + 2.0, 3.0) / 2.0;
		}
		return 4.0 * num * num * num;
	}

	private static double SmoothStep(double edge0, double edge1, double value)
	{
		if (edge1 <= edge0)
		{
			if (!(value >= edge1))
			{
				return 0.0;
			}
			return 1.0;
		}
		double num = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
		return num * num * (3.0 - 2.0 * num);
	}

	private async Task SearchAsync(bool titleOnly)
	{
		int generation = ++_searchGeneration;
		_searchCancellation?.Cancel();
		_searchCancellation?.Dispose();
		_searchCancellation = new CancellationTokenSource();
		SearchButton.IsEnabled = false;
		if (_titleOnlyButton != null)
		{
			_titleOnlyButton.IsEnabled = false;
		}
		_isSearching = true;
		StatusText.Text = T("Searching LRCLIB…");
		ResultsList.ItemsSource = null;
		foreach (Ellipse loadingDot in _loadingDots)
		{
			loadingDot.Opacity = 0.14;
		}
		LoadingDotLine.Visibility = Visibility.Visible;
		_loadingStopwatch.Restart();
		_loadingTimer.Start();
		try
		{
			Progress<LyricsSearchProgress> progress = new Progress<LyricsSearchProgress>(delegate(LyricsSearchProgress update)
			{
				if (generation == _searchGeneration && _isSearching)
				{
					StatusText.Text = string.Format(T("Searching LRCLIB… {0} searches checked · {1} candidates found"), update.CompletedQueries, update.CandidateCount);
					if (update.Candidates.Count > 0)
					{
						ResultsList.ItemsSource = update.Candidates.Select(ToViewModel).ToArray();
					}
				}
			});
			LyricsSearchRequest request = titleOnly
				? new LyricsSearchRequest(TitleBox.Text, string.Empty, string.Empty, string.Empty)
				: new LyricsSearchRequest(TitleBox.Text, ArtistBox.Text, AlbumBox.Text, KeywordBox.Text);
			IReadOnlyList<LyricsCandidate> readOnlyList = await _lyricsService.SearchCandidatesAsync(_track, request, _searchCancellation.Token, progress);
			if (generation == _searchGeneration)
			{
				if (readOnlyList.Count == 0)
				{
					StatusText.Text = T("No LRCLIB results were found. Try another title, artist, or English name.");
					return;
				}
				ResultsList.ItemsSource = readOnlyList.Select(ToViewModel).ToArray();
				StatusText.Text = string.Format(T("{0} candidates found. Review the artist and duration before selecting."), readOnlyList.Count);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (LyricsServiceException ex2)
		{
			StatusText.Text = ex2.Message;
		}
		catch (Exception ex3)
		{
			StatusText.Text = string.Format(T("Search failed: {0}"), ex3.Message);
		}
		finally
		{
			if (generation == _searchGeneration)
			{
				_isSearching = false;
				_loadingTimer.Stop();
				_loadingStopwatch.Reset();
				LoadingDotLine.Visibility = Visibility.Collapsed;
				SearchButton.IsEnabled = true;
				if (_titleOnlyButton != null)
				{
					_titleOnlyButton.IsEnabled = true;
				}
			}
		}
	}

	private CandidateCardViewModel ToViewModel(LyricsCandidate candidate)
	{
		LrclibRecord record = candidate.Record;
		bool flag = !string.IsNullOrWhiteSpace(record.SyncedLyrics) || string.IsNullOrWhiteSpace(record.PlainLyrics) || record.Instrumental || _plainFallbackEnabled;
		string value = (record.Instrumental ? T("Instrumental") : ((!string.IsNullOrWhiteSpace(record.SyncedLyrics)) ? T("Synced") : T("Plain")));
		string value2 = (candidate.DurationDifferenceSeconds.HasValue ? string.Format(T("difference {0:+0.0;-0.0;0.0} s"), candidate.DurationDifferenceSeconds.Value) : T("duration unavailable"));
		return new CandidateCardViewModel
		{
			Candidate = candidate,
			Title = (record.TrackName ?? T("Unknown title")),
			Artist = (record.ArtistName ?? T("Unknown artist")),
			Album = (record.AlbumName ?? T("Unknown album")),
			Quality = T(candidate.QualityKey),
			QualityBrush = QualityBrush(candidate.QualityKey),
			Summary = $"LRCLIB #{record.Id}  ·  {FormatDuration(record.Duration)}  ·  {value2}  ·  {value}  ·  {T("Score")} {candidate.Score}",
			Matches = T("Matched") + ": " + JoinTranslated(candidate.MatchedFields),
			Mismatches = T("Not matched") + ": " + ((candidate.MismatchedFields.Count == 0) ? T("None") : JoinTranslated(candidate.MismatchedFields)),
			CanUse = flag,
			DisabledReason = (flag ? string.Empty : T("Enable Plain Lyrics Fallback to use this result.")),
			PreviewLabel = T("Preview"),
			UseLabel = T("Use these lyrics"),
			OpenLabel = T("Open in LRCLIB")
		};
	}

	private string JoinTranslated(IEnumerable<string> keys)
	{
		return string.Join(", ", keys.Select(T));
	}

	private static string FormatDuration(double seconds)
	{
		if (!(seconds <= 0.0))
		{
			return TimeSpan.FromSeconds(seconds).ToString("m\\:ss");
		}
		return "--:--";
	}

	private static Brush QualityBrush(string key)
	{
		return new SolidColorBrush(key switch
		{
			"High match" => Color.FromRgb(105, 230, 166), 
			"Duration mismatch" => Color.FromRgb(byte.MaxValue, 174, 92), 
			"Artist mismatch" => Color.FromRgb(byte.MaxValue, 112, 112), 
			"Romanized lyrics" => Color.FromRgb(byte.MaxValue, 140, 100), 
			"Plain only" => Color.FromRgb(113, 190, byte.MaxValue), 
			"Instrumental" => Color.FromRgb(190, 142, byte.MaxValue), 
			_ => Color.FromRgb(byte.MaxValue, 194, 103), 
		});
	}

	private void Preview_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: CandidateCardViewModel tag })
		{
			LyricsPreviewWindow lyricsPreviewWindow = new LyricsPreviewWindow(_track, tag.Candidate, _language, _accentColor);
			lyricsPreviewWindow.Owner = this;
			lyricsPreviewWindow.Show();
		}
	}

	private async void Use_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { Tag: CandidateCardViewModel { CanUse: not false } tag }))
		{
			return;
		}
		try
		{
			_isSearching = false;
			_searchCancellation?.Cancel();
			SearchButton.IsEnabled = false;
			ResultsList.IsEnabled = false;
			StatusText.Text = T("Applying selected lyrics…");
			SelectedResult = await _lyricsService.ApplyManualSelectionAsync(_track, tag.Candidate.Record.Id, CancellationToken.None);
			this.SelectionApplied?.Invoke(this, EventArgs.Empty);
			Close();
		}
		catch (Exception ex)
		{
			StatusText.Text = ex.Message;
			SearchButton.IsEnabled = true;
			ResultsList.IsEnabled = true;
		}
	}

	private void Open_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: CandidateCardViewModel tag })
		{
			OpenUrl(LyricsService.GetLrclibRecordUri(tag.Candidate.Record.Id));
		}
	}

	private static void OpenUrl(Uri uri)
	{
		Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
		{
			UseShellExecute = true
		});
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
			Uri resourceLocator = new Uri("/FlowLyrics;component/flowlyrics/candidatesearchwindow.xaml", UriKind.Relative);
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
			TitleLabel = (TextBlock)target;
			break;
		case 2:
			TitleBox = (TextBox)target;
			break;
		case 3:
			ArtistLabel = (TextBlock)target;
			break;
		case 4:
			ArtistBox = (TextBox)target;
			break;
		case 5:
			AlbumLabel = (TextBlock)target;
			break;
		case 6:
			AlbumBox = (TextBox)target;
			break;
		case 7:
			KeywordLabel = (TextBlock)target;
			break;
		case 8:
			KeywordBox = (TextBox)target;
			break;
		case 9:
			StatusText = (TextBlock)target;
			break;
		case 10:
			LoadingDotLine = (StackPanel)target;
			break;
		case 11:
			SearchButton = (Button)target;
			SearchButton.Click += Search_Click;
			break;
		case 12:
			ResultsList = (ItemsControl)target;
			break;
		case 16:
			CloseButton = (Button)target;
			CloseButton.Click += Close_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.10.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 13:
			((Button)target).Click += Preview_Click;
			break;
		case 14:
			((Button)target).Click += Use_Click;
			break;
		case 15:
			((Button)target).Click += Open_Click;
			break;
		}
	}
}
