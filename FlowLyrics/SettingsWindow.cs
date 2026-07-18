using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Microsoft.Win32;

namespace FlowLyrics;

public class SettingsWindow : Window, IComponentConnector
{
	private readonly AppSettings _originalSettings;

	private readonly string _lrcDirectory;

	private readonly LyricsService _lyricsService;

	private readonly Func<TrackInfo?> _currentTrackProvider;

	private readonly Func<LyricsLookupResult?> _lookupProvider;

	private readonly Func<Task> _reloadCurrentTrack;

	private readonly System.Windows.Media.FontFamily _englishDotFont;

	private readonly Dictionary<TextBlock, string> _localizedText = new Dictionary<TextBlock, string>();

	private readonly Dictionary<ContentControl, string> _localizedContent = new Dictionary<ContentControl, string>();

	private readonly Dictionary<HeaderedContentControl, string> _localizedHeaders = new Dictionary<HeaderedContentControl, string>();

	private bool _suppressPreview;

	private int _randomPaletteSeed;

	private string _currentLanguage = "en-US";

	private CandidateSearchWindow? _candidateSearchWindow;

	private readonly Dictionary<string, string> _individualBlendModes = new Dictionary<string, string>(StringComparer.Ordinal);

	private readonly Dictionary<string, List<System.Windows.Controls.Button>> _blendModeButtons = new Dictionary<string, List<System.Windows.Controls.Button>>(StringComparer.Ordinal);

	private readonly List<StackPanel> _individualBlendPanels = new List<StackPanel>();

	private readonly List<System.Windows.Controls.Button> _scopeButtons = new List<System.Windows.Controls.Button>();

	private string _blendModeScope = "All";

	private string _globalBlendMode = "Normal";

	private TextBlock? _versionText;

	private bool _brandingInitialized;

	private bool _blendModeControlsInitialized;

	internal System.Windows.Controls.TabControl SettingsTabs;

	internal System.Windows.Controls.ComboBox FontFamilyBox;

	internal Slider FontSizeSlider;

	internal Slider MinimumFontSizeSlider;

	internal System.Windows.Controls.ComboBox AlignmentBox;

	internal System.Windows.Controls.ComboBox CurrentPositionBox;

	internal Slider LineSpacingSlider;

	internal Slider DisplayLinesSlider;

	internal Slider InactiveScaleSlider;

	internal Slider PreviousOpacitySlider;

	internal Slider NextOpacitySlider;

	internal Slider MaximumWrapLinesSlider;

	internal System.Windows.Controls.CheckBox WrapLongLinesBox;

	internal System.Windows.Controls.CheckBox AutoFitTextBox;

	internal Slider OutlineSlider;

	internal Slider ShadowSlider;

	internal Slider BackgroundOpacitySlider;

	internal Slider OverlayOpacitySlider;

	internal Slider CornerRadiusSlider;

	internal Slider PanelPaddingSlider;

	internal TabItem LyricsTab;

	internal TextBlock LyricsEmptyText;

	internal StackPanel CurrentTrackPanel;

	internal TextBlock CurrentTrackTitleText;

	internal TextBlock CurrentTrackArtistText;

	internal TextBlock CurrentTrackAlbumText;

	internal TextBlock CurrentTrackDurationText;

	internal TextBlock SpotifyTrackIdText;

	internal TextBlock LyricsSourceText;

	internal TextBlock LrclibIdText;

	internal TextBlock LrclibTitleText;

	internal TextBlock LrclibArtistText;

	internal TextBlock LrclibAlbumText;

	internal TextBlock LrclibDurationText;

	internal TextBlock SelectionModeText;

	internal TextBlock LoadedFromCacheText;

	internal TextBlock LocalLrcStateText;

	internal TextBlock LyricsGuidanceText;

	internal System.Windows.Controls.Button ChooseCandidatesButton;

	internal System.Windows.Controls.Button ResetManualButton;

	internal System.Windows.Controls.Button OpenLrclibButton;

	internal TextBlock LyricsActionStatusText;

	internal System.Windows.Controls.Button ChooseLocalLrcButton;

	internal System.Windows.Controls.TextBox LrcFolderPathBox;

	internal System.Windows.Controls.CheckBox ShowPanelBorderBox;

	internal System.Windows.Controls.CheckBox ShowTrackInfoBox;

	internal System.Windows.Controls.CheckBox ShowPlaybackControlsBox;

	internal System.Windows.Controls.CheckBox ShowProgressBarBox;

	internal Slider BorderThicknessSlider;

	internal System.Windows.Controls.CheckBox AlwaysOnTopBox;

	internal System.Windows.Controls.CheckBox HideWhenPausedBox;

	internal System.Windows.Controls.CheckBox ShowIdleStatusBox;

	internal System.Windows.Controls.CheckBox PlainLyricsFallbackBox;

	internal System.Windows.Controls.CheckBox LockOnStartupBox;

	internal System.Windows.Controls.CheckBox StartWithWindowsBox;

	internal System.Windows.Controls.CheckBox ShortcutsEnabledBox;

	internal System.Windows.Controls.CheckBox PauseEyeAnimationBox;

	internal System.Windows.Controls.ComboBox LanguageBox;

	internal Slider GlobalOffsetSlider;

	internal System.Windows.Controls.TextBox CurrentColorBox;

	internal System.Windows.Controls.TextBox NextColorBox;

	internal System.Windows.Controls.TextBox OutlineColorBox;

	internal System.Windows.Controls.TextBox ShadowColorBox;

	internal System.Windows.Controls.TextBox BackgroundColorBox;

	internal System.Windows.Controls.TextBox BorderColorBox;

	internal System.Windows.Controls.TextBox UiColorBox;

	private bool _contentLoaded;

	public AppSettings ResultSettings { get; private set; }

	public bool Accepted { get; private set; }

	public event Action<AppSettings>? PreviewChanged;

