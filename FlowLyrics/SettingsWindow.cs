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
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Microsoft.Win32;

namespace FlowLyrics;

public class SettingsWindow : Window, IComponentConnector
{
	private readonly AppSettings _originalSettings;

	private readonly string _lrcDirectory;

	private readonly LyricsService _lyricsService;

	private readonly PersonalSyncStore _personalSyncStore;

	private readonly MediaSessionService _mediaSessionService;

	private readonly Func<PlaybackSnapshot?> _currentSnapshotProvider;

	private readonly Func<TrackInfo?> _currentTrackProvider;

	private readonly Func<LyricsLookupResult?> _lookupProvider;

	private readonly Func<PersonalSyncDiagnosticSnapshot?> _personalSyncDiagnosticsProvider;

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

	private readonly List<System.Windows.Controls.Button> _presetButtons = new List<System.Windows.Controls.Button>();

	private readonly List<System.Windows.Controls.RadioButton> _alignmentChoices = new List<System.Windows.Controls.RadioButton>();

	private readonly List<System.Windows.Controls.RadioButton> _positionChoices = new List<System.Windows.Controls.RadioButton>();

	private System.Windows.Controls.TextBox? _paletteNameBox;

	private System.Windows.Controls.ComboBox? _savedPaletteBox;

	private System.Windows.Controls.Button? _uiColorPickButton;

	private System.Windows.Controls.CheckBox? _plainLyricsAutoScrollBox;

	private System.Windows.Controls.CheckBox? _showAllLyricsBox;

	private bool _textControlsInitialized;

	private Style? _compactComboBoxStyle;

	private Style? _compactComboBoxItemStyle;

	private Style? _faderScrollBarStyle;

	private Border? _settingsHeaderBorder;

	private bool _mediaSessionControlsInitialized;

	private bool _updatingMediaSessionControls;

	private System.Windows.Controls.ComboBox? _playbackSourceBox;

	private StackPanel? _ignoredMediaSourcesPanel;

	private ToggleButton? _ignoredMediaSourcesToggle;

	private Canvas? _ignoredMediaSourcesGlyph;

	private TextBlock? _ignoredMediaSourcesLabel;

	private TextBlock? _mediaSessionStatusText;

	private readonly List<System.Windows.Controls.CheckBox> _ignoredMediaSourceBoxes = new();

	private IReadOnlyList<MediaSessionInfo> _detectedMediaSessions = Array.Empty<MediaSessionInfo>();

	private MediaSessionDiagnosticsWindow? _mediaSessionDiagnosticsWindow;

	private System.Windows.Controls.Button? _lyricsOnlyButton;

	private bool _lyricsOnlyMode;

	private bool _personalSyncProfilesInitialized;

	private StackPanel? _personalSyncProfilesPanel;

	private PersonalSyncManagerWindow? _personalSyncManagerWindow;

	private bool _glowControlsInitialized;

	private System.Windows.Controls.TextBox? _glowColorBox;

	private Slider? _glowStrengthSlider;

	private Slider? _glowOpacitySlider;

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

	public SettingsWindow(AppSettings settings, string lrcDirectory, LyricsService lyricsService, MediaSessionService mediaSessionService, PersonalSyncStore personalSyncStore, Func<PlaybackSnapshot?> currentSnapshotProvider, Func<TrackInfo?> currentTrackProvider, Func<LyricsLookupResult?> lookupProvider, Func<PersonalSyncDiagnosticSnapshot?> personalSyncDiagnosticsProvider, Func<Task> reloadCurrentTrack)
	{
		InitializeComponent();
		_englishDotFont = (System.Windows.Media.FontFamily)base.Resources["DotFont"];
		InitializeLyricsOnlyControl();
		SettingsTabs.Items.Remove(LyricsTab);
		SettingsTabs.Items.Insert(0, LyricsTab);
		CaptureLocalizableContent(this);
		_lrcDirectory = lrcDirectory;
		_lyricsService = lyricsService;
		_mediaSessionService = mediaSessionService;
		_personalSyncStore = personalSyncStore;
		_currentSnapshotProvider = currentSnapshotProvider;
		_currentTrackProvider = currentTrackProvider;
		_lookupProvider = lookupProvider;
		_personalSyncDiagnosticsProvider = personalSyncDiagnosticsProvider;
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
		base.Loaded += async delegate
		{
			DisableDialogOnlyButtons();
			InitializeBranding();
			InitializeTextControls();
			InitializeReverseColorsControl();
			InitializePaletteManager();
			InitializeGlowControls();
			InitializeMediaSessionControls();
			InitializePersonalSyncProfiles();
			InitializeBehaviorReset();
			InitializeCompactComboBoxes();
			ApplySoftSettingsTheme();
			RefreshReverseColorsButton();
			UpdateAccentColor(ResultSettings.UiColor);
			CaptureLocalizableContent(this);
			ApplyLanguage(_currentLanguage);
			RefreshLyricsTab();
			await RefreshMediaSessionsAsync();
		};
		_mediaSessionService.SessionsChanged += MediaSessionService_SessionsChanged;
		_personalSyncStore.ProfilesChanged += PersonalSyncStore_ProfilesChanged;
		base.Closed += delegate
		{
			_mediaSessionService.SessionsChanged -= MediaSessionService_SessionsChanged;
			_personalSyncStore.ProfilesChanged -= PersonalSyncStore_ProfilesChanged;
			_mediaSessionDiagnosticsWindow?.Close();
			_mediaSessionDiagnosticsWindow = null;
			_personalSyncManagerWindow?.Close();
			_personalSyncManagerWindow = null;
		};
	}

	private void InitializeLyricsOnlyControl()
	{
		if (_lyricsOnlyButton != null || ShowPanelBorderBox.Parent is not WrapPanel componentOptions || componentOptions.Parent is not StackPanel components)
		{
			return;
		}
		DockPanel? titleRow = components.Children.OfType<DockPanel>().FirstOrDefault();
		System.Windows.Controls.Button? button = titleRow?.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault()
			?? components.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault(candidate => string.Equals(candidate.Content?.ToString(), "LYRICS ONLY", StringComparison.OrdinalIgnoreCase));
		Grid? borderWidthRow = BorderThicknessSlider.Parent as Grid;
		if (titleRow == null || button == null || borderWidthRow == null)
		{
			return;
		}

		if (button.Parent is System.Windows.Controls.Panel owner && !ReferenceEquals(owner, components))
		{
			owner.Children.Remove(button);
		}
		else if (ReferenceEquals(button.Parent, components))
		{
			components.Children.Remove(button);
		}
		int borderWidthIndex = components.Children.IndexOf(borderWidthRow);
		components.Children.Insert(Math.Max(0, borderWidthIndex), button);
		button.Content = "LYRICS ONLY";
		button.FontFamily = _englishDotFont;
		button.FontSize = 9.0;
		button.FontWeight = FontWeights.Bold;
		button.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
		button.Padding = new Thickness(12.0, 6.0, 12.0, 6.0);
		button.Margin = new Thickness(0.0, 9.0, 0.0, 2.0);
		button.Tag = "NoTranslate";
		button.ToolTip = "Show only lyrics without changing the saved component choices";
		_lyricsOnlyButton = button;
	}

