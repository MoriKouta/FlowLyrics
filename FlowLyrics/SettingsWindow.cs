using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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

	private System.Windows.Controls.Button? _reverseColorsSettingsButton;

	private bool _reverseColors;

	private TextBlock? _versionText;

	private bool _brandingInitialized;

	private bool _reverseColorsControlInitialized;

	private bool _softThemeInitialized;

	private bool _paletteManagerInitialized;

	private bool _behaviorResetInitialized;

	private readonly List<SavedColorPalette> _savedColorPalettes;

	private System.Windows.Controls.TextBox? _paletteNameBox;

	private System.Windows.Controls.ComboBox? _savedPaletteBox;

	private Border? _settingsHeaderBorder;

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
		_savedColorPalettes = settings.SavedColorPalettes.Select(ClonePalette).ToList();
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
			InitializeReverseColorsControl();
			InitializePaletteManager();
			InitializeBehaviorReset();
			ApplySoftSettingsTheme();
			RefreshReverseColorsButton();
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
				InitializeReverseColorsControl();
				InitializePaletteManager();
				InitializeBehaviorReset();
				CaptureLocalizableContent(this);
				ApplyLanguage(_currentLanguage);
				ApplySoftSettingsTheme();
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

	private void InitializeReverseColorsControl()
	{
		if (_reverseColorsControlInitialized)
		{
			return;
		}
		System.Windows.Controls.Button? pickButton = FindVisualChildren<System.Windows.Controls.Button>(this)
			.FirstOrDefault((System.Windows.Controls.Button button) => string.Equals(button.Tag?.ToString(), "UiColorBox", StringComparison.Ordinal));
		if (pickButton == null || VisualTreeHelper.GetParent(pickButton) is not Grid colorGrid || VisualTreeHelper.GetParent(colorGrid) is not StackPanel colorCard)
		{
			return;
		}

		DockPanel reversePanel = new DockPanel
		{
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
			LastChildFill = false,
			Tag = "NoTranslate"
		};
		reversePanel.Children.Add(new TextBlock
		{
			Text = "REVERSE COLORS",
			FontFamily = _englishDotFont,
			FontSize = 10.0,
			FontWeight = FontWeights.Bold,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 12.0, 0.0),
			Tag = "NoTranslate"
		});
		_reverseColorsSettingsButton = CreateSmallFeatureButton();
		_reverseColorsSettingsButton.ToolTip = "Reverse every custom color except Player UI";
		_reverseColorsSettingsButton.Click += delegate
		{
			_reverseColors = !_reverseColors;
			ResultSettings.ReverseColors = _reverseColors;
			ApplySoftSettingsTheme();
			RefreshReverseColorsButton();
			NotifyPreviewChanged();
		};
		reversePanel.Children.Add(_reverseColorsSettingsButton);
		colorCard.Children.Add(reversePanel);
		_reverseColorsControlInitialized = true;
	}

	private StackPanel? GetTabStack(string originalHeader)
	{
		TabItem? tab = SettingsTabs.Items.OfType<TabItem>().FirstOrDefault((TabItem item) =>
			_localizedHeaders.TryGetValue(item, out string? header) && string.Equals(header, originalHeader, StringComparison.Ordinal));
		return tab?.Content is ScrollViewer scroll && scroll.Content is StackPanel stack ? stack : null;
	}

	private void InitializePaletteManager()
	{
		if (_paletteManagerInitialized || GetTabStack("Color") is not StackPanel colorStack)
		{
			return;
		}
		Border card = new Border();
		card.SetResourceReference(FrameworkElement.StyleProperty, "Card");
		StackPanel content = new StackPanel { Tag = "NoTranslate" };
		content.Children.Add(new TextBlock
		{
			Text = "MY PALETTES",
			FontFamily = _englishDotFont,
			FontSize = 18.0,
			FontWeight = FontWeights.Bold,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
			Tag = "NoTranslate"
		});

		WrapPanel saveRow = new WrapPanel { Margin = new Thickness(-4.0, 0.0, 0.0, 4.0) };
		_paletteNameBox = new System.Windows.Controls.TextBox
		{
			Width = 210.0,
			Text = "My Palette",
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
			Margin = new Thickness(4.0)
		};
		saveRow.Children.Add(_paletteNameBox);
		saveRow.Children.Add(CreatePaletteButton("SAVE CURRENT", SaveCurrentPalette_Click));
		content.Children.Add(saveRow);

		WrapPanel manageRow = new WrapPanel { Margin = new Thickness(-4.0, 0.0, 0.0, 0.0) };
		_savedPaletteBox = new System.Windows.Controls.ComboBox
		{
			Width = 210.0,
			Margin = new Thickness(4.0),
			FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
		};
		manageRow.Children.Add(_savedPaletteBox);
		manageRow.Children.Add(CreatePaletteButton("APPLY", ApplySavedPalette_Click));
		manageRow.Children.Add(CreatePaletteButton("DELETE", DeleteSavedPalette_Click));
		manageRow.Children.Add(CreatePaletteButton("EXPORT", ExportPalette_Click));
		manageRow.Children.Add(CreatePaletteButton("IMPORT", ImportPalette_Click));
		content.Children.Add(manageRow);
		card.Child = content;
		colorStack.Children.Insert(Math.Min(1, colorStack.Children.Count), card);
		RefreshSavedPaletteList();
		_paletteManagerInitialized = true;
	}

	private System.Windows.Controls.Button CreatePaletteButton(string text, RoutedEventHandler handler)
	{
		System.Windows.Controls.Button button = new System.Windows.Controls.Button
		{
			Content = text,
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			Tag = "NoTranslate",
			Margin = new Thickness(4.0)
		};
		button.Click += handler;
		return button;
	}

	private SavedColorPalette CaptureCurrentPalette(string name)
	{
		return new SavedColorPalette
		{
			Name = name.Trim(),
			CurrentTextColor = NormalizeColor(CurrentColorBox.Text),
			NextTextColor = NormalizeColor(NextColorBox.Text),
			OutlineColor = NormalizeColor(OutlineColorBox.Text),
			ShadowColor = NormalizeColor(ShadowColorBox.Text),
			BackgroundColor = NormalizeColor(BackgroundColorBox.Text),
			BorderColor = NormalizeColor(BorderColorBox.Text),
			UiColor = NormalizeColor(UiColorBox.Text)
		};
	}

	private static SavedColorPalette ClonePalette(SavedColorPalette palette)
	{
		return new SavedColorPalette
		{
			FormatVersion = palette.FormatVersion,
			Name = palette.Name,
			CurrentTextColor = palette.CurrentTextColor,
			NextTextColor = palette.NextTextColor,
			OutlineColor = palette.OutlineColor,
			ShadowColor = palette.ShadowColor,
			BackgroundColor = palette.BackgroundColor,
			BorderColor = palette.BorderColor,
			UiColor = palette.UiColor
		};
	}

	private void RefreshSavedPaletteList(string? selectName = null)
	{
		if (_savedPaletteBox == null)
		{
			return;
		}
		string? previous = selectName ?? _savedPaletteBox.SelectedItem?.ToString();
		_savedPaletteBox.ItemsSource = _savedColorPalettes.Select((SavedColorPalette palette) => palette.Name).ToArray();
		_savedPaletteBox.SelectedItem = previous;
		if (_savedPaletteBox.SelectedIndex < 0 && _savedPaletteBox.Items.Count > 0)
		{
			_savedPaletteBox.SelectedIndex = 0;
		}
	}

	private SavedColorPalette? SelectedSavedPalette()
	{
		string? name = _savedPaletteBox?.SelectedItem?.ToString();
		return _savedColorPalettes.FirstOrDefault((SavedColorPalette palette) => string.Equals(palette.Name, name, StringComparison.OrdinalIgnoreCase));
	}

	private void SaveCurrentPalette_Click(object sender, RoutedEventArgs e)
	{
		string name = _paletteNameBox?.Text.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(name))
		{
			System.Windows.MessageBox.Show(this, "Enter a palette name.", "FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		try
		{
			SavedColorPalette palette = CaptureCurrentPalette(name);
			int index = _savedColorPalettes.FindIndex((SavedColorPalette item) => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
			if (index >= 0)
			{
				_savedColorPalettes[index] = palette;
			}
			else
			{
				_savedColorPalettes.Add(palette);
			}
			RefreshSavedPaletteList(palette.Name);
			NotifyPreviewChanged();
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(this, ex.Message, "FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void ApplySavedPalette_Click(object sender, RoutedEventArgs e)
	{
		if (SelectedSavedPalette() is SavedColorPalette palette)
		{
			ApplySavedPalette(palette);
		}
	}

	private void ApplySavedPalette(SavedColorPalette palette)
	{
		ValidatePalette(palette);
		_suppressPreview = true;
		CurrentColorBox.Text = palette.CurrentTextColor;
		NextColorBox.Text = palette.NextTextColor;
		OutlineColorBox.Text = palette.OutlineColor;
		ShadowColorBox.Text = palette.ShadowColor;
		BackgroundColorBox.Text = palette.BackgroundColor;
		BorderColorBox.Text = palette.BorderColor;
		UiColorBox.Text = palette.UiColor;
		_suppressPreview = false;
		UpdateAccentColor(UiColorBox.Text);
		NotifyPreviewChanged();
	}

	private void ValidatePalette(SavedColorPalette palette)
	{
		foreach (string color in new[] { palette.CurrentTextColor, palette.NextTextColor, palette.OutlineColor, palette.ShadowColor, palette.BackgroundColor, palette.BorderColor, palette.UiColor })
		{
			ValidateColor(color);
		}
	}

	private void DeleteSavedPalette_Click(object sender, RoutedEventArgs e)
	{
		if (SelectedSavedPalette() is not SavedColorPalette palette || System.Windows.MessageBox.Show(this, $"Delete ‘{palette.Name}’?", "FlowLyrics", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
		{
			return;
		}
		_savedColorPalettes.Remove(palette);
		RefreshSavedPaletteList();
		NotifyPreviewChanged();
	}

	private void ExportPalette_Click(object sender, RoutedEventArgs e)
	{
		if (SelectedSavedPalette() is not SavedColorPalette palette)
		{
			return;
		}
		Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
		{
			Title = "Export FlowLyrics palette",
			Filter = "FlowLyrics palette (*.flowpalette)|*.flowpalette",
			DefaultExt = ".flowpalette",
			AddExtension = true,
			FileName = string.Concat(palette.Name.Where((char c) => !System.IO.Path.GetInvalidFileNameChars().Contains(c)))
		};
		if (dialog.ShowDialog(this) == true)
		{
			File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(palette, new JsonSerializerOptions { WriteIndented = true }));
		}
	}

	private void ImportPalette_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "Import FlowLyrics palette",
			Filter = "FlowLyrics palette (*.flowpalette)|*.flowpalette"
		};
		if (dialog.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			SavedColorPalette palette = JsonSerializer.Deserialize<SavedColorPalette>(File.ReadAllText(dialog.FileName), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("This palette file is empty.");
			palette.Name = string.IsNullOrWhiteSpace(palette.Name) ? System.IO.Path.GetFileNameWithoutExtension(dialog.FileName) : palette.Name.Trim();
			ValidatePalette(palette);
			int index = _savedColorPalettes.FindIndex((SavedColorPalette item) => string.Equals(item.Name, palette.Name, StringComparison.OrdinalIgnoreCase));
			if (index >= 0)
			{
				_savedColorPalettes[index] = palette;
			}
			else
			{
				_savedColorPalettes.Add(palette);
			}
			RefreshSavedPaletteList(palette.Name);
			ApplySavedPalette(palette);
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(this, ex.Message, "FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void InitializeBehaviorReset()
	{
		if (_behaviorResetInitialized || GetTabStack("Behavior") is not StackPanel behaviorStack || base.Content is not Grid root)
		{
			return;
		}
		Border? footer = root.Children.OfType<Border>().FirstOrDefault((Border item) => Grid.GetRow(item) == 2);
		if (footer?.Child is not DockPanel footerPanel)
		{
			return;
		}
		System.Windows.Controls.Button? resetButton = footerPanel.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault();
		if (resetButton == null)
		{
			return;
		}
		footerPanel.Children.Remove(resetButton);
		resetButton.Content = "RESET ALL SETTINGS";
		resetButton.FontFamily = _englishDotFont;
		resetButton.FontSize = 9.0;
		resetButton.Tag = "NoTranslate";
		resetButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
		Border card = new Border();
		card.SetResourceReference(FrameworkElement.StyleProperty, "Card");
		StackPanel panel = new StackPanel { Tag = "NoTranslate" };
		panel.Children.Add(new TextBlock
		{
			Text = "RESET",
			FontFamily = _englishDotFont,
			FontSize = 18.0,
			FontWeight = FontWeights.Bold,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			Tag = "NoTranslate"
		});
		panel.Children.Add(new TextBlock
		{
			Text = "Restore every setting, including saved color palettes.",
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
			Tag = "NoTranslate"
		});
		panel.Children.Add(resetButton);
		card.Child = panel;
		behaviorStack.Children.Add(card);
		_behaviorResetInitialized = true;
	}

	private void StylePresetLabels()
	{
		foreach (System.Windows.Controls.Button button in FindVisualChildren<System.Windows.Controls.Button>(this))
		{
			if (button.Tag is string tag && int.TryParse(tag, out int index) && index >= 0 && index < ColorPalettes.Themes.Count)
			{
				TextBlock? name = FindVisualChildren<TextBlock>(button).LastOrDefault();
				if (name != null)
				{
					name.Foreground = System.Windows.Media.Brushes.Black;
					name.FontFamily = _englishDotFont;
					name.Tag = "NoTranslate";
				}
			}
		}
	}

	private void ApplySoftSettingsTheme()
	{
		bool darkTheme = !_reverseColors;
		SolidColorBrush windowBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(26, 24, 27)
			: System.Windows.Media.Color.FromRgb(229, 231, 228));
		SolidColorBrush cardBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(34, 32, 35)
			: System.Windows.Media.Color.FromRgb(242, 243, 241));
		SolidColorBrush cardBorderBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(67, 63, 67)
			: System.Windows.Media.Color.FromRgb(207, 211, 207));
		SolidColorBrush controlBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(48, 45, 49)
			: System.Windows.Media.Color.FromRgb(220, 223, 220));
		SolidColorBrush controlBorderBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(88, 83, 88)
			: System.Windows.Media.Color.FromRgb(174, 180, 175));
		SolidColorBrush textBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(224, 221, 223)
			: System.Windows.Media.Color.FromRgb(29, 32, 30));
		SolidColorBrush mutedBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(188, 183, 186)
			: System.Windows.Media.Color.FromRgb(58, 63, 60));
		SolidColorBrush headerBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(220, 222, 220)
			: System.Windows.Media.Color.FromRgb(48, 50, 49));
		SolidColorBrush headerTextBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(29, 32, 30)
			: System.Windows.Media.Colors.White);
		SolidColorBrush footerBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(37, 34, 38)
			: System.Windows.Media.Color.FromRgb(218, 221, 218));
		SolidColorBrush inputBrush = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(42, 39, 43)
			: System.Windows.Media.Color.FromRgb(250, 250, 248));
		SolidColorBrush accent = ParseColorBrush(UiColorBox.Text, System.Windows.Media.Color.FromRgb(byte.MaxValue, 138, 61));

		base.Background = windowBrush;
		base.Foreground = textBrush;
		base.Resources["Panel"] = cardBrush;
		base.Resources["Line"] = cardBorderBrush;
		base.Resources["Paper"] = textBrush;
		base.Resources["Muted"] = mutedBrush;

		if (base.Content is Grid root)
		{
			foreach (Border border in root.Children.OfType<Border>())
			{
				int row = Grid.GetRow(border);
				if (row == 0)
				{
					_settingsHeaderBorder = border;
					border.Background = headerBrush;
				}
				else if (row == 2)
				{
					border.Background = footerBrush;
				}
			}
		}

		object? cardStyle = base.Resources["Card"];
		foreach (Border border in FindVisualChildren<Border>(this))
		{
			if ((ReferenceEquals(border.Style, cardStyle) || border.CornerRadius.TopLeft >= 9.0) && !IsInsideHeader(border))
			{
				border.Background = cardBrush;
				border.BorderBrush = cardBorderBrush;
			}
		}

		ControlTemplate softButtonTemplate = CreateSoftButtonTemplate(accent);
		foreach (System.Windows.Controls.Button button in FindVisualChildren<System.Windows.Controls.Button>(this))
		{
			button.Template = softButtonTemplate;
			button.Foreground = textBrush;
			button.BorderBrush = controlBorderBrush;
			if (!IsAccentBrush(button.Background, accent.Color))
			{
				button.Background = controlBrush;
			}
		}

		foreach (TextBlock text in FindVisualChildren<TextBlock>(this))
		{
			if (IsAccentBrush(text.Foreground, accent.Color))
			{
				continue;
			}
			if (IsInsideHeader(text))
			{
				text.Foreground = headerTextBrush;
				continue;
			}
			text.Foreground = text.FontWeight >= FontWeights.SemiBold ? textBrush : mutedBrush;
		}

		foreach (System.Windows.Controls.CheckBox checkBox in FindVisualChildren<System.Windows.Controls.CheckBox>(this))
		{
			checkBox.Foreground = textBrush;
		}
		foreach (System.Windows.Controls.RadioButton radioButton in FindVisualChildren<System.Windows.Controls.RadioButton>(this))
		{
			radioButton.Foreground = textBrush;
			radioButton.Background = controlBrush;
			radioButton.BorderBrush = controlBorderBrush;
		}
		foreach (System.Windows.Controls.TextBox textBox in FindVisualChildren<System.Windows.Controls.TextBox>(this))
		{
			textBox.Foreground = textBrush;
			textBox.Background = inputBrush;
			textBox.BorderBrush = controlBorderBrush;
		}
		foreach (System.Windows.Controls.ComboBox comboBox in FindVisualChildren<System.Windows.Controls.ComboBox>(this))
		{
			comboBox.Foreground = textBrush;
			comboBox.Background = inputBrush;
			comboBox.BorderBrush = controlBorderBrush;
		}
		if (_reverseColors)
		{
			SolidColorBrush languageText = new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 32, 30));
			LanguageBox.Foreground = languageText;
			foreach (System.Windows.Controls.ComboBoxItem item in LanguageBox.Items.OfType<System.Windows.Controls.ComboBoxItem>())
			{
				item.Foreground = languageText;
			}
		}
		ControlTemplate softTabTemplate = CreateSoftTabTemplate(accent);
		foreach (TabItem tab in FindVisualChildren<TabItem>(this))
		{
			tab.Foreground = mutedBrush;
			tab.Background = controlBrush;
			tab.BorderBrush = controlBorderBrush;
			tab.Template = softTabTemplate;
		}

		if (_versionText != null)
		{
			_versionText.Foreground = headerTextBrush;
		}
		StylePresetLabels();
		RefreshReverseColorsButton();
		_softThemeInitialized = true;
		_candidateSearchWindow?.SetAppearance(UiColorBox.Text, _reverseColors);
	}

	private ControlTemplate CreateSoftButtonTemplate(System.Windows.Media.Brush accent)
	{
		FrameworkElementFactory surface = new FrameworkElementFactory(typeof(Border), "Surface");
		surface.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(5.0));
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("ContentTemplate") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		presenter.SetValue(System.Windows.FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
		presenter.SetValue(System.Windows.FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		surface.AppendChild(presenter);

		ControlTemplate template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = surface };
		Trigger hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
		hover.Setters.Add(new Setter(Border.BorderBrushProperty, accent, "Surface"));
		hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.86, "Surface"));
		template.Triggers.Add(hover);
		Trigger pressed = new Trigger { Property = System.Windows.Controls.Button.IsPressedProperty, Value = true };
		pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.68, "Surface"));
		template.Triggers.Add(pressed);
		Trigger disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
		disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.38, "Surface"));
		template.Triggers.Add(disabled);
		return template;
	}

	private static ControlTemplate CreateSoftTabTemplate(System.Windows.Media.Brush accent)
	{
		FrameworkElementFactory surface = new FrameworkElementFactory(typeof(Border), "TabSurface");
		surface.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetValue(Border.BorderThicknessProperty, new Thickness(1.0));
		surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(6.0));
		surface.SetValue(Border.PaddingProperty, new Thickness(18.0, 11.0, 18.0, 11.0));
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
		presenter.SetValue(System.Windows.FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
		presenter.SetValue(System.Windows.FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		surface.AppendChild(presenter);
		ControlTemplate template = new ControlTemplate(typeof(TabItem)) { VisualTree = surface };
		Trigger selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
		selected.Setters.Add(new Setter(Border.BackgroundProperty, accent, "TabSurface"));
		selected.Setters.Add(new Setter(Border.BorderBrushProperty, accent, "TabSurface"));
		selected.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 32, 30))));
		template.Triggers.Add(selected);
		Trigger hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
		hover.Setters.Add(new Setter(Border.BorderBrushProperty, accent, "TabSurface"));
		template.Triggers.Add(hover);
		return template;
	}

	private bool IsInsideHeader(DependencyObject element)
	{
		DependencyObject? current = element;
		while (current != null)
		{
			if (ReferenceEquals(current, _settingsHeaderBorder))
			{
				return true;
			}
			current = VisualTreeHelper.GetParent(current);
		}
		return false;
	}

	private static bool IsAccentBrush(System.Windows.Media.Brush? brush, System.Windows.Media.Color accent)
	{
		return brush is SolidColorBrush solid && solid.Color.R == accent.R && solid.Color.G == accent.G && solid.Color.B == accent.B;
	}

	private static double GetLuminance(System.Windows.Media.Color color)
	{
		return 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
	}

	private static SolidColorBrush ParseColorBrush(string value, System.Windows.Media.Color fallback)
	{
		try
		{
			return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value.Trim()));
		}
		catch
		{
			return new SolidColorBrush(fallback);
		}
	}

	private System.Windows.Controls.Button CreateSmallFeatureButton()
	{
		return new System.Windows.Controls.Button
		{
			Content = "OFF",
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			FontWeight = FontWeights.Bold,
			Padding = new Thickness(9.0, 4.0, 9.0, 4.0),
			Margin = new Thickness(0.0),
			MinWidth = 52.0,
			Tag = "NoTranslate"
		};
	}

	private void RefreshReverseColorsButton()
	{
		if (_reverseColorsSettingsButton == null)
		{
			return;
		}
		_reverseColorsSettingsButton.Content = _reverseColors ? "ON" : "OFF";
		if (_reverseColors)
		{
			_reverseColorsSettingsButton.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "Orange");
			_reverseColorsSettingsButton.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 32, 31));
		}
		else
		{
			bool darkTheme = !_reverseColors;
			_reverseColorsSettingsButton.Background = new SolidColorBrush(darkTheme
				? System.Windows.Media.Color.FromRgb(48, 45, 49)
				: System.Windows.Media.Color.FromRgb(220, 223, 220));
			_reverseColorsSettingsButton.Foreground = new SolidColorBrush(darkTheme
				? System.Windows.Media.Color.FromRgb(224, 221, 223)
				: System.Windows.Media.Color.FromRgb(29, 32, 30));
		}
	}

	public void SetReverseColors(bool enabled)
	{
		_reverseColors = enabled;
		ResultSettings.ReverseColors = enabled;
		ApplySoftSettingsTheme();
		RefreshReverseColorsButton();
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
		_reverseColors = settings.ReverseColors;
		_savedColorPalettes.Clear();
		_savedColorPalettes.AddRange(settings.SavedColorPalettes.Select(ClonePalette));
		RefreshSavedPaletteList();
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
		RefreshReverseColorsButton();
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
				text = ((!flag2) ? T("If the lyrics or timing are incorrect, you can choose another result from LRCLIB.") : T("Plain lyrics scroll continuously. Choose from LRCLIB to look for synchronized lyrics."));
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
		CandidateSearchWindow window = new CandidateSearchWindow(trackInfo, _lyricsService, _currentLanguage, ResultSettings.EnablePlainLyricsFallback, UiColorBox.Text, _reverseColors)
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
		appSettings.ReverseColors = _reverseColors;
		appSettings.SavedColorPalettes = _savedColorPalettes.Select(ClonePalette).ToList();
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
			AppSettings appSettings = new AppSettings();
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
		_reverseColors = false;
		ResultSettings.ReverseColors = false;
		ApplySoftSettingsTheme();
		RefreshReverseColorsButton();
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
			if (_softThemeInitialized)
			{
				ApplySoftSettingsTheme();
			}
			if (_versionText != null)
			{
				_versionText.Foreground = _reverseColors
					? new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 32, 30))
					: System.Windows.Media.Brushes.White;
			}
			_candidateSearchWindow?.SetAppearance(value, _reverseColors);
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
