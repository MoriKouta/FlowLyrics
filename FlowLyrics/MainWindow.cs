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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlowLyrics.Controls;
using FlowLyrics.Core;
using FlowLyrics.Interop;
using FlowLyrics.Models;
using FlowLyrics.Services;

namespace FlowLyrics;

public class MainWindow : Window, IComponentConnector
{
	private readonly SettingsService _settingsService = new SettingsService();

	private readonly MediaSessionService _mediaSessionService = new MediaSessionService();

	private readonly SystemVolumeService _systemVolumeService = new SystemVolumeService();

	private readonly LyricsService _lyricsService;

	private readonly PersonalSyncStore _personalSyncStore;

	private readonly System.Windows.Media.FontFamily _englishDotFont;

	private readonly DispatcherTimer _mediaTimer;

	private readonly DispatcherTimer _renderTimer;

	private readonly DispatcherTimer _saveTimer;

	private readonly DispatcherTimer _volumePopupCloseTimer;

	private readonly DispatcherTimer _volumeWriteTimer;

	private readonly DispatcherTimer _localLrcReloadTimer;

	private readonly List<OutlinedText> _lineControls = new List<OutlinedText>();

	private AppSettings _settings;

	private PlaybackSnapshot? _snapshot;

	private LyricsResult? _lyrics;

	private LyricsLookupResult? _lyricsLookup;

	private CancellationTokenSource? _lyricsCancellation;

	private HotkeyService? _hotkeys;

	private TrayIconService? _tray;

	private nint _windowHandle;

	private HwndSource? _windowSource;

	private string? _activeTrackKey;

	private string? _lyricsRetryTrackKey;

	private int _lyricsRetryAttempt;

	private bool _lyricsRetryScheduled;

	private int _lastLineIndex = int.MinValue;

	private int _activeLineIndex = -1;

	private int _visibleFirstLineIndex;

	private double _scrollAnimationStart;

	private double _scrollAnimationTarget;

	private DateTime _scrollAnimationStartedUtc;

	private bool _scrollAnimationActive;

	private bool _mediaPollRunning;

	private bool _isInitialized;

	private bool _isLocked;

	private bool _userHidden;

	private bool _pauseHidden;

	private bool _allowClose;

	private bool? _mousePassThroughEnabled;

	private bool _isSeeking;

	private double? _pendingSeekRatio;

	private bool _seekWasDirectClick;

	private bool _pauseEyesAnimating;

	private DateTime _nextPauseBlinkUtc;

	private bool _updatingVolume;

	private bool _isDirectVolumeDrag;

	private double _lastSessionVolume = 1.0;

	private double? _pendingSessionVolume;

	private string _volumeSourceId = string.Empty;

	private DateTime _volumeLastInsideUtc = DateTime.MinValue;

	private SettingsWindow? _settingsWindow;

	private AppSettings? _settingsBeforeWindow;

	private Action<AppSettings>? _settingsPreviewHandler;

	private Grid? _playbackTimeline;

	private TextBlock? _playbackPositionText;

	private TextBlock? _playbackDurationText;

	private Popup? _seekHoverPopup;

	private TextBlock? _seekHoverText;

	private bool _resizeRecenterPending;

	private bool _isInteractiveResize;

	private bool _resizeRefreshPending;

	private bool _plainLyricsScrollMode;

	private string? _plainLyricsLayoutKey;

	private bool _plainLyricsUserScrollPaused;

	private bool _plainLyricsAutoScrollAnchorActive;

	private double _plainLyricsAutoScrollAnchorOffset;

	private TimeSpan _plainLyricsAutoScrollAnchorPosition;

	private bool _showAllLyrics;

	private string? _fullLyricsLayoutKey;

	private int _lastFullLyricsActiveIndex = int.MinValue;

	private Viewbox? _fullLyricsViewbox;

	private System.Windows.Controls.Button? _reverseColorsButton;

	private FrameworkElement? _reverseColorsIcon;

	private FrameworkElement? _volumeIcon;

	private System.Windows.Media.Color _trackStatusColor = System.Windows.Media.Color.FromRgb(142, 151, 166);

	private System.Windows.Controls.Button? _personalSyncButton;

	private Popup? _personalSyncPopup;

	private TextBlock? _personalSyncOffsetText;

	private TextBlock? _personalSyncHintText;

	private System.Windows.Controls.Button? _personalSyncAlignButton;

	private System.Windows.Controls.Button? _personalSyncUndoButton;

	private System.Windows.Controls.Button? _personalSyncRedoButton;

	private System.Windows.Controls.CheckBox? _personalSyncAllSourcesBox;

	private PersonalSyncProfile? _personalSyncActiveProfile;

	private PersonalSyncProfile? _personalSyncEditingProfile;

	private PersonalSyncResolution _personalSyncResolution = new(null, false);

	private readonly Stack<PersonalSyncProfile> _personalSyncUndo = new();

	private readonly Stack<PersonalSyncProfile> _personalSyncRedo = new();

	private int _personalSyncSelectedLineIndex = -1;

	private string _personalSyncContextKey = string.Empty;

	private bool _personalSyncUpdatingUi;

	private PersonalSyncWindow? _personalSyncAdvancedWindow;

	internal System.Windows.Controls.ContextMenu OverlayMenu;

	internal System.Windows.Controls.MenuItem SettingsMenuItem;

	internal System.Windows.Controls.MenuItem LockMenuItem;

	internal System.Windows.Controls.MenuItem HideMenuItem;

	internal System.Windows.Controls.MenuItem PlaybackMenuItem;

	internal System.Windows.Controls.MenuItem PreviousMenuItem;

	internal System.Windows.Controls.MenuItem PlayPauseMenuItem;

	internal System.Windows.Controls.MenuItem NextMenuItem;

	internal System.Windows.Controls.MenuItem CurrentTrackMenuItem;

	internal System.Windows.Controls.MenuItem ReloadLyricsMenuItem;

	internal System.Windows.Controls.MenuItem ClearLyricsMenuItem;

	internal System.Windows.Controls.MenuItem OffsetMenuItem;

	internal System.Windows.Controls.MenuItem EarlierSmallMenuItem;

	internal System.Windows.Controls.MenuItem EarlierLargeMenuItem;

	internal System.Windows.Controls.MenuItem LaterSmallMenuItem;

	internal System.Windows.Controls.MenuItem LaterLargeMenuItem;

	internal System.Windows.Controls.MenuItem ResetTrackOffsetMenuItem;

	internal System.Windows.Controls.MenuItem ExitMenuItem;

	internal Grid HitTestRoot;

	internal Border OverlayPanel;

	internal RowDefinition HeaderRow;

	internal RowDefinition FooterRow;

	internal Grid HeaderPanel;

	internal StackPanel TrackInfoPanel;

	internal Ellipse StatusDot;

	internal TextBlock TrackStatusText;

	internal TextBlock TrackTitleText;

	internal Grid LyricsPanel;

	internal ScrollViewer LyricsScrollViewer;

	internal StackPanel LyricsStackPanel;

	internal StackPanel FooterPanel;

	internal Slider PlaybackSeekSlider;

	internal Grid ControlBar;

	internal StackPanel LeftControlGroup;

	internal System.Windows.Controls.Button LockButton;

	internal Path LockIcon;

	internal StackPanel TransportControlGroup;

	internal System.Windows.Controls.Button PreviousButton;

	internal System.Windows.Controls.Button PlayPauseButton;

	internal Path PlayPauseIcon;

	internal Grid PauseEyes;

	internal ScaleTransform PauseBlinkTransform;

	internal TranslateTransform PauseLookTransform;

	internal System.Windows.Controls.Button NextButton;

	internal StackPanel RightControlGroup;

	internal System.Windows.Controls.Button VolumeButton;

	internal TextBlock VolumeLabel;

	internal System.Windows.Controls.Button SettingsButton;

	internal Popup VolumePopup;

	internal Border VolumePopupSurface;

	internal Slider VolumeSlider;

	internal Thumb ResizeTopLeft;

	internal Thumb ResizeTopRight;

	internal Thumb ResizeBottomLeft;

	internal Thumb ResizeBottomRight;

	private bool _contentLoaded;