	public SettingsWindow(AppSettings settings, string lrcDirectory, LyricsService lyricsService, Func<TrackInfo?> currentTrackProvider, Func<LyricsLookupResult?> lookupProvider, Func<Task> reloadCurrentTrack)
	{
		InitializeComponent();
		_englishDotFont = (System.Windows.Media.FontFamily)base.Resources["DotFont"];
		SettingsTabs.Items.Remove(LyricsTab);
		SettingsTabs.Items.Insert(0, LyricsTab);
		CaptureLocalizableContent(this);
		_lrcDirectory = lrcDirectory;
		_lyricsService = lyricsService;
		_currentTrackProvider = currentTrackProvider;
		_lookupProvider = lookupProvider;
		_reloadCurrentTrack = reloadCurrentTrack;
		LrcFolderPathBox.Text = _lrcDirectory;
		_originalSettings = settings.Clone();
		ResultSettings = settings.Clone();
		_randomPaletteSeed = settings.RandomPaletteSeed;
		FontFamilyBox.ItemsSource = Fonts.SystemFontFamilies.Select((System.Windows.Media.FontFamily font) => font.Source).Distinct<string>(StringComparer.CurrentCultureIgnoreCase).OrderBy<string, string>((string name) => name, StringComparer.CurrentCultureIgnoreCase)
			.ToArray();
		_suppressPreview = true;
		PopulateControls(ResultSettings);
		UpdateAccentColor(ResultSettings.UiColor);
		ApplyLanguage(ResultSettings.Language);
		_suppressPreview = false;
		AttachPreviewHandlers();
		base.Loaded += delegate
		{
			DisableDialogOnlyButtons();
			InitializeBranding();
			InitializeBlendModeControls();
			RefreshBlendModeButtons();
			UpdateAccentColor(ResultSettings.UiColor);
			CaptureLocalizableContent(this);
			ApplyLanguage(_currentLanguage);
			RefreshLyricsTab();
		};
	}

	private void DisableDialogOnlyButtons()
	{
		foreach (System.Windows.Controls.Button button in FindVisualChildren<System.Windows.Controls.Button>(this))
		{
			if (button.IsCancel)
			{
				button.IsCancel = false;
			}
		}
	}