	private void RefreshLyricsOnlyControl()
	{
		bool controlsEnabled = !_lyricsOnlyMode;
		foreach (System.Windows.Controls.CheckBox box in new[] { ShowPanelBorderBox, ShowTrackInfoBox, ShowPlaybackControlsBox, ShowProgressBarBox })
		{
			box.IsEnabled = controlsEnabled;
			box.Opacity = controlsEnabled ? 1.0 : 0.38;
		}
		if (BorderThicknessSlider.Parent is FrameworkElement borderWidthRow)
		{
			borderWidthRow.IsEnabled = controlsEnabled;
			borderWidthRow.Opacity = controlsEnabled ? 1.0 : 0.38;
		}
		if (BackgroundOpacitySlider.Parent is FrameworkElement backgroundOpacityRow)
		{
			backgroundOpacityRow.IsEnabled = controlsEnabled;
			backgroundOpacityRow.Opacity = controlsEnabled ? 1.0 : 0.38;
		}
		if (_lyricsOnlyButton != null)
		{
			_lyricsOnlyButton.Content = _lyricsOnlyMode ? "●  LYRICS ONLY" : "LYRICS ONLY";
			_lyricsOnlyButton.BorderThickness = new Thickness(1.0);
			if (_lyricsOnlyMode)
			{
				_lyricsOnlyButton.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "Orange");
				_lyricsOnlyButton.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "Orange");
				_lyricsOnlyButton.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 32, 30));
			}
			else
			{
				_lyricsOnlyButton.Background = System.Windows.Media.Brushes.Transparent;
				_lyricsOnlyButton.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "Orange");
				_lyricsOnlyButton.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Orange");
			}
		}
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
				InitializeTextControls();
				InitializeReverseColorsControl();
				InitializePaletteManager();
				InitializeMediaSessionControls();
				InitializeBehaviorReset();
				InitializeCompactComboBoxes();
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
		System.Windows.Controls.Button? pickButton = _uiColorPickButton ?? FindVisualChildren<System.Windows.Controls.Button>(this)
			.FirstOrDefault((System.Windows.Controls.Button button) => string.Equals(button.Tag?.ToString(), "UiColorBox", StringComparison.Ordinal));
		if (pickButton == null || pickButton.Parent is not Grid colorGrid || colorGrid.Parent is not StackPanel colorCard)
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
			string.Equals(item.Header?.ToString(), originalHeader, StringComparison.Ordinal)
			|| (_localizedHeaders.TryGetValue(item, out string? header) && string.Equals(header, originalHeader, StringComparison.Ordinal)));
		tab ??= originalHeader switch
		{
			"Lyrics" => LyricsTab,
			"Text" => SettingsTabs.Items.OfType<TabItem>().ElementAtOrDefault(1),
			"Color" => SettingsTabs.Items.OfType<TabItem>().ElementAtOrDefault(2),
			"Behavior" => SettingsTabs.Items.OfType<TabItem>().ElementAtOrDefault(3),
			_ => null
		};
		return tab?.Content is ScrollViewer scroll && scroll.Content is StackPanel stack ? stack : null;
	}

	private void InitializeTextControls()
	{
		if (_textControlsInitialized)
		{
			RefreshTextControlLabels();
			RefreshChoiceSelectors();
			return;
		}

		if (DisplayLinesSlider.Parent is Grid lineGrid)
		{
			int row = Grid.GetRow(DisplayLinesSlider);
			foreach (UIElement child in lineGrid.Children.Cast<UIElement>().Where((UIElement child) => Grid.GetRow(child) == row).ToArray())
			{
				child.Visibility = Visibility.Collapsed;
			}
			if (row >= 0 && row < lineGrid.RowDefinitions.Count)
			{
				lineGrid.RowDefinitions[row].Height = new GridLength(0.0);
			}
			if (lineGrid.Parent is StackPanel lineCard)
			{
				WrapPanel plainLyricsOptions = new WrapPanel
				{
					Margin = new Thickness(0.0, 7.0, 0.0, 0.0),
					Tag = "NoTranslate"
				};
				_plainLyricsAutoScrollBox = new System.Windows.Controls.CheckBox
				{
					IsChecked = ResultSettings.PlainLyricsAutoScroll,
					Margin = new Thickness(0.0, 0.0, 18.0, 0.0),
					Tag = "NoTranslate"
				};
				_plainLyricsAutoScrollBox.Checked += PlainLyricsAutoScroll_Changed;
				_plainLyricsAutoScrollBox.Unchecked += PlainLyricsAutoScroll_Changed;
				_showAllLyricsBox = new System.Windows.Controls.CheckBox
				{
					IsChecked = ResultSettings.ShowAllLyrics,
					Margin = new Thickness(0.0),
					Tag = "NoTranslate"
				};
				_showAllLyricsBox.Checked += PlainLyricsAutoScroll_Changed;
				_showAllLyricsBox.Unchecked += PlainLyricsAutoScroll_Changed;
				plainLyricsOptions.Children.Add(_plainLyricsAutoScrollBox);
				plainLyricsOptions.Children.Add(_showAllLyricsBox);
				lineCard.Children.Add(plainLyricsOptions);
			}
		}

		InitializeChoiceSelector(AlignmentBox, _alignmentChoices, "TextAlignment", new[] { "Left", "Center", "Right" });
		InitializeChoiceSelector(CurrentPositionBox, _positionChoices, "CurrentLinePosition", new[] { "Top", "Center", "Bottom" });
		_textControlsInitialized = true;
		RefreshTextControlLabels();
		RefreshChoiceSelectors();
	}

	private void InitializeChoiceSelector(System.Windows.Controls.ComboBox source, List<System.Windows.Controls.RadioButton> choices, string groupName, IEnumerable<string> values)
	{
		if (source.Parent is not Grid grid)
		{
			return;
		}
		WrapPanel panel = new WrapPanel
		{
			Margin = new Thickness(5.0, 3.0, 5.0, 3.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetRow(panel, Grid.GetRow(source));
		Grid.SetColumn(panel, Grid.GetColumn(source));
		Grid.SetColumnSpan(panel, Math.Max(1, Grid.GetColumnSpan(source)));
		foreach (string value in values)
		{
			System.Windows.Controls.RadioButton choice = new System.Windows.Controls.RadioButton
			{
				Tag = value,
				GroupName = groupName,
				Content = T(value),
				FontFamily = _englishDotFont,
				FontSize = 9.0,
				Margin = new Thickness(0.0, 0.0, 7.0, 0.0)
			};
			choice.Checked += delegate
			{
				SelectItemByTag(source, value);
				NotifyPreviewChanged();
			};
			choices.Add(choice);
			panel.Children.Add(choice);
		}
		source.Visibility = Visibility.Collapsed;
		grid.Children.Add(panel);
	}

	private void PlainLyricsAutoScroll_Changed(object sender, RoutedEventArgs e)
	{
		NotifyPreviewChanged();
	}

	private void InitializeCompactComboBoxes()
	{
		if (_compactComboBoxStyle == null || _compactComboBoxItemStyle == null)
		{
			const string xaml = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style x:Key="FlowDropDownItem" TargetType="{x:Type ComboBoxItem}">
    <Setter Property="Foreground" Value="{DynamicResource DropDownText}" />
    <Setter Property="FontFamily" Value="{DynamicResource DotFont}" />
    <Setter Property="FontSize" Value="11" />
    <Setter Property="Padding" Value="0" />
    <Setter Property="HorizontalContentAlignment" Value="Stretch" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type ComboBoxItem}">
          <Border x:Name="ItemSurface" MinHeight="34" Background="Transparent"
                  BorderBrush="{DynamicResource DropDownDivider}" BorderThickness="0,0,0,1"
                  Padding="12,7" SnapsToDevicePixels="True">
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="18" />
                <ColumnDefinition Width="*" />
              </Grid.ColumnDefinitions>
              <Ellipse x:Name="Indicator" Width="6" Height="6" HorizontalAlignment="Left"
                       VerticalAlignment="Center" Fill="Transparent"
                       Stroke="{DynamicResource DropDownText}" StrokeThickness="1" />
              <ContentPresenter Grid.Column="1" VerticalAlignment="Center"
                                HorizontalAlignment="Left" />
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="ItemSurface" Property="Background" Value="{DynamicResource DropDownHover}" />
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="ItemSurface" Property="Background" Value="{DynamicResource DropDownSelected}" />
              <Setter TargetName="Indicator" Property="Fill" Value="{DynamicResource Orange}" />
              <Setter TargetName="Indicator" Property="Stroke" Value="{DynamicResource Orange}" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="ItemSurface" Property="Opacity" Value="0.38" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key="FlowDropDown" TargetType="{x:Type ComboBox}">
    <Setter Property="Foreground" Value="{DynamicResource DropDownText}" />
    <Setter Property="Background" Value="{DynamicResource InputSurface}" />
    <Setter Property="BorderBrush" Value="{DynamicResource ControlBorder}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="MinHeight" Value="34" />
    <Setter Property="Padding" Value="12,0,46,0" />
    <Setter Property="FontFamily" Value="{DynamicResource DotFont}" />
    <Setter Property="FontSize" Value="11" />
    <Setter Property="MaxDropDownHeight" Value="288" />
    <Setter Property="ScrollViewer.CanContentScroll" Value="True" />
    <Setter Property="ItemContainerStyle" Value="{StaticResource FlowDropDownItem}" />
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="{x:Type ComboBox}">
          <Grid>
            <Border x:Name="ClosedSurface" MinHeight="34"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="8" SnapsToDevicePixels="True">
              <Grid>
                <ToggleButton x:Name="DropDownToggle" Focusable="False" ClickMode="Press"
                              IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                  <ToggleButton.Template>
                    <ControlTemplate TargetType="{x:Type ToggleButton}">
                      <Border Background="Transparent" />
                    </ControlTemplate>
                  </ToggleButton.Template>
                </ToggleButton>
				<Ellipse Width="6" Height="6" HorizontalAlignment="Left" VerticalAlignment="Center"
				         Margin="12,0,0,0" Fill="{DynamicResource Orange}" />
				<ContentPresenter x:Name="ContentSite" Margin="28,0,29,0"
				                  VerticalAlignment="Center" HorizontalAlignment="Left"
				                  IsHitTestVisible="False"
				                  Content="{TemplateBinding SelectionBoxItem}"
				                  ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
				                  ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}" />
				<TextBox x:Name="PART_EditableTextBox" Margin="25,2,29,2" Padding="3,0"
				         VerticalContentAlignment="Center" Visibility="Collapsed"
				         Background="Transparent" BorderThickness="0"
				         Foreground="{DynamicResource DropDownText}"
				         FontFamily="{TemplateBinding FontFamily}" FontSize="{TemplateBinding FontSize}"
				         IsReadOnly="{TemplateBinding IsReadOnly}" />
				<Path x:Name="Arrow" Width="9" Height="5" HorizontalAlignment="Right"
                      VerticalAlignment="Center" Margin="0,0,12,0" Stretch="Fill"
                      Stroke="{DynamicResource DropDownText}" StrokeThickness="1.5"
                      StrokeStartLineCap="Round" StrokeEndLineCap="Round"
                      Data="M0,0 L4.5,4 L9,0" />
              </Grid>
            </Border>
            <Popup x:Name="PART_Popup" Placement="Bottom" AllowsTransparency="True"
                   PopupAnimation="Fade" Focusable="False" IsOpen="{TemplateBinding IsDropDownOpen}">
              <Border x:Name="PopupSurface" Margin="0,3,0,0" Padding="0"
                      MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"
                      MaxHeight="{TemplateBinding MaxDropDownHeight}"
                      Background="{DynamicResource InputSurface}"
                      BorderBrush="{DynamicResource ControlBorder}" BorderThickness="1"
                      CornerRadius="8" SnapsToDevicePixels="True">
                <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled"
                              CanContentScroll="True">
                  <ItemsPresenter />
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="ClosedSurface" Property="BorderBrush" Value="{DynamicResource Orange}" />
            </Trigger>
            <Trigger Property="IsKeyboardFocusWithin" Value="True">
              <Setter TargetName="ClosedSurface" Property="BorderBrush" Value="{DynamicResource Orange}" />
            </Trigger>
            <Trigger Property="IsDropDownOpen" Value="True">
              <Setter TargetName="ClosedSurface" Property="BorderBrush" Value="{DynamicResource Orange}" />
              <Setter TargetName="Arrow" Property="Data" Value="M0,4 L4.5,0 L9,4" />
            </Trigger>
            <Trigger Property="IsEditable" Value="True">
              <Setter TargetName="ContentSite" Property="Visibility" Value="Collapsed" />
              <Setter TargetName="PART_EditableTextBox" Property="Visibility" Value="Visible" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="ClosedSurface" Property="Opacity" Value="0.38" />
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>
""";
			ResourceDictionary styles = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml);
			_compactComboBoxStyle = (Style)styles["FlowDropDown"];
			_compactComboBoxItemStyle = (Style)styles["FlowDropDownItem"];
		}

		foreach (System.Windows.Controls.ComboBox comboBox in FindVisualChildren<System.Windows.Controls.ComboBox>(this))
		{
			comboBox.Style = _compactComboBoxStyle;
			comboBox.ItemContainerStyle = _compactComboBoxItemStyle;
		}
	}

	private void RefreshTextControlLabels()
	{
		if (_plainLyricsAutoScrollBox != null)
		{
			_plainLyricsAutoScrollBox.Content = T("Auto scroll plain lyrics");
		}
		if (_showAllLyricsBox != null)
		{
			_showAllLyricsBox.Content = T("Show all lyrics");
		}
		foreach (System.Windows.Controls.RadioButton choice in _alignmentChoices.Concat(_positionChoices))
		{
			choice.Content = T(choice.Tag?.ToString() ?? string.Empty);
		}
	}

	private void RefreshChoiceSelectors()
	{
		string alignment = GetSelectedTag(AlignmentBox, "Left");
		string position = GetSelectedTag(CurrentPositionBox, "Center");
		foreach (System.Windows.Controls.RadioButton choice in _alignmentChoices)
		{
			choice.IsChecked = string.Equals(choice.Tag?.ToString(), alignment, StringComparison.OrdinalIgnoreCase);
		}
		foreach (System.Windows.Controls.RadioButton choice in _positionChoices)
		{
			choice.IsChecked = string.Equals(choice.Tag?.ToString(), position, StringComparison.OrdinalIgnoreCase);
		}
	}

	private void InitializePaletteManager()
	{
		if (_paletteManagerInitialized)
		{
			return;
		}
		System.Windows.Controls.Button? pickButton = _uiColorPickButton ?? FindVisualChildren<System.Windows.Controls.Button>(this)
			.FirstOrDefault((System.Windows.Controls.Button button) => string.Equals(button.Tag?.ToString(), "UiColorBox", StringComparison.Ordinal));
		if (pickButton == null || pickButton.Parent is not Grid colorGrid || colorGrid.Parent is not StackPanel colorCard)
		{
			return;
		}
		StackPanel content = new StackPanel { Tag = "NoTranslate" };
		content.Children.Add(new TextBlock
		{
			Text = "MY PALETTES",
			FontFamily = _englishDotFont,
			FontSize = 13.0,
			FontWeight = FontWeights.Bold,
			Margin = new Thickness(0.0, 16.0, 0.0, 8.0),
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
		int insertIndex = Math.Min(colorCard.Children.Count, colorCard.Children.IndexOf(colorGrid) + 1);
		colorCard.Children.Insert(insertIndex, content);
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

	private void InitializeGlowControls()
	{
		if (_glowControlsInitialized)
		{
			return;
		}

		System.Windows.Controls.Button? uiPickButton = _uiColorPickButton ?? FindVisualChildren<System.Windows.Controls.Button>(this)
			.FirstOrDefault(button => string.Equals(button.Tag?.ToString(), "UiColorBox", StringComparison.Ordinal));
		if (uiPickButton?.Parent is not Grid colorGrid || OutlineSlider.Parent is not Grid effectsGrid || effectsGrid.Parent is not StackPanel effectsCard)
		{
			return;
		}

		_glowColorBox = new System.Windows.Controls.TextBox
		{
			Text = ResultSettings.GlowColor,
			Visibility = Visibility.Collapsed,
			Tag = "NoTranslate"
		};
		_glowStrengthSlider = new Slider { Minimum = 0.0, Maximum = 40.0, TickFrequency = 0.5, Value = ResultSettings.GlowStrength };
		_glowOpacitySlider = new Slider { Minimum = 0.0, Maximum = 1.0, TickFrequency = 0.05, Value = ResultSettings.GlowOpacity };

		int glowColorRow = colorGrid.RowDefinitions.Count;
		colorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		TextBlock glowColorLabel = CreateFieldLabel("Glow");
		Border glowSwatch = new() { Margin = new Thickness(4.0), CornerRadius = new CornerRadius(3.0) };
		glowSwatch.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(System.Windows.Controls.TextBox.Text)) { Source = _glowColorBox });
		System.Windows.Controls.Button glowPick = new()
		{
			Content = "Pick",
			Tag = "GlowColorBox",
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left
		};
		glowPick.Click += CustomColor_Click;
		Grid.SetRow(glowColorLabel, glowColorRow);
		Grid.SetRow(glowSwatch, glowColorRow);
		Grid.SetColumn(glowSwatch, 1);
		Grid.SetRow(glowPick, glowColorRow);
		Grid.SetColumn(glowPick, 2);
		colorGrid.Children.Add(glowColorLabel);
		colorGrid.Children.Add(glowSwatch);
		colorGrid.Children.Add(glowPick);

		int effectsIndex = effectsCard.Children.IndexOf(effectsGrid);
		Grid textEffectsGrid = CreateThreeColumnGrid();
		MoveSettingsRow(effectsGrid, 0, textEffectsGrid, 0);
		MoveSettingsRow(effectsGrid, 1, textEffectsGrid, 1);
		AddSettingsRow(textEffectsGrid, 2, "Glow Strength", _glowStrengthSlider, "{0:0.0}px");
		AddSettingsRow(textEffectsGrid, 3, "Glow Opacity", _glowOpacitySlider, "{0:P0}");

		Grid surfaceGrid = CreateThreeColumnGrid();
		MoveSettingsRow(effectsGrid, 2, surfaceGrid, 0);
		MoveSettingsRow(effectsGrid, 3, surfaceGrid, 1);
		MoveSettingsRow(effectsGrid, 4, surfaceGrid, 2);
		MoveSettingsRow(effectsGrid, 5, surfaceGrid, 3);
		effectsCard.Children.Remove(effectsGrid);
		TextBlock? sectionTitle = effectsCard.Children.OfType<TextBlock>().FirstOrDefault();
		if (sectionTitle != null)
		{
			sectionTitle.Text = "Text Effects";
			sectionTitle.Tag = "NoTranslate";
		}
		effectsCard.Children.Insert(Math.Max(0, effectsIndex), textEffectsGrid);
		TextBlock surfaceTitle = new()
		{
			Text = "Surface",
			Margin = new Thickness(0.0, 18.0, 0.0, 4.0),
			Tag = "NoTranslate"
		};
		surfaceTitle.SetResourceReference(FrameworkElement.StyleProperty, "SectionTitle");
		effectsCard.Children.Insert(Math.Max(0, effectsIndex) + 1, surfaceTitle);
		effectsCard.Children.Insert(Math.Max(0, effectsIndex) + 2, surfaceGrid);

		_glowColorBox.TextChanged += delegate { NotifyPreviewChanged(); };
		_glowStrengthSlider.ValueChanged += delegate { NotifyPreviewChanged(); };
		_glowOpacitySlider.ValueChanged += delegate { NotifyPreviewChanged(); };
		_glowControlsInitialized = true;
	}

	private static Grid CreateThreeColumnGrid()
	{
		Grid grid = new();
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150.0) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78.0) });
		return grid;
	}

	private static TextBlock CreateFieldLabel(string text)
	{
		TextBlock label = new() { Text = text, Tag = "NoTranslate" };
		label.SetResourceReference(FrameworkElement.StyleProperty, "FieldLabel");
		return label;
	}

	private static void AddSettingsRow(Grid grid, int row, string labelText, Slider slider, string format)
	{
		while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		TextBlock label = CreateFieldLabel(labelText);
		TextBlock value = new()
		{
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Tag = "NoTranslate"
		};
		value.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Slider.Value)) { Source = slider, StringFormat = format });
		Grid.SetRow(label, row);
		Grid.SetRow(slider, row);
		Grid.SetColumn(slider, 1);
		Grid.SetRow(value, row);
		Grid.SetColumn(value, 2);
		grid.Children.Add(label);
		grid.Children.Add(slider);
		grid.Children.Add(value);
	}

	private static void MoveSettingsRow(Grid source, int sourceRow, Grid destination, int destinationRow)
	{
		while (destination.RowDefinitions.Count <= destinationRow) destination.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		foreach (UIElement child in source.Children.Cast<UIElement>().Where(item => Grid.GetRow(item) == sourceRow).ToArray())
		{
			source.Children.Remove(child);
			Grid.SetRow(child, destinationRow);
			destination.Children.Add(child);
		}
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
			GlowColor = NormalizeColor(_glowColorBox?.Text ?? ResultSettings.GlowColor),
			GlowStrength = _glowStrengthSlider?.Value ?? ResultSettings.GlowStrength,
			GlowOpacity = _glowOpacitySlider?.Value ?? ResultSettings.GlowOpacity,
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
			GlowColor = palette.GlowColor,
			GlowStrength = palette.GlowStrength,
			GlowOpacity = palette.GlowOpacity,
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
		if (_glowColorBox != null) _glowColorBox.Text = palette.GlowColor;
		if (_glowStrengthSlider != null) _glowStrengthSlider.Value = Math.Clamp(palette.GlowStrength, 0.0, 40.0);
		if (_glowOpacitySlider != null) _glowOpacitySlider.Value = Math.Clamp(palette.GlowOpacity, 0.0, 1.0);
		BackgroundColorBox.Text = palette.BackgroundColor;
		BorderColorBox.Text = palette.BorderColor;
		UiColorBox.Text = palette.UiColor;
		_suppressPreview = false;
		UpdateAccentColor(UiColorBox.Text);
		NotifyPreviewChanged();
	}

	private void ValidatePalette(SavedColorPalette palette)
	{
		foreach (string color in new[] { palette.CurrentTextColor, palette.NextTextColor, palette.OutlineColor, palette.ShadowColor, palette.GlowColor, palette.BackgroundColor, palette.BorderColor, palette.UiColor })
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

	private void InitializeMediaSessionControls()
	{
		if (_mediaSessionControlsInitialized || GetTabStack("Lyrics") is not StackPanel lyricsStack)
		{
			return;
		}

		Border playerCard = new Border();
		playerCard.SetResourceReference(FrameworkElement.StyleProperty, "Card");
		StackPanel playerContent = new StackPanel();
		TextBlock playerTitle = new TextBlock
		{
			Text = "PLAYER",
			Tag = "NoTranslate"
		};
		playerTitle.SetResourceReference(FrameworkElement.StyleProperty, "SectionTitle");
		playerContent.Children.Add(playerTitle);
		_playbackSourceBox = new System.Windows.Controls.ComboBox
		{
			MinWidth = 250.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		_playbackSourceBox.SelectionChanged += PlaybackSourceBox_SelectionChanged;
		playerContent.Children.Add(_playbackSourceBox);

		_ignoredMediaSourcesGlyph = new Canvas
		{
			Width = 6.0,
			Height = 9.0,
			VerticalAlignment = VerticalAlignment.Center
		};
		_ignoredMediaSourcesLabel = new TextBlock
		{
			Text = "EXCLUDE 0",
			Margin = new Thickness(7.0, 0.0, 0.0, 0.0),
			Foreground = System.Windows.Media.Brushes.White,
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			FontWeight = FontWeights.Bold,
			VerticalAlignment = VerticalAlignment.Center,
			Tag = "NoTranslate"
		};
		StackPanel ignoredToggleContent = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal
		};
		ignoredToggleContent.Children.Add(_ignoredMediaSourcesGlyph);
		ignoredToggleContent.Children.Add(_ignoredMediaSourcesLabel);

		_ignoredMediaSourcesToggle = new ToggleButton
		{
			Content = ignoredToggleContent,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
			HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
			Margin = new Thickness(12.0, 0.0, 0.0, 0.0),
			Padding = new Thickness(0.0, 5.0, 0.0, 5.0),
			Background = System.Windows.Media.Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			FontWeight = FontWeights.Bold,
			Cursor = System.Windows.Input.Cursors.Hand,
			Foreground = System.Windows.Media.Brushes.White,
			FocusVisualStyle = null,
			Template = CreatePlainToggleTemplate(),
			Tag = "NoTranslate"
		};
		playerContent.Children.Add(_ignoredMediaSourcesToggle);

		StackPanel ignoredContent = new StackPanel();
		ignoredContent.Children.Add(new TextBlock
		{
			Text = "Not used by AUTO · Browser = all sessions",
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			Tag = "NoTranslate"
		});
		_ignoredMediaSourcesPanel = new StackPanel();
		ignoredContent.Children.Add(_ignoredMediaSourcesPanel);
		_mediaSessionStatusText = new TextBlock
		{
			TextWrapping = TextWrapping.Wrap,
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			Margin = new Thickness(0.0, 8.0, 0.0, 7.0),
			Tag = "NoTranslate"
		};
		ignoredContent.Children.Add(_mediaSessionStatusText);
		System.Windows.Controls.Button diagnosticsButton = new System.Windows.Controls.Button
		{
			Content = "DIAGNOSTICS",
			FontFamily = _englishDotFont,
			FontSize = 8.5,
			Padding = new Thickness(10.0, 5.0, 10.0, 5.0),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
			Margin = new Thickness(0.0),
			Tag = "NoTranslate"
		};
		diagnosticsButton.Click += OpenMediaSessionDiagnostics_Click;
		ignoredContent.Children.Add(diagnosticsButton);

		Border detailsSurface = new Border
		{
			Child = ignoredContent,
			BorderThickness = new Thickness(0.0),
			Padding = new Thickness(18.0, 2.0, 0.0, 0.0),
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		_ignoredMediaSourcesToggle.Checked += delegate
		{
			detailsSurface.Visibility = Visibility.Visible;
			UpdateIgnoredMediaSourcesToggle();
		};
		_ignoredMediaSourcesToggle.Unchecked += delegate
		{
			detailsSurface.Visibility = Visibility.Collapsed;
			UpdateIgnoredMediaSourcesToggle();
		};
		playerContent.Children.Add(detailsSurface);
		playerCard.Child = playerContent;

		lyricsStack.Children.Insert(0, playerCard);
		_mediaSessionControlsInitialized = true;
		PopulateMediaSessionControls();
	}

	private void InitializePersonalSyncProfiles()
	{
		if (_personalSyncProfilesInitialized || GetTabStack("Lyrics") is not StackPanel lyricsStack)
		{
			return;
		}
		Border card = new();
		card.SetResourceReference(FrameworkElement.StyleProperty, "Card");
		StackPanel content = new();
		content.Children.Add(new TextBlock
		{
			Text = "PERSONAL SYNC",
			FontFamily = _englishDotFont,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44)),
			FontSize = 13.0,
			FontWeight = FontWeights.Bold,
			Tag = "NoTranslate"
		});
		content.Children.Add(new TextBlock
		{
			Text = "Current track timing and saved sync history.",
			Margin = new Thickness(0.0, 4.0, 0.0, 9.0),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 187, 192)),
			TextWrapping = TextWrapping.Wrap,
			Tag = "NoTranslate"
		});
		_personalSyncProfilesPanel = new StackPanel();
		content.Children.Add(_personalSyncProfilesPanel);
		System.Windows.Controls.Button historyButton = CreateProfileButton("ALL SYNC HISTORY");
		historyButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
		historyButton.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		historyButton.Click += delegate { OpenPersonalSyncHistory(); };
		content.Children.Add(historyButton);
		card.Child = content;
		// The Local LRC card is the final static card in this tab.
		lyricsStack.Children.Insert(Math.Max(0, lyricsStack.Children.Count - 1), card);
		_personalSyncProfilesInitialized = true;
		RefreshPersonalSyncProfiles();
	}

	private async void RefreshPersonalSyncProfiles()
	{
		if (_personalSyncProfilesPanel == null) return;
		_personalSyncProfilesPanel.Children.Clear();
		PlaybackSnapshot? snapshot = _currentSnapshotProvider();
		LyricsLookupResult? lookup = _lookupProvider();
		if (snapshot == null || lookup == null)
		{
			_personalSyncProfilesPanel.Children.Add(new TextBlock
			{
				Text = "PLAY A TRACK TO VIEW ITS SYNC",
				FontFamily = _englishDotFont,
				FontSize = 9.0,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 166, 171)),
				Tag = "NoTranslate"
			});
			return;
		}
		PersonalSyncResolution resolution;
		try
		{
			PersonalSyncContext context = PersonalSyncIdentity.Create(snapshot, lookup);
			resolution = await _personalSyncStore.ResolveAsync(context);
		}
		catch { resolution = new PersonalSyncResolution(null, false); }
		PersonalSyncProfile? profile = resolution.Profile;
		if (profile == null)
		{
			_personalSyncProfilesPanel.Children.Add(new TextBlock
			{
				Text = resolution.HasProfileForDifferentLyrics ? "SYNC EXISTS FOR DIFFERENT LYRICS" : "CURRENT TRACK · NOT SYNCED",
				FontFamily = _englishDotFont,
				FontSize = 9.0,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 166, 171)),
				Tag = "NoTranslate"
			});
			return;
		}
		System.Windows.Controls.Button currentProfile = new()
		{
			HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
			Padding = new Thickness(11.0, 9.0, 11.0, 9.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 2.0),
			Tag = "NoTranslate"
		};
		StackPanel body = new();
		body.Children.Add(new TextBlock
		{
			Text = profile.Track.Title + (string.IsNullOrWhiteSpace(profile.Track.Artist) ? string.Empty : " — " + profile.Track.Artist),
			FontWeight = FontWeights.SemiBold,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		body.Children.Add(new TextBlock
		{
			Text = (profile.Scope == PersonalSyncScope.Track ? "ALL PLAYERS" : profile.Source.Source.ToUpperInvariant()) + " · "
				+ (profile.Mode == PersonalSyncMode.Advanced
					? profile.Anchors.Count + " POINT / " + profile.Segments.Count + " HOLD"
					: profile.OffsetSeconds.ToString("+0.0;-0.0;0.0") + " s"),
			FontFamily = _englishDotFont,
			FontSize = 8.5,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(byte.MaxValue, 138, 61)),
			Tag = "NoTranslate"
		});
		currentProfile.Content = body;
		currentProfile.Click += delegate { OpenPersonalSyncHistory(profile.Id); };
		_personalSyncProfilesPanel.Children.Add(currentProfile);
	}

	private void OpenPersonalSyncHistory(Guid? profileId = null)
	{
		if (_personalSyncManagerWindow != null)
		{
			_personalSyncManagerWindow.Activate();
			return;
		}
		_personalSyncManagerWindow = new PersonalSyncManagerWindow(_personalSyncStore, _currentLanguage, profileId) { Owner = this };
		_personalSyncManagerWindow.Closed += delegate { _personalSyncManagerWindow = null; RefreshPersonalSyncProfiles(); };
		_personalSyncManagerWindow.Show();
	}

	private void PersonalSyncStore_ProfilesChanged(object? sender, EventArgs e)
	{
		Dispatcher.BeginInvoke((Action)RefreshPersonalSyncProfiles);
	}

	private System.Windows.Controls.Button CreateProfileButton(string content)
	{
		return new System.Windows.Controls.Button
		{
			Content = content,
			FontFamily = _englishDotFont,
			FontSize = 7.5,
			Padding = new Thickness(8.0, 4.0, 8.0, 4.0),
			Margin = new Thickness(3.0),
			Tag = "NoTranslate"
		};
	}

	private static void NudgePersonalSyncProfile(PersonalSyncProfile profile, double delta)
	{
		profile.OffsetSeconds = Math.Clamp(profile.OffsetSeconds + delta, -120.0, 120.0);
		if (profile.Mode != PersonalSyncMode.Advanced) return;
		foreach (PersonalSyncAnchor anchor in profile.Anchors) anchor.LyricsSeconds = Math.Max(0.0, anchor.LyricsSeconds - delta);
		foreach (PersonalSyncSegment segment in profile.Segments) segment.LyricsTimeSeconds = Math.Max(0.0, segment.LyricsTimeSeconds - delta);
	}

	private async Task RefreshMediaSessionsAsync()
	{
		try
		{
			_detectedMediaSessions = await _mediaSessionService.GetSessionsAsync();
			PopulateMediaSessionControls();
		}
		catch (Exception ex)
		{
			if (_mediaSessionStatusText != null)
			{
				_mediaSessionStatusText.Text = T("Could not read Windows Media Sessions.") + " " + ex.Message;
			}
		}
	}

	private void PopulateMediaSessionControls(string? preferredOverride = null, IEnumerable<string>? ignoredOverride = null, bool useOverride = false)
	{
		if (!_mediaSessionControlsInitialized || _playbackSourceBox == null || _ignoredMediaSourcesPanel == null)
		{
			return;
		}

		string preferred = useOverride ? preferredOverride?.Trim() ?? string.Empty : GetSelectedMediaSourceId();
		if (!useOverride && string.IsNullOrWhiteSpace(preferred)) preferred = ResultSettings.PreferredMediaSourceId;
		HashSet<string> ignored = useOverride
			? new HashSet<string>(ignoredOverride ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
			: GetIgnoredMediaSourceIds().ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (!useOverride)
		{
			foreach (string stored in ResultSettings.IgnoredMediaSourceIds) ignored.Add(stored);
		}
		MediaSessionInfo[] sources = _detectedMediaSessions
			.Where(session => !string.IsNullOrWhiteSpace(session.SourceAppUserModelId))
			.GroupBy(session => session.SourceAppUserModelId, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.OrderByDescending(session => session.IsSelectedByFlowLyrics).ThenByDescending(session => session.IsPlaying).First())
			.OrderBy(session => session.DisplaySourceName, StringComparer.CurrentCultureIgnoreCase)
			.ToArray();

		_updatingMediaSessionControls = true;
		try
		{
			_playbackSourceBox.Items.Clear();
			_playbackSourceBox.Items.Add(CreateSourceComboItem("AUTO", string.Empty, "Automatically follow a stable active session."));
			foreach (MediaSessionInfo source in sources)
			{
				_playbackSourceBox.Items.Add(CreateSourceComboItem(source.DisplaySourceName, source.SourceAppUserModelId, source.SourceAppUserModelId));
			}
			if (!string.IsNullOrWhiteSpace(preferred) && !sources.Any(source => string.Equals(source.SourceAppUserModelId, preferred, StringComparison.OrdinalIgnoreCase)))
			{
				_playbackSourceBox.Items.Add(CreateSourceComboItem(MediaSourceClassifier.GetDisplayName(preferred) + " (" + T("not detected") + ")", preferred, preferred));
			}
			_playbackSourceBox.SelectedItem = _playbackSourceBox.Items.OfType<System.Windows.Controls.ComboBoxItem>()
				.FirstOrDefault(item => string.Equals(item.Tag?.ToString() ?? string.Empty, preferred, StringComparison.OrdinalIgnoreCase))
				?? _playbackSourceBox.Items[0];

			_ignoredMediaSourcesPanel.Children.Clear();
			_ignoredMediaSourceBoxes.Clear();
			string[] ignoredChoices = sources.Select(source => source.SourceAppUserModelId)
				.Concat(ignored)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(id => MediaSourceClassifier.GetDisplayName(id), StringComparer.CurrentCultureIgnoreCase)
				.ToArray();
			if (ignoredChoices.Length == 0)
			{
				_ignoredMediaSourcesPanel.Children.Add(new TextBlock { Text = T("No media sources detected."), TextWrapping = TextWrapping.Wrap });
			}
			foreach (string sourceId in ignoredChoices)
			{
				string displayName = MediaSourceClassifier.GetDisplayName(sourceId);
				System.Windows.Controls.CheckBox box = new System.Windows.Controls.CheckBox
				{
					Content = MediaSourceClassifier.IsBrowser(sourceId) ? displayName + " · " + T("all browser sessions") : displayName,
					Tag = sourceId,
					ToolTip = sourceId,
					IsChecked = ignored.Contains(sourceId),
					Margin = new Thickness(0.0, 2.0, 0.0, 2.0)
				};
				box.Checked += IgnoredMediaSource_Changed;
				box.Unchecked += IgnoredMediaSource_Changed;
				_ignoredMediaSourceBoxes.Add(box);
				_ignoredMediaSourcesPanel.Children.Add(box);
			}
		}
		finally
		{
			_updatingMediaSessionControls = false;
		}

		if (_mediaSessionStatusText != null)
		{
			MediaSessionInfo? selected = _detectedMediaSessions.FirstOrDefault(session => session.IsSelectedByFlowLyrics);
			string selectedLabel = selected?.DisplaySourceName ?? T("None");
			bool preferredPresent = string.IsNullOrWhiteSpace(preferred) || sources.Any(source => string.Equals(source.SourceAppUserModelId, preferred, StringComparison.OrdinalIgnoreCase));
			string mode = string.IsNullOrWhiteSpace(preferred)
				? "AUTO"
				: preferredPresent ? "FIXED" : "FALLBACK";
			_mediaSessionStatusText.Text = mode + " · " + selectedLabel + " · " + _detectedMediaSessions.Count;
		}
		UpdateIgnoredMediaSourcesToggle();
	}

	private void UpdateIgnoredMediaSourcesToggle()
	{
		if (_ignoredMediaSourcesToggle == null) return;
		int count = _ignoredMediaSourceBoxes.Count(box => box.IsChecked == true);
		if (_ignoredMediaSourcesLabel != null) _ignoredMediaSourcesLabel.Text = "EXCLUDE " + count;
		UpdateIgnoredMediaSourcesGlyph(_ignoredMediaSourcesToggle.IsChecked == true);
	}

	private void UpdateIgnoredMediaSourcesGlyph(bool expanded)
	{
		if (_ignoredMediaSourcesGlyph == null) return;
		_ignoredMediaSourcesGlyph.Children.Clear();
		(double X, double Y)[] dots = expanded
			? [(0.0, 1.0), (2.0, 1.0), (4.0, 1.0), (1.0, 3.0), (3.0, 3.0), (2.0, 5.0)]
			: [(0.0, 0.0), (0.0, 2.0), (2.0, 2.0), (0.0, 4.0), (2.0, 4.0), (4.0, 4.0), (0.0, 6.0), (2.0, 6.0), (0.0, 8.0)];
		foreach ((double x, double y) in dots)
		{
			System.Windows.Shapes.Rectangle dot = new System.Windows.Shapes.Rectangle
			{
				Width = 1.35,
				Height = 1.35,
				SnapsToDevicePixels = true
			};
			dot.SetResourceReference(Shape.FillProperty, "Orange");
			Canvas.SetLeft(dot, x);
			Canvas.SetTop(dot, y);
			_ignoredMediaSourcesGlyph.Children.Add(dot);
		}
	}

	private static System.Windows.Controls.ComboBoxItem CreateSourceComboItem(string content, string sourceId, string toolTip)
	{
		return new System.Windows.Controls.ComboBoxItem
		{
			Content = content,
			Tag = sourceId,
			ToolTip = toolTip
		};
	}

	private async void PlaybackSourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_updatingMediaSessionControls || _suppressPreview) return;
		string preferred = GetSelectedMediaSourceId();
		_updatingMediaSessionControls = true;
		try
		{
			foreach (System.Windows.Controls.CheckBox box in _ignoredMediaSourceBoxes)
			{
				if (box.IsChecked == true && string.Equals(box.Tag?.ToString(), preferred, StringComparison.OrdinalIgnoreCase))
				{
					box.IsChecked = false;
				}
			}
		}
		finally
		{
			_updatingMediaSessionControls = false;
		}
		NotifyPreviewChanged();
		await RefreshMediaSessionsAsync();
	}

	private async void IgnoredMediaSource_Changed(object sender, RoutedEventArgs e)
	{
		if (_updatingMediaSessionControls || _suppressPreview) return;
		if (sender is System.Windows.Controls.CheckBox { IsChecked: true } box
			&& string.Equals(box.Tag?.ToString(), GetSelectedMediaSourceId(), StringComparison.OrdinalIgnoreCase)
			&& _playbackSourceBox != null)
		{
			_updatingMediaSessionControls = true;
			try
			{
				_playbackSourceBox.SelectedIndex = 0;
			}
			finally
			{
				_updatingMediaSessionControls = false;
			}
		}
		UpdateIgnoredMediaSourcesToggle();
		NotifyPreviewChanged();
		await RefreshMediaSessionsAsync();
	}

	private string GetSelectedMediaSourceId()
	{
		return (_playbackSourceBox?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString()?.Trim() ?? string.Empty;
	}

	private IEnumerable<string> GetIgnoredMediaSourceIds()
	{
		return _ignoredMediaSourceBoxes
			.Where(box => box.IsChecked == true && box.Tag is string sourceId && !string.IsNullOrWhiteSpace(sourceId))
			.Select(box => ((string)box.Tag).Trim());
	}

	private void MediaSessionService_SessionsChanged(object? sender, EventArgs e)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			base.Dispatcher.BeginInvoke((Action)(async () => await RefreshMediaSessionsAsync()));
			return;
		}
		_ = RefreshMediaSessionsAsync();
	}

	private void OpenMediaSessionDiagnostics_Click(object sender, RoutedEventArgs e)
	{
		if (_mediaSessionDiagnosticsWindow != null)
		{
			_mediaSessionDiagnosticsWindow.Activate();
			return;
		}
		_mediaSessionDiagnosticsWindow = new MediaSessionDiagnosticsWindow(_mediaSessionService, _currentSnapshotProvider, _personalSyncDiagnosticsProvider, _currentLanguage)
		{
			Owner = this
		};
		_mediaSessionDiagnosticsWindow.Closed += delegate { _mediaSessionDiagnosticsWindow = null; };
		_mediaSessionDiagnosticsWindow.Show();
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
		resetButton.Content = "RESET";
		resetButton.FontFamily = _englishDotFont;
		resetButton.FontSize = 8.5;
		resetButton.Tag = "NoTranslate";
		resetButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		resetButton.VerticalAlignment = VerticalAlignment.Center;
		resetButton.Padding = new Thickness(12.0, 6.0, 12.0, 6.0);
		resetButton.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		Border card = new Border();
		card.SetResourceReference(FrameworkElement.StyleProperty, "Card");
		card.Padding = new Thickness(16.0, 13.0, 16.0, 13.0);
		DockPanel panel = new DockPanel { LastChildFill = true, Tag = "NoTranslate" };
		DockPanel.SetDock(resetButton, Dock.Right);
		panel.Children.Add(resetButton);
		panel.Children.Add(new TextBlock
		{
			Text = "RESET SETTINGS",
			FontFamily = _englishDotFont,
			FontSize = 11.0,
			FontWeight = FontWeights.Bold,
			VerticalAlignment = VerticalAlignment.Center,
			Tag = "NoTranslate"
		});
		card.Child = panel;
		behaviorStack.Children.Add(card);
		_behaviorResetInitialized = true;
	}

	private void StylePresetLabels()
	{
		System.Windows.Media.Brush nameBrush = _reverseColors
			? System.Windows.Media.Brushes.Black
			: System.Windows.Media.Brushes.White;
		foreach (System.Windows.Controls.Button button in _presetButtons)
		{
			if (button.Content is System.Windows.Controls.Panel content)
			{
				TextBlock? name = content.Children.OfType<TextBlock>().LastOrDefault();
				if (name != null)
				{
					name.Foreground = nameBrush;
					name.FontFamily = _englishDotFont;
					name.FontSize = 9.0;
					name.FontWeight = FontWeights.SemiBold;
					name.Tag = "NoTranslate";
				}
			}
		}
	}

	private void ApplySliderChrome()
	{
		base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
		{
			foreach (Slider slider in FindVisualChildren<Slider>(this))
			{
				slider.ApplyTemplate();
				slider.UpdateLayout();
				foreach (Thumb thumb in FindVisualChildren<Thumb>(slider))
				{
					foreach (Shape shape in FindVisualChildren<Shape>(thumb))
					{
						shape.Stroke = System.Windows.Media.Brushes.Transparent;
						shape.StrokeThickness = 0.0;
					}
				}
				foreach (RepeatButton repeat in FindVisualChildren<RepeatButton>(slider))
				{
					Border? rail = FindVisualChildren<Border>(repeat).FirstOrDefault();
					if (rail == null)
					{
						continue;
					}
					bool decrease = ReferenceEquals(repeat.Command, Slider.DecreaseLarge);
					rail.CornerRadius = new CornerRadius(3.0);
					rail.Margin = slider.Orientation == System.Windows.Controls.Orientation.Vertical
						? (decrease ? new Thickness(0.0, 0.0, 0.0, 2.0) : new Thickness(0.0, 2.0, 0.0, 0.0))
						: (decrease ? new Thickness(0.0, 0.0, 2.0, 0.0) : new Thickness(2.0, 0.0, 0.0, 0.0));
				}
			}
		});
	}

	private void ApplyFaderScrollBars(System.Windows.Media.Brush accent, System.Windows.Media.Brush trackBrush, System.Windows.Media.Brush gripBrush)
	{
		base.Resources["FaderAccentBrush"] = accent;
		base.Resources["FaderTrackBrush"] = trackBrush;
		base.Resources["FaderGripBrush"] = gripBrush;
		Style style = _faderScrollBarStyle ??= CreateFaderScrollBarStyle();
		base.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = style;
		base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
		{
			foreach (System.Windows.Controls.Primitives.ScrollBar scrollBar in FindVisualChildren<System.Windows.Controls.Primitives.ScrollBar>(this))
			{
				scrollBar.Style = style;
			}
		});
	}

	private static Style CreateFaderScrollBarStyle()
	{
		const string xaml = """
<Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       TargetType="{x:Type ScrollBar}">
  <Setter Property="Width" Value="16" />
  <Setter Property="Orientation" Value="Vertical" />
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="{x:Type ScrollBar}">
        <Grid Width="16" Background="Transparent">
          <Border Width="2" HorizontalAlignment="Center" Background="{DynamicResource FaderTrackBrush}" CornerRadius="1" />
          <Track x:Name="PART_Track" Orientation="Vertical" IsDirectionReversed="True">
            <Track.DecreaseRepeatButton>
              <RepeatButton Command="{x:Static ScrollBar.PageUpCommand}" Background="Transparent" BorderThickness="0" Opacity="0.01" />
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command="{x:Static ScrollBar.PageDownCommand}" Background="Transparent" BorderThickness="0" Opacity="0.01" />
            </Track.IncreaseRepeatButton>
            <Track.Thumb>
              <Thumb Width="12" MinHeight="34">
                <Thumb.Template>
                  <ControlTemplate TargetType="{x:Type Thumb}">
                    <Border Background="{DynamicResource FaderAccentBrush}" CornerRadius="3">
                      <Grid Width="7" Height="9" HorizontalAlignment="Center" VerticalAlignment="Center">
                        <Rectangle Height="1" VerticalAlignment="Top" Fill="{DynamicResource FaderGripBrush}" />
                        <Rectangle Height="1" VerticalAlignment="Center" Fill="{DynamicResource FaderGripBrush}" />
                        <Rectangle Height="1" VerticalAlignment="Bottom" Fill="{DynamicResource FaderGripBrush}" />
                      </Grid>
                    </Border>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
""";
		return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
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
		base.Resources["InputSurface"] = inputBrush;
		base.Resources["ControlBorder"] = controlBorderBrush;
		SolidColorBrush selectionText = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(245, 245, 245)
			: System.Windows.Media.Color.FromRgb(20, 20, 20));
		base.Resources["DropDownText"] = selectionText;
		base.Resources["DropDownDivider"] = cardBorderBrush;
		base.Resources["DropDownSelected"] = controlBrush;
		base.Resources["DropDownHover"] = new SolidColorBrush(darkTheme
			? System.Windows.Media.Color.FromRgb(57, 53, 58)
			: System.Windows.Media.Color.FromRgb(235, 237, 234));

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
		InitializeCompactComboBoxes();
		foreach (System.Windows.Controls.ComboBox comboBox in FindVisualChildren<System.Windows.Controls.ComboBox>(this))
		{
			comboBox.Foreground = selectionText;
			comboBox.Background = inputBrush;
			comboBox.BorderBrush = controlBorderBrush;
			comboBox.Style = _compactComboBoxStyle;
			comboBox.ItemContainerStyle = _compactComboBoxItemStyle;
			comboBox.ApplyTemplate();
			foreach (System.Windows.Controls.TextBox editor in FindVisualChildren<System.Windows.Controls.TextBox>(comboBox))
			{
				editor.Foreground = selectionText;
			}
			foreach (TextBlock selection in FindVisualChildren<TextBlock>(comboBox))
			{
				selection.Foreground = selectionText;
			}
			foreach (System.Windows.Controls.ComboBoxItem item in comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>())
			{
				item.Foreground = selectionText;
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
		ApplySliderChrome();
		ApplyFaderScrollBars(accent, controlBorderBrush, darkTheme ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Black);
		RefreshReverseColorsButton();
		RefreshLyricsOnlyControl();
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

	private static ControlTemplate CreatePlainToggleTemplate()
	{
		FrameworkElementFactory surface = new FrameworkElementFactory(typeof(Border));
		surface.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
		presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new System.Windows.Data.Binding("ContentTemplate") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		presenter.SetBinding(System.Windows.FrameworkElement.HorizontalAlignmentProperty, new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		presenter.SetBinding(System.Windows.FrameworkElement.VerticalAlignmentProperty, new System.Windows.Data.Binding("VerticalContentAlignment") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
		surface.AppendChild(presenter);
		return new ControlTemplate(typeof(ToggleButton)) { VisualTree = surface };
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
		if (_glowColorBox != null) _glowColorBox.Text = settings.GlowColor;
		if (_glowStrengthSlider != null) _glowStrengthSlider.Value = settings.GlowStrength;
		if (_glowOpacitySlider != null) _glowOpacitySlider.Value = settings.GlowOpacity;
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
		_lyricsOnlyMode = settings.LyricsOnlyMode;
		AlwaysOnTopBox.IsChecked = settings.AlwaysOnTop;
		HideWhenPausedBox.IsChecked = settings.HideWhenPaused;
		ShowIdleStatusBox.IsChecked = settings.ShowStatusWhenIdle;
		PlainLyricsFallbackBox.IsChecked = settings.EnablePlainLyricsFallback;
		if (_plainLyricsAutoScrollBox != null)
		{
			_plainLyricsAutoScrollBox.IsChecked = settings.PlainLyricsAutoScroll;
		}
		if (_showAllLyricsBox != null)
		{
			_showAllLyricsBox.IsChecked = settings.ShowAllLyrics;
		}
		LockOnStartupBox.IsChecked = settings.LockOnStartup;
		StartWithWindowsBox.IsChecked = settings.StartWithWindows;
		ShortcutsEnabledBox.IsChecked = settings.ShortcutsEnabled;
		PauseEyeAnimationBox.IsChecked = settings.PauseEyeAnimation;
		GlobalOffsetSlider.Value = settings.GlobalLyricsOffsetMs;
		ResultSettings.PreferredMediaSourceId = settings.PreferredMediaSourceId;
		ResultSettings.IgnoredMediaSourceIds = new List<string>(settings.IgnoredMediaSourceIds);
		PopulateMediaSessionControls(settings.PreferredMediaSourceId, settings.IgnoredMediaSourceIds, useOverride: true);
		RefreshReverseColorsButton();
		SelectItemByTag(LanguageBox, settings.Language);
		RefreshChoiceSelectors();
		RefreshLyricsOnlyControl();
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
		RefreshTextControlLabels();
		StylePresetLabels();
		RefreshLyricsTab();
		PopulateMediaSessionControls();
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
		ResetManualButton.Content = T("Clear selection and cache");
		if (flag && !(trackInfo == null))
		{
			LrclibRecord lrclibRecord = lyricsLookupResult?.LrclibRecord;
			CurrentTrackTitleText.Text = trackInfo.Title;
			CurrentTrackArtistText.Text = trackInfo.Artist;
			CurrentTrackAlbumText.Text = trackInfo.Album;
			CurrentTrackDurationText.Text = FormatDuration(trackInfo.Duration.TotalSeconds);
			PlaybackSnapshot? snapshot = _currentSnapshotProvider();
			SpotifyTrackIdText.Text = snapshot == null
				? T("Unavailable")
				: snapshot.SourceDisplayName + (string.IsNullOrWhiteSpace(snapshot.SourceAppUserModelId) ? string.Empty : "\n" + snapshot.SourceAppUserModelId);
			if (SpotifyTrackIdText.Parent is Grid metadataGrid)
			{
				TextBlock? sourceLabel = metadataGrid.Children.OfType<TextBlock>().FirstOrDefault(text =>
					Grid.GetRow(text) == Grid.GetRow(SpotifyTrackIdText) && Grid.GetColumn(text) == 0);
				if (sourceLabel != null) sourceLabel.Text = T("Playback source");
			}
			LyricsSourceText.Text = SourceLabel(lyricsLookupResult);
			LrclibIdText.Text = ((lrclibRecord == null) ? "—" : lrclibRecord.Id.ToString());
			LrclibTitleText.Text = ValueOrDash(lrclibRecord?.TrackName);
			LrclibArtistText.Text = ValueOrDash(lrclibRecord?.ArtistName);
			LrclibAlbumText.Text = ValueOrDash(lrclibRecord?.AlbumName);
			LrclibDurationText.Text = ((lrclibRecord == null) ? "—" : FormatDuration(lrclibRecord.Duration));
			SelectionModeText.Text = ((lyricsLookupResult == null || lyricsLookupResult.Status == LyricsLookupStatus.NoLyrics || lyricsLookupResult.Status == LyricsLookupStatus.CandidatesFound)
				? "—"
				: lyricsLookupResult.Status == LyricsLookupStatus.LrclibBestMatch
					? "Best match"
					: lyricsLookupResult.SelectedManually ? "Manually selected" : "Auto selected");
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
			ResetManualButton.IsEnabled = true;
		}
		if (_personalSyncProfilesInitialized) RefreshPersonalSyncProfiles();
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
			LyricsLookupStatus.LrclibBestMatch => "LRCLIB — Best match",
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
			LyricsActionStatusText.Text = string.Empty;
			await action();
			LyricsActionStatusText.Text = string.Empty;
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

	protected override void OnClosing(CancelEventArgs e)
	{
		// Settings are applied live. Treat the title-bar close button exactly like Close
		// so presentation-only options (including Show All Lyrics) are never rolled back.
		if (!Accepted && TryBuildSettings(out AppSettings settings, showError: false))
		{
			ResultSettings = settings;
			Accepted = true;
		}
		base.OnClosing(e);
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
		ValidateColor(_glowColorBox?.Text ?? ResultSettings.GlowColor);
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
		appSettings.GlowColor = NormalizeColor(_glowColorBox?.Text ?? ResultSettings.GlowColor);
		appSettings.GlowStrength = _glowStrengthSlider?.Value ?? ResultSettings.GlowStrength;
		appSettings.GlowOpacity = _glowOpacitySlider?.Value ?? ResultSettings.GlowOpacity;
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
		appSettings.LyricsOnlyMode = _lyricsOnlyMode;
		appSettings.AlwaysOnTop = AlwaysOnTopBox.IsChecked == true;
		appSettings.HideWhenPaused = HideWhenPausedBox.IsChecked == true;
		appSettings.ShowStatusWhenIdle = ShowIdleStatusBox.IsChecked == true;
		appSettings.EnablePlainLyricsFallback = PlainLyricsFallbackBox.IsChecked == true;
		appSettings.PlainLyricsAutoScroll = _plainLyricsAutoScrollBox?.IsChecked ?? ResultSettings.PlainLyricsAutoScroll;
		appSettings.ShowAllLyrics = _showAllLyricsBox?.IsChecked ?? ResultSettings.ShowAllLyrics;
		appSettings.LockOnStartup = LockOnStartupBox.IsChecked == true;
		appSettings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
		appSettings.ShortcutsEnabled = ShortcutsEnabledBox.IsChecked == true;
		appSettings.PauseEyeAnimation = PauseEyeAnimationBox.IsChecked == true;
		appSettings.PreferredMediaSourceId = _mediaSessionControlsInitialized ? GetSelectedMediaSourceId() : ResultSettings.PreferredMediaSourceId;
		appSettings.IgnoredMediaSourceIds = _mediaSessionControlsInitialized
			? GetIgnoredMediaSourceIds().Distinct(StringComparer.OrdinalIgnoreCase).ToList()
			: new List<string>(ResultSettings.IgnoredMediaSourceIds);
		appSettings.GlobalLyricsOffsetMs = (int)Math.Round(GlobalOffsetSlider.Value);
		appSettings.Language = GetSelectedTag(LanguageBox, "en-US");
		appSettings.Normalize();
		return appSettings;
	}

	private void LyricsOnly_Click(object sender, RoutedEventArgs e)
	{
		_lyricsOnlyMode = !_lyricsOnlyMode;
		RefreshLyricsOnlyControl();
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
		if (_glowColorBox != null) _glowColorBox.Text = theme.Glow;
		if (_glowStrengthSlider != null) _glowStrengthSlider.Value = 14.0;
		if (_glowOpacitySlider != null) _glowOpacitySlider.Value = 0.55;
		BorderColorBox.Text = theme.Border;
		UiColorBox.Text = theme.Ui;
		_suppressPreview = false;
		NotifyPreviewChanged();
	}

	private void CustomColor_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not System.Windows.Controls.Button { Tag: string tag })
		{
			return;
		}
		System.Windows.Controls.TextBox? textBox = string.Equals(tag, "GlowColorBox", StringComparison.Ordinal)
			? _glowColorBox
			: FindName(tag) as System.Windows.Controls.TextBox;
		if (textBox == null) return;
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
		if (_glowColorBox != null) _glowColorBox.Text = appSettings.GlowColor;
		if (_glowStrengthSlider != null) _glowStrengthSlider.Value = appSettings.GlowStrength;
		if (_glowOpacitySlider != null) _glowOpacitySlider.Value = appSettings.GlowOpacity;
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
		case 16:
		case 17:
		case 18:
		case 19:
		case 20:
		case 21:
		case 22:
		case 23:
		case 24:
			System.Windows.Controls.Button presetButton = (System.Windows.Controls.Button)target;
			_presetButtons.Add(presetButton);
			presetButton.Click += ThemePreset_Click;
			break;
		case 25:
			((System.Windows.Controls.Button)target).Click += RandomColors_Click;
			break;
		case 26:
			((System.Windows.Controls.Button)target).Click += ResetColors_Click;
			break;
		case 27:
		case 28:
		case 29:
		case 30:
		case 31:
		case 32:
		case 33:
			System.Windows.Controls.Button colorButton = (System.Windows.Controls.Button)target;
			if (string.Equals(colorButton.Tag?.ToString(), "UiColorBox", StringComparison.Ordinal))
			{
				_uiColorPickButton = colorButton;
			}
			colorButton.Click += CustomColor_Click;
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
			((System.Windows.Controls.Button)target).Visibility = Visibility.Collapsed;
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