	public MainWindow()
	{
		InitializeComponent();
		ApplyCompactUtilityControlSizing();
		VolumePopup.PlacementTarget = VolumeButton;
		VolumePopup.CustomPopupPlacementCallback = PlaceVolumePopup;
		VolumePopup.Opened += delegate
		{
			base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)AlignVolumePopupToButton);
		};
		_englishDotFont = (System.Windows.Media.FontFamily)base.Resources["DotFont"];
		InitializeVolumeIcon();
		InitializePlaybackTimeline();
		EnsureReverseColorsButton();
		_settings = _settingsService.Load();
		_mediaSessionService.ConfigureSelection(_settings.PreferredMediaSourceId, _settings.IgnoredMediaSourceIds);
		_showAllLyrics = _settings.ShowAllLyrics;
		LocalizationService.SetCurrentLanguage(_settings.Language);
		_lyricsService = new LyricsService(_settingsService.AppDataDirectory);
		_personalSyncStore = new PersonalSyncStore(_settingsService.AppDataDirectory);
		_personalSyncStore.ProfilesChanged += delegate { base.Dispatcher.BeginInvoke((Action)(() => RefreshPersonalSyncResolution(force: true))); };
		InitializePersonalSyncUi();
		_mediaTimer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(550L)
		};
		_mediaTimer.Tick += async delegate
		{
			await PollMediaAsync();
		};
		_renderTimer = new DispatcherTimer(DispatcherPriority.Render)
		{
			Interval = TimeSpan.FromMilliseconds(33L)
		};
		_renderTimer.Tick += delegate
		{
			UpdateSelectiveClickThrough();
			UpdatePauseEyes();
			RenderLyrics();
		};
		_saveTimer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(650L)
		};
		_saveTimer.Tick += async delegate
		{
			_saveTimer.Stop();
			await SaveSettingsSafeAsync();
		};
		_volumePopupCloseTimer = new DispatcherTimer(DispatcherPriority.Input)
		{
			Interval = TimeSpan.FromMilliseconds(45L)
		};
		_volumePopupCloseTimer.Tick += delegate
		{
			if (!VolumePopup.IsOpen)
			{
				_volumePopupCloseTimer.Stop();
			}
			else if (IsMouseOverVolumeControls())
			{
				_volumeLastInsideUtc = DateTime.UtcNow;
			}
			else if (DateTime.UtcNow - _volumeLastInsideUtc >= TimeSpan.FromMilliseconds(150L))
			{
				CloseVolumePopup();
			}
		};
		base.Deactivated += delegate { CloseVolumePopup(); };
		_volumeWriteTimer = new DispatcherTimer(DispatcherPriority.Input)
		{
			Interval = TimeSpan.FromMilliseconds(40L)
		};
		_volumeWriteTimer.Tick += delegate
		{
			_volumeWriteTimer.Stop();
			double? pendingSessionVolume = _pendingSessionVolume;
			if (pendingSessionVolume.HasValue && !string.IsNullOrWhiteSpace(_volumeSourceId))
			{
				double valueOrDefault = pendingSessionVolume.GetValueOrDefault();
				_pendingSessionVolume = null;
				_systemVolumeService.TrySetVolume(_volumeSourceId, valueOrDefault);
			}
		};
		_localLrcReloadTimer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromMilliseconds(650L)
		};
		_localLrcReloadTimer.Tick += async delegate
		{
			_localLrcReloadTimer.Stop();
			if (_snapshot != null)
			{
				_lyrics = null;
				ResetLyricsPresentationState();
				await LoadLyricsAsync(_snapshot.Track, forceRefresh: false);
			}
		};
		_lyricsService.LocalLrcFilesChanged += delegate
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				_localLrcReloadTimer.Stop();
				_localLrcReloadTimer.Start();
			});
		};
		base.SourceInitialized += MainWindow_SourceInitialized;
		base.Loaded += MainWindow_Loaded;
		base.Closing += MainWindow_Closing;
		base.LocationChanged += delegate
		{
			CaptureWindowBounds();
		};
		base.SizeChanged += delegate
		{
			CaptureWindowBounds();
			if (_isInteractiveResize)
			{
				_resizeRefreshPending = true;
				return;
			}
			UpdateChromeVisibility();
			if (_showAllLyrics)
			{
				_fullLyricsLayoutKey = null;
				UpdateFullLyricsViewport();
			}
			else if (_plainLyricsScrollMode)
			{
				_plainLyricsLayoutKey = null;
			}
			else
			{
				QueueActiveLineRecentering();
			}
		};
		LyricsScrollViewer.PreviewMouseWheel += LyricsScrollViewer_PreviewMouseWheel;
		LyricsScrollViewer.PreviewTouchMove += LyricsScrollViewer_PreviewTouchMove;
		base.MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
		base.Width = _settings.WindowWidth;
		base.Height = _settings.WindowHeight;
		ApplyVisualSettings();
	}

	public void HideOverlay()
	{
		_volumePopupCloseTimer.Stop();
		VolumePopup.IsOpen = false;
		_userHidden = true;
		RefreshWindowVisibility();
	}

	private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		RestoreWindowPosition();
		_isInitialized = true;
		_tray = new TrayIconService(_settings.Language);
		_tray.ToggleVisibilityRequested += ToggleVisibility;
		_tray.ToggleLockRequested += ToggleLock;
		_tray.SettingsRequested += OpenSettings;
		_tray.ExitRequested += ExitApplication;
		SetLocked(_settings.LockOnStartup || _settings.IsLocked, persist: false);
		base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
		{
			UpdatePlayerButtonBorders();
			UpdateLockButtonVisual();
		});
		_tray.UpdateState(base.IsVisible, _isLocked);
		if (_settings.StartWithWindows)
		{
			StartupService.TrySetEnabled(enabled: true, out string _);
		}
		_mediaTimer.Start();
		_renderTimer.Start();
		await PollMediaAsync();
	}

	private void MainWindow_SourceInitialized(object? sender, EventArgs e)
	{
		_windowHandle = new WindowInteropHelper(this).Handle;
		_windowSource = HwndSource.FromHwnd(_windowHandle);
		_windowSource?.AddHook(WindowMessageHook);
		ApplyWindowStyles();
		ConfigureHotkeys();
	}

	private void ConfigureHotkeys()
	{
		_hotkeys?.Dispose();
		_hotkeys = null;
		if (!_settings.ShortcutsEnabled || _windowHandle == IntPtr.Zero)
		{
			return;
		}
		try
		{
			_hotkeys = new HotkeyService(_windowHandle);
			_hotkeys.ToggleLockRequested += ToggleLock;
			_hotkeys.ToggleVisibilityRequested += ToggleVisibility;
		}
		catch
		{
			_hotkeys = null;
		}
	}

	private void MainWindow_Closing(object? sender, CancelEventArgs e)
	{
		if (!_allowClose)
		{
			e.Cancel = true;
			HideOverlay();
		}
	}

	private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (_isLocked || e.ChangedButton != MouseButton.Left || FindVisualParent<Thumb>(e.OriginalSource as DependencyObject) != null || FindVisualParent<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) != null)
		{
			return;
		}
		try
		{
			DragMove();
		}
		catch (InvalidOperationException)
		{
		}
	}

	private async Task PollMediaAsync()
	{
		if (_mediaPollRunning)
		{
			return;
		}
		_mediaPollRunning = true;
		try
		{
			PlaybackSnapshot? playbackSnapshot = await _mediaSessionService.GetSnapshotAsync();
			if ((object)playbackSnapshot == null)
			{
				ClosePersonalSyncEditor(save: true);
				_snapshot = null;
				_personalSyncActiveProfile = null;
				_personalSyncContextKey = string.Empty;
				UpdatePersonalSyncButton();
				_pauseHidden = false;
				UpdatePlaybackChrome();
				RefreshWindowVisibility();
				if (_activeTrackKey != null)
				{
					_activeTrackKey = null;
					_lyricsRetryTrackKey = null;
					_lyricsRetryAttempt = 0;
					_lyricsRetryScheduled = false;
					_lyrics = null;
					_lyricsLookup = null;
					ResetLyricsPresentationState();
					_lyricsCancellation?.Cancel();
				}
				TrackStatusText.Text = "MEDIA SESSION / WAITING";
				TrackTitleText.Text = T("Play something in a media player");
				_trackStatusColor = System.Windows.Media.Color.FromRgb(142, 151, 166);
				StatusDot.Fill = new SolidColorBrush(_trackStatusColor);
				if (_settings.ShowStatusWhenIdle)
				{
					SetStatus(T("Play something in a media player"), T("Following the selected Windows Media Session"), animate: false);
				}
				else
				{
					SetStatus(string.Empty, string.Empty, animate: false);
				}
			}
			else
			{
				_snapshot = playbackSnapshot;
				RefreshPersonalSyncResolution();
				_pauseHidden = _settings.HideWhenPaused && !playbackSnapshot.IsPlaying;
				UpdatePlaybackChrome();
				RefreshWindowVisibility();
				if (!string.Equals(_activeTrackKey, playbackSnapshot.Track.CacheKey, StringComparison.Ordinal))
				{
					ClosePersonalSyncEditor(save: true);
					_activeTrackKey = playbackSnapshot.Track.CacheKey;
					_personalSyncActiveProfile = null;
					_personalSyncContextKey = string.Empty;
					UpdatePersonalSyncButton();
					_lyricsRetryTrackKey = playbackSnapshot.Track.CacheKey;
					_lyricsRetryAttempt = 0;
					_lyricsRetryScheduled = false;
					_lyrics = null;
					_lyricsLookup = null;
					ResetLyricsPresentationState();
					TrackStatusText.Text = GetPlaybackSourceLabel(playbackSnapshot) + " / CHECKING CACHE";
					TrackTitleText.Text = playbackSnapshot.Track.DisplayName;
					_trackStatusColor = System.Windows.Media.Color.FromRgb(142, 151, 166);
					StatusDot.Fill = new SolidColorBrush(_trackStatusColor);
					SetStatus(T("Checking saved lyrics…"), playbackSnapshot.Track.DisplayName, animate: false);
					LoadLyricsAsync(playbackSnapshot.Track, forceRefresh: false);
				}
			}
		}
		finally
		{
			_mediaPollRunning = false;
		}
	}

	private async Task LoadLyricsAsync(TrackInfo track, bool forceRefresh)
	{
		_lyricsCancellation?.Cancel();
		_lyricsCancellation?.Dispose();
		_lyricsCancellation = new CancellationTokenSource();
		CancellationToken token = _lyricsCancellation.Token;
		try
		{
			LyricsLookupResult lyricsLookupResult = await _lyricsService.GetLyricsAsync(track, forceRefresh, token, delegate
			{
				if (!token.IsCancellationRequested && string.Equals(_activeTrackKey, track.CacheKey, StringComparison.Ordinal))
				{
					SetTrackStatus("SEARCHING LYRICS", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
					SetStatus(T("Searching lyrics…"), T("The first lookup may take a moment. Saved results load faster next time."), animate: true);
				}
			});
			if (!token.IsCancellationRequested && string.Equals(_activeTrackKey, track.CacheKey, StringComparison.Ordinal))
			{
				_lyricsRetryTrackKey = track.CacheKey;
				_lyricsRetryAttempt = 0;
				_lyricsRetryScheduled = false;
				_lyricsLookup = lyricsLookupResult;
				LyricsResult lyrics = lyricsLookupResult.Lyrics;
				ResetLyricsPresentationState();
				if (lyricsLookupResult.Status == LyricsLookupStatus.CandidatesFound)
				{
					_lyrics = null;
					RefreshPersonalSyncResolution(force: true);
					SetTrackStatus("LRCLIB CANDIDATES FOUND", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
					SetStatus(T("Lyrics candidates were found in LRCLIB."), T("Open Settings > Lyrics to choose the correct lyrics."), animate: true);
				}
				else if (lyrics == null)
				{
					_lyrics = null;
					RefreshPersonalSyncResolution(force: true);
					SetTrackStatus("NO LYRICS", System.Windows.Media.Color.FromRgb(byte.MaxValue, 139, 143));
					SetStatus(T("No synced lyrics were found."), T("Open Settings > Lyrics to search LRCLIB using another title or English name, or add a local LRC file."), animate: true);
				}
				else if (lyrics.IsInstrumental)
				{
					_lyrics = lyrics;
					RefreshPersonalSyncResolution(force: true);
					SetTrackStatus("INSTRUMENTAL", System.Windows.Media.Color.FromRgb(117, 230, byte.MaxValue));
					SetStatus("♪  " + T("Instrumental"), track.DisplayName, animate: true);
				}
				else if (lyrics.HasSyncedLyrics)
				{
					_lyrics = lyrics;
					RefreshPersonalSyncResolution(force: true);
					string text = ((!lyricsLookupResult.LoadedFromCache) ? (lyricsLookupResult.Status switch
					{
						LyricsLookupStatus.LocalLrc => "LOCAL LRC", 
						LyricsLookupStatus.LrclibManual => "LRCLIB — MANUALLY SELECTED", 
						LyricsLookupStatus.LrclibBestMatch => "LRCLIB — BEST MATCH",
						LyricsLookupStatus.Cache => "CACHE", 
						_ => "LRCLIB — AUTO SELECTED", 
					}) : "CACHE");
					string status = text;
					SetTrackStatus(status, (lyricsLookupResult.Status == LyricsLookupStatus.LocalLrc) ? System.Windows.Media.Color.FromRgb(167, 149, byte.MaxValue) : System.Windows.Media.Color.FromRgb(102, 229, 174));
					RenderLyrics();
				}
				else if (_settings.EnablePlainLyricsFallback && lyrics.HasPlainLyrics)
				{
					_lyrics = lyrics;
					RefreshPersonalSyncResolution(force: true);
					_plainLyricsScrollMode = true;
					SetTrackStatus("PLAIN LYRICS", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
					RenderLyrics();
				}
				else
				{
					_lyrics = lyrics;
					RefreshPersonalSyncResolution(force: true);
					SetTrackStatus("NO SYNCED LYRICS", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
					SetStatus(track.DisplayName, T("Only plain lyrics are available · Enable fallback in Settings"), animate: true);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (LyricsServiceException ex2) when (ex2.Kind == LyricsErrorKind.Network || ex2.Kind == LyricsErrorKind.Timeout || ex2.Kind == LyricsErrorKind.RateLimited)
		{
			if (string.Equals(_activeTrackKey, track.CacheKey, StringComparison.Ordinal))
			{
				SetTrackStatus("CONNECTING", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
				if (_lyrics == null)
				{
					SetStatus(T("Lyrics service is busy. Retrying automatically."), T("Open Settings > Lyrics to retry manually."), animate: true);
				}
				ScheduleLyricsRetry(track, token);
			}
		}
		catch (LyricsServiceException ex3)
		{
			if (string.Equals(_activeTrackKey, track.CacheKey, StringComparison.Ordinal))
			{
				SetTrackStatus("ERROR", System.Windows.Media.Color.FromRgb(byte.MaxValue, 139, 143));
				if (_lyrics == null)
				{
					SetStatus(track.DisplayName, ex3.Message, animate: true);
				}
			}
		}
		catch (Exception ex4)
		{
			if (string.Equals(_activeTrackKey, track.CacheKey, StringComparison.Ordinal))
			{
				SetTrackStatus("ERROR", System.Windows.Media.Color.FromRgb(byte.MaxValue, 139, 143));
				if (_lyrics == null)
				{
					SetStatus(track.DisplayName, string.Format(T("Could not load lyrics: {0}"), ex4.Message), animate: true);
				}
			}
		}
	}

	private void ScheduleLyricsRetry(TrackInfo track, CancellationToken cancellationToken)
	{
		if (!_lyricsRetryScheduled)
		{
			if (!string.Equals(_lyricsRetryTrackKey, track.CacheKey, StringComparison.Ordinal))
			{
				_lyricsRetryTrackKey = track.CacheKey;
				_lyricsRetryAttempt = 0;
			}
			_lyricsRetryScheduled = true;
			RetryLyricsAfterDelayAsync(track, ++_lyricsRetryAttempt, cancellationToken);
		}
	}

	private async Task RetryLyricsAfterDelayAsync(TrackInfo track, int attempt, CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			await Task.Delay(TimeSpan.FromSeconds(attempt switch
			{
				1 => 2, 
				2 => 5, 
				3 => 10, 
				_ => 20, 
			}), cancellationToken);
			if (!cancellationToken.IsCancellationRequested && string.Equals(_activeTrackKey, track.CacheKey, StringComparison.Ordinal))
			{
				_lyricsRetryScheduled = false;
				SetTrackStatus("RECONNECTING", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
				await LoadLyricsAsync(track, forceRefresh: true);
			}
		}
		catch (OperationCanceledException)
		{
			if (string.Equals(_lyricsRetryTrackKey, track.CacheKey, StringComparison.Ordinal))
			{
				_lyricsRetryScheduled = false;
			}
		}
	}

	private void InitializePersonalSyncUi()
	{
		_personalSyncButton = new System.Windows.Controls.Button
		{
			Content = "SYNC",
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = System.Windows.Media.Brushes.White,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(34, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(9.0, 4.0, 9.0, 4.0),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Cursor = System.Windows.Input.Cursors.Hand,
			IsEnabled = false
		};
		_personalSyncButton.Click += PersonalSyncButton_Click;
		HeaderPanel.Children.Add(_personalSyncButton);
		TrackInfoPanel.Margin = new Thickness(0.0, 0.0, 76.0, 0.0);

		StackPanel content = new() { Margin = new Thickness(13.0), Width = 286.0 };
		DockPanel titleRow = new() { LastChildFill = true };
		System.Windows.Controls.Button closeButton = CreatePersonalSyncButton("×", compact: true);
		closeButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
		closeButton.Click += delegate { ClosePersonalSyncEditor(save: true); };
		DockPanel.SetDock(closeButton, Dock.Right);
		titleRow.Children.Add(closeButton);
		titleRow.Children.Add(new TextBlock
		{
			Text = PersonalSyncText("LYRICS TIMING", "歌詞タイミング"),
			FontFamily = _englishDotFont,
			FontSize = 11.0,
			FontWeight = FontWeights.Bold,
			Foreground = System.Windows.Media.Brushes.White,
			VerticalAlignment = VerticalAlignment.Center
		});
		content.Children.Add(titleRow);
		_personalSyncHintText = new TextBlock
		{
			Margin = new Thickness(0.0, 8.0, 0.0, 5.0),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(207, 205, 207)),
			FontSize = 11.0,
			TextWrapping = TextWrapping.Wrap
		};
		content.Children.Add(_personalSyncHintText);
		_personalSyncOffsetText = new TextBlock
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 9.0),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			Foreground = FindResource("UiAccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Orange,
			FontFamily = _englishDotFont,
			FontSize = 14.0,
			FontWeight = FontWeights.Bold
		};
		content.Children.Add(_personalSyncOffsetText);

		Grid offsetButtons = new();
		offsetButtons.ColumnDefinitions.Add(new ColumnDefinition());
		offsetButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7.0) });
		offsetButtons.ColumnDefinitions.Add(new ColumnDefinition());
		System.Windows.Controls.Button earlier = CreatePersonalSyncButton(PersonalSyncText("◀ EARLIER", "◀ 歌詞を早く"));
		earlier.Click += delegate { AdjustPersonalSyncOffset(-0.1); };
		offsetButtons.Children.Add(earlier);
		System.Windows.Controls.Button later = CreatePersonalSyncButton(PersonalSyncText("LATER ▶", "歌詞を遅く ▶"));
		later.Click += delegate { AdjustPersonalSyncOffset(0.1); };
		Grid.SetColumn(later, 2);
		offsetButtons.Children.Add(later);
		content.Children.Add(offsetButtons);

		_personalSyncAlignButton = CreatePersonalSyncButton(PersonalSyncText("ALIGN SELECTED LINE TO NOW", "選択した行を今ここに合わせる"));
		_personalSyncAlignButton.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		_personalSyncAlignButton.Click += PersonalSyncAlign_Click;
		content.Children.Add(_personalSyncAlignButton);

		WrapPanel history = new() { Margin = new Thickness(-3.0, 7.0, 0.0, 0.0) };
		_personalSyncUndoButton = CreatePersonalSyncButton("↶ " + PersonalSyncText("UNDO", "戻す"), compact: true);
		_personalSyncUndoButton.Click += delegate { UndoPersonalSync(); };
		history.Children.Add(_personalSyncUndoButton);
		_personalSyncRedoButton = CreatePersonalSyncButton("↷ " + PersonalSyncText("REDO", "やり直す"), compact: true);
		_personalSyncRedoButton.Click += delegate { RedoPersonalSync(); };
		history.Children.Add(_personalSyncRedoButton);
		System.Windows.Controls.Button reset = CreatePersonalSyncButton(PersonalSyncText("RESET", "リセット"), compact: true);
		reset.Click += delegate { ResetPersonalSync(); };
		history.Children.Add(reset);
		content.Children.Add(history);

		_personalSyncAllSourcesBox = new System.Windows.Controls.CheckBox
		{
			Content = PersonalSyncText("USE FOR THIS TRACK ON ALL PLAYERS", "この曲をすべての再生元で使う"),
			Margin = new Thickness(0.0, 9.0, 0.0, 0.0),
			Foreground = System.Windows.Media.Brushes.White,
			FontSize = 10.5
		};
		_personalSyncAllSourcesBox.Checked += PersonalSyncScopeChanged;
		_personalSyncAllSourcesBox.Unchecked += PersonalSyncScopeChanged;
		content.Children.Add(_personalSyncAllSourcesBox);

		System.Windows.Controls.Button advanced = CreatePersonalSyncButton(PersonalSyncText("ADVANCED TIMELINE…", "詳細タイムライン…"));
		advanced.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		advanced.Click += OpenAdvancedPersonalSync_Click;
		content.Children.Add(advanced);

		Border surface = new()
		{
			Child = content,
			CornerRadius = new CornerRadius(12.0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(246, 30, 28, 31)),
			BorderBrush = FindResource("UiAccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Orange,
			BorderThickness = new Thickness(1.0)
		};
		_personalSyncPopup = new Popup
		{
			Child = surface,
			PlacementTarget = _personalSyncButton,
			Placement = PlacementMode.Bottom,
			HorizontalOffset = -235.0,
			VerticalOffset = 5.0,
			AllowsTransparency = true,
			StaysOpen = true
		};
		_personalSyncPopup.Closed += PersonalSyncPopup_Closed;
		UpdatePersonalSyncButton();
	}

	private System.Windows.Controls.Button CreatePersonalSyncButton(string content, bool compact = false)
	{
		return new System.Windows.Controls.Button
		{
			Content = content,
			FontFamily = _englishDotFont,
			FontSize = compact ? 8.0 : 8.5,
			Foreground = System.Windows.Media.Brushes.White,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 45, 49)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(91, 85, 93)),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(compact ? 8.0 : 10.0, compact ? 4.0 : 7.0, compact ? 8.0 : 10.0, compact ? 4.0 : 7.0),
			Margin = compact ? new Thickness(3.0) : new Thickness(0.0),
			Cursor = System.Windows.Input.Cursors.Hand
		};
	}

	private string PersonalSyncText(string english, string japanese)
	{
		return string.Equals(LocalizationService.NormalizeLanguage(_settings.Language), "ja-JP", StringComparison.OrdinalIgnoreCase) ? japanese : english;
	}

	private void PersonalSyncButton_Click(object sender, RoutedEventArgs e)
	{
		if (_personalSyncPopup == null || _snapshot == null || _lyrics?.HasSyncedLyrics != true || _lyricsLookup == null)
		{
			return;
		}
		if (_personalSyncPopup.IsOpen)
		{
			ClosePersonalSyncEditor(save: true);
			return;
		}

		_personalSyncEditingProfile = _personalSyncActiveProfile == null
			? CreateCurrentPersonalSyncProfile()
			: _personalSyncActiveProfile.Clone();
		_personalSyncUndo.Clear();
		_personalSyncRedo.Clear();
		_personalSyncSelectedLineIndex = -1;
		_personalSyncActiveProfile = _personalSyncEditingProfile;
		RefreshPersonalSyncPopup();
		_personalSyncPopup.IsOpen = true;
		InvalidatePersonalSyncRendering();
	}

	private PersonalSyncProfile CreateCurrentPersonalSyncProfile()
	{
		if (_snapshot == null || _lyricsLookup == null) return new PersonalSyncProfile();
		PersonalSyncContext context = PersonalSyncIdentity.Create(_snapshot, _lyricsLookup);
		return new PersonalSyncProfile
		{
			Track = context.Track,
			Source = context.Source,
			Lyrics = context.Lyrics,
			Scope = PersonalSyncScope.Source,
			Mode = PersonalSyncMode.Offset
		};
	}

	private async void RefreshPersonalSyncResolution(bool force = false)
	{
		if (_personalSyncEditingProfile != null || _snapshot == null || _lyricsLookup == null || _lyrics?.HasSyncedLyrics != true)
		{
			if (_lyrics?.HasSyncedLyrics != true)
			{
				_personalSyncActiveProfile = null;
				_personalSyncResolution = new PersonalSyncResolution(null, false);
				_personalSyncContextKey = string.Empty;
				UpdatePersonalSyncButton();
			}
			return;
		}
		PersonalSyncContext personalSyncContext = PersonalSyncIdentity.Create(_snapshot, _lyricsLookup);
		string context = personalSyncContext.Track.StableTrackKey + "|" + personalSyncContext.Source.StableSourceKey + "|" + personalSyncContext.Lyrics.Key;
		if (!force && string.Equals(context, _personalSyncContextKey, StringComparison.Ordinal)) return;
		_personalSyncContextKey = context;
		PersonalSyncResolution resolution;
		try
		{
			resolution = await _personalSyncStore.ResolveAsync(personalSyncContext);
			if (resolution.Profile == null
				&& _settings.TrackOffsetsMs.TryGetValue(_snapshot.Track.CacheKey, out int legacyTrackOffset)
				&& legacyTrackOffset != 0)
			{
				PersonalSyncProfile migrated = CreateCurrentPersonalSyncProfile();
				// Legacy offsets were added to playback; Personal Sync subtracts its offset.
				migrated.OffsetSeconds = Math.Clamp(-legacyTrackOffset / 1000.0, -3600.0, 3600.0);
				migrated = await _personalSyncStore.UpsertAsync(migrated);
				_settings.TrackOffsetsMs.Remove(_snapshot.Track.CacheKey);
				await SaveSettingsSafeAsync();
				resolution = new PersonalSyncResolution(migrated, false);
			}
		}
		catch
		{
			resolution = new PersonalSyncResolution(null, false);
		}
		if (!string.Equals(context, _personalSyncContextKey, StringComparison.Ordinal) || _personalSyncEditingProfile != null) return;
		_personalSyncResolution = resolution;
		_personalSyncActiveProfile = _personalSyncResolution.Profile;
		UpdatePersonalSyncButton();
		InvalidatePersonalSyncRendering();
	}

	private TimeSpan GetBasePlaybackPosition()
	{
		if (_snapshot == null) return TimeSpan.Zero;
		return _snapshot.EstimatedPosition(DateTimeOffset.UtcNow) + TimeSpan.FromMilliseconds(_settings.GlobalLyricsOffsetMs);
	}

	private TimeSpan GetEffectiveLyricsPosition() => PersonalSyncMapper.MapPlaybackToLyrics(GetBasePlaybackPosition(), _personalSyncActiveProfile);

	private void AdjustPersonalSyncOffset(double delta)
	{
		if (_personalSyncEditingProfile == null) return;
		PushPersonalSyncUndo();
		_personalSyncEditingProfile.Mode = PersonalSyncMode.Offset;
		_personalSyncEditingProfile.OffsetSeconds = Math.Clamp(Math.Round((_personalSyncEditingProfile.OffsetSeconds + delta) * 10.0) / 10.0, -120.0, 120.0);
		_personalSyncEditingProfile.Anchors.Clear();
		_personalSyncEditingProfile.Segments.Clear();
		ApplyPersonalSyncEdit();
	}

	private void PersonalSyncAlign_Click(object sender, RoutedEventArgs e)
	{
		if (_personalSyncEditingProfile == null || _lyrics?.HasSyncedLyrics != true || _personalSyncSelectedLineIndex < 0 || _personalSyncSelectedLineIndex >= _lyrics.Lines.Count) return;
		PushPersonalSyncUndo();
		_personalSyncEditingProfile.Mode = PersonalSyncMode.Offset;
		_personalSyncEditingProfile.OffsetSeconds = Math.Clamp(GetBasePlaybackPosition().TotalSeconds - _lyrics.Lines[_personalSyncSelectedLineIndex].Time.TotalSeconds, -120.0, 120.0);
		_personalSyncEditingProfile.Anchors.Clear();
		_personalSyncEditingProfile.Segments.Clear();
		ApplyPersonalSyncEdit();
	}

	private void PersonalSyncScopeChanged(object sender, RoutedEventArgs e)
	{
		if (_personalSyncUpdatingUi || _personalSyncEditingProfile == null || _personalSyncAllSourcesBox == null) return;
		PushPersonalSyncUndo();
		_personalSyncEditingProfile.Scope = _personalSyncAllSourcesBox.IsChecked == true ? PersonalSyncScope.Track : PersonalSyncScope.Source;
		ApplyPersonalSyncEdit();
	}

	private void PushPersonalSyncUndo()
	{
		if (_personalSyncEditingProfile == null) return;
		_personalSyncUndo.Push(_personalSyncEditingProfile.Clone());
		while (_personalSyncUndo.Count > 60)
		{
			PersonalSyncProfile[] keep = _personalSyncUndo.Take(60).Reverse().ToArray();
			_personalSyncUndo.Clear();
			foreach (PersonalSyncProfile item in keep) _personalSyncUndo.Push(item);
		}
		_personalSyncRedo.Clear();
	}

	private void UndoPersonalSync()
	{
		if (_personalSyncEditingProfile == null || _personalSyncUndo.Count == 0) return;
		_personalSyncRedo.Push(_personalSyncEditingProfile.Clone());
		_personalSyncEditingProfile = _personalSyncUndo.Pop();
		ApplyPersonalSyncEdit();
	}

	private void RedoPersonalSync()
	{
		if (_personalSyncEditingProfile == null || _personalSyncRedo.Count == 0) return;
		_personalSyncUndo.Push(_personalSyncEditingProfile.Clone());
		_personalSyncEditingProfile = _personalSyncRedo.Pop();
		ApplyPersonalSyncEdit();
	}

	private void ResetPersonalSync()
	{
		if (_personalSyncEditingProfile == null) return;
		PushPersonalSyncUndo();
		_personalSyncEditingProfile.Mode = PersonalSyncMode.None;
		_personalSyncEditingProfile.OffsetSeconds = 0.0;
		_personalSyncEditingProfile.Anchors.Clear();
		_personalSyncEditingProfile.Segments.Clear();
		ApplyPersonalSyncEdit();
	}

	private void ApplyPersonalSyncEdit()
	{
		_personalSyncActiveProfile = _personalSyncEditingProfile;
		RefreshPersonalSyncPopup();
		UpdatePersonalSyncButton();
		InvalidatePersonalSyncRendering();
	}

	private void RefreshPersonalSyncPopup()
	{
		if (_personalSyncEditingProfile == null) return;
		_personalSyncUpdatingUi = true;
		try
		{
			if (_personalSyncOffsetText != null)
			{
				_personalSyncOffsetText.Text = _personalSyncEditingProfile.Mode == PersonalSyncMode.Advanced
					? PersonalSyncText("ADVANCED", "詳細調整")
					: _personalSyncEditingProfile.OffsetSeconds.ToString("+0.0;-0.0;0.0") + " s";
			}
			if (_personalSyncHintText != null)
			{
				_personalSyncHintText.Text = _personalSyncSelectedLineIndex >= 0 && _lyrics != null
					? "♪ " + _lyrics.Lines[_personalSyncSelectedLineIndex].Text
					: (_personalSyncResolution.HasProfileForDifferentLyrics
						? PersonalSyncText("Saved timing belongs to different lyrics and was not applied.", "別の歌詞用の調整は適用していません。")
						: PersonalSyncText("Select a synced lyric line, or nudge by 0.1 s.", "同期歌詞の行を選ぶか、0.1秒ずつ調整します。"));
			}
			if (_personalSyncAlignButton != null) _personalSyncAlignButton.IsEnabled = _personalSyncSelectedLineIndex >= 0 && _lyrics?.HasSyncedLyrics == true;
			if (_personalSyncUndoButton != null) _personalSyncUndoButton.IsEnabled = _personalSyncUndo.Count > 0;
			if (_personalSyncRedoButton != null) _personalSyncRedoButton.IsEnabled = _personalSyncRedo.Count > 0;
			if (_personalSyncAllSourcesBox != null) _personalSyncAllSourcesBox.IsChecked = _personalSyncEditingProfile.Scope == PersonalSyncScope.Track;
		}
		finally
		{
			_personalSyncUpdatingUi = false;
		}
	}

	private void UpdatePersonalSyncButton()
	{
		if (_personalSyncButton == null) return;
		_personalSyncButton.IsEnabled = _snapshot != null && _lyrics?.HasSyncedLyrics == true;
		PersonalSyncProfile? profile = _personalSyncActiveProfile;
		if (profile == null || profile.Mode == PersonalSyncMode.None)
		{
			_personalSyncButton.Content = _personalSyncResolution.HasProfileForDifferentLyrics ? "SYNC ?" : "SYNC";
			_personalSyncButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(34, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		}
		else
		{
			_personalSyncButton.Content = profile.Mode == PersonalSyncMode.Advanced
				? "SYNC •"
				: "SYNC " + profile.OffsetSeconds.ToString("+0.0;-0.0;0.0");
			_personalSyncButton.Background = CreateDisplayBrush(_settings.UiColor, 0.5, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), preservePlayerUi: true, ignoreSourceAlpha: true);
		}
		_personalSyncButton.ToolTip = _personalSyncResolution.HasProfileForDifferentLyrics
			? PersonalSyncText("A profile for different lyrics was not applied.", "別の歌詞用の調整は適用されていません。")
			: PersonalSyncText("Personal lyric timing", "個人用の歌詞タイミング");
	}

	private async void PersonalSyncPopup_Closed(object? sender, EventArgs e)
	{
		PersonalSyncProfile? profile = _personalSyncEditingProfile;
		_personalSyncEditingProfile = null;
		_personalSyncSelectedLineIndex = -1;
		_personalSyncUndo.Clear();
		_personalSyncRedo.Clear();
		RefreshPersonalSyncSelectionVisuals();
		if (profile != null)
		{
			try { await _personalSyncStore.UpsertAsync(profile); } catch { }
		}
		RefreshPersonalSyncResolution(force: true);
	}

	private void ClosePersonalSyncEditor(bool save)
	{
		if (_personalSyncAdvancedWindow != null)
		{
			_personalSyncAdvancedWindow.Close();
			_personalSyncAdvancedWindow = null;
		}
		if (_personalSyncPopup?.IsOpen == true)
		{
			if (!save) _personalSyncEditingProfile = null;
			_personalSyncPopup.IsOpen = false;
		}
	}

	private void PersonalSyncLyricLine_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (_personalSyncEditingProfile == null || _personalSyncPopup?.IsOpen != true || _lyrics?.HasSyncedLyrics != true || sender is not OutlinedText line || line.Tag is not int index) return;
		_personalSyncSelectedLineIndex = index;
		RefreshPersonalSyncPopup();
		RefreshPersonalSyncSelectionVisuals();
		e.Handled = true;
	}

	private void ApplyPersonalSyncSelectionVisual(OutlinedText line)
	{
		if (_personalSyncEditingProfile != null && line.Tag is int index && index == _personalSyncSelectedLineIndex)
		{
			line.Stroke = CreateDisplayBrush(_settings.UiColor, 1.0, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), preservePlayerUi: true, ignoreSourceAlpha: true);
			line.StrokeThickness = Math.Max(1.0, _settings.OutlineThickness + 0.75);
			line.Opacity = 1.0;
		}
	}

	private void RefreshPersonalSyncSelectionVisuals()
	{
		System.Windows.Media.Brush normal = CreateDisplayBrush(_settings.OutlineColor, 1.0, Colors.Black);
		foreach (OutlinedText line in _lineControls)
		{
			line.Stroke = normal;
			line.StrokeThickness = _settings.OutlineThickness;
			ApplyPersonalSyncSelectionVisual(line);
		}
	}

	private void InvalidatePersonalSyncRendering()
	{
		_lastLineIndex = int.MinValue;
		_lastFullLyricsActiveIndex = int.MinValue;
		RenderLyrics();
	}

	private void OpenAdvancedPersonalSync_Click(object sender, RoutedEventArgs e)
	{
		if (_personalSyncEditingProfile == null || _snapshot == null || _lyrics?.HasSyncedLyrics != true) return;
		if (_personalSyncAdvancedWindow != null)
		{
			_personalSyncAdvancedWindow.Activate();
			return;
		}
		if (_lyricsLookup == null) return;
		PersonalSyncContext context = PersonalSyncIdentity.Create(_snapshot, _lyricsLookup);
		_personalSyncAdvancedWindow = new PersonalSyncWindow(
			_personalSyncStore,
			context,
			_personalSyncEditingProfile,
			_lyrics.Lines,
			() =>
			{
				int index = _personalSyncSelectedLineIndex >= 0 ? _personalSyncSelectedLineIndex : _activeLineIndex;
				return index >= 0 && index < _lyrics.Lines.Count ? index : (int?)null;
			},
			GetBasePlaybackPosition,
			_settings.Language)
		{
			Owner = this
		};
		_personalSyncAdvancedWindow.PreviewChanged += delegate(object? _, PersonalSyncProfile? profile)
		{
			if (profile == null)
			{
				_personalSyncEditingProfile = null;
				_personalSyncActiveProfile = null;
				InvalidatePersonalSyncRendering();
				return;
			}
			if (_personalSyncEditingProfile != null) _personalSyncUndo.Push(_personalSyncEditingProfile.Clone());
			_personalSyncEditingProfile = profile.Clone();
			_personalSyncRedo.Clear();
			ApplyPersonalSyncEdit();
		};
		_personalSyncAdvancedWindow.Closed += delegate
		{
			_personalSyncAdvancedWindow = null;
			RefreshPersonalSyncPopup();
		};
		_personalSyncAdvancedWindow.Show();
	}

	private PersonalSyncDiagnosticSnapshot? GetPersonalSyncDiagnostics()
	{
		if (_snapshot == null || _lyrics == null || _lyricsLookup == null) return null;
		PersonalSyncProfile? profile = _personalSyncActiveProfile;
		TimeSpan playback = GetBasePlaybackPosition();
		TimeSpan mapped = PersonalSyncMapper.MapPlaybackToLyrics(playback, profile);
		PersonalSyncContext context = PersonalSyncIdentity.Create(_snapshot, _lyricsLookup);
		return new PersonalSyncDiagnosticSnapshot(
			profile?.Id.ToString("N") ?? "—",
			profile?.Mode.ToString() ?? "None",
			profile?.Scope == PersonalSyncScope.Track ? "Track + Lyrics" : "Source + Track + Lyrics",
			context.Track.StableTrackKey,
			context.Source.StableSourceKey,
			context.Lyrics.Key,
			profile?.OffsetSeconds ?? 0.0,
			profile?.Anchors.Count ?? 0,
			profile?.Segments.Count(segment => segment.Type == PersonalSyncSegmentType.Hold) ?? 0,
			_personalSyncResolution.HasProfileForDifferentLyrics,
			playback.TotalSeconds,
			mapped.TotalSeconds,
			PersonalSyncMapper.DescribeActiveSegment(playback.TotalSeconds, profile));
	}

	private void RenderLyrics()
	{
		UpdatePlaybackProgress();
		if (_showAllLyrics && _lyrics != null && (_lyrics.HasSyncedLyrics || _lyrics.HasPlainLyrics))
		{
			EnsureFullLyricsLayout();
			UpdateFullLyricsHighlight();
			return;
		}
		if (_plainLyricsScrollMode && _lyrics?.HasPlainLyrics == true)
		{
			EnsurePlainLyricsLayout();
			UpdatePlainLyricsScroll();
			return;
		}
		UpdateScrollAnimation();
		if ((object)_snapshot != null && (object)_lyrics != null && _lyrics.HasSyncedLyrics)
		{
			TimeSpan position = GetEffectiveLyricsPosition();
			IReadOnlyList<LyricLine> lines = _lyrics.Lines;
			int num2 = FindActiveLine(lines, position);
			if (num2 != _lastLineIndex)
			{
				_lastLineIndex = num2;
				DisplayLyricContext(lines, num2, animate: true);
			}
		}
	}

	private void ResetLyricsPresentationState()
	{
		_showAllLyrics = _settings.ShowAllLyrics;
		_plainLyricsScrollMode = false;
		_plainLyricsUserScrollPaused = false;
		_plainLyricsAutoScrollAnchorActive = false;
		_plainLyricsLayoutKey = null;
		_fullLyricsLayoutKey = null;
		_lastFullLyricsActiveIndex = int.MinValue;
		_lastLineIndex = int.MinValue;
		DisableFullLyricsViewport();
	}

	private void EnsurePlainLyricsLayout()
	{
		if (_lyrics?.PlainLyrics == null)
		{
			return;
		}
		string layoutKey = string.Join("|", _activeTrackKey, _settings.FontFamily, _settings.FontSize, _settings.MinimumFontSize,
			_settings.TextAlignment, _settings.LineSpacing, _settings.MaximumWrapLines, _settings.WrapLongLines, _settings.AutoFitText,
			LyricsScrollViewer.ViewportWidth);
		if (string.Equals(_plainLyricsLayoutKey, layoutKey, StringComparison.Ordinal))
		{
			return;
		}

		List<LyricLine> lines = ParsePlainLyrics(_lyrics.PlainLyrics);
		if (lines.Count == 0)
		{
			return;
		}

		RebuildLineControls(lines.Count, lines, 0);
		LyricsStackPanel.Margin = new Thickness(0.0, Math.Max(6.0, LyricsScrollViewer.ViewportHeight * 0.07), 0.0,
			Math.Max(10.0, LyricsScrollViewer.ViewportHeight * 0.14));
		_activeLineIndex = -1;
		_lastLineIndex = int.MinValue;
		for (int i = 0; i < _lineControls.Count; i++)
		{
			OutlinedText line = _lineControls[i];
			line.Fill = ResolveTextBrush(i, isActive: true);
			line.FontWeight = FontWeights.SemiBold;
			line.FontSize = Math.Max(_settings.MinimumFontSize, _settings.FontSize * Math.Max(0.68, _settings.InactiveFontScale * 0.86));
			line.MaximumLines = Math.Max(8, _settings.MaximumWrapLines);
			line.AutoFit = true;
			line.Wrap = true;
			line.Margin = new Thickness(0.0, _settings.LineSpacing * 0.25, 0.0, _settings.LineSpacing * 0.25);
			line.Opacity = 1.0;
		}
		LyricsStackPanel.UpdateLayout();
		_plainLyricsLayoutKey = layoutKey;
	}

	private void UpdatePlainLyricsScroll()
	{
		if (!_settings.PlainLyricsAutoScroll || _plainLyricsUserScrollPaused || _showAllLyrics || _snapshot == null || _snapshot.Track.Duration <= TimeSpan.Zero || LyricsScrollViewer.ViewportHeight <= 0.0)
		{
			return;
		}
		double maxOffset = Math.Max(0.0, LyricsScrollViewer.ExtentHeight - LyricsScrollViewer.ViewportHeight);
		TimeSpan position = _snapshot.EstimatedPosition(DateTimeOffset.UtcNow);
		double targetOffset;
		if (_plainLyricsAutoScrollAnchorActive)
		{
			double elapsedRatio = (position - _plainLyricsAutoScrollAnchorPosition).TotalMilliseconds /
				_snapshot.Track.Duration.TotalMilliseconds;
			targetOffset = _plainLyricsAutoScrollAnchorOffset + maxOffset * elapsedRatio;
		}
		else
		{
			double ratio = Math.Clamp(position.TotalMilliseconds /
				_snapshot.Track.Duration.TotalMilliseconds, 0.0, 1.0);
			targetOffset = maxOffset * ratio;
		}
		LyricsScrollViewer.ScrollToVerticalOffset(Math.Clamp(targetOffset, 0.0, maxOffset));
	}

	private static List<LyricLine> ParsePlainLyrics(string plainLyrics)
	{
		return plainLyrics
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Split('\n')
			.Select((string line) => line.Trim())
			.Where((string line) => !string.IsNullOrWhiteSpace(line))
			.Where((string line) => !line.StartsWith('[') || !line.EndsWith(']'))
			.Select((string line) => new LyricLine(TimeSpan.Zero, line))
			.ToList();
	}

	private void LyricsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		PausePlainLyricsAutoScroll();
	}

	private void LyricsScrollViewer_PreviewTouchMove(object sender, TouchEventArgs e)
	{
		PausePlainLyricsAutoScroll();
	}

	private void PausePlainLyricsAutoScroll()
	{
		if (_plainLyricsScrollMode && _settings.PlainLyricsAutoScroll && !_showAllLyrics)
		{
			_plainLyricsUserScrollPaused = true;
			_plainLyricsAutoScrollAnchorActive = false;
		}
	}

	private void ResumePlainLyricsAutoScrollFromCurrentPosition()
	{
		if (!_plainLyricsScrollMode || !_plainLyricsUserScrollPaused || !_settings.PlainLyricsAutoScroll
			|| _showAllLyrics || _snapshot == null || _snapshot.Track.Duration <= TimeSpan.Zero)
		{
			return;
		}
		_plainLyricsAutoScrollAnchorOffset = LyricsScrollViewer.VerticalOffset;
		_plainLyricsAutoScrollAnchorPosition = _snapshot.EstimatedPosition(DateTimeOffset.UtcNow);
		_plainLyricsAutoScrollAnchorActive = true;
		_plainLyricsUserScrollPaused = false;
	}

	private void EnsureFullLyricsLayout()
	{
		if (_lyrics == null)
		{
			return;
		}
		IReadOnlyList<LyricLine> lines = _lyrics.HasSyncedLyrics
			? _lyrics.Lines
			: ParsePlainLyrics(_lyrics.PlainLyrics ?? string.Empty);
		if (lines.Count == 0)
		{
			return;
		}
		string layoutKey = string.Join("|", _activeTrackKey, lines.Count, _settings.FontFamily, _settings.FontSize,
			_settings.MinimumFontSize, _settings.TextAlignment, _settings.LineSpacing, LyricsScrollViewer.ViewportWidth, LyricsScrollViewer.ViewportHeight);
		if (!string.Equals(_fullLyricsLayoutKey, layoutKey, StringComparison.Ordinal))
		{
			RebuildLineControls(lines.Count, lines, 0);
			LyricsStackPanel.Margin = new Thickness(2.0);
			for (int i = 0; i < _lineControls.Count; i++)
			{
				OutlinedText line = _lineControls[i];
				line.FontSize = _settings.FontSize;
				line.MinimumFontSize = Math.Min(4.0, _settings.MinimumFontSize);
				line.MaximumLines = Math.Max(8, _settings.MaximumWrapLines);
				line.AutoFit = true;
				line.Wrap = true;
				line.Margin = new Thickness(0.0, Math.Min(2.0, _settings.LineSpacing * 0.18), 0.0, Math.Min(2.0, _settings.LineSpacing * 0.18));
			}
			EnableFullLyricsViewport();
			_fullLyricsLayoutKey = layoutKey;
			_lastFullLyricsActiveIndex = int.MinValue;
		}
	}

	private void UpdateFullLyricsHighlight()
	{
		if (_lyrics == null || _lineControls.Count == 0)
		{
			return;
		}
		int active = -1;
		if (_lyrics.HasSyncedLyrics && _snapshot != null)
		{
			active = FindActiveLine(_lyrics.Lines, GetEffectiveLyricsPosition());
		}
		if (active == _lastFullLyricsActiveIndex)
		{
			return;
		}
		if (_lastFullLyricsActiveIndex == int.MinValue || active < 0 || _lastFullLyricsActiveIndex < 0)
		{
			for (int i = 0; i < _lineControls.Count; i++)
			{
				ApplyFullLyricsLineStyle(i, active);
			}
		}
		else
		{
			ApplyFullLyricsLineStyle(_lastFullLyricsActiveIndex, active);
			ApplyFullLyricsLineStyle(active, active);
		}
		_lastFullLyricsActiveIndex = active;
	}

	private void ApplyFullLyricsLineStyle(int lineIndex, int activeIndex)
	{
		if (lineIndex < 0 || lineIndex >= _lineControls.Count)
		{
			return;
		}
		bool isActive = activeIndex >= 0 && lineIndex == activeIndex;
		OutlinedText line = _lineControls[lineIndex];
		line.Fill = ResolveTextBrush(lineIndex, isActive || activeIndex < 0);
		line.Opacity = isActive || activeIndex < 0 ? 1.0 : Math.Max(0.58, _settings.NextLineOpacity);
		line.FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold;
		ApplyPersonalSyncSelectionVisual(line);
	}

	private void EnableFullLyricsViewport()
	{
		if (_fullLyricsViewbox == null)
		{
			LyricsScrollViewer.Content = null;
			_fullLyricsViewbox = new Viewbox
			{
				Stretch = Stretch.Uniform,
				StretchDirection = StretchDirection.DownOnly,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Top,
				Child = LyricsStackPanel
			};
			LyricsScrollViewer.Content = _fullLyricsViewbox;
		}
		UpdateFullLyricsViewport();
		LyricsScrollViewer.ScrollToTop();
	}

	private void UpdateFullLyricsViewport()
	{
		if (_fullLyricsViewbox == null)
		{
			return;
		}
		double width = Math.Max(1.0, LyricsScrollViewer.ViewportWidth);
		double height = Math.Max(1.0, LyricsScrollViewer.ViewportHeight);
		_fullLyricsViewbox.Width = width;
		_fullLyricsViewbox.Height = height;
		LyricsStackPanel.Width = width;
	}

	private void DisableFullLyricsViewport()
	{
		if (_fullLyricsViewbox != null)
		{
			_fullLyricsViewbox.Child = null;
			LyricsScrollViewer.Content = LyricsStackPanel;
			_fullLyricsViewbox = null;
		}
		LyricsStackPanel.Width = double.NaN;
		_fullLyricsLayoutKey = null;
	}

	private void DisplayLyricContext(IReadOnlyList<LyricLine> lines, int activeIndex, bool animate)
	{
		if (lines.Count != 0)
		{
			int num = Math.Clamp(activeIndex, 0, lines.Count - 1);
			EnsureLyricWindow(lines, num);
			_activeLineIndex = activeIndex;
			for (int i = 0; i < _lineControls.Count; i++)
			{
				int num2 = _visibleFirstLineIndex + i;
				bool isActive = num2 == activeIndex;
				UpdateLyricControl(_lineControls[i], lines[num2].Text, num2, isActive, animate);
			}
			CenterActiveLine(num, animate);
		}
	}

	private void SetStatus(string primary, string secondary, bool animate)
	{
		EnsureLineControls(2);
		_activeLineIndex = 0;
		UpdateLyricControl(_lineControls[0], primary, 0, isActive: true, animate);
		UpdateLyricControl(_lineControls[1], secondary, 1, isActive: false, animate);
		CenterActiveLine(0, animate);
	}

	private void EnsureLineControls(int? requiredCount = null, IReadOnlyList<LyricLine>? lines = null)
	{
		int val = requiredCount ?? 2;
		val = Math.Max(1, val);
		if (_lineControls.Count == val && _visibleFirstLineIndex == 0)
		{
			if (lines != null)
			{
				for (int i = 0; i < Math.Min(lines.Count, _lineControls.Count); i++)
				{
					_lineControls[i].Text = lines[i].Text;
				}
			}
		}
		else
		{
			RebuildLineControls(val, lines);
		}
	}

	private void EnsureLyricWindow(IReadOnlyList<LyricLine> lines, int activeIndex)
	{
		double viewportHeight = Math.Max(180.0, LyricsScrollViewer.ViewportHeight);
		double estimatedLineHeight = Math.Max(18.0, _settings.FontSize * Math.Max(0.58, _settings.InactiveFontScale) + _settings.LineSpacing);
		int automaticWindow = (int)Math.Ceiling(viewportHeight / estimatedLineHeight) + 12;
		int num = Math.Min(lines.Count, Math.Clamp(automaticWindow, 15, 40));
		int num2 = _visibleFirstLineIndex + Math.Min(4, Math.Max(1, _lineControls.Count / 4));
		int num3 = _visibleFirstLineIndex + _lineControls.Count - Math.Min(5, Math.Max(2, _lineControls.Count / 4));
		bool flag = _visibleFirstLineIndex == 0;
		bool flag2 = _visibleFirstLineIndex + num >= lines.Count;
		if (_lineControls.Count != num || !(activeIndex >= num2 || flag) || !(activeIndex <= num3 || flag2) || _visibleFirstLineIndex + num > lines.Count)
		{
			string currentLinePosition = _settings.CurrentLinePosition;
			double num4 = ((currentLinePosition == "Top") ? 0.25 : ((!(currentLinePosition == "Bottom")) ? 0.5 : 0.72));
			int value = activeIndex - (int)Math.Round((double)(num - 1) * num4);
			value = Math.Clamp(value, 0, Math.Max(0, lines.Count - num));
			RebuildLineControls(num, lines, value);
		}
	}

	private void RebuildLineControls(int? requestedCount = null, IReadOnlyList<LyricLine>? lines = null, int firstLineIndex = 0)
	{
		int num = Math.Max(1, requestedCount ?? 2);
		_visibleFirstLineIndex = Math.Max(0, firstLineIndex);
		if (!_plainLyricsScrollMode)
		{
			LyricsStackPanel.Margin = new Thickness(0.0);
		}
		LyricsStackPanel.Children.Clear();
		_lineControls.Clear();
		for (int i = 0; i < num; i++)
		{
			OutlinedText outlinedText = new OutlinedText
			{
				Text = ((lines != null && _visibleFirstLineIndex + i < lines.Count) ? lines[_visibleFirstLineIndex + i].Text : string.Empty),
				VerticalAlignment = VerticalAlignment.Top,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
				Tag = _visibleFirstLineIndex + i
			};
			outlinedText.PreviewMouseLeftButtonDown += PersonalSyncLyricLine_PreviewMouseLeftButtonDown;
			LyricsStackPanel.Children.Add(outlinedText);
			_lineControls.Add(outlinedText);
		}
		ApplyTextSettingsToControls();
		LyricsStackPanel.UpdateLayout();
	}

	private void ApplyTextSettingsToControls()
	{
		System.Windows.Media.FontFamily fontFamily = new System.Windows.Media.FontFamily(_settings.FontFamily);
		System.Windows.Media.Brush stroke = CreateDisplayBrush(_settings.OutlineColor, 1.0, Colors.Black);
		System.Windows.Media.Brush shadowBrush = CreateDisplayBrush(_settings.ShadowColor, 1.0, Colors.Black);
		string textAlignment = _settings.TextAlignment;
		TextAlignment textAlignment2 = ((textAlignment == "Center") ? TextAlignment.Center : ((textAlignment == "Right") ? TextAlignment.Right : TextAlignment.Left));
		for (int i = 0; i < _lineControls.Count; i++)
		{
			OutlinedText outlinedText = _lineControls[i];
			outlinedText.FontFamily = fontFamily;
			outlinedText.FontSize = _settings.FontSize * _settings.InactiveFontScale;
			outlinedText.MinimumFontSize = _settings.MinimumFontSize;
			outlinedText.FontWeight = FontWeights.SemiBold;
			outlinedText.Stroke = stroke;
			outlinedText.StrokeThickness = _settings.OutlineThickness;
			outlinedText.ShadowBrush = shadowBrush;
			outlinedText.ShadowDepth = _settings.ShadowDepth;
			outlinedText.TextAlignment = textAlignment2;
			outlinedText.AutoFit = _settings.AutoFitText;
			outlinedText.Wrap = _settings.WrapLongLines;
			outlinedText.MaximumLines = _settings.MaximumWrapLines;
			outlinedText.Margin = new Thickness(0.0, _settings.LineSpacing / 2.0, 0.0, _settings.LineSpacing / 2.0);
			ApplyPersonalSyncSelectionVisual(outlinedText);
		}
	}

	private void UpdateLyricControl(OutlinedText control, string text, int lineIndex, bool isActive, bool animate)
	{
		double num = (string.IsNullOrWhiteSpace(text) ? 0.0 : (isActive ? 1.0 : ((lineIndex < _activeLineIndex) ? _settings.PreviousLineOpacity : _settings.NextLineOpacity)));
		control.Fill = ResolveTextBrush(lineIndex, isActive);
		control.FontWeight = (isActive ? FontWeights.Bold : FontWeights.SemiBold);
		control.FontSize = (isActive ? _settings.FontSize : (_settings.FontSize * _settings.InactiveFontScale));
		ApplyPersonalSyncSelectionVisual(control);
		if (string.Equals(control.Text, text, StringComparison.Ordinal))
		{
			control.BeginAnimation(UIElement.OpacityProperty, null);
			control.Opacity = num;
			return;
		}
		control.Text = text;
		control.BeginAnimation(UIElement.OpacityProperty, null);
		control.Opacity = num;
		if (animate && !(num <= 0.0))
		{
			DoubleAnimation animation = new DoubleAnimation
			{
				From = Math.Min(num, 0.08),
				To = num,
				Duration = TimeSpan.FromMilliseconds(isActive ? 210 : 150),
				EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			control.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
		}
	}

	private void CenterActiveLine(int lineIndex, bool animate)
	{
		int num = lineIndex - _visibleFirstLineIndex;
		if (num < 0 || num >= _lineControls.Count)
		{
			return;
		}
		base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
		{
			int num2 = lineIndex - _visibleFirstLineIndex;
			if (num2 >= 0 && num2 < _lineControls.Count && !(LyricsScrollViewer.ViewportHeight <= 0.0))
			{
				OutlinedText outlinedText = _lineControls[num2];
				outlinedText.UpdateLayout();
				System.Windows.Point point = outlinedText.TranslatePoint(new System.Windows.Point(0.0, outlinedText.ActualHeight / 2.0), LyricsScrollViewer);
				string currentLinePosition = _settings.CurrentLinePosition;
				double num3 = ((currentLinePosition == "Top") ? 0.28 : ((!(currentLinePosition == "Bottom")) ? 0.5 : 0.72));
				double value = LyricsScrollViewer.VerticalOffset + point.Y - LyricsScrollViewer.ViewportHeight * num3;
				double max = Math.Max(0.0, LyricsScrollViewer.ExtentHeight - LyricsScrollViewer.ViewportHeight);
				value = Math.Clamp(value, 0.0, max);
				if (!animate)
				{
					_scrollAnimationActive = false;
					LyricsScrollViewer.ScrollToVerticalOffset(value);
				}
				else
				{
					_scrollAnimationStart = LyricsScrollViewer.VerticalOffset;
					_scrollAnimationTarget = value;
					_scrollAnimationStartedUtc = DateTime.UtcNow;
					_scrollAnimationActive = Math.Abs(_scrollAnimationTarget - _scrollAnimationStart) > 0.5;
				}
			}
		});
	}

	private void UpdateScrollAnimation()
	{
		if (_scrollAnimationActive)
		{
			double num = Math.Clamp((DateTime.UtcNow - _scrollAnimationStartedUtc).TotalMilliseconds / 260.0, 0.0, 1.0);
			double num2 = 1.0 - Math.Pow(1.0 - num, 3.0);
			LyricsScrollViewer.ScrollToVerticalOffset(_scrollAnimationStart + (_scrollAnimationTarget - _scrollAnimationStart) * num2);
			if (num >= 1.0)
			{
				_scrollAnimationActive = false;
			}
		}
	}

	private void QueueActiveLineRecentering()
	{
		if (!_resizeRecenterPending && _activeLineIndex >= 0)
		{
			_resizeRecenterPending = true;
			base.Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)delegate
			{
				_resizeRecenterPending = false;
				CenterActiveLine(_activeLineIndex, animate: false);
			});
		}
	}

	private System.Windows.Media.Brush ResolveTextBrush(int lyricIndex, bool isActive)
	{
		if (_settings.TextColorMode == "Fixed")
		{
			return CreateDisplayBrush(isActive ? _settings.CurrentTextColor : _settings.NextTextColor, 1.0, Colors.White);
		}
		int num = StableIndex($"{_activeTrackKey}|{_settings.RandomPaletteSeed}", int.MaxValue);
		if (_settings.TextColorMode == "TrackRandom")
		{
			CuratedColorPalette theme = ColorPalettes.GetTheme(num);
			return CreateDisplayBrush(isActive ? theme.Primary : theme.Secondary, 1.0, Colors.White);
		}
		return CreateDisplayBrush(ColorPalettes.GetAccent(StableIndex($"{num}|{lyricIndex}", int.MaxValue)), 1.0, Colors.White);
	}

	private static int StableIndex(string value, int count)
	{
		uint num = 2166136261u;
		foreach (char c in value)
		{
			num ^= c;
			num *= 16777619;
		}
		return (int)(num % (uint)Math.Max(1, count));
	}

	private void ApplyVisualSettings()
	{
		_settings.Normalize();
		bool showAllChanged = _showAllLyrics != _settings.ShowAllLyrics;
		_showAllLyrics = _settings.ShowAllLyrics;
		if (showAllChanged)
		{
			_fullLyricsLayoutKey = null;
			_plainLyricsLayoutKey = null;
			_lastLineIndex = int.MinValue;
			if (!_showAllLyrics)
			{
				DisableFullLyricsViewport();
			}
		}
		ApplyUiLanguage();
		base.Resources["UiAccentBrush"] = CreateDisplayBrush(_settings.UiColor, 1.0, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), preservePlayerUi: true, ignoreSourceAlpha: true);
		UpdatePlayerButtonBorders();
		OverlayPanel.Padding = new Thickness(_settings.PanelPadding);
		OverlayPanel.CornerRadius = new CornerRadius(_settings.CornerRadius);
		double backgroundOpacity = _settings.LyricsOnlyMode ? 0.0 : _settings.BackgroundOpacity;
		OverlayPanel.Background = CreateDisplayBrush(_settings.BackgroundColor, backgroundOpacity, Colors.Black, ignoreSourceAlpha: true);
		OverlayPanel.BorderBrush = CreateDisplayBrush(_settings.BorderColor, 1.0, Colors.White);
		OverlayPanel.BorderThickness = (!_settings.LyricsOnlyMode && _settings.ShowPanelBorder ? new Thickness(_settings.BorderThickness) : new Thickness(0.0));
		base.Opacity = _settings.OverlayOpacity;
		base.Topmost = _settings.AlwaysOnTop;
		_plainLyricsLayoutKey = null;
		// The compact rebuild below replaces the Viewbox contents. In full-lyrics
		// mode its cache key must be invalidated so RenderLyrics restores every line.
		if (_showAllLyrics)
		{
			_fullLyricsLayoutKey = null;
		}
		RebuildLineControls();
		UpdateChromeVisibility();
		UpdatePlaybackChrome();
		UpdateLockVisuals();
		UpdatePersonalSyncButton();
		_lastLineIndex = int.MinValue;
		RenderLyrics();
		if (_snapshot == null)
		{
			TrackStatusText.Text = "MEDIA SESSION / WAITING";
			TrackTitleText.Text = T("Play something in a media player");
			if (_settings.ShowStatusWhenIdle)
			{
				SetStatus(T("Play something in a media player"), T("Following the selected Windows Media Session"), animate: false);
			}
			else
			{
				SetStatus(string.Empty, string.Empty, animate: false);
			}
		}
	}

	private void UpdatePlayerButtonBorders()
	{
		System.Windows.Media.Brush accent = CreateDisplayBrush(_settings.UiColor, 1.0, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), preservePlayerUi: true, ignoreSourceAlpha: true);
		System.Windows.Media.Brush surface = CreatePlayerSurfaceBrush();
		System.Windows.Media.Brush icon = _settings.ReverseColors
			? new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 32, 30))
			: System.Windows.Media.Brushes.White;
		foreach (System.Windows.Controls.Button button in GetPlayerButtons())
		{
			button.BorderBrush = accent;
			button.BorderThickness = new Thickness(1.25);
			button.Background = surface;
			button.Foreground = icon;
			foreach (Shape shape in FindVisualChildren<Shape>(button))
			{
				shape.Fill = icon;
			}
			foreach (TextBlock text in FindVisualChildren<TextBlock>(button))
			{
				text.Foreground = icon;
			}
		}
		UpdateReverseColorsButtonVisual();
		UpdateOverlayChromeColors();
	}

	private System.Windows.Media.Brush CreatePlayerSurfaceBrush()
	{
		return new SolidColorBrush(_settings.ReverseColors
			? System.Windows.Media.Color.FromArgb(218, 222, 225, 222)
			: System.Windows.Media.Color.FromArgb(46, byte.MaxValue, byte.MaxValue, byte.MaxValue));
	}

	private IEnumerable<System.Windows.Controls.Button> GetPlayerButtons()
	{
		yield return PreviousButton;
		yield return PlayPauseButton;
		yield return NextButton;
		if (_reverseColorsButton != null)
		{
			yield return _reverseColorsButton;
		}
		yield return VolumeButton;
		yield return SettingsButton;
	}

	private string T(string key)
	{
		return LocalizationService.Translate(_settings.Language, key);
	}

	private void ApplyUiLanguage()
	{
		LocalizationService.SetCurrentLanguage(_settings.Language);
		base.Resources["DotFont"] = _englishDotFont;
		SettingsMenuItem.Header = T("Settings...");
		LockMenuItem.Header = T(_isLocked ? "Unlock" : "Lock");
		HideMenuItem.Header = T("Hide overlay") + "  (Ctrl+Alt+K)";
		PlaybackMenuItem.Header = T("Media controls");
		PreviousMenuItem.Header = T("Previous track");
		PlayPauseMenuItem.Header = T("Play / Pause");
		NextMenuItem.Header = T("Next track");
		CurrentTrackMenuItem.Header = T("Current track");
		ReloadLyricsMenuItem.Header = T("Reload lyrics");
		ClearLyricsMenuItem.Header = T("Clear saved lyrics");
		OffsetMenuItem.Header = T("Lyrics timing");
		EarlierSmallMenuItem.Header = T("0.1 s earlier");
		EarlierLargeMenuItem.Header = T("0.5 s earlier");
		LaterSmallMenuItem.Header = T("0.1 s later");
		LaterLargeMenuItem.Header = T("0.5 s later");
		ResetTrackOffsetMenuItem.Header = T("Reset track timing");
		ExitMenuItem.Header = T("Exit FlowLyrics");
		PreviousButton.ToolTip = T("Previous track");
		PlayPauseButton.ToolTip = T("Play / Pause");
		NextButton.ToolTip = T("Next track");
		VolumeButton.ToolTip = T("Volume");
		SettingsButton.ToolTip = T("Settings...");
		UpdateReverseColorsButtonVisual();
		_tray?.UpdateLanguage(_settings.Language);
		UpdateLockButtonVisual();
	}

	private void UpdateChromeVisibility()
	{
		double num = ((base.ActualHeight > 0.0) ? base.ActualHeight : base.Height);
		double num2 = ((base.ActualWidth > 0.0) ? base.ActualWidth : base.Width);
		bool flag = num >= 210.0 && num2 >= 260.0;
		bool flag2 = num >= 40.0 && num2 >= 76.0;
		bool flag3 = num >= 58.0 && num2 >= 210.0;
		bool flag4 = num >= 58.0 && num2 >= 310.0;
		bool flag5 = num >= 64.0;
		bool lyricsOnly = _settings.LyricsOnlyMode;
		TrackInfoPanel.Visibility = ((!(!lyricsOnly && _settings.ShowTrackInfo && flag)) ? Visibility.Collapsed : Visibility.Visible);
		HeaderPanel.Visibility = TrackInfoPanel.Visibility;
		PlaybackSeekSlider.Visibility = ((!(!lyricsOnly && _settings.ShowProgressBar && flag5)) ? Visibility.Collapsed : Visibility.Visible);
		if (_playbackTimeline != null)
		{
			_playbackTimeline.Visibility = PlaybackSeekSlider.Visibility;
		}
		ControlBar.Visibility = ((!(!lyricsOnly && _settings.ShowPlaybackControls && flag2)) ? Visibility.Collapsed : Visibility.Visible);
		PreviousButton.Visibility = ((!flag3) ? Visibility.Collapsed : Visibility.Visible);
		NextButton.Visibility = ((!flag3) ? Visibility.Collapsed : Visibility.Visible);
		VolumeButton.Visibility = ((!flag4) ? Visibility.Collapsed : Visibility.Visible);
		if (_reverseColorsButton != null)
		{
			_reverseColorsButton.Visibility = VolumeButton.Visibility;
		}
		LockButton.Visibility = Visibility.Visible;
		SettingsButton.Visibility = Visibility.Visible;
		bool flag6 = num < 68.0;
		PlayPauseButton.Width = (flag6 ? 34 : 50);
		PlayPauseButton.Height = (flag6 ? 34 : 50);
		PlayPauseIcon.Width = (flag6 ? 14 : 17);
		PlayPauseIcon.Height = (flag6 ? 15 : 18);
		FooterPanel.Visibility = ((PlaybackSeekSlider.Visibility != Visibility.Visible && ControlBar.Visibility != Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible);
		bool flag7 = num < 260.0 || num2 < 320.0;
		HeaderPanel.Margin = ((HeaderPanel.Visibility == Visibility.Visible) ? new Thickness(0.0, 0.0, 0.0, flag7 ? 6 : 14) : new Thickness(0.0));
		FooterPanel.Margin = ((FooterPanel.Visibility == Visibility.Visible) ? new Thickness(0.0, flag7 ? 5 : 14, 0.0, 0.0) : new Thickness(0.0));
		double uniformLength = Math.Min(_settings.PanelPadding, Math.Max(2.0, Math.Min(num2, num) * 0.06));
		OverlayPanel.Padding = new Thickness(uniformLength);
		OverlayPanel.Margin = (flag7 ? new Thickness(2.0) : new Thickness(7.0));
	}

	private void UpdatePlaybackChrome()
	{
		PlaybackSnapshot snapshot = _snapshot;
		if ((object)snapshot == null)
		{
			ShowPlayPauseVisual(showPause: false);
			PreviousButton.IsEnabled = false;
			PlayPauseButton.IsEnabled = false;
			NextButton.IsEnabled = false;
			LockButton.IsEnabled = true;
			VolumeButton.IsEnabled = false;
			if (_reverseColorsButton != null)
			{
				_reverseColorsButton.IsEnabled = true;
			}
			SettingsButton.IsEnabled = true;
			PlaybackMenuItem.IsEnabled = false;
			if (!_isSeeking)
			{
				PlaybackSeekSlider.Value = 0.0;
			}
			PlaybackSeekSlider.IsEnabled = false;
			UpdateLockButtonVisual();
		}
		else
		{
			ShowPlayPauseVisual(snapshot.IsPlaying);
			PreviousButton.IsEnabled = snapshot.CanSkipPrevious;
			PlayPauseButton.IsEnabled = snapshot.CanTogglePlayPause;
			NextButton.IsEnabled = snapshot.CanSkipNext;
			LockButton.IsEnabled = true;
			VolumeButton.IsEnabled = !string.IsNullOrWhiteSpace(snapshot.SourceAppUserModelId);
			if (_reverseColorsButton != null)
			{
				_reverseColorsButton.IsEnabled = true;
			}
			SettingsButton.IsEnabled = true;
			PlaybackSeekSlider.IsEnabled = snapshot.CanSeek && snapshot.Track.Duration > TimeSpan.Zero;
			PlaybackMenuItem.IsEnabled = true;
			TrackTitleText.Text = snapshot.Track.DisplayName;
			UpdateLockButtonVisual();
			UpdatePlaybackProgress();
		}
	}

	private void ShowPlayPauseVisual(bool showPause)
	{
		PlayPauseIcon.Visibility = (showPause ? Visibility.Collapsed : Visibility.Visible);
		PauseEyes.Visibility = ((!showPause) ? Visibility.Collapsed : Visibility.Visible);
		if (!showPause || !_settings.PauseEyeAnimation)
		{
			StopPauseEyeAnimation();
		}
		else if (!_pauseEyesAnimating)
		{
			_pauseEyesAnimating = true;
			_nextPauseBlinkUtc = DateTime.UtcNow.AddSeconds(Random.Shared.NextDouble() * 5.0 + 4.0);
		}
	}

	private void UpdatePauseEyes()
	{
		if (_pauseEyesAnimating && PauseEyes.Visibility == Visibility.Visible)
		{
			System.Drawing.Point position = System.Windows.Forms.Cursor.Position;
			System.Windows.Point point = PointFromScreen(new System.Windows.Point(position.X, position.Y));
			System.Windows.Point point2 = PlayPauseButton.TranslatePoint(new System.Windows.Point(PlayPauseButton.ActualWidth / 2.0, PlayPauseButton.ActualHeight / 2.0), this);
			double num = Math.Clamp((point.X - point2.X) / 75.0, -1.0, 1.0) * 5.2;
			double num2 = Math.Clamp((point.Y - point2.Y) / 75.0, -1.0, 1.0) * 3.0;
			PauseLookTransform.X += (num - PauseLookTransform.X) * 0.2;
			PauseLookTransform.Y += (num2 - PauseLookTransform.Y) * 0.2;
			if (DateTime.UtcNow >= _nextPauseBlinkUtc)
			{
				DoubleAnimation animation = new DoubleAnimation(1.0, 0.12, TimeSpan.FromMilliseconds(95.0))
				{
					AutoReverse = true,
					FillBehavior = FillBehavior.Stop
				};
				PauseBlinkTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
				_nextPauseBlinkUtc = DateTime.UtcNow.AddSeconds(Random.Shared.NextDouble() * 6.0 + 4.5);
			}
		}
	}

	private void StopPauseEyeAnimation()
	{
		PauseLookTransform.BeginAnimation(TranslateTransform.XProperty, null);
		PauseLookTransform.BeginAnimation(TranslateTransform.YProperty, null);
		PauseBlinkTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		PauseLookTransform.X = 0.0;
		PauseLookTransform.Y = 0.0;
		PauseBlinkTransform.ScaleY = 1.0;
		_pauseEyesAnimating = false;
	}

	private void UpdatePlaybackProgress()
	{
		if ((object)_snapshot == null || _snapshot.Track.Duration <= TimeSpan.Zero)
		{
			if (!_isSeeking)
			{
				PlaybackSeekSlider.Value = 0.0;
			}
			SetPlaybackTimeText(_playbackPositionText, TimeSpan.Zero);
			SetPlaybackTimeText(_playbackDurationText, TimeSpan.Zero);
			return;
		}
		TimeSpan position = _isSeeking
			? TimeSpan.FromMilliseconds(_snapshot.Track.Duration.TotalMilliseconds * Math.Clamp(PlaybackSeekSlider.Value, 0.0, 1.0))
			: _snapshot.EstimatedPosition(DateTimeOffset.UtcNow);
		double value = position.TotalMilliseconds / _snapshot.Track.Duration.TotalMilliseconds;
		if (!_isSeeking)
		{
			PlaybackSeekSlider.Value = Math.Clamp(value, 0.0, 1.0);
		}
		SetPlaybackTimeText(_playbackPositionText, position);
		SetPlaybackTimeText(_playbackDurationText, _snapshot.Track.Duration);
	}

	private void InitializePlaybackTimeline()
	{
		if (PlaybackSeekSlider.Parent is not System.Windows.Controls.Panel parent)
		{
			return;
		}
		int index = parent.Children.IndexOf(PlaybackSeekSlider);
		if (index < 0)
		{
			return;
		}
		parent.Children.RemoveAt(index);
		Grid timeline = new Grid
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
		timeline.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		_playbackPositionText = CreatePlaybackTimeText(System.Windows.HorizontalAlignment.Left);
		_playbackDurationText = CreatePlaybackTimeText(System.Windows.HorizontalAlignment.Right);
		Grid.SetColumn(_playbackPositionText, 0);
		Grid.SetColumn(PlaybackSeekSlider, 1);
		Grid.SetColumn(_playbackDurationText, 2);
		PlaybackSeekSlider.Margin = new Thickness(9.0, 0.0, 9.0, 0.0);
		timeline.Children.Add(_playbackPositionText);
		timeline.Children.Add(PlaybackSeekSlider);
		timeline.Children.Add(_playbackDurationText);
		parent.Children.Insert(index, timeline);
		_playbackTimeline = timeline;

		_seekHoverText = new TextBlock
		{
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = System.Windows.Media.Brushes.White,
			Text = "0:00"
		};
		_seekHoverPopup = new Popup
		{
			Placement = PlacementMode.Relative,
			PlacementTarget = PlaybackSeekSlider,
			AllowsTransparency = true,
			StaysOpen = true,
			IsHitTestVisible = false,
			Child = _seekHoverText
		};
		PlaybackSeekSlider.MouseEnter += PlaybackSeekSlider_MouseEnter;
		PlaybackSeekSlider.MouseMove += PlaybackSeekSlider_MouseMove;
		PlaybackSeekSlider.MouseLeave += PlaybackSeekSlider_MouseLeave;
		SetPlaybackTimeText(_playbackPositionText, TimeSpan.Zero);
		SetPlaybackTimeText(_playbackDurationText, TimeSpan.Zero);
	}

	private void InitializeVolumeIcon()
	{
		VolumeLabel.Visibility = Visibility.Collapsed;
		using System.IO.Stream stream = typeof(MainWindow).Assembly.GetManifestResourceStream("assets/player/volume.png")
			?? throw new InvalidOperationException("The volume icon resource is missing.");
		// Decode a small copy and crop the transparent source canvas once. At runtime the
		// alpha channel is used as a brush mask, so the selected player color remains live.
		BitmapImage bitmap = new BitmapImage();
		bitmap.BeginInit();
		bitmap.StreamSource = stream;
		bitmap.DecodePixelWidth = 256;
		bitmap.CacheOption = BitmapCacheOption.OnLoad;
		bitmap.EndInit();
		bitmap.Freeze();
		CroppedBitmap cropped = new CroppedBitmap(bitmap, new Int32Rect(41, 47, 175, 153));
		cropped.Freeze();
		ImageBrush mask = new ImageBrush(cropped)
		{
			Stretch = Stretch.Uniform
		};
		mask.Freeze();
		Rectangle icon = new Rectangle
		{
			Width = 19.0,
			Height = 16.5,
			Fill = System.Windows.Media.Brushes.White,
			OpacityMask = mask,
			IsHitTestVisible = false
		};
		_volumeIcon = icon;
		VolumeButton.Content = icon;
	}

	private void ApplyCompactUtilityControlSizing()
	{
		foreach (System.Windows.Controls.Button button in new[] { LockButton, VolumeButton, SettingsButton })
		{
			button.Width = 30.0;
			button.Height = 30.0;
			button.Margin = new Thickness(2.0, 0.0, 2.0, 0.0);
		}
		LockIcon.Width = 15.0;
		LockIcon.Height = 16.0;
		LockIcon.RenderTransform = new TranslateTransform(-1.0, 0.0);
		if (SettingsButton.Content is Grid settingsGlyph)
		{
			settingsGlyph.Width = 15.0;
			settingsGlyph.Height = 2.6;
			settingsGlyph.RenderTransform = new TranslateTransform(-0.8, 0.0);
			double[] columns = { 2.6, 3.6, 2.6, 3.6, 2.6 };
			for (int index = 0; index < Math.Min(columns.Length, settingsGlyph.ColumnDefinitions.Count); index++)
			{
				settingsGlyph.ColumnDefinitions[index].Width = new GridLength(columns[index]);
			}
			foreach (Ellipse dot in settingsGlyph.Children.OfType<Ellipse>())
			{
				dot.Width = 2.6;
				dot.Height = 2.6;
			}
		}
		VolumePopupSurface.Padding = new Thickness(7.0, 10.0, 7.0, 10.0);
		VolumePopupSurface.Margin = new Thickness(0.0);
		VolumePopupSurface.CornerRadius = new CornerRadius(8.5);
		VolumePopupSurface.BorderThickness = new Thickness(0.5);
		VolumeSlider.Width = 28.0;
	}

	private TextBlock CreateDotReverseIcon()
	{
		TextBlock icon = new TextBlock
		{
			Text = "R",
			FontFamily = _englishDotFont,
			FontSize = 16.5,
			FontWeight = FontWeights.Bold,
			LineHeight = 16.5,
			TextAlignment = TextAlignment.Center,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			RenderTransform = new TranslateTransform(1.2, 0.0),
			IsHitTestVisible = false
		};
		TextOptions.SetTextFormattingMode(icon, TextFormattingMode.Display);
		return icon;
	}

	private void UpdateVolumeIcon(bool muted)
	{
		if (_volumeIcon != null)
		{
			_volumeIcon.Opacity = muted ? 0.34 : 1.0;
		}
	}

	private void EnsureReverseColorsButton()
	{
		if (_reverseColorsButton != null)
		{
			return;
		}
		_reverseColorsIcon = CreateDotReverseIcon();
		_reverseColorsButton = new System.Windows.Controls.Button
		{
			Content = _reverseColorsIcon,
			ToolTip = "Reverse Colors",
			Tag = "NoTranslate",
			Width = 30.0,
			Height = 30.0,
			Margin = new Thickness(2.0, 0.0, 2.0, 0.0)
		};
		if (base.Resources["SmallMediaButton"] is Style style)
		{
			_reverseColorsButton.Style = style;
		}
		_reverseColorsButton.Click += ReverseColorsButton_Click;
		_reverseColorsButton.MouseEnter += delegate
		{
			CloseVolumePopup();
			if (_settings.ReverseColors) _reverseColorsButton.Opacity = 0.82;
		};
		_reverseColorsButton.MouseLeave += delegate
		{
			if (_settings.ReverseColors) _reverseColorsButton.Opacity = 1.0;
		};
		int index = Math.Max(0, RightControlGroup.Children.IndexOf(VolumeButton));
		RightControlGroup.Children.Insert(index, _reverseColorsButton);
	}

	private void ReverseColorsButton_Click(object sender, RoutedEventArgs e)
	{
		_settings.ReverseColors = !_settings.ReverseColors;
		_settingsWindow?.SetReverseColors(_settings.ReverseColors);
		ApplyVisualSettings();
		ScheduleSettingsSave();
	}

	private void UpdateReverseColorsButtonVisual()
	{
		if (_reverseColorsButton == null || _reverseColorsIcon == null)
		{
			return;
		}
		_reverseColorsButton.ToolTip = _settings.ReverseColors ? "Reverse Colors: On" : "Reverse Colors: Off";
		_reverseColorsIcon.Opacity = _settings.ReverseColors ? 1.0 : 0.72;
		_reverseColorsButton.Opacity = 1.0;
		if (_settings.ReverseColors)
		{
			System.Windows.Media.Color accent = ParseColor(_settings.UiColor, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44));
			System.Windows.Media.Brush icon = GetContrastingBrush(accent);
			_reverseColorsButton.Background = new SolidColorBrush(accent);
			_reverseColorsButton.Foreground = icon;
			foreach (Shape shape in FindVisualChildren<Shape>(_reverseColorsButton)) shape.Fill = icon;
			foreach (TextBlock text in FindVisualChildren<TextBlock>(_reverseColorsButton)) text.Foreground = icon;
		}
	}

	private void UpdateOverlayChromeColors()
	{
		System.Windows.Media.Brush content = _settings.ReverseColors
			? new SolidColorBrush(System.Windows.Media.Color.FromArgb(226, 29, 32, 30))
			: new SolidColorBrush(System.Windows.Media.Color.FromArgb(226, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		System.Windows.Media.Brush contentStrong = _settings.ReverseColors
			? new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 24, 23))
			: System.Windows.Media.Brushes.White;
		TrackStatusText.Foreground = content;
		TrackTitleText.Foreground = contentStrong;
		StatusDot.Fill = new SolidColorBrush(_trackStatusColor);
		if (_playbackPositionText != null)
		{
			_playbackPositionText.Foreground = content;
		}
		if (_playbackDurationText != null)
		{
			_playbackDurationText.Foreground = content;
		}
		if (_seekHoverText != null)
		{
			_seekHoverText.Foreground = contentStrong;
		}
		VolumePopupSurface.Background = CreatePlayerSurfaceBrush();
		VolumePopupSurface.BorderBrush = CreateDisplayBrush(_settings.UiColor, 1.0, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), preservePlayerUi: true, ignoreSourceAlpha: true);
		base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
		{
			System.Windows.Media.Brush rail = _settings.ReverseColors
				? new SolidColorBrush(System.Windows.Media.Color.FromArgb(94, 38, 41, 39))
				: new SolidColorBrush(System.Windows.Media.Color.FromArgb(66, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			System.Windows.Media.Color accentColor = ParseColor(_settings.UiColor, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44));
			foreach (Slider slider in new[] { PlaybackSeekSlider, VolumeSlider })
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
					if (repeat.Background is not SolidColorBrush brush || brush.Color.R != accentColor.R || brush.Color.G != accentColor.G || brush.Color.B != accentColor.B)
					{
						repeat.Background = rail;
					}
					Border? railSurface = FindVisualChildren<Border>(repeat).FirstOrDefault();
					if (railSurface != null)
					{
						bool decrease = ReferenceEquals(repeat.Command, Slider.DecreaseLarge);
						railSurface.CornerRadius = new CornerRadius(2.0);
						railSurface.Margin = slider.Orientation == Orientation.Vertical
							? (decrease ? new Thickness(0.0, 0.0, 0.0, 2.0) : new Thickness(0.0, 2.0, 0.0, 0.0))
							: (decrease ? new Thickness(0.0, 0.0, 2.0, 0.0) : new Thickness(2.0, 0.0, 0.0, 0.0));
					}
				}
			}
		});
		foreach (Thumb thumb in new[] { ResizeTopLeft, ResizeTopRight, ResizeBottomLeft, ResizeBottomRight })
		{
			foreach (Shape dot in FindVisualChildren<Shape>(thumb))
			{
				dot.Fill = content;
			}
		}
	}

	private TextBlock CreatePlaybackTimeText(System.Windows.HorizontalAlignment alignment)
	{
		return new TextBlock
		{
			MinWidth = 34.0,
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
			TextAlignment = alignment == System.Windows.HorizontalAlignment.Right ? TextAlignment.Right : TextAlignment.Left,
			FontFamily = _englishDotFont,
			FontSize = 9.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(222, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			Text = "0:00",
			Tag = "NoTranslate"
		};
	}

	private void PlaybackSeekSlider_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
	{
		UpdateSeekHover(e);
	}

	private void PlaybackSeekSlider_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		UpdateSeekHover(e);
	}

	private void PlaybackSeekSlider_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (_seekHoverPopup != null)
		{
			_seekHoverPopup.IsOpen = false;
		}
	}

	private void UpdateSeekHover(System.Windows.Input.MouseEventArgs e)
	{
		if (_seekHoverPopup == null || _seekHoverText == null || _snapshot == null || _snapshot.Track.Duration <= TimeSpan.Zero || PlaybackSeekSlider.ActualWidth <= 0.0)
		{
			return;
		}
		double x = Math.Clamp(e.GetPosition(PlaybackSeekSlider).X, 0.0, PlaybackSeekSlider.ActualWidth);
		double ratio = x / PlaybackSeekSlider.ActualWidth;
		_seekHoverText.Text = FormatPlaybackTime(TimeSpan.FromMilliseconds(_snapshot.Track.Duration.TotalMilliseconds * ratio));
		_seekHoverText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
		_seekHoverPopup.HorizontalOffset = x - _seekHoverText.DesiredSize.Width / 2.0 + 26.0;
		_seekHoverPopup.VerticalOffset = -_seekHoverText.DesiredSize.Height - 7.0;
		_seekHoverPopup.IsOpen = true;
	}

	private static void SetPlaybackTimeText(TextBlock? target, TimeSpan value)
	{
		if (target == null)
		{
			return;
		}
		string text = FormatPlaybackTime(value);
		if (!string.Equals(target.Text, text, StringComparison.Ordinal))
		{
			target.Text = text;
		}
	}

	private static string FormatPlaybackTime(TimeSpan value)
	{
		long totalSeconds = Math.Max(0L, (long)Math.Floor(value.TotalSeconds));
		long hours = totalSeconds / 3600L;
		long minutes = totalSeconds / 60L;
		long seconds = totalSeconds % 60L;
		return hours > 0L ? $"{hours}:{minutes % 60L:00}:{seconds:00}" : $"{minutes}:{seconds:00}";
	}

	private void SetTrackStatus(string status, System.Windows.Media.Color color)
	{
		TrackStatusText.Text = GetPlaybackSourceLabel(_snapshot) + " / " + status;
		TrackTitleText.Text = _snapshot?.Track.DisplayName ?? "FlowLyrics";
		_trackStatusColor = color;
		StatusDot.Fill = new SolidColorBrush(_trackStatusColor);
	}

	private static string GetPlaybackSourceLabel(PlaybackSnapshot? snapshot)
	{
		string source = snapshot?.SourceDisplayName;
		return string.IsNullOrWhiteSpace(source) ? "MEDIA SESSION" : source.Trim().ToUpperInvariant();
	}

	private static System.Windows.Media.Brush GetContrastingBrush(System.Windows.Media.Color color)
	{
		double luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
		return luminance > 0.58
			? new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 24, 23))
			: System.Windows.Media.Brushes.White;
	}

	private static int FindActiveLine(IReadOnlyList<LyricLine> lines, TimeSpan position)
	{
		int num = 0;
		int num2 = lines.Count - 1;
		int result = -1;
		while (num <= num2)
		{
			int num3 = num + (num2 - num) / 2;
			if (lines[num3].Time <= position)
			{
				result = num3;
				num = num3 + 1;
			}
			else
			{
				num2 = num3 - 1;
			}
		}
		return result;
	}

	private System.Windows.Media.Brush CreateDisplayBrush(string value, double opacity, System.Windows.Media.Color fallback, bool preservePlayerUi = false, bool ignoreSourceAlpha = false)
	{
		System.Windows.Media.Color color = ParseColor(value, fallback);
		if (_settings.ReverseColors && !preservePlayerUi)
		{
			color = ApplyReverseColor(color);
		}
		double sourceAlpha = ignoreSourceAlpha ? 1.0 : color.A / 255.0;
		SolidColorBrush brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
			(byte)Math.Round(Math.Clamp(opacity * sourceAlpha, 0.0, 1.0) * 255.0), color.R, color.G, color.B));
		brush.Freeze();
		return brush;
	}

	private System.Windows.Media.Color ApplyReverseColor(System.Windows.Media.Color color)
	{
		return _settings.ReverseColors
			? System.Windows.Media.Color.FromArgb(color.A, (byte)(byte.MaxValue - color.R), (byte)(byte.MaxValue - color.G), (byte)(byte.MaxValue - color.B))
			: color;
	}

	private static System.Windows.Media.Color ParseColor(string value, System.Windows.Media.Color fallback)
	{
		try
		{
			return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
		}
		catch
		{
			return fallback;
		}
	}

	private void ToggleLock()
	{
		ResumePlainLyricsAutoScrollFromCurrentPosition();
		SetLocked(!_isLocked, persist: true);
	}

	private void SetLocked(bool locked, bool persist)
	{
		if (locked)
		{
			_volumePopupCloseTimer.Stop();
			VolumePopup.IsOpen = false;
			_updatingVolume = false;
			Mouse.Capture(null);
			Keyboard.ClearFocus();
		}
		_isLocked = locked;
		if (persist)
		{
			_settings.IsLocked = locked;
			ScheduleSettingsSave();
		}
		ApplyWindowStyles();
		UpdateLockVisuals();
		_tray?.UpdateState(base.IsVisible, _isLocked);
	}

	private void ApplyWindowStyles()
	{
		if (_windowHandle == IntPtr.Zero)
		{
			return;
		}
		try
		{
			long extendedStyle = NativeMethods.GetExtendedStyle(_windowHandle);
			extendedStyle |= 0x80080;
			if (_isLocked)
			{
				extendedStyle |= 0x20;
				extendedStyle |= 0x8000000;
			}
			else
			{
				extendedStyle &= -134217761;
			}
			NativeMethods.SetExtendedStyle(_windowHandle, extendedStyle);
			_mousePassThroughEnabled = _isLocked;
		}
		catch
		{
		}
	}

	private void UpdateSelectiveClickThrough()
	{
		if (_windowHandle == IntPtr.Zero)
		{
			return;
		}
		bool flag = false;
		if (_isLocked)
		{
			flag = true;
			if (NativeMethods.GetCursorPos(out var point))
			{
				flag = !IsPointOverPlaybackButton(new System.Windows.Point(point.X, point.Y));
			}
		}
		if (_mousePassThroughEnabled == flag)
		{
			return;
		}
		try
		{
			long extendedStyle = NativeMethods.GetExtendedStyle(_windowHandle);
			extendedStyle = ((!flag) ? (extendedStyle & -33) : (extendedStyle | 0x20));
			NativeMethods.SetExtendedStyle(_windowHandle, extendedStyle);
			_mousePassThroughEnabled = flag;
		}
		catch
		{
		}
	}

	private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
	{
		const int WmEnterSizeMove = 0x0231;
		const int WmExitSizeMove = 0x0232;
		if (message == WmEnterSizeMove)
		{
			_isInteractiveResize = true;
			_resizeRefreshPending = false;
			_renderTimer.Stop();
			return IntPtr.Zero;
		}
		if (message == WmExitSizeMove)
		{
			_isInteractiveResize = false;
			CaptureWindowBounds();
			QueueResizeRefresh();
			return IntPtr.Zero;
		}
		if (message != 132 || !_isLocked)
		{
			return IntPtr.Zero;
		}
		nint num = lParam;
		long num2 = ((IntPtr)num).ToInt64();
		System.Windows.Point screenPoint = new System.Windows.Point((short)(num2 & 0xFFFF), (short)((num2 >> 16) & 0xFFFF));
		if (IsPointOverPlaybackButton(screenPoint))
		{
			return IntPtr.Zero;
		}
		handled = true;
		return new IntPtr(-1);
	}

	private void QueueResizeRefresh()
	{
		if (!_resizeRefreshPending)
		{
			if (_isInitialized)
			{
				_renderTimer.Start();
			}
			return;
		}
		_resizeRefreshPending = false;
		base.Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)delegate
		{
			UpdateChromeVisibility();
			if (_showAllLyrics)
			{
				_fullLyricsLayoutKey = null;
				UpdateFullLyricsViewport();
			}
			else if (_plainLyricsScrollMode)
			{
				_plainLyricsLayoutKey = null;
			}
			else
			{
				QueueActiveLineRecentering();
			}
			RenderLyrics();
			if (_isInitialized)
			{
				_renderTimer.Start();
			}
		});
	}

	private bool IsPointOverPlaybackButton(System.Windows.Point screenPoint)
	{
		if (VolumePopup.IsOpen && IsPointOverElement(VolumePopupSurface, screenPoint))
		{
			return true;
		}
		if (!IsPointOverElement(PreviousButton, screenPoint) && !IsPointOverElement(PlayPauseButton, screenPoint) && !IsPointOverElement(NextButton, screenPoint) && (_reverseColorsButton == null || !IsPointOverElement(_reverseColorsButton, screenPoint)) && !IsPointOverElement(VolumeButton, screenPoint) && !IsPointOverElement(LockButton, screenPoint) && !IsPointOverElement(SettingsButton, screenPoint) && !IsPointOverElement(PlaybackSeekSlider, screenPoint))
		{
			return IsPointOverElement(VolumeSlider, screenPoint);
		}
		return true;
	}

	private static bool IsPointOverElement(FrameworkElement element, System.Windows.Point screenPoint)
	{
		if (element.Visibility != Visibility.Visible || !element.IsEnabled || element.ActualWidth <= 0.0 || element.ActualHeight <= 0.0)
		{
			return false;
		}
		try
		{
			System.Windows.Point point = element.PointToScreen(new System.Windows.Point(0.0, 0.0));
			return new Rect(point.X - 4.0, point.Y - 4.0, element.ActualWidth + 8.0, element.ActualHeight + 8.0).Contains(screenPoint);
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private void UpdateLockVisuals()
	{
		Visibility visibility = (_isLocked ? Visibility.Collapsed : Visibility.Visible);
		ResizeTopLeft.Visibility = visibility;
		ResizeTopRight.Visibility = visibility;
		ResizeBottomLeft.Visibility = visibility;
		ResizeBottomRight.Visibility = visibility;
		LockMenuItem.Header = T(_isLocked ? "Unlock" : "Lock") + "  (Ctrl+Alt+L)";
		UpdateLockButtonVisual();
		UpdateChromeVisibility();
	}

	private void UpdateLockButtonVisual()
	{
		LockButton.ToolTip = T(_isLocked ? "Locked — click to unlock" : "Lock — click to enable");
		System.Windows.Media.Brush accent = CreateDisplayBrush(_settings.UiColor, 1.0, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), preservePlayerUi: true, ignoreSourceAlpha: true);
		LockButton.Background = _isLocked
			? CreateDisplayBrush(_settings.UiColor, 0.42, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), preservePlayerUi: true, ignoreSourceAlpha: true)
			: new SolidColorBrush(_settings.ReverseColors
				? System.Windows.Media.Color.FromArgb(218, 222, 225, 222)
				: System.Windows.Media.Color.FromArgb(46, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		LockButton.BorderBrush = accent;
		LockButton.BorderThickness = new Thickness(1.25);
		LockIcon.Fill = _isLocked
			? accent
			: (_settings.ReverseColors ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 32, 30)) : System.Windows.Media.Brushes.White);
	}

	private void ToggleVisibility()
	{
		_userHidden = base.IsVisible;
		if (!_userHidden)
		{
			_pauseHidden = false;
		}
		RefreshWindowVisibility();
	}

	private void RefreshWindowVisibility()
	{
		bool flag = !_userHidden && !_pauseHidden;
		if (flag && !base.IsVisible)
		{
			Show();
			base.Topmost = _settings.AlwaysOnTop;
		}
		else if (!flag && base.IsVisible)
		{
			Hide();
		}
		_tray?.UpdateState(base.IsVisible, _isLocked);
	}

	private void OpenSettings()
	{
		CloseVolumePopup();
		if (_settingsWindow != null)
		{
			_settingsWindow.ApplyAndClose();
			return;
		}
		_settingsBeforeWindow = _settings.Clone();
		SettingsWindow settingsWindow = new SettingsWindow(_settings.Clone(), _lyricsService.LyricsDirectory, _lyricsService, _mediaSessionService, _personalSyncStore, () => _snapshot, () => _snapshot?.Track, () => _lyricsLookup, GetPersonalSyncDiagnostics, async delegate
		{
			if (_snapshot != null)
			{
				await LoadLyricsAsync(_snapshot.Track, forceRefresh: false);
			}
		})
		{
			Owner = this
		};
		_settingsPreviewHandler = PreviewSettings;
		settingsWindow.PreviewChanged += _settingsPreviewHandler;
		settingsWindow.Closed += SettingsWindow_Closed;
		_settingsWindow = settingsWindow;
		settingsWindow.Show();
		settingsWindow.Activate();

		void PreviewSettings(AppSettings preview)
		{
			preview.IsLocked = _isLocked;
			preview.WindowLeft = base.Left;
			preview.WindowTop = base.Top;
			preview.WindowWidth = base.Width;
			preview.WindowHeight = base.Height;
			bool num3 = _settings.ShortcutsEnabled != preview.ShortcutsEnabled;
			bool autoScrollResumed = !_settings.PlainLyricsAutoScroll && preview.PlainLyricsAutoScroll;
			_settings = preview;
			_mediaSessionService.ConfigureSelection(_settings.PreferredMediaSourceId, _settings.IgnoredMediaSourceIds);
			if (autoScrollResumed) ResumePlainLyricsAutoScrollFromCurrentPosition();
			if (num3)
			{
				ConfigureHotkeys();
			}
			ApplyVisualSettings();
		}
	}

	private async void SettingsWindow_Closed(object? sender, EventArgs e)
	{
		if (sender is not SettingsWindow settingsWindow || !ReferenceEquals(settingsWindow, _settingsWindow))
		{
			return;
		}
		if (_settingsPreviewHandler != null)
		{
			settingsWindow.PreviewChanged -= _settingsPreviewHandler;
		}
		settingsWindow.Closed -= SettingsWindow_Closed;
		AppSettings original = _settingsBeforeWindow ?? _settings.Clone();
		_settingsWindow = null;
		_settingsBeforeWindow = null;
		_settingsPreviewHandler = null;
		if (settingsWindow.Accepted)
		{
			AppSettings resultSettings = settingsWindow.ResultSettings;
			resultSettings.IsLocked = _isLocked;
			resultSettings.WindowLeft = base.Left;
			resultSettings.WindowTop = base.Top;
			resultSettings.WindowWidth = base.Width;
			resultSettings.WindowHeight = base.Height;
			if (resultSettings.StartWithWindows != original.StartWithWindows && !StartupService.TrySetEnabled(resultSettings.StartWithWindows, out string error))
			{
				resultSettings.StartWithWindows = original.StartWithWindows;
				System.Windows.MessageBox.Show(this, string.Format(T("Could not change Windows startup settings.\n\n{0}"), error), "FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			bool shortcutsChanged = _settings.ShortcutsEnabled != resultSettings.ShortcutsEnabled;
			_settings = resultSettings;
			_mediaSessionService.ConfigureSelection(_settings.PreferredMediaSourceId, _settings.IgnoredMediaSourceIds);
			if (shortcutsChanged)
			{
				ConfigureHotkeys();
			}
			ApplyVisualSettings();
			await SaveSettingsSafeAsync();
		}
		else
		{
			bool shortcutsChanged = _settings.ShortcutsEnabled != original.ShortcutsEnabled;
			_settings = original;
			_mediaSessionService.ConfigureSelection(_settings.PreferredMediaSourceId, _settings.IgnoredMediaSourceIds);
			if (shortcutsChanged)
			{
				ConfigureHotkeys();
			}
			ApplyVisualSettings();
		}
	}

	private void CaptureWindowBounds()
	{
		if (_isInitialized && base.WindowState == WindowState.Normal)
		{
			_settings.WindowLeft = base.Left;
			_settings.WindowTop = base.Top;
			_settings.WindowWidth = base.ActualWidth;
			_settings.WindowHeight = base.ActualHeight;
			if (!_isInteractiveResize)
			{
				ScheduleSettingsSave();
			}
		}
	}

	private void RestoreWindowPosition()
	{
		base.Width = _settings.WindowWidth;
		base.Height = _settings.WindowHeight;
		double virtualScreenLeft = SystemParameters.VirtualScreenLeft;
		double virtualScreenTop = SystemParameters.VirtualScreenTop;
		double num = virtualScreenLeft + SystemParameters.VirtualScreenWidth;
		double num2 = virtualScreenTop + SystemParameters.VirtualScreenHeight;
		double num3 = SystemParameters.WorkArea.Left + Math.Max(0.0, (SystemParameters.WorkArea.Width - base.Width) / 2.0);
		double num4 = SystemParameters.WorkArea.Bottom - base.Height - 36.0;
		double value = _settings.WindowLeft ?? num3;
		double value2 = _settings.WindowTop ?? num4;
		base.Left = Math.Clamp(value, virtualScreenLeft - base.Width + 80.0, num - 80.0);
		base.Top = Math.Clamp(value2, virtualScreenTop, num2 - 50.0);
	}

	private void ScheduleSettingsSave()
	{
		if (_isInitialized)
		{
			_saveTimer.Stop();
			_saveTimer.Start();
		}
	}

	private async Task SaveSettingsSafeAsync()
	{
		try
		{
			await _settingsService.SaveAsync(_settings);
		}
		catch
		{
		}
	}

	private async void AdjustCurrentTrackOffset(int deltaMilliseconds)
	{
		if (_snapshot == null || _lyrics?.HasSyncedLyrics != true || _lyricsLookup == null) return;
		if (_personalSyncEditingProfile != null)
		{
			AdjustPersonalSyncOffset(-deltaMilliseconds / 1000.0);
			return;
		}
		PersonalSyncProfile profile = _personalSyncActiveProfile?.Clone() ?? CreateCurrentPersonalSyncProfile();
		profile.Mode = PersonalSyncMode.Offset;
		profile.OffsetSeconds = Math.Clamp(Math.Round((profile.OffsetSeconds - deltaMilliseconds / 1000.0) * 10.0) / 10.0, -120.0, 120.0);
		profile.Anchors.Clear();
		profile.Segments.Clear();
		try
		{
			profile = await _personalSyncStore.UpsertAsync(profile);
			_personalSyncActiveProfile = profile;
			_personalSyncResolution = new PersonalSyncResolution(profile, false);
			UpdatePersonalSyncButton();
			InvalidatePersonalSyncRendering();
		}
		catch { }
	}

	private int GetCurrentTrackOffset()
	{
		if ((object)_snapshot == null)
		{
			return 0;
		}
		return _personalSyncActiveProfile == null
			? 0
			: -(int)Math.Round(_personalSyncActiveProfile.OffsetSeconds * 1000.0);
	}

	private async void ExitApplication()
	{
		if (!_allowClose)
		{
			_allowClose = true;
			_mediaTimer.Stop();
			_renderTimer.Stop();
			_saveTimer.Stop();
			_volumePopupCloseTimer.Stop();
			_localLrcReloadTimer.Stop();
			_lyricsCancellation?.Cancel();
			CaptureWindowBounds();
			await SaveSettingsSafeAsync();
			_hotkeys?.Dispose();
			_tray?.Dispose();
			_mediaSessionService.Dispose();
			_lyricsService.Dispose();
			_windowSource?.RemoveHook(WindowMessageHook);
			Close();
			System.Windows.Application.Current.Shutdown();
		}
	}

	private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
	{
		while (child != null)
		{
			if (child is T result)
			{
				return result;
			}
			child = VisualTreeHelper.GetParent(child);
		}
		return null;
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

	private void ResizeCorner_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (!_isLocked && sender is Thumb thumb)
		{
			int value = ((thumb == ResizeTopLeft) ? 13 : ((thumb == ResizeTopRight) ? 14 : ((thumb == ResizeBottomLeft) ? 16 : 17)));
			e.Handled = true;
			NativeMethods.ReleaseCapture();
			NativeMethods.SendMessage(_windowHandle, 161, new IntPtr(value), IntPtr.Zero);
		}
	}

	private void OverlayMenu_Opened(object sender, RoutedEventArgs e)
	{
		LockMenuItem.Header = T(_isLocked ? "Unlock" : "Lock") + "  (Ctrl+Alt+L)";
		int currentTrackOffset = GetCurrentTrackOffset();
		OffsetMenuItem.Header = (((object)_snapshot == null) ? T("Lyrics timing") : $"{T("Lyrics timing")}  ({(double)currentTrackOffset / 1000.0:+0.0;-0.0;0.0} s)");
		PlaybackMenuItem.IsEnabled = (object)_snapshot != null;
	}

	private async Task RunPlaybackCommandAsync(Func<MediaSessionService, CancellationToken, Task<bool>> command)
	{
		_ = 2;
		try
		{
			if (!(await command(_mediaSessionService, CancellationToken.None)))
			{
				throw new InvalidOperationException("The selected media session rejected the command.");
			}
			await Task.Delay(90);
			await PollMediaAsync();
		}
		catch
		{
			_tray?.ShowMessage("FlowLyrics", T("Could not control the selected media player. Start playback and try again."));
		}
	}

	private void Settings_Click(object sender, RoutedEventArgs e)
	{
		OpenSettings();
	}

	private void Lock_Click(object sender, RoutedEventArgs e)
	{
		ToggleLock();
	}

	private void Hide_Click(object sender, RoutedEventArgs e)
	{
		HideOverlay();
	}

	private void Exit_Click(object sender, RoutedEventArgs e)
	{
		ExitApplication();
	}

	private async void Previous_Click(object sender, RoutedEventArgs e)
	{
		await RunPlaybackCommandAsync((MediaSessionService service, CancellationToken token) => service.TrySkipPreviousAsync(token));
	}

	private async void PlayPause_Click(object sender, RoutedEventArgs e)
	{
		await RunPlaybackCommandAsync((MediaSessionService service, CancellationToken token) => service.TryTogglePlayPauseAsync(token));
	}

	private async void Next_Click(object sender, RoutedEventArgs e)
	{
		await RunPlaybackCommandAsync((MediaSessionService service, CancellationToken token) => service.TrySkipNextAsync(token));
	}

	private void LockButton_Click(object sender, RoutedEventArgs e)
	{
		ToggleLock();
	}

	private async void PlaybackSeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_pendingSeekRatio = null;
		_seekWasDirectClick = false;
		if (!(PlaybackSeekSlider.ActualWidth > 0.0) || (object)_snapshot == null || !_snapshot.CanSeek || !(_snapshot.Track.Duration > TimeSpan.Zero))
		{
			return;
		}
		_isSeeking = true;
		if (FindVisualParent<Thumb>(e.OriginalSource as DependencyObject) != null)
		{
			return;
		}
		double num = Math.Clamp(e.GetPosition(PlaybackSeekSlider).X / PlaybackSeekSlider.ActualWidth, 0.0, 1.0);
		_seekWasDirectClick = true;
		e.Handled = true;
		PlaybackSeekSlider.Value = num;
		TimeSpan destination = TimeSpan.FromMilliseconds(_snapshot.Track.Duration.TotalMilliseconds * num);
		try
		{
			await RunPlaybackCommandAsync((MediaSessionService service, CancellationToken token) => service.TrySeekAsync(destination, token));
		}
		finally
		{
			_isSeeking = false;
		}
	}

	private async void PlaybackSeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_seekWasDirectClick)
		{
			_seekWasDirectClick = false;
			e.Handled = true;
			return;
		}
		if (!_isSeeking || (object)_snapshot == null || !_snapshot.CanSeek || _snapshot.Track.Duration <= TimeSpan.Zero)
		{
			_isSeeking = false;
			_pendingSeekRatio = null;
			return;
		}
		double num = _pendingSeekRatio ?? Math.Clamp(PlaybackSeekSlider.Value, 0.0, 1.0);
		_pendingSeekRatio = null;
		TimeSpan destination = TimeSpan.FromMilliseconds(_snapshot.Track.Duration.TotalMilliseconds * num);
		try
		{
			await RunPlaybackCommandAsync((MediaSessionService service, CancellationToken token) => service.TrySeekAsync(destination, token));
		}
		finally
		{
			_isSeeking = false;
		}
	}

	private void VolumeHover_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
	{
		_volumeLastInsideUtc = DateTime.UtcNow;
		_volumeWriteTimer.Stop();
		_pendingSessionVolume = null;
		_volumeSourceId = _snapshot?.SourceAppUserModelId?.Trim() ?? string.Empty;
		_updatingVolume = true;
		double volume = _lastSessionVolume;
		bool muted = false;
		bool num = !string.IsNullOrWhiteSpace(_volumeSourceId)
			&& _systemVolumeService.TryGetVolume(_volumeSourceId, out volume, out muted);
		VolumeSlider.IsEnabled = num;
		if (num)
		{
			_lastSessionVolume = volume;
			VolumeSlider.Value = _lastSessionVolume;
			VolumeLabel.Text = (muted ? "MUTE" : "VOL");
			UpdateVolumeIcon(muted);
		}
		else
		{
			VolumeSlider.Value = _lastSessionVolume;
			VolumeLabel.Text = "VOL";
			UpdateVolumeIcon(muted: false);
		}
		_updatingVolume = false;
		if (!VolumePopup.IsOpen)
		{
			// Start every opening from the mathematical center. Once WPF creates the
			// popup HWND, AlignVolumePopupToButton corrects its actual device-pixel
			// center so DPI rounding and Popup chrome cannot shift the frame.
			VolumePopup.HorizontalOffset = 0.0;
			VolumePopup.VerticalOffset = 0.0;
			VolumePopup.IsOpen = true;
		}
		if (!_volumePopupCloseTimer.IsEnabled)
		{
			_volumePopupCloseTimer.Start();
		}
	}

	private void VolumeHover_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (VolumePopup.IsOpen && !_volumePopupCloseTimer.IsEnabled)
		{
			_volumePopupCloseTimer.Start();
		}
	}

	private bool IsMouseOverVolumeControls()
	{
		if (!VolumePopup.IsOpen)
		{
			return false;
		}
		try
		{
			System.Drawing.Point cursor = System.Windows.Forms.Cursor.Position;
			System.Windows.Point screenPoint = new System.Windows.Point(cursor.X, cursor.Y);
			if (IsPointOverElement(VolumeButton, screenPoint) || IsPointOverElement(VolumePopupSurface, screenPoint))
			{
				return true;
			}
			if (TryGetElementScreenRect(VolumeButton, out Rect buttonRect) && TryGetElementScreenRect(VolumePopupSurface, out Rect popupRect))
			{
				Rect hoverBridge = Rect.Union(buttonRect, popupRect);
				hoverBridge.Inflate(5.0, 6.0);
				return hoverBridge.Contains(screenPoint);
			}
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private static bool TryGetElementScreenRect(FrameworkElement element, out Rect rect)
	{
		rect = Rect.Empty;
		if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0.0 || element.ActualHeight <= 0.0)
		{
			return false;
		}
		try
		{
			System.Windows.Point origin = element.PointToScreen(new System.Windows.Point(0.0, 0.0));
			rect = new Rect(origin.X, origin.Y, element.ActualWidth, element.ActualHeight);
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private void CloseVolumePopup()
	{
		_volumePopupCloseTimer.Stop();
		VolumePopup.IsOpen = false;
		if (_isDirectVolumeDrag)
		{
			_isDirectVolumeDrag = false;
			if (VolumeSlider.IsMouseCaptured)
			{
				VolumeSlider.ReleaseMouseCapture();
			}
		}
	}

	private void VolumeSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (FindVisualParent<Thumb>(e.OriginalSource as DependencyObject) == null && VolumeSlider.ActualHeight > 0.0)
		{
			_isDirectVolumeDrag = true;
			VolumeSlider.CaptureMouse();
			UpdateVolumeFromPointer(e);
			e.Handled = true;
		}
	}

	private void VolumeSlider_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (_isDirectVolumeDrag && e.LeftButton == MouseButtonState.Pressed)
		{
			UpdateVolumeFromPointer(e);
			e.Handled = true;
		}
	}

	private void VolumeSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_isDirectVolumeDrag)
		{
			UpdateVolumeFromPointer(e);
			_isDirectVolumeDrag = false;
			VolumeSlider.ReleaseMouseCapture();
			e.Handled = true;
		}
	}

	private void UpdateVolumeFromPointer(System.Windows.Input.MouseEventArgs e)
	{
		double num = 1.0 - Math.Clamp(e.GetPosition(VolumeSlider).Y / Math.Max(1.0, VolumeSlider.ActualHeight), 0.0, 1.0);
		VolumeSlider.Value = VolumeSlider.Minimum + (VolumeSlider.Maximum - VolumeSlider.Minimum) * num;
	}

	private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_updatingVolume && base.IsLoaded)
		{
			_lastSessionVolume = e.NewValue;
			_pendingSessionVolume = e.NewValue;
			VolumeLabel.Text = "VOL";
			UpdateVolumeIcon(muted: false);
			if (!_volumeWriteTimer.IsEnabled)
			{
				_volumeWriteTimer.Start();
			}
		}
	}

	private void VolumeButton_Click(object sender, RoutedEventArgs e)
	{
		_volumeWriteTimer.Stop();
		_pendingSessionVolume = null;
		_volumeSourceId = _snapshot?.SourceAppUserModelId?.Trim() ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(_volumeSourceId)
			&& _systemVolumeService.TryToggleMute(_volumeSourceId)
			&& _systemVolumeService.TryGetVolume(_volumeSourceId, out var volume, out var muted))
		{
			_updatingVolume = true;
			_lastSessionVolume = volume;
			VolumeSlider.Value = volume;
			VolumeLabel.Text = (muted ? "MUTE" : "VOL");
			UpdateVolumeIcon(muted);
			_updatingVolume = false;
		}
	}

	private static CustomPopupPlacement[] PlaceVolumePopup(System.Windows.Size popupSize, System.Windows.Size targetSize, System.Windows.Point offset)
	{
		// Center the complete popup surface on the button. The slider template uses
		// the exact same centerline, so neither its 28 px track nor thumb can overflow.
		double x = Math.Round((targetSize.Width - popupSize.Width) / 2.0, MidpointRounding.AwayFromZero) + offset.X;
		double y = 0.0 - popupSize.Height - 2.0 + offset.Y;
		return new CustomPopupPlacement[1]
		{
			new CustomPopupPlacement(new System.Windows.Point(x, y), PopupPrimaryAxis.Vertical)
		};
	}

	private void AlignVolumePopupToButton()
	{
		if (!VolumePopup.IsOpen || VolumeButton.ActualWidth <= 0.0 || VolumePopupSurface.ActualWidth <= 0.0)
		{
			return;
		}
		try
		{
			System.Windows.Point buttonCenter = VolumeButton.PointToScreen(new System.Windows.Point(VolumeButton.ActualWidth / 2.0, 0.0));
			System.Windows.Point popupCenter = VolumePopupSurface.PointToScreen(new System.Windows.Point(VolumePopupSurface.ActualWidth / 2.0, 0.0));
			double correctionPixels = buttonCenter.X - popupCenter.X;
			if (Math.Abs(correctionPixels) < 0.25)
			{
				return;
			}
			DpiScale dpi = VisualTreeHelper.GetDpi(VolumeButton);
			double correctionDip = correctionPixels / Math.Max(0.01, dpi.DpiScaleX);
			VolumePopup.HorizontalOffset = Math.Round((VolumePopup.HorizontalOffset + correctionDip) * 2.0, MidpointRounding.AwayFromZero) / 2.0;
		}
		catch (InvalidOperationException)
		{
		}
	}

	private void LyricsEarlierSmall_Click(object sender, RoutedEventArgs e)
	{
		AdjustCurrentTrackOffset(100);
	}

	private void LyricsEarlierLarge_Click(object sender, RoutedEventArgs e)
	{
		AdjustCurrentTrackOffset(500);
	}

	private void LyricsLaterSmall_Click(object sender, RoutedEventArgs e)
	{
		AdjustCurrentTrackOffset(-100);
	}

	private void LyricsLaterLarge_Click(object sender, RoutedEventArgs e)
	{
		AdjustCurrentTrackOffset(-500);
	}

	private async void ResetTrackOffset_Click(object sender, RoutedEventArgs e)
	{
		if (_snapshot == null) return;
		if (_personalSyncEditingProfile != null)
		{
			ResetPersonalSync();
			return;
		}
		try
		{
			if (_personalSyncActiveProfile != null) await _personalSyncStore.DeleteAsync(_personalSyncActiveProfile.Id);
			_settings.TrackOffsetsMs.Remove(_snapshot.Track.CacheKey);
			_personalSyncActiveProfile = null;
			_personalSyncResolution = new PersonalSyncResolution(null, false);
			UpdatePersonalSyncButton();
			InvalidatePersonalSyncRendering();
			ScheduleSettingsSave();
			RefreshPersonalSyncResolution(force: true);
		}
		catch { }
	}

	private async void ReloadLyrics_Click(object sender, RoutedEventArgs e)
	{
		if ((object)_snapshot != null)
		{
			SetTrackStatus("SEARCHING", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
			await LoadLyricsAsync(_snapshot.Track, forceRefresh: true);
		}
	}

	private async void ClearLyricsCache_Click(object sender, RoutedEventArgs e)
	{
		if ((object)_snapshot != null)
		{
			await _lyricsService.ClearTrackCacheAsync(_snapshot.Track);
			SetTrackStatus("SEARCHING", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
			await LoadLyricsAsync(_snapshot.Track, forceRefresh: true);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.10.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/FlowLyrics;component/flowlyrics.mainwindow.xaml", UriKind.Relative);
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
			OverlayMenu = (System.Windows.Controls.ContextMenu)target;
			OverlayMenu.Opened += OverlayMenu_Opened;
			break;
		case 2:
			SettingsMenuItem = (System.Windows.Controls.MenuItem)target;
			SettingsMenuItem.Click += Settings_Click;
			break;
		case 3:
			LockMenuItem = (System.Windows.Controls.MenuItem)target;
			LockMenuItem.Click += Lock_Click;
			break;
		case 4:
			HideMenuItem = (System.Windows.Controls.MenuItem)target;
			HideMenuItem.Click += Hide_Click;
			break;
		case 5:
			PlaybackMenuItem = (System.Windows.Controls.MenuItem)target;
			break;
		case 6:
			PreviousMenuItem = (System.Windows.Controls.MenuItem)target;
			PreviousMenuItem.Click += Previous_Click;
			break;
		case 7:
			PlayPauseMenuItem = (System.Windows.Controls.MenuItem)target;
			PlayPauseMenuItem.Click += PlayPause_Click;
			break;
		case 8:
			NextMenuItem = (System.Windows.Controls.MenuItem)target;
			NextMenuItem.Click += Next_Click;
			break;
		case 9:
			CurrentTrackMenuItem = (System.Windows.Controls.MenuItem)target;
			break;
		case 10:
			ReloadLyricsMenuItem = (System.Windows.Controls.MenuItem)target;
			ReloadLyricsMenuItem.Click += ReloadLyrics_Click;
			break;
		case 11:
			ClearLyricsMenuItem = (System.Windows.Controls.MenuItem)target;
			ClearLyricsMenuItem.Click += ClearLyricsCache_Click;
			break;
		case 12:
			OffsetMenuItem = (System.Windows.Controls.MenuItem)target;
			break;
		case 13:
			EarlierSmallMenuItem = (System.Windows.Controls.MenuItem)target;
			EarlierSmallMenuItem.Click += LyricsEarlierSmall_Click;
			break;
		case 14:
			EarlierLargeMenuItem = (System.Windows.Controls.MenuItem)target;
			EarlierLargeMenuItem.Click += LyricsEarlierLarge_Click;
			break;
		case 15:
			LaterSmallMenuItem = (System.Windows.Controls.MenuItem)target;
			LaterSmallMenuItem.Click += LyricsLaterSmall_Click;
			break;
		case 16:
			LaterLargeMenuItem = (System.Windows.Controls.MenuItem)target;
			LaterLargeMenuItem.Click += LyricsLaterLarge_Click;
			break;
		case 17:
			ResetTrackOffsetMenuItem = (System.Windows.Controls.MenuItem)target;
			ResetTrackOffsetMenuItem.Click += ResetTrackOffset_Click;
			break;
		case 18:
			ExitMenuItem = (System.Windows.Controls.MenuItem)target;
			ExitMenuItem.Click += Exit_Click;
			break;
		case 19:
			HitTestRoot = (Grid)target;
			break;
		case 20:
			OverlayPanel = (Border)target;
			break;
		case 21:
			HeaderRow = (RowDefinition)target;
			break;
		case 22:
			FooterRow = (RowDefinition)target;
			break;
		case 23:
			HeaderPanel = (Grid)target;
			break;
		case 24:
			TrackInfoPanel = (StackPanel)target;
			break;
		case 25:
			StatusDot = (Ellipse)target;
			break;
		case 26:
			TrackStatusText = (TextBlock)target;
			break;
		case 27:
			TrackTitleText = (TextBlock)target;
			break;
		case 28:
			LyricsPanel = (Grid)target;
			break;
		case 29:
			LyricsScrollViewer = (ScrollViewer)target;
			break;
		case 30:
			LyricsStackPanel = (StackPanel)target;
			break;
		case 31:
			FooterPanel = (StackPanel)target;
			break;
		case 32:
			PlaybackSeekSlider = (Slider)target;
			PlaybackSeekSlider.PreviewMouseLeftButtonDown += PlaybackSeekSlider_PreviewMouseLeftButtonDown;
			PlaybackSeekSlider.PreviewMouseLeftButtonUp += PlaybackSeekSlider_PreviewMouseLeftButtonUp;
			break;
		case 33:
			ControlBar = (Grid)target;
			break;
		case 34:
			LeftControlGroup = (StackPanel)target;
			break;
		case 35:
			LockButton = (System.Windows.Controls.Button)target;
			LockButton.Click += LockButton_Click;
			break;
		case 36:
			LockIcon = (Path)target;
			break;
		case 37:
			TransportControlGroup = (StackPanel)target;
			break;
		case 38:
			PreviousButton = (System.Windows.Controls.Button)target;
			PreviousButton.Click += Previous_Click;
			break;
		case 39:
			PlayPauseButton = (System.Windows.Controls.Button)target;
			PlayPauseButton.Click += PlayPause_Click;
			break;
		case 40:
			PlayPauseIcon = (Path)target;
			break;
		case 41:
			PauseEyes = (Grid)target;
			break;
		case 42:
			PauseBlinkTransform = (ScaleTransform)target;
			break;
		case 43:
			PauseLookTransform = (TranslateTransform)target;
			break;
		case 44:
			NextButton = (System.Windows.Controls.Button)target;
			NextButton.Click += Next_Click;
			break;
		case 45:
			RightControlGroup = (StackPanel)target;
			break;
		case 46:
			VolumeButton = (System.Windows.Controls.Button)target;
			VolumeButton.Click += VolumeButton_Click;
			VolumeButton.MouseEnter += VolumeHover_MouseEnter;
			VolumeButton.MouseLeave += VolumeHover_MouseLeave;
			break;
		case 47:
			VolumeLabel = (TextBlock)target;
			break;
		case 48:
			SettingsButton = (System.Windows.Controls.Button)target;
			SettingsButton.Click += Settings_Click;
			break;
		case 49:
			VolumePopup = (Popup)target;
			break;
		case 50:
			VolumePopupSurface = (Border)target;
			VolumePopupSurface.MouseEnter += VolumeHover_MouseEnter;
			VolumePopupSurface.MouseLeave += VolumeHover_MouseLeave;
			break;
		case 51:
			VolumeSlider = (Slider)target;
			VolumeSlider.PreviewMouseLeftButtonDown += VolumeSlider_PreviewMouseLeftButtonDown;
			VolumeSlider.PreviewMouseMove += VolumeSlider_PreviewMouseMove;
			VolumeSlider.PreviewMouseLeftButtonUp += VolumeSlider_PreviewMouseLeftButtonUp;
			VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
			break;
		case 52:
			ResizeTopLeft = (Thumb)target;
			ResizeTopLeft.PreviewMouseLeftButtonDown += ResizeCorner_PreviewMouseLeftButtonDown;
			break;
		case 53:
			ResizeTopRight = (Thumb)target;
			ResizeTopRight.PreviewMouseLeftButtonDown += ResizeCorner_PreviewMouseLeftButtonDown;
			break;
		case 54:
			ResizeBottomLeft = (Thumb)target;
			ResizeBottomLeft.PreviewMouseLeftButtonDown += ResizeCorner_PreviewMouseLeftButtonDown;
			break;
		case 55:
			ResizeBottomRight = (Thumb)target;
			ResizeBottomRight.PreviewMouseLeftButtonDown += ResizeCorner_PreviewMouseLeftButtonDown;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
