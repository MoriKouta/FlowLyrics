using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlowLyrics.Controls;
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

	private double _lastSpotifyVolume = 1.0;

	private double? _pendingSpotifyVolume;

	private bool _resizeRecenterPending;

	private readonly Bitmap _backdropSampleBitmap;

	private readonly Graphics _backdropSampleGraphics;

	private System.Windows.Media.Color _sampledBackdropColor = System.Windows.Media.Color.FromRgb(45, 45, 45);

	private DateTime _nextBackdropSampleUtc;

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
		_backdropSampleBitmap = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		_backdropSampleGraphics = Graphics.FromImage(_backdropSampleBitmap);
		VolumePopup.CustomPopupPlacementCallback = PlaceVolumePopup;
		_englishDotFont = (System.Windows.Media.FontFamily)base.Resources["DotFont"];
		_settings = _settingsService.Load();
		LocalizationService.SetCurrentLanguage(_settings.Language);
		_lyricsService = new LyricsService(_settingsService.AppDataDirectory);
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
			UpdateBackdropSampleIfNeeded();
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
			Interval = TimeSpan.FromMilliseconds(450L)
		};
		_volumePopupCloseTimer.Tick += delegate
		{
			_volumePopupCloseTimer.Stop();
			if (!VolumeButton.IsMouseOver && !VolumePopupSurface.IsMouseOver)
			{
				VolumePopup.IsOpen = false;
			}
		};
		_volumeWriteTimer = new DispatcherTimer(DispatcherPriority.Input)
		{
			Interval = TimeSpan.FromMilliseconds(40L)
		};
		_volumeWriteTimer.Tick += delegate
		{
			_volumeWriteTimer.Stop();
			double? pendingSpotifyVolume = _pendingSpotifyVolume;
			if (pendingSpotifyVolume.HasValue)
			{
				double valueOrDefault = pendingSpotifyVolume.GetValueOrDefault();
				_pendingSpotifyVolume = null;
				_systemVolumeService.TrySetVolume(valueOrDefault);
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
				_lastLineIndex = int.MinValue;
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
			UpdateChromeVisibility();
			QueueActiveLineRecentering();
		};
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
			PlaybackSnapshot playbackSnapshot = await _mediaSessionService.GetSpotifySnapshotAsync();
			if ((object)playbackSnapshot == null)
			{
				_snapshot = null;
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
					_lyricsCancellation?.Cancel();
					_lastLineIndex = int.MinValue;
				}
				TrackStatusText.Text = "SPOTIFY / WAITING";
				TrackTitleText.Text = T("Play something in Spotify");
				StatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(142, 151, 166));
				if (_settings.ShowStatusWhenIdle)
				{
					SetStatus(T("Play something in Spotify"), T("Following Spotify for Windows automatically"), animate: false);
				}
				else
				{
					SetStatus(string.Empty, string.Empty, animate: false);
				}
			}
			else
			{
				_snapshot = playbackSnapshot;
				_pauseHidden = _settings.HideWhenPaused && !playbackSnapshot.IsPlaying;
				UpdatePlaybackChrome();
				RefreshWindowVisibility();
				if (!string.Equals(_activeTrackKey, playbackSnapshot.Track.CacheKey, StringComparison.Ordinal))
				{
					_activeTrackKey = playbackSnapshot.Track.CacheKey;
					_lyricsRetryTrackKey = playbackSnapshot.Track.CacheKey;
					_lyricsRetryAttempt = 0;
					_lyricsRetryScheduled = false;
					_lyrics = null;
					_lyricsLookup = null;
					_lastLineIndex = int.MinValue;
					TrackStatusText.Text = "SPOTIFY / CHECKING CACHE";
					TrackTitleText.Text = playbackSnapshot.Track.DisplayName;
					StatusDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(142, 151, 166));
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
				_lastLineIndex = int.MinValue;
				if (lyricsLookupResult.Status == LyricsLookupStatus.CandidatesFound)
				{
					_lyrics = null;
					SetTrackStatus("LRCLIB CANDIDATES FOUND", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
					SetStatus(T("Lyrics candidates were found in LRCLIB."), T("Open Settings > Lyrics to choose the correct lyrics."), animate: true);
				}
				else if (lyrics == null)
				{
					_lyrics = null;
					SetTrackStatus("NO LYRICS", System.Windows.Media.Color.FromRgb(byte.MaxValue, 139, 143));
					SetStatus(T("No synced lyrics were found."), T("Open Settings > Lyrics to search LRCLIB using another title or English name, or add a local LRC file."), animate: true);
				}
				else if (lyrics.IsInstrumental)
				{
					_lyrics = lyrics;
					SetTrackStatus("INSTRUMENTAL", System.Windows.Media.Color.FromRgb(117, 230, byte.MaxValue));
					SetStatus("♪  " + T("Instrumental"), track.DisplayName, animate: true);
				}
				else if (lyrics.HasSyncedLyrics)
				{
					_lyrics = lyrics;
					string text = ((!lyricsLookupResult.LoadedFromCache) ? (lyricsLookupResult.Status switch
					{
						LyricsLookupStatus.LocalLrc => "LOCAL LRC", 
						LyricsLookupStatus.LrclibManual => "LRCLIB — MANUALLY SELECTED", 
						LyricsLookupStatus.Cache => "CACHE", 
						_ => "LRCLIB — AUTO SELECTED", 
					}) : "CACHE");
					string status = text;
					SetTrackStatus(status, (lyricsLookupResult.Status == LyricsLookupStatus.LocalLrc) ? System.Windows.Media.Color.FromRgb(167, 149, byte.MaxValue) : System.Windows.Media.Color.FromRgb(102, 229, 174));
					RenderLyrics();
				}
				else if (_settings.EnablePlainLyricsFallback && lyrics.HasPlainLyrics)
				{
					_lyrics = CreateEstimatedLyrics(lyrics, track);
					SetTrackStatus("ESTIMATED", System.Windows.Media.Color.FromRgb(byte.MaxValue, 194, 103));
					RenderLyrics();
				}
				else
				{
					_lyrics = lyrics;
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

	private void RenderLyrics()
	{
		UpdateScrollAnimation();
		UpdatePlaybackProgress();
		if ((object)_snapshot != null && (object)_lyrics != null && _lyrics.HasSyncedLyrics)
		{
			int num = _settings.GlobalLyricsOffsetMs;
			if (_settings.TrackOffsetsMs.TryGetValue(_snapshot.Track.CacheKey, out var value))
			{
				num += value;
			}
			TimeSpan position = _snapshot.EstimatedPosition(DateTimeOffset.UtcNow) + TimeSpan.FromMilliseconds(num);
			IReadOnlyList<LyricLine> lines = _lyrics.Lines;
			int num2 = FindActiveLine(lines, position);
			if (num2 != _lastLineIndex)
			{
				_lastLineIndex = num2;
				DisplayLyricContext(lines, num2, animate: true);
			}
		}
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
		int num = Math.Min(lines.Count, Math.Clamp(Math.Max(15, _settings.DisplayLines * 3), 15, 30));
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
		LyricsStackPanel.Children.Clear();
		_lineControls.Clear();
		for (int i = 0; i < num; i++)
		{
			OutlinedText outlinedText = new OutlinedText
			{
				Text = ((lines != null && _visibleFirstLineIndex + i < lines.Count) ? lines[_visibleFirstLineIndex + i].Text : string.Empty),
				VerticalAlignment = VerticalAlignment.Top,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
			};
			LyricsStackPanel.Children.Add(outlinedText);
			_lineControls.Add(outlinedText);
		}
		ApplyTextSettingsToControls();
		LyricsStackPanel.UpdateLayout();
	}

	private void ApplyTextSettingsToControls()
	{
		System.Windows.Media.FontFamily fontFamily = new System.Windows.Media.FontFamily(_settings.FontFamily);
		System.Windows.Media.Brush stroke = CreateBlendBrush(_settings.OutlineColor, 1.0, Colors.Black, _settings.OutlineBlendMode);
		System.Windows.Media.Brush shadowBrush = CreateBlendBrush(_settings.ShadowColor, 1.0, Colors.Black, _settings.ShadowBlendMode);
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
		}
	}

	private void UpdateLyricControl(OutlinedText control, string text, int lineIndex, bool isActive, bool animate)
	{
		double num = (string.IsNullOrWhiteSpace(text) ? 0.0 : (isActive ? 1.0 : ((lineIndex < _activeLineIndex) ? _settings.PreviousLineOpacity : _settings.NextLineOpacity)));
		control.Fill = ResolveTextBrush(lineIndex, isActive);
		control.FontWeight = (isActive ? FontWeights.Bold : FontWeights.SemiBold);
		control.FontSize = (isActive ? _settings.FontSize : (_settings.FontSize * _settings.InactiveFontScale));
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
			return CreateBlendBrush(isActive ? _settings.CurrentTextColor : _settings.NextTextColor, 1.0, Colors.White, isActive ? _settings.CurrentTextBlendMode : _settings.NextTextBlendMode);
		}
		int num = StableIndex($"{_activeTrackKey}|{_settings.RandomPaletteSeed}", int.MaxValue);
		if (_settings.TextColorMode == "TrackRandom")
		{
			CuratedColorPalette theme = ColorPalettes.GetTheme(num);
			return CreateBlendBrush(isActive ? theme.Primary : theme.Secondary, 1.0, Colors.White, isActive ? _settings.CurrentTextBlendMode : _settings.NextTextBlendMode);
		}
		return CreateBlendBrush(ColorPalettes.GetAccent(StableIndex($"{num}|{lyricIndex}", int.MaxValue)), 1.0, Colors.White, isActive ? _settings.CurrentTextBlendMode : _settings.NextTextBlendMode);
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
		ApplyUiLanguage();
		base.Resources["UiAccentBrush"] = CreateBlendBrush(_settings.UiColor, 1.0, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), _settings.UiBlendMode, ignoreSourceAlpha: true);
		OverlayPanel.Padding = new Thickness(_settings.PanelPadding);
		OverlayPanel.CornerRadius = new CornerRadius(_settings.CornerRadius);
		OverlayPanel.Background = CreateBlendBrush(_settings.BackgroundColor, _settings.BackgroundOpacity, Colors.Black, _settings.BackgroundBlendMode, ignoreSourceAlpha: true);
		OverlayPanel.BorderBrush = CreateBlendBrush(_settings.BorderColor, 1.0, Colors.White, _settings.BorderBlendMode);
		OverlayPanel.BorderThickness = (_settings.ShowPanelBorder ? new Thickness(_settings.BorderThickness) : new Thickness(0.0));
		base.Opacity = _settings.OverlayOpacity;
		base.Topmost = _settings.AlwaysOnTop;
		RebuildLineControls();
		UpdateChromeVisibility();
		UpdatePlaybackChrome();
		UpdateLockVisuals();
		_lastLineIndex = int.MinValue;
		RenderLyrics();
		if (_snapshot == null)
		{
			TrackStatusText.Text = "SPOTIFY / WAITING";
			TrackTitleText.Text = T("Play something in Spotify");
			if (_settings.ShowStatusWhenIdle)
			{
				SetStatus(T("Play something in Spotify"), T("Following Spotify for Windows automatically"), animate: false);
			}
			else
			{
				SetStatus(string.Empty, string.Empty, animate: false);
			}
		}
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
		PlaybackMenuItem.Header = T("Spotify controls");
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
		TrackInfoPanel.Visibility = ((!(_settings.ShowTrackInfo && flag)) ? Visibility.Collapsed : Visibility.Visible);
		HeaderPanel.Visibility = TrackInfoPanel.Visibility;
		PlaybackSeekSlider.Visibility = ((!(_settings.ShowProgressBar && flag5)) ? Visibility.Collapsed : Visibility.Visible);
		ControlBar.Visibility = ((!(_settings.ShowPlaybackControls && flag2)) ? Visibility.Collapsed : Visibility.Visible);
		PreviousButton.Visibility = ((!flag3) ? Visibility.Collapsed : Visibility.Visible);
		NextButton.Visibility = ((!flag3) ? Visibility.Collapsed : Visibility.Visible);
		VolumeButton.Visibility = ((!flag4) ? Visibility.Collapsed : Visibility.Visible);
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
			VolumeButton.IsEnabled = true;
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
			VolumeButton.IsEnabled = true;
			SettingsButton.IsEnabled = true;
			PlaybackSeekSlider.IsEnabled = snapshot.Track.Duration > TimeSpan.Zero;
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
			return;
		}
		double value = _snapshot.EstimatedPosition(DateTimeOffset.UtcNow).TotalMilliseconds / _snapshot.Track.Duration.TotalMilliseconds;
		if (!_isSeeking)
		{
			PlaybackSeekSlider.Value = Math.Clamp(value, 0.0, 1.0);
		}
	}

	private void SetTrackStatus(string status, System.Windows.Media.Color color)
	{
		TrackStatusText.Text = "SPOTIFY / " + status;
		TrackTitleText.Text = _snapshot?.Track.DisplayName ?? "FlowLyrics";
		StatusDot.Fill = new SolidColorBrush(color);
	}

	private static LyricsResult CreateEstimatedLyrics(LyricsResult result, TrackInfo track)
	{
		string[] array = (from line in (result.PlainLyrics ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
			select line.Trim() into line
			where !string.IsNullOrWhiteSpace(line)
			where !line.StartsWith('[') || !line.EndsWith(']')
			select line).ToArray();
		if (array.Length == 0)
		{
			return result;
		}
		TimeSpan timeSpan = ((track.Duration > TimeSpan.FromSeconds(20L)) ? track.Duration : TimeSpan.FromSeconds(Math.Max(30, array.Length * 4)));
		TimeSpan timeSpan2 = TimeSpan.FromSeconds(Math.Min(10.0, timeSpan.TotalSeconds * 0.05));
		double num = Math.Max(1.0, timeSpan.TotalSeconds - timeSpan2.TotalSeconds - 4.0);
		double[] array2 = array.Select((string line) => Math.Clamp((double)line.Length / 18.0, 0.72, 2.2)).ToArray();
		double num2 = array2.Sum();
		double num3 = 0.0;
		List<LyricLine> list = new List<LyricLine>(array.Length);
		for (int num4 = 0; num4 < array.Length; num4++)
		{
			TimeSpan time = timeSpan2 + TimeSpan.FromSeconds(num * num3 / num2);
			list.Add(new LyricLine(time, array[num4]));
			num3 += array2[num4];
		}
		return new LyricsResult(list, result.PlainLyrics, result.Source, IsInstrumental: false, IsEstimatedTiming: true);
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

	private void UpdateBackdropSampleIfNeeded()
	{
		if (!BlendModeService.RequiresBackdropSampling(_settings) || !base.IsVisible || _windowHandle == IntPtr.Zero)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow < _nextBackdropSampleUtc)
		{
			return;
		}
		_nextBackdropSampleUtc = utcNow.AddMilliseconds(240.0);
		if (!TrySampleBackdrop(out System.Windows.Media.Color sampled))
		{
			return;
		}

		System.Windows.Media.Color smoothed = System.Windows.Media.Color.FromRgb(
			SmoothChannel(_sampledBackdropColor.R, sampled.R),
			SmoothChannel(_sampledBackdropColor.G, sampled.G),
			SmoothChannel(_sampledBackdropColor.B, sampled.B));
		int difference = Math.Abs(smoothed.R - _sampledBackdropColor.R) + Math.Abs(smoothed.G - _sampledBackdropColor.G) + Math.Abs(smoothed.B - _sampledBackdropColor.B);
		if (difference < 5)
		{
			return;
		}
		_sampledBackdropColor = smoothed;
		ApplyBlendModeBrushes();
	}

	private bool TrySampleBackdrop(out System.Windows.Media.Color color)
	{
		color = _sampledBackdropColor;
		if (!NativeMethods.GetWindowRect(_windowHandle, out NativeMethods.Rect rect))
		{
			return false;
		}

		System.Drawing.Rectangle screen = SystemInformation.VirtualScreen;
		int margin = 10;
		List<System.Drawing.Point> points = new List<System.Drawing.Point>(8);
		AddBackdropPoint(points, rect.Left + (rect.Right - rect.Left) / 4, rect.Top - margin, rect, screen);
		AddBackdropPoint(points, rect.Left + (rect.Right - rect.Left) / 2, rect.Top - margin, rect, screen);
		AddBackdropPoint(points, rect.Left + (rect.Right - rect.Left) * 3 / 4, rect.Top - margin, rect, screen);
		AddBackdropPoint(points, rect.Left + (rect.Right - rect.Left) / 4, rect.Bottom + margin, rect, screen);
		AddBackdropPoint(points, rect.Left + (rect.Right - rect.Left) * 3 / 4, rect.Bottom + margin, rect, screen);
		AddBackdropPoint(points, rect.Left - margin, rect.Top + (rect.Bottom - rect.Top) / 2, rect, screen);
		AddBackdropPoint(points, rect.Right + margin, rect.Top + (rect.Bottom - rect.Top) / 3, rect, screen);
		AddBackdropPoint(points, rect.Right + margin, rect.Top + (rect.Bottom - rect.Top) * 2 / 3, rect, screen);
		if (points.Count == 0)
		{
			return false;
		}

		long red = 0;
		long green = 0;
		long blue = 0;
		int samples = 0;
		try
		{
			foreach (System.Drawing.Point point in points)
			{
				_backdropSampleGraphics.CopyFromScreen(point.X, point.Y, 0, 0, new System.Drawing.Size(1, 1), CopyPixelOperation.SourceCopy);
				System.Drawing.Color pixel = _backdropSampleBitmap.GetPixel(0, 0);
				red += pixel.R;
				green += pixel.G;
				blue += pixel.B;
				samples++;
			}
		}
		catch
		{
			return false;
		}
		if (samples == 0)
		{
			return false;
		}
		color = System.Windows.Media.Color.FromRgb((byte)(red / samples), (byte)(green / samples), (byte)(blue / samples));
		return true;
	}

	private static void AddBackdropPoint(List<System.Drawing.Point> points, int x, int y, NativeMethods.Rect window, System.Drawing.Rectangle screen)
	{
		if (x < screen.Left || x >= screen.Right || y < screen.Top || y >= screen.Bottom)
		{
			return;
		}
		if (x >= window.Left && x < window.Right && y >= window.Top && y < window.Bottom)
		{
			return;
		}
		points.Add(new System.Drawing.Point(x, y));
	}

	private static byte SmoothChannel(byte current, byte target)
	{
		return (byte)Math.Clamp((int)Math.Round(current + (target - current) * 0.32), 0, 255);
	}

	private void ApplyBlendModeBrushes()
	{
		base.Resources["UiAccentBrush"] = CreateBlendBrush(_settings.UiColor, 1.0, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), _settings.UiBlendMode, ignoreSourceAlpha: true);
		OverlayPanel.Background = CreateBlendBrush(_settings.BackgroundColor, _settings.BackgroundOpacity, Colors.Black, _settings.BackgroundBlendMode, ignoreSourceAlpha: true);
		OverlayPanel.BorderBrush = CreateBlendBrush(_settings.BorderColor, 1.0, Colors.White, _settings.BorderBlendMode);
		System.Windows.Media.Brush stroke = CreateBlendBrush(_settings.OutlineColor, 1.0, Colors.Black, _settings.OutlineBlendMode);
		System.Windows.Media.Brush shadow = CreateBlendBrush(_settings.ShadowColor, 1.0, Colors.Black, _settings.ShadowBlendMode);
		foreach (OutlinedText line in _lineControls)
		{
			line.Stroke = stroke;
			line.ShadowBrush = shadow;
		}
		UpdateLockVisuals();
		_lastLineIndex = int.MinValue;
	}

	private System.Windows.Media.Brush CreateBlendBrush(string value, double opacity, System.Windows.Media.Color fallback, string individualMode, bool ignoreSourceAlpha = false)
	{
		System.Windows.Media.Color color = ParseColor(value, fallback);
		string mode = BlendModeService.ResolveMode(_settings, individualMode);
		color = BlendModeService.Apply(color, _sampledBackdropColor, mode);
		double sourceAlpha = ignoreSourceAlpha ? 1.0 : color.A / 255.0;
		SolidColorBrush brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
			(byte)Math.Round(Math.Clamp(opacity * sourceAlpha, 0.0, 1.0) * 255.0), color.R, color.G, color.B));
		brush.Freeze();
		return brush;
	}

	private static System.Windows.Media.Brush CreateBrush(string value, double opacity, System.Windows.Media.Color fallback, bool ignoreSourceAlpha = false)
	{
		System.Windows.Media.Color color = ParseColor(value, fallback);
		double num = (ignoreSourceAlpha ? 1.0 : ((double)(int)color.A / 255.0));
		SolidColorBrush solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)Math.Round(Math.Clamp(opacity * num, 0.0, 1.0) * 255.0), color.R, color.G, color.B));
		solidColorBrush.Freeze();
		return solidColorBrush;
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

	private bool IsPointOverPlaybackButton(System.Windows.Point screenPoint)
	{
		if (VolumePopup.IsOpen && IsPointOverElement(VolumePopupSurface, screenPoint))
		{
			return true;
		}
		if (!IsPointOverElement(PreviousButton, screenPoint) && !IsPointOverElement(PlayPauseButton, screenPoint) && !IsPointOverElement(NextButton, screenPoint) && !IsPointOverElement(VolumeButton, screenPoint) && !IsPointOverElement(LockButton, screenPoint) && !IsPointOverElement(SettingsButton, screenPoint) && !IsPointOverElement(PlaybackSeekSlider, screenPoint))
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
		LockButton.Background = (_isLocked ? CreateBlendBrush(_settings.UiColor, 0.42, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), _settings.UiBlendMode, ignoreSourceAlpha: true) : new SolidColorBrush(System.Windows.Media.Color.FromArgb(46, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
		LockButton.BorderBrush = (_isLocked ? CreateBlendBrush(_settings.UiColor, 1.0, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), _settings.UiBlendMode, ignoreSourceAlpha: true) : new SolidColorBrush(System.Windows.Media.Color.FromArgb(58, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
		LockIcon.Fill = (_isLocked ? CreateBlendBrush(_settings.UiColor, 1.0, System.Windows.Media.Color.FromRgb(byte.MaxValue, 107, 44), _settings.UiBlendMode, ignoreSourceAlpha: true) : System.Windows.Media.Brushes.White);
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

	private async void OpenSettings()
	{
		bool wasLocked = _isLocked;
		AppSettings appSettings = _settings.Clone();
		SetLocked(locked: false, persist: false);
		SettingsWindow settingsWindow = new SettingsWindow(_settings.Clone(), _lyricsService.LyricsDirectory, _lyricsService, () => _snapshot?.Track, () => _lyricsLookup, async delegate
		{
			if (_snapshot != null)
			{
				await LoadLyricsAsync(_snapshot.Track, forceRefresh: false);
			}
		})
		{
			Owner = this
		};
		settingsWindow.PreviewChanged += PreviewSettings;
		bool valueOrDefault = settingsWindow.ShowDialog() == true;
		settingsWindow.PreviewChanged -= PreviewSettings;
		if (valueOrDefault)
		{
			bool startWithWindows = appSettings.StartWithWindows;
			AppSettings resultSettings = settingsWindow.ResultSettings;
			resultSettings.IsLocked = wasLocked;
			resultSettings.WindowLeft = base.Left;
			resultSettings.WindowTop = base.Top;
			resultSettings.WindowWidth = base.Width;
			resultSettings.WindowHeight = base.Height;
			if (resultSettings.StartWithWindows != startWithWindows && !StartupService.TrySetEnabled(resultSettings.StartWithWindows, out string error))
			{
				resultSettings.StartWithWindows = startWithWindows;
				System.Windows.MessageBox.Show(this, string.Format(T("Could not change Windows startup settings.\n\n{0}"), error), "FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			bool num = _settings.ShortcutsEnabled != resultSettings.ShortcutsEnabled;
			_settings = resultSettings;
			if (num)
			{
				ConfigureHotkeys();
			}
			ApplyVisualSettings();
			await SaveSettingsSafeAsync();
		}
		else
		{
			bool num2 = _settings.ShortcutsEnabled != appSettings.ShortcutsEnabled;
			_settings = appSettings;
			if (num2)
			{
				ConfigureHotkeys();
			}
			ApplyVisualSettings();
		}
		SetLocked(wasLocked, persist: false);
		void PreviewSettings(AppSettings preview)
		{
			preview.IsLocked = wasLocked;
			preview.WindowLeft = base.Left;
			preview.WindowTop = base.Top;
			preview.WindowWidth = base.Width;
			preview.WindowHeight = base.Height;
			bool num3 = _settings.ShortcutsEnabled != preview.ShortcutsEnabled;
			_settings = preview;
			if (num3)
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
			ScheduleSettingsSave();
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

	private void AdjustCurrentTrackOffset(int deltaMilliseconds)
	{
		if ((object)_snapshot != null)
		{
			string cacheKey = _snapshot.Track.CacheKey;
			_settings.TrackOffsetsMs.TryGetValue(cacheKey, out var value);
			int num = Math.Clamp(value + deltaMilliseconds, -10000, 10000);
			if (num == 0)
			{
				_settings.TrackOffsetsMs.Remove(cacheKey);
			}
			else
			{
				_settings.TrackOffsetsMs[cacheKey] = num;
			}
			_lastLineIndex = int.MinValue;
			RenderLyrics();
			ScheduleSettingsSave();
		}
	}

	private int GetCurrentTrackOffset()
	{
		if ((object)_snapshot == null)
		{
			return 0;
		}
		if (!_settings.TrackOffsetsMs.TryGetValue(_snapshot.Track.CacheKey, out var value))
		{
			return 0;
		}
		return value;
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
			_lyricsService.Dispose();
			_backdropSampleGraphics.Dispose();
			_backdropSampleBitmap.Dispose();
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
				throw new InvalidOperationException("Spotify rejected the media command.");
			}
			await Task.Delay(90);
			await PollMediaAsync();
		}
		catch
		{
			_tray?.ShowMessage("FlowLyrics", T("Could not control Spotify. Start playback in Spotify and try again."));
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
		_isSeeking = true;
		_pendingSeekRatio = null;
		_seekWasDirectClick = false;
		if (FindVisualParent<Thumb>(e.OriginalSource as DependencyObject) != null || !(PlaybackSeekSlider.ActualWidth > 0.0) || (object)_snapshot == null || !(_snapshot.Track.Duration > TimeSpan.Zero))
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
		if (!_isSeeking || (object)_snapshot == null || _snapshot.Track.Duration <= TimeSpan.Zero)
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
		_volumePopupCloseTimer.Stop();
		_volumeWriteTimer.Stop();
		_pendingSpotifyVolume = null;
		_updatingVolume = true;
		double volume;
		bool muted;
		bool num = _systemVolumeService.TryGetVolume(out volume, out muted);
		VolumeSlider.IsEnabled = _snapshot != null;
		if (num)
		{
			_lastSpotifyVolume = volume;
			VolumeSlider.Value = _lastSpotifyVolume;
			VolumeLabel.Text = (muted ? "MUTE" : "VOL");
		}
		else
		{
			VolumeSlider.Value = _lastSpotifyVolume;
			VolumeLabel.Text = "VOL";
		}
		_updatingVolume = false;
		VolumePopup.IsOpen = true;
	}

	private void VolumeHover_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
	{
		_volumePopupCloseTimer.Stop();
		_volumePopupCloseTimer.Start();
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
			_lastSpotifyVolume = e.NewValue;
			_pendingSpotifyVolume = e.NewValue;
			VolumeLabel.Text = "VOL";
			if (!_volumeWriteTimer.IsEnabled)
			{
				_volumeWriteTimer.Start();
			}
		}
	}

	private void VolumeButton_Click(object sender, RoutedEventArgs e)
	{
		_volumeWriteTimer.Stop();
		_pendingSpotifyVolume = null;
		if (_systemVolumeService.TryToggleMute() && _systemVolumeService.TryGetVolume(out var volume, out var muted))
		{
			_updatingVolume = true;
			_lastSpotifyVolume = volume;
			VolumeSlider.Value = volume;
			VolumeLabel.Text = (muted ? "MUTE" : "VOL");
			_updatingVolume = false;
		}
	}

	private static CustomPopupPlacement[] PlaceVolumePopup(System.Windows.Size popupSize, System.Windows.Size targetSize, System.Windows.Point offset)
	{
		double x = (targetSize.Width - popupSize.Width) / 2.0 - 3.0;
		double y = 0.0 - popupSize.Height - 2.0;
		return new CustomPopupPlacement[1]
		{
			new CustomPopupPlacement(new System.Windows.Point(x, y), PopupPrimaryAxis.Vertical)
		};
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

	private void ResetTrackOffset_Click(object sender, RoutedEventArgs e)
	{
		if ((object)_snapshot != null)
		{
			_settings.TrackOffsetsMs.Remove(_snapshot.Track.CacheKey);
			_lastLineIndex = int.MinValue;
			RenderLyrics();
			ScheduleSettingsSave();
		}
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