	private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.OriginalSource == SettingsTabs)
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				CaptureLocalizableContent(this);
				ApplyLanguage(_currentLanguage);
				RefreshLyricsTab();
			});
		}
	}

	private void OpenLrcFolder_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Directory.CreateDirectory(_lrcDirectory);
			Process.Start(new ProcessStartInfo
			{
				FileName = _lrcDirectory,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(this, ex.Message, T("Could not open the folder."), MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void InitializeBranding()
	{
		if (_brandingInitialized)
		{
			return;
		}

		TextBlock? title = FindVisualChildren<TextBlock>(this).FirstOrDefault((TextBlock item) => string.Equals(item.Text, "FLOW LYRICS", StringComparison.Ordinal));
		if (title != null && VisualTreeHelper.GetParent(title) is Grid header)
		{
			StackPanel wordmark = new StackPanel
			{
				Orientation = System.Windows.Controls.Orientation.Horizontal,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Tag = "NoTranslate"
			};
			wordmark.Children.Add(new TextBlock
			{
				Text = "Flow ",
				FontFamily = _englishDotFont,
				FontSize = 31.0,
				FontWeight = FontWeights.Bold,
				Foreground = System.Windows.Media.Brushes.White,
				Tag = "NoTranslate"
			});
			TextBlock lyrics = new TextBlock
			{
				Text = "Lyrics",
				FontFamily = _englishDotFont,
				FontSize = 31.0,
				FontWeight = FontWeights.Bold,
				Tag = "NoTranslate"
			};
			lyrics.SetResourceReference(TextBlock.ForegroundProperty, "Orange");
			wordmark.Children.Add(lyrics);
			header.Children.Remove(title);
			header.Children.Add(wordmark);
		}

		_versionText = FindVisualChildren<TextBlock>(this).FirstOrDefault((TextBlock item) => item.Text?.StartsWith("v1.", StringComparison.OrdinalIgnoreCase) == true);
		if (_versionText != null)
		{
			_versionText.Text = "v" + BuildInfo.Version;
			_versionText.TextAlignment = TextAlignment.Center;
			_versionText.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			_versionText.VerticalAlignment = VerticalAlignment.Center;
			_versionText.FontSize = 11.5;
			_versionText.FontWeight = FontWeights.SemiBold;
			_versionText.Foreground = System.Windows.Media.Brushes.White;
			if (VisualTreeHelper.GetParent(_versionText) is Border badge)
			{
				badge.Width = double.NaN;
				badge.MinWidth = 0.0;
				badge.Padding = new Thickness(0.0);
				badge.Background = System.Windows.Media.Brushes.Transparent;
				badge.BorderBrush = System.Windows.Media.Brushes.Transparent;
				badge.BorderThickness = new Thickness(0.0);
				badge.CornerRadius = new CornerRadius(0.0);
			}
		}
		_brandingInitialized = true;
	}

	private void InitializeBlendModeControls()
	{
		if (_blendModeControlsInitialized)
		{
			return;
		}

		(string Tag, string Property)[] rows =
		{
			("CurrentColorBox", nameof(AppSettings.CurrentTextBlendMode)),
			("NextColorBox", nameof(AppSettings.NextTextBlendMode)),
			("OutlineColorBox", nameof(AppSettings.OutlineBlendMode)),
			("ShadowColorBox", nameof(AppSettings.ShadowBlendMode)),
			("BackgroundColorBox", nameof(AppSettings.BackgroundBlendMode)),
			("BorderColorBox", nameof(AppSettings.BorderBlendMode)),
			("UiColorBox", nameof(AppSettings.UiBlendMode))
		};

		Grid? colorGrid = null;
		foreach ((string tag, string property) in rows)
		{
			System.Windows.Controls.Button? pickButton = FindVisualChildren<System.Windows.Controls.Button>(this)
				.FirstOrDefault((System.Windows.Controls.Button button) => string.Equals(button.Tag?.ToString(), tag, StringComparison.Ordinal));
			if (pickButton == null || VisualTreeHelper.GetParent(pickButton) is not Grid grid)
			{
				continue;
			}

			colorGrid ??= grid;
			if (grid.ColumnDefinitions.Count == 3)
			{
				grid.ColumnDefinitions[2].Width = GridLength.Auto;
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			}

			StackPanel modes = new StackPanel
			{
				Orientation = System.Windows.Controls.Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
				Tag = "NoTranslate"
			};
			Grid.SetRow(modes, Grid.GetRow(pickButton));
			Grid.SetColumn(modes, 3);
			_individualBlendPanels.Add(modes);
			_blendModeButtons[property] = AddModeButtons(modes, (string mode) =>
			{
				_individualBlendModes[property] = mode;
				RefreshBlendModeButtons();
				NotifyPreviewChanged();
			});
			grid.Children.Add(modes);
		}

		if (colorGrid == null || VisualTreeHelper.GetParent(colorGrid) is not StackPanel colorCard)
		{
			return;
		}
		_blendModeControlsInitialized = true;

		StackPanel globalPanel = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
			Tag = "NoTranslate"
		};
		globalPanel.Children.Add(new TextBlock
		{
			Text = "BLEND",
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			FontWeight = FontWeights.Bold,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 7.0, 0.0),
			Tag = "NoTranslate"
		});
		_scopeButtons.Add(CreateSmallModeButton("ALL", () => SetBlendScope("All")));
		_scopeButtons.Add(CreateSmallModeButton("EACH", () => SetBlendScope("Individual")));
		foreach (System.Windows.Controls.Button button in _scopeButtons)
		{
			globalPanel.Children.Add(button);
		}
		globalPanel.Children.Add(new Border { Width = 8.0 });
		_blendModeButtons["Global"] = AddModeButtons(globalPanel, (string mode) =>
		{
			_globalBlendMode = mode;
			RefreshBlendModeButtons();
			NotifyPreviewChanged();
		});
		int colorGridIndex = colorCard.Children.IndexOf(colorGrid);
		colorCard.Children.Insert(Math.Max(1, colorGridIndex), globalPanel);
	}

	private List<System.Windows.Controls.Button> AddModeButtons(System.Windows.Controls.Panel panel, Action<string> onSelect)
	{
		List<System.Windows.Controls.Button> result = new List<System.Windows.Controls.Button>();
		foreach (string mode in BlendModeService.Modes)
		{
			string captured = mode;
			System.Windows.Controls.Button button = CreateSmallModeButton(ModeLabel(mode), () => onSelect(captured));
			button.ToolTip = mode;
			panel.Children.Add(button);
			result.Add(button);
		}
		return result;
	}

	private System.Windows.Controls.Button CreateSmallModeButton(string label, Action onClick)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button
		{
			Content = label,
			FontFamily = _englishDotFont,
			FontSize = 8.0,
			FontWeight = FontWeights.Bold,
			Padding = new Thickness(5.0, 3.0, 5.0, 3.0),
			Margin = new Thickness(1.0),
			MinWidth = label.Length > 5 ? 48.0 : 36.0,
			Tag = "NoTranslate"
		};
		button.Click += delegate { onClick(); };
		return button;
	}

	private void SetBlendScope(string scope)
	{
		_blendModeScope = scope;
		RefreshBlendModeButtons();
		NotifyPreviewChanged();
	}

	private void RefreshBlendModeButtons()
	{
		bool individual = string.Equals(_blendModeScope, "Individual", StringComparison.Ordinal);
		foreach (StackPanel panel in _individualBlendPanels)
		{
			panel.IsEnabled = individual;
			panel.Opacity = individual ? 1.0 : 0.32;
		}
		for (int i = 0; i < _scopeButtons.Count; i++)
		{
			SetModeButtonState(_scopeButtons[i], i == (individual ? 1 : 0));
		}
		foreach ((string key, List<System.Windows.Controls.Button> buttons) in _blendModeButtons)
		{
			string selected = key == "Global" ? _globalBlendMode : (_individualBlendModes.TryGetValue(key, out string? value) ? value : "Normal");
			foreach (System.Windows.Controls.Button button in buttons)
			{
				SetModeButtonState(button, string.Equals(button.ToolTip?.ToString(), selected, StringComparison.OrdinalIgnoreCase));
			}
		}
	}

	private void SetModeButtonState(System.Windows.Controls.Button button, bool selected)
	{
		if (selected)
		{
			button.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "Orange");
			button.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 17, 17));
		}
		else
		{
			button.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 52, 52));
			button.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Paper");
		}
	}

	private static string ModeLabel(string mode)
	{
		return mode switch
		{
			"Normal" => "NORM",
			"Auto" => "AUTO",
			"Invert" => "INVERT",
			"Screen" => "SCREEN",
			"Overlay" => "OVERLAY",
			_ => mode.ToUpperInvariant()
		};
	}

	private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			if (child is T match)
			{
				yield return match;
			}
			foreach (T descendant in FindVisualChildren<T>(child))
			{
				yield return descendant;
			}
		}
	}

	private void AttachPreviewHandlers()
	{
		Slider[] array = new Slider[16]
		{
			DisplayLinesSlider, LineSpacingSlider, InactiveScaleSlider, FontSizeSlider, MinimumFontSizeSlider, MaximumWrapLinesSlider, PreviousOpacitySlider, NextOpacitySlider, OutlineSlider, ShadowSlider,
			BackgroundOpacitySlider, OverlayOpacitySlider, CornerRadiusSlider, PanelPaddingSlider, BorderThicknessSlider, GlobalOffsetSlider
		};
		for (int i = 0; i < array.Length; i++)
		{
			array[i].ValueChanged += delegate
			{
				NotifyPreviewChanged();
			};
		}
		System.Windows.Controls.ComboBox[] array2 = new System.Windows.Controls.ComboBox[3] { CurrentPositionBox, AlignmentBox, FontFamilyBox };
		for (int num = 0; num < array2.Length; num++)
		{
			array2[num].SelectionChanged += delegate
			{
				NotifyPreviewChanged();
			};
		}
		FontFamilyBox.DropDownClosed += delegate
		{
			NotifyPreviewChanged();
		};
		FontFamilyBox.LostKeyboardFocus += delegate
		{
			NotifyPreviewChanged();
		};
		LanguageBox.SelectionChanged += LanguageBox_SelectionChanged;
		System.Windows.Controls.CheckBox[] array3 = new System.Windows.Controls.CheckBox[14]
		{
			WrapLongLinesBox, AutoFitTextBox, ShowPanelBorderBox, ShowTrackInfoBox, ShowPlaybackControlsBox, ShowProgressBarBox, AlwaysOnTopBox, HideWhenPausedBox, ShowIdleStatusBox, PlainLyricsFallbackBox,
			LockOnStartupBox, StartWithWindowsBox, ShortcutsEnabledBox, PauseEyeAnimationBox
		};
		foreach (System.Windows.Controls.CheckBox obj in array3)
		{
			obj.Checked += delegate
			{
				NotifyPreviewChanged();
			};
			obj.Unchecked += delegate
			{
				NotifyPreviewChanged();
			};
		}
		System.Windows.Controls.TextBox[] array4 = new System.Windows.Controls.TextBox[7] { CurrentColorBox, NextColorBox, OutlineColorBox, ShadowColorBox, BackgroundColorBox, BorderColorBox, UiColorBox };
		foreach (System.Windows.Controls.TextBox colorBox in array4)
		{
			colorBox.TextChanged += delegate
			{
				if (colorBox == UiColorBox)
				{
					UpdateAccentColor(UiColorBox.Text);
				}
				NotifyPreviewChanged();
			};
		}
	}

	private void PopulateControls(AppSettings settings)
	{
		settings.Normalize();
		FontFamilyBox.Text = settings.FontFamily;
		FontSizeSlider.Value = settings.FontSize;
		MinimumFontSizeSlider.Value = settings.MinimumFontSize;
		DisplayLinesSlider.Value = settings.DisplayLines;
		SelectItemByTag(CurrentPositionBox, settings.CurrentLinePosition);
		SelectItemByTag(AlignmentBox, settings.TextAlignment);
		LineSpacingSlider.Value = settings.LineSpacing;
		InactiveScaleSlider.Value = settings.InactiveFontScale;
		MaximumWrapLinesSlider.Value = settings.MaximumWrapLines;
		WrapLongLinesBox.IsChecked = settings.WrapLongLines;
		AutoFitTextBox.IsChecked = settings.AutoFitText;
		PreviousOpacitySlider.Value = settings.PreviousLineOpacity;
		NextOpacitySlider.Value = settings.NextLineOpacity;
		_randomPaletteSeed = settings.RandomPaletteSeed;
		CurrentColorBox.Text = settings.CurrentTextColor;
		NextColorBox.Text = settings.NextTextColor;
		OutlineColorBox.Text = settings.OutlineColor;
		OutlineSlider.Value = settings.OutlineThickness;
		ShadowColorBox.Text = settings.ShadowColor;
		ShadowSlider.Value = settings.ShadowDepth;
		BackgroundColorBox.Text = settings.BackgroundColor;
		BackgroundOpacitySlider.Value = settings.BackgroundOpacity;
		OverlayOpacitySlider.Value = settings.OverlayOpacity;
		CornerRadiusSlider.Value = settings.CornerRadius;
		PanelPaddingSlider.Value = settings.PanelPadding;
		ShowPanelBorderBox.IsChecked = settings.ShowPanelBorder;
		BorderColorBox.Text = settings.BorderColor;
		UiColorBox.Text = settings.UiColor;
		BorderThicknessSlider.Value = settings.BorderThickness;
		ShowTrackInfoBox.IsChecked = settings.ShowTrackInfo;
		ShowPlaybackControlsBox.IsChecked = settings.ShowPlaybackControls;
		ShowProgressBarBox.IsChecked = settings.ShowProgressBar;
		AlwaysOnTopBox.IsChecked = settings.AlwaysOnTop;
		HideWhenPausedBox.IsChecked = settings.HideWhenPaused;
		ShowIdleStatusBox.IsChecked = settings.ShowStatusWhenIdle;
		PlainLyricsFallbackBox.IsChecked = settings.EnablePlainLyricsFallback;
		LockOnStartupBox.IsChecked = settings.LockOnStartup;
		StartWithWindowsBox.IsChecked = settings.StartWithWindows;
		ShortcutsEnabledBox.IsChecked = settings.ShortcutsEnabled;
		PauseEyeAnimationBox.IsChecked = settings.PauseEyeAnimation;
		GlobalOffsetSlider.Value = settings.GlobalLyricsOffsetMs;
		_blendModeScope = settings.BlendModeScope;
		_globalBlendMode = settings.GlobalBlendMode;
		_individualBlendModes[nameof(AppSettings.CurrentTextBlendMode)] = settings.CurrentTextBlendMode;
		_individualBlendModes[nameof(AppSettings.NextTextBlendMode)] = settings.NextTextBlendMode;
		_individualBlendModes[nameof(AppSettings.OutlineBlendMode)] = settings.OutlineBlendMode;
		_individualBlendModes[nameof(AppSettings.ShadowBlendMode)] = settings.ShadowBlendMode;
		_individualBlendModes[nameof(AppSettings.BackgroundBlendMode)] = settings.BackgroundBlendMode;
		_individualBlendModes[nameof(AppSettings.BorderBlendMode)] = settings.BorderBlendMode;
		_individualBlendModes[nameof(AppSettings.UiBlendMode)] = settings.UiBlendMode;
		RefreshBlendModeButtons();
		SelectItemByTag(LanguageBox, settings.Language);
	}

	private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_suppressPreview)
		{
			ApplyLanguage(GetSelectedTag(LanguageBox, "en-US"));
			NotifyPreviewChanged();
		}
	}

	private void CaptureLocalizableContent(DependencyObject element)
	{
		int num;
		if (element is FrameworkElement { Tag: string tag })
		{
			num = (string.Equals(tag, "NoTranslate", StringComparison.Ordinal) ? 1 : 0);
			if (num != 0)
			{
				goto IL_0072;
			}
		}
		else
		{
			num = 0;
		}
		if (element is TextBlock textBlock && !_localizedText.ContainsKey(textBlock) && !BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty) && !string.IsNullOrWhiteSpace(textBlock.Text))
		{
			_localizedText[textBlock] = textBlock.Text;
		}
		goto IL_0072;
		IL_0072:
		if (num == 0 && element is HeaderedContentControl { Header: string header } headeredContentControl && !_localizedHeaders.ContainsKey(headeredContentControl) && !string.IsNullOrWhiteSpace(header))
		{
			_localizedHeaders[headeredContentControl] = header;
		}
		if (num == 0 && element is ContentControl { Content: string content } contentControl && !_localizedContent.ContainsKey(contentControl) && !string.IsNullOrWhiteSpace(content))
		{
			_localizedContent[contentControl] = content;
		}
		foreach (object child in LogicalTreeHelper.GetChildren(element))
		{
			if (child is DependencyObject element2)
			{
				CaptureLocalizableContent(element2);
			}
		}
	}

	private void ApplyLanguage(string? language)
	{
		_currentLanguage = LocalizationService.NormalizeLanguage(language);
		base.Resources["DotFont"] = LocalizedUiFont.Resolve(_currentLanguage, _englishDotFont);
		base.Title = "FlowLyrics Settings";
		string value;
		foreach (KeyValuePair<TextBlock, string> item in _localizedText)
		{
			item.Deconstruct(out var key, out value);
			TextBlock textBlock = key;
			string key2 = value;
			textBlock.Text = T(key2);
		}
		foreach (KeyValuePair<ContentControl, string> item2 in _localizedContent)
		{
			item2.Deconstruct(out var key3, out value);
			ContentControl contentControl = key3;
			string key4 = value;
			contentControl.Content = T(key4);
		}
		foreach (KeyValuePair<HeaderedContentControl, string> localizedHeader in _localizedHeaders)
		{
			localizedHeader.Deconstruct(out var key5, out value);
			HeaderedContentControl headeredContentControl = key5;
			string key6 = value;
			headeredContentControl.Header = T(key6);
		}
		RefreshLyricsTab();
	}

	private void RefreshLyricsTab()
	{
		if (LyricsEmptyText == null || CurrentTrackPanel == null)
		{
			return;
		}
		TrackInfo trackInfo = _currentTrackProvider();
		LyricsLookupResult lyricsLookupResult = _lookupProvider();
		bool flag = trackInfo != null;
		LyricsEmptyText.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		CurrentTrackPanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		System.Windows.Controls.Button[] array = new System.Windows.Controls.Button[4] { ChooseCandidatesButton, ResetManualButton, ChooseLocalLrcButton, OpenLrclibButton };
		for (int i = 0; i < array.Length; i++)
		{
			array[i].IsEnabled = flag;
		}
		if (flag && !(trackInfo == null))
		{
			LrclibRecord lrclibRecord = lyricsLookupResult?.LrclibRecord;
			CurrentTrackTitleText.Text = trackInfo.Title;
			CurrentTrackArtistText.Text = trackInfo.Artist;
			CurrentTrackAlbumText.Text = trackInfo.Album;
			CurrentTrackDurationText.Text = FormatDuration(trackInfo.Duration.TotalSeconds);
			SpotifyTrackIdText.Text = (string.IsNullOrWhiteSpace(trackInfo.SpotifyTrackId) ? "Unavailable (stable metadata key is used)" : trackInfo.SpotifyTrackId);
			LyricsSourceText.Text = SourceLabel(lyricsLookupResult);
			LrclibIdText.Text = ((lrclibRecord == null) ? "—" : lrclibRecord.Id.ToString());
			LrclibTitleText.Text = ValueOrDash(lrclibRecord?.TrackName);
			LrclibArtistText.Text = ValueOrDash(lrclibRecord?.ArtistName);
			LrclibAlbumText.Text = ValueOrDash(lrclibRecord?.AlbumName);
			LrclibDurationText.Text = ((lrclibRecord == null) ? "—" : FormatDuration(lrclibRecord.Duration));
			SelectionModeText.Text = ((lyricsLookupResult == null || lyricsLookupResult.Status == LyricsLookupStatus.NoLyrics || lyricsLookupResult.Status == LyricsLookupStatus.CandidatesFound) ? "—" : (lyricsLookupResult.SelectedManually ? "Manually selected" : "Auto selected"));
			LoadedFromCacheText.Text = ((lyricsLookupResult != null && lyricsLookupResult.LoadedFromCache) ? "Yes" : "No");
			LocalLrcStateText.Text = ((lyricsLookupResult != null && lyricsLookupResult.Status == LyricsLookupStatus.LocalLrc) ? ("Yes · " + ValueOrDash(lyricsLookupResult.LocalLrcPath)) : "No");
			bool flag2 = lyricsLookupResult != null && lyricsLookupResult.Lyrics?.HasPlainLyrics == true && !lyricsLookupResult.Lyrics.HasSyncedLyrics;
			TextBlock lyricsGuidanceText = LyricsGuidanceText;
			string text;
			switch (lyricsLookupResult?.Status)
			{
			case LyricsLookupStatus.CandidatesFound:
				text = T("Lyrics candidates were found in LRCLIB. Choose the correct lyrics below.");
				break;
			case null:
			case LyricsLookupStatus.NoLyrics:
				text = T("No synced lyrics were found. Search using another title or English name, or add a local LRC file.");
				break;
			default:
				text = ((!flag2) ? T("If the lyrics or timing are incorrect, you can choose another result from LRCLIB.") : T("Estimated timing is being used. Choose from LRCLIB to look for synchronized lyrics."));
				break;
			}
			lyricsGuidanceText.Text = text;
			OpenLrclibButton.IsEnabled = lrclibRecord != null;
			ResetManualButton.IsEnabled = lyricsLookupResult?.SelectedManually ?? false;
		}
	}

	private string SourceLabel(LyricsLookupResult? lookup)
	{
		if (lookup == null)
		{
			return "No lyrics";
		}
		return lookup.Status switch
		{
			LyricsLookupStatus.LrclibAuto => "LRCLIB — Auto selected", 
			LyricsLookupStatus.LrclibManual => "LRCLIB — Manually selected", 
			LyricsLookupStatus.LocalLrc => "Local LRC", 
			LyricsLookupStatus.Cache => "Cache" + (lookup.SelectedManually ? " · Manually selected" : string.Empty), 
			LyricsLookupStatus.CandidatesFound => "LRCLIB candidates found", 
			_ => "No lyrics", 
		};
	}

	private void ChooseCandidates_Click(object sender, RoutedEventArgs e)
	{
		TrackInfo trackInfo = _currentTrackProvider();
		if (trackInfo == null)
		{
			RefreshLyricsTab();
			return;
		}
		CandidateSearchWindow candidateSearchWindow = _candidateSearchWindow;
		if (candidateSearchWindow != null && candidateSearchWindow.IsVisible)
		{
			_candidateSearchWindow.Activate();
			return;
		}
		CandidateSearchWindow window = new CandidateSearchWindow(trackInfo, _lyricsService, _currentLanguage, ResultSettings.EnablePlainLyricsFallback, UiColorBox.Text)
		{
			Owner = this
		};
		_candidateSearchWindow = window;
		window.SelectionApplied += async delegate
		{
			await _reloadCurrentTrack();
			if (base.IsLoaded)
			{
				RefreshLyricsTab();
			}
		};
		window.Closed += delegate
		{
			if (_candidateSearchWindow == window)
			{
				_candidateSearchWindow = null;
			}
		};
		window.Show();
	}

	private async void ResetManualSelection_Click(object sender, RoutedEventArgs e)
	{
		TrackInfo track = _currentTrackProvider();
		if (!(track == null))
		{
			await RunLyricsActionAsync(async delegate
			{
				await _lyricsService.ResetManualSelectionAsync(track);
				await _reloadCurrentTrack();
			}, "Could not reset the manual lyrics selection.");
		}
	}

	private async void ChooseLocalLrc_Click(object sender, RoutedEventArgs e)
	{
		TrackInfo track = _currentTrackProvider();
		if (track == null)
		{
			return;
		}
		Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = T("Choose local LRC"),
			Filter = T("LRC files") + " (*.lrc)|*.lrc|" + T("All files") + " (*.*)|*.*",
			CheckFileExists = true,
			Multiselect = false
		};
		if (dialog.ShowDialog(this) == true)
		{
			await RunLyricsActionAsync(async delegate
			{
				await _lyricsService.ImportLocalLrcFileAsync(track, dialog.FileName);
				await _reloadCurrentTrack();
			}, "Could not read timestamped LRC lyrics.");
		}
	}

	private void OpenCurrentLrclib_Click(object sender, RoutedEventArgs e)
	{
		int? num = _lookupProvider()?.LrclibRecord?.Id;
		if (num.HasValue)
		{
			Process.Start(new ProcessStartInfo(LyricsService.GetLrclibRecordUri(num.Value).AbsoluteUri)
			{
				UseShellExecute = true
			});
		}
	}

	private async Task RunLyricsActionAsync(Func<Task> action, string fallbackMessage)
	{
		try
		{
			LyricsActionStatusText.Text = T("Working…");
			await action();
			LyricsActionStatusText.Text = T("Updated.");
		}
		catch (Exception ex)
		{
			LyricsActionStatusText.Text = ex.Message;
			System.Windows.MessageBox.Show(this, ex.Message, T(fallbackMessage), MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		finally
		{
			RefreshLyricsTab();
		}
	}

	private string YesNo(bool value)
	{
		return T(value ? "Yes" : "No");
	}

	private static string ValueOrDash(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		return "—";
	}

	private static string FormatDuration(double seconds)
	{
		if (!(seconds <= 0.0))
		{
			return TimeSpan.FromSeconds(seconds).ToString("m\\:ss");
		}
		return "--:--";
	}

	private string T(string key)
	{
		return LocalizationService.Translate(_currentLanguage, key);
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		if (TryBuildSettings(out AppSettings settings, showError: true))
		{
			ResultSettings = settings;
			Accepted = true;
			Close();
		}
	}

	public void ApplyAndClose()
	{
		Save_Click(this, new RoutedEventArgs());
	}

	private void NotifyPreviewChanged()
	{
		if (!_suppressPreview && base.IsLoaded && TryBuildSettings(out AppSettings settings, showError: false))
		{
			ResultSettings = settings;
			this.PreviewChanged?.Invoke(settings.Clone());
		}
	}

	private bool TryBuildSettings(out AppSettings settings, bool showError)
	{
		try
		{
			settings = BuildSettingsFromControls();
			return true;
		}
		catch (Exception ex)
		{
			settings = ResultSettings.Clone();
			if (showError)
			{
				System.Windows.MessageBox.Show(this, ex.Message, T("Check settings"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			return false;
		}
	}

	private AppSettings BuildSettingsFromControls()
	{
		ValidateColor(CurrentColorBox.Text);
		ValidateColor(NextColorBox.Text);
		ValidateColor(OutlineColorBox.Text);
		ValidateColor(ShadowColorBox.Text);
		ValidateColor(BackgroundColorBox.Text);
		ValidateColor(BorderColorBox.Text);
		ValidateColor(UiColorBox.Text);
		string text = FontFamilyBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new InvalidOperationException(T("Select a font."));
		}
		new System.Windows.Media.FontFamily(text);
		AppSettings appSettings = _originalSettings.Clone();
		appSettings.FontFamily = text;
		appSettings.FontSize = FontSizeSlider.Value;
		appSettings.MinimumFontSize = MinimumFontSizeSlider.Value;
		appSettings.DisplayLines = (int)Math.Round(DisplayLinesSlider.Value);
		appSettings.CurrentLinePosition = GetSelectedTag(CurrentPositionBox, "Center");
		appSettings.TextAlignment = GetSelectedTag(AlignmentBox, "Left");
		appSettings.LineSpacing = LineSpacingSlider.Value;
		appSettings.InactiveFontScale = InactiveScaleSlider.Value;
		appSettings.MaximumWrapLines = (int)Math.Round(MaximumWrapLinesSlider.Value);
		appSettings.WrapLongLines = WrapLongLinesBox.IsChecked == true;
		appSettings.AutoFitText = AutoFitTextBox.IsChecked == true;
		appSettings.PreviousLineOpacity = PreviousOpacitySlider.Value;
		appSettings.NextLineOpacity = NextOpacitySlider.Value;
		appSettings.TextColorMode = "Fixed";
		appSettings.RandomPaletteSeed = _randomPaletteSeed;
		appSettings.CurrentTextColor = NormalizeColor(CurrentColorBox.Text);
		appSettings.NextTextColor = NormalizeColor(NextColorBox.Text);
		appSettings.OutlineColor = NormalizeColor(OutlineColorBox.Text);
		appSettings.OutlineThickness = OutlineSlider.Value;
		appSettings.ShadowColor = NormalizeColor(ShadowColorBox.Text);
		appSettings.ShadowDepth = ShadowSlider.Value;
		appSettings.BackgroundColor = NormalizeColor(BackgroundColorBox.Text);
		appSettings.BackgroundOpacity = BackgroundOpacitySlider.Value;
		appSettings.OverlayOpacity = OverlayOpacitySlider.Value;
		appSettings.CornerRadius = CornerRadiusSlider.Value;
		appSettings.PanelPadding = PanelPaddingSlider.Value;
		appSettings.ShowPanelBorder = ShowPanelBorderBox.IsChecked == true;
		appSettings.BorderColor = NormalizeColor(BorderColorBox.Text);
		appSettings.UiColor = NormalizeColor(UiColorBox.Text);
		appSettings.BlendModeScope = _blendModeScope;
		appSettings.GlobalBlendMode = _globalBlendMode;
		appSettings.CurrentTextBlendMode = GetIndividualBlendMode(nameof(AppSettings.CurrentTextBlendMode));
		appSettings.NextTextBlendMode = GetIndividualBlendMode(nameof(AppSettings.NextTextBlendMode));
		appSettings.OutlineBlendMode = GetIndividualBlendMode(nameof(AppSettings.OutlineBlendMode));
		appSettings.ShadowBlendMode = GetIndividualBlendMode(nameof(AppSettings.ShadowBlendMode));
		appSettings.BackgroundBlendMode = GetIndividualBlendMode(nameof(AppSettings.BackgroundBlendMode));
		appSettings.BorderBlendMode = GetIndividualBlendMode(nameof(AppSettings.BorderBlendMode));
		appSettings.UiBlendMode = GetIndividualBlendMode(nameof(AppSettings.UiBlendMode));
		appSettings.BorderThickness = BorderThicknessSlider.Value;
		appSettings.ShowUnlockedBadge = false;
		appSettings.ShowTrackInfo = ShowTrackInfoBox.IsChecked == true;
		appSettings.ShowPlaybackControls = ShowPlaybackControlsBox.IsChecked == true;
		appSettings.ShowProgressBar = ShowProgressBarBox.IsChecked == true;
		appSettings.AlwaysOnTop = AlwaysOnTopBox.IsChecked == true;
		appSettings.HideWhenPaused = HideWhenPausedBox.IsChecked == true;
		appSettings.ShowStatusWhenIdle = ShowIdleStatusBox.IsChecked == true;
		appSettings.EnablePlainLyricsFallback = PlainLyricsFallbackBox.IsChecked == true;
		appSettings.LockOnStartup = LockOnStartupBox.IsChecked == true;
		appSettings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
		appSettings.ShortcutsEnabled = ShortcutsEnabledBox.IsChecked == true;
		appSettings.PauseEyeAnimation = PauseEyeAnimationBox.IsChecked == true;
		appSettings.GlobalLyricsOffsetMs = (int)Math.Round(GlobalOffsetSlider.Value);
		appSettings.Language = GetSelectedTag(LanguageBox, "en-US");
		appSettings.Normalize();
		return appSettings;
	}

	private string GetIndividualBlendMode(string property)
	{
		return _individualBlendModes.TryGetValue(property, out string? value) ? value : "Normal";
	}

	private void LyricsOnly_Click(object sender, RoutedEventArgs e)
	{
		ShowPanelBorderBox.IsChecked = false;
		ShowTrackInfoBox.IsChecked = false;
		ShowPlaybackControlsBox.IsChecked = false;
		ShowProgressBarBox.IsChecked = false;
		BackgroundOpacitySlider.Value = 0.0;
		NotifyPreviewChanged();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		Accepted = false;
		Close();
	}

	private void Reset_Click(object sender, RoutedEventArgs e)
	{
		if (System.Windows.MessageBox.Show(this, T("Reset all settings?"), "FlowLyrics", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
		{
			AppSettings appSettings = new AppSettings
			{
				WindowLeft = _originalSettings.WindowLeft,
				WindowTop = _originalSettings.WindowTop,
				WindowWidth = _originalSettings.WindowWidth,
				WindowHeight = _originalSettings.WindowHeight,
				TrackOffsetsMs = new Dictionary<string, int>(_originalSettings.TrackOffsetsMs, StringComparer.Ordinal),
				IsLocked = _originalSettings.IsLocked
			};
			_randomPaletteSeed = appSettings.RandomPaletteSeed;
			_suppressPreview = true;
			PopulateControls(appSettings);
			ApplyLanguage(appSettings.Language);
			_suppressPreview = false;
			NotifyPreviewChanged();
		}
	}

	private void ThemePreset_Click(object sender, RoutedEventArgs e)
	{
		if (sender is System.Windows.Controls.Button { Tag: string tag } && int.TryParse(tag, out var result) && result >= 0 && result < ColorPalettes.Themes.Count)
		{
			ApplyTheme(ColorPalettes.Themes[result]);
		}
	}

	private void RandomColors_Click(object sender, RoutedEventArgs e)
	{
		_randomPaletteSeed = Random.Shared.Next();
		ApplyTheme(ColorPalettes.GetTheme(_randomPaletteSeed));
	}

	private void ApplyTheme(CuratedColorPalette theme)
	{
		_suppressPreview = true;
		CurrentColorBox.Text = theme.Primary;
		NextColorBox.Text = theme.Secondary;
		BackgroundColorBox.Text = theme.Background;
		OutlineColorBox.Text = theme.Outline;
		ShadowColorBox.Text = theme.Shadow;
		BorderColorBox.Text = theme.Border;
		UiColorBox.Text = theme.Ui;
		_suppressPreview = false;
		NotifyPreviewChanged();
	}

	private void CustomColor_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is System.Windows.Controls.Button { Tag: string tag }) || !(FindName(tag) is System.Windows.Controls.TextBox textBox))
		{
			return;
		}
		System.Windows.Media.Color color = ParseColor(textBox.Text);
		using ColorDialog colorDialog = new ColorDialog
		{
			AllowFullOpen = true,
			AnyColor = true,
			FullOpen = true,
			Color = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B)
		};
		if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			System.Drawing.Color color2 = colorDialog.Color;
			textBox.Text = $"#{color2.A:X2}{color2.R:X2}{color2.G:X2}{color2.B:X2}";
			NotifyPreviewChanged();
		}
	}

	private void ResetColors_Click(object sender, RoutedEventArgs e)
	{
		AppSettings appSettings = new AppSettings();
		_suppressPreview = true;
		CurrentColorBox.Text = appSettings.CurrentTextColor;
		NextColorBox.Text = appSettings.NextTextColor;
		OutlineColorBox.Text = appSettings.OutlineColor;
		ShadowColorBox.Text = appSettings.ShadowColor;
		BackgroundColorBox.Text = appSettings.BackgroundColor;
		BorderColorBox.Text = appSettings.BorderColor;
		UiColorBox.Text = appSettings.UiColor;
		_blendModeScope = "All";
		_globalBlendMode = "Normal";
		foreach (string key in _individualBlendModes.Keys.ToArray())
		{
			_individualBlendModes[key] = "Normal";
		}
		RefreshBlendModeButtons();
		_suppressPreview = false;
		NotifyPreviewChanged();
	}

	private static void SelectItemByTag(System.Windows.Controls.ComboBox comboBox, string tag)
	{
		comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault((ComboBoxItem item) => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) ?? ((ComboBoxItem)comboBox.Items[0]);
	}

	private static string GetSelectedTag(System.Windows.Controls.ComboBox comboBox, string fallback)
	{
		if (comboBox.SelectedItem is ComboBoxItem { Tag: var tag })
		{
			return tag?.ToString() ?? fallback;
		}
		return fallback;
	}

	private void ValidateColor(string value)
	{
		ParseColor(value);
	}

	private void UpdateAccentColor(string value)
	{
		try
		{
			System.Windows.Media.Color color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value.Trim());
			base.Resources["Orange"] = new SolidColorBrush(color);
			if (_versionText != null)
			{
				_versionText.Foreground = System.Windows.Media.Brushes.White;
			}
			_candidateSearchWindow?.SetAccentColor(value);
		}
		catch
		{
		}
	}

	private System.Windows.Media.Color ParseColor(string value)
	{
		try
		{
			return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value.Trim());
		}
		catch
		{
			throw new FormatException(T("Invalid color format. Use #FFFFFFFF or #FFFFFF."));
		}
	}

	private string NormalizeColor(string value)
	{
		System.Windows.Media.Color color = ParseColor(value);
		return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.10.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/FlowLyrics;component/flowlyrics.settingswindow.xaml", UriKind.Relative);
			System.Windows.Application.LoadComponent(this, resourceLocator);
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
			SettingsTabs = (System.Windows.Controls.TabControl)target;
			SettingsTabs.SelectionChanged += SettingsTabs_SelectionChanged;
			break;
		case 2:
			FontFamilyBox = (System.Windows.Controls.ComboBox)target;
			break;
		case 3:
			FontSizeSlider = (Slider)target;
			break;
		case 4:
			MinimumFontSizeSlider = (Slider)target;
			break;
		case 5:
			AlignmentBox = (System.Windows.Controls.ComboBox)target;
			break;
		case 6:
			CurrentPositionBox = (System.Windows.Controls.ComboBox)target;
			break;
		case 7:
			LineSpacingSlider = (Slider)target;
			break;
		case 8:
			DisplayLinesSlider = (Slider)target;
			break;
		case 9:
			InactiveScaleSlider = (Slider)target;
			break;
		case 10:
			PreviousOpacitySlider = (Slider)target;
			break;
		case 11:
			NextOpacitySlider = (Slider)target;
			break;
		case 12:
			MaximumWrapLinesSlider = (Slider)target;
			break;
		case 13:
			WrapLongLinesBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 14:
			AutoFitTextBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 15:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 16:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 17:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 18:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 19:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 20:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 21:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 22:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 23:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 24:
			((System.Windows.Controls.Button)target).Click += ThemePreset_Click;
			break;
		case 25:
			((System.Windows.Controls.Button)target).Click += RandomColors_Click;
			break;
		case 26:
			((System.Windows.Controls.Button)target).Click += ResetColors_Click;
			break;
		case 27:
			((System.Windows.Controls.Button)target).Click += CustomColor_Click;
			break;
		case 28:
			((System.Windows.Controls.Button)target).Click += CustomColor_Click;
			break;
		case 29:
			((System.Windows.Controls.Button)target).Click += CustomColor_Click;
			break;
		case 30:
			((System.Windows.Controls.Button)target).Click += CustomColor_Click;
			break;
		case 31:
			((System.Windows.Controls.Button)target).Click += CustomColor_Click;
			break;
		case 32:
			((System.Windows.Controls.Button)target).Click += CustomColor_Click;
			break;
		case 33:
			((System.Windows.Controls.Button)target).Click += CustomColor_Click;
			break;
		case 34:
			OutlineSlider = (Slider)target;
			break;
		case 35:
			ShadowSlider = (Slider)target;
			break;
		case 36:
			BackgroundOpacitySlider = (Slider)target;
			break;
		case 37:
			OverlayOpacitySlider = (Slider)target;
			break;
		case 38:
			CornerRadiusSlider = (Slider)target;
			break;
		case 39:
			PanelPaddingSlider = (Slider)target;
			break;
		case 40:
			LyricsTab = (TabItem)target;
			break;
		case 41:
			LyricsEmptyText = (TextBlock)target;
			break;
		case 42:
			CurrentTrackPanel = (StackPanel)target;
			break;
		case 43:
			CurrentTrackTitleText = (TextBlock)target;
			break;
		case 44:
			CurrentTrackArtistText = (TextBlock)target;
			break;
		case 45:
			CurrentTrackAlbumText = (TextBlock)target;
			break;
		case 46:
			CurrentTrackDurationText = (TextBlock)target;
			break;
		case 47:
			SpotifyTrackIdText = (TextBlock)target;
			break;
		case 48:
			LyricsSourceText = (TextBlock)target;
			break;
		case 49:
			LrclibIdText = (TextBlock)target;
			break;
		case 50:
			LrclibTitleText = (TextBlock)target;
			break;
		case 51:
			LrclibArtistText = (TextBlock)target;
			break;
		case 52:
			LrclibAlbumText = (TextBlock)target;
			break;
		case 53:
			LrclibDurationText = (TextBlock)target;
			break;
		case 54:
			SelectionModeText = (TextBlock)target;
			break;
		case 55:
			LoadedFromCacheText = (TextBlock)target;
			break;
		case 56:
			LocalLrcStateText = (TextBlock)target;
			break;
		case 57:
			LyricsGuidanceText = (TextBlock)target;
			break;
		case 58:
			ChooseCandidatesButton = (System.Windows.Controls.Button)target;
			ChooseCandidatesButton.Click += ChooseCandidates_Click;
			break;
		case 59:
			ResetManualButton = (System.Windows.Controls.Button)target;
			ResetManualButton.Click += ResetManualSelection_Click;
			break;
		case 60:
			OpenLrclibButton = (System.Windows.Controls.Button)target;
			OpenLrclibButton.Click += OpenCurrentLrclib_Click;
			break;
		case 61:
			LyricsActionStatusText = (TextBlock)target;
			break;
		case 62:
			ChooseLocalLrcButton = (System.Windows.Controls.Button)target;
			ChooseLocalLrcButton.Click += ChooseLocalLrc_Click;
			break;
		case 63:
			LrcFolderPathBox = (System.Windows.Controls.TextBox)target;
			break;
		case 64:
			((System.Windows.Controls.Button)target).Click += OpenLrcFolder_Click;
			break;
		case 65:
			((System.Windows.Controls.Button)target).Click += LyricsOnly_Click;
			break;
		case 66:
			ShowPanelBorderBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 67:
			ShowTrackInfoBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 68:
			ShowPlaybackControlsBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 69:
			ShowProgressBarBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 70:
			BorderThicknessSlider = (Slider)target;
			break;
		case 71:
			AlwaysOnTopBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 72:
			HideWhenPausedBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 73:
			ShowIdleStatusBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 74:
			PlainLyricsFallbackBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 75:
			LockOnStartupBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 76:
			StartWithWindowsBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 77:
			ShortcutsEnabledBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 78:
			PauseEyeAnimationBox = (System.Windows.Controls.CheckBox)target;
			break;
		case 79:
			LanguageBox = (System.Windows.Controls.ComboBox)target;
			break;
		case 80:
			GlobalOffsetSlider = (Slider)target;
			break;
		case 81:
			((System.Windows.Controls.Button)target).Click += Reset_Click;
			break;
		case 82:
			((System.Windows.Controls.Button)target).Click += Cancel_Click;
			break;
		case 83:
			((System.Windows.Controls.Button)target).Click += Save_Click;
			break;
		case 84:
			CurrentColorBox = (System.Windows.Controls.TextBox)target;
			break;
		case 85:
			NextColorBox = (System.Windows.Controls.TextBox)target;
			break;
		case 86:
			OutlineColorBox = (System.Windows.Controls.TextBox)target;
			break;
		case 87:
			ShadowColorBox = (System.Windows.Controls.TextBox)target;
			break;
		case 88:
			BackgroundColorBox = (System.Windows.Controls.TextBox)target;
			break;
		case 89:
			BorderColorBox = (System.Windows.Controls.TextBox)target;
			break;
		case 90:
			UiColorBox = (System.Windows.Controls.TextBox)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
