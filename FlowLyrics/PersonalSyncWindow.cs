using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;

namespace FlowLyrics;

public sealed class PersonalSyncWindow : Window
{
	private readonly PersonalSyncStore _store;
	private readonly PersonalSyncContext _context;
	private readonly IReadOnlyList<LyricLine> _lines;
	private readonly Func<int?> _selectedLineProvider;
	private readonly Func<TimeSpan> _playbackPositionProvider;
	private readonly string _language;
	private readonly Stack<PersonalSyncProfile> _undo = new();
	private readonly Stack<PersonalSyncProfile> _redo = new();
	private readonly DispatcherTimer _previewTimer;
	private PersonalSyncProfile _profile;
	private bool _dirty;
	private bool _deleted;
	private double? _pendingHoldStart;
	private double _pendingHoldLyricsTime;

	private readonly TextBlock _offsetText;
	private readonly TextBlock _selectionText;
	private readonly Button _matchButton;
	private readonly Button _undoButton;
	private readonly Button _redoButton;
	private readonly Button _holdButton;
	private readonly CheckBox _trackScopeBox;
	private readonly StackPanel _advancedPanel;
	private readonly Canvas _timeline;
	private readonly ListBox _pointsList;

	public event EventHandler<PersonalSyncProfile?>? PreviewChanged;

	public PersonalSyncWindow(
		PersonalSyncStore store,
		PersonalSyncContext context,
		PersonalSyncProfile? profile,
		IReadOnlyList<LyricLine> lines,
		Func<int?> selectedLineProvider,
		Func<TimeSpan> playbackPositionProvider,
		string language)
	{
		_store = store;
		_context = context;
		_lines = lines;
		_selectedLineProvider = selectedLineProvider;
		_playbackPositionProvider = playbackPositionProvider;
		_language = language;
		_profile = profile?.Clone() ?? CreateProfile(context);

		Title = "FlowLyrics · " + L("歌詞タイミング", "Personal Sync");
		Width = 620;
		Height = 720;
		MinWidth = 520;
		MinHeight = 570;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = Brush(25, 23, 26);
		Foreground = Brush(235, 232, 234);
		ShowInTaskbar = false;

		ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
		StackPanel root = new() { Margin = new Thickness(22) };
		scroll.Content = root;
		Content = scroll;

		root.Children.Add(new TextBlock
		{
			Text = "SYNC",
			FontSize = 25,
			FontWeight = FontWeights.Bold,
			Foreground = Accent()
		});
		root.Children.Add(new TextBlock
		{
			Text = context.Track.Title + (context.Track.Artist.Length > 0 ? " — " + context.Track.Artist : string.Empty),
			FontSize = 15,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0, 4, 0, 2),
			TextWrapping = TextWrapping.Wrap
		});
		root.Children.Add(new TextBlock
		{
			Text = context.Source.Source,
			Foreground = Muted(),
			Margin = new Thickness(0, 0, 0, 18)
		});

		Border simpleCard = Card();
		StackPanel simple = new() { Margin = new Thickness(16) };
		simpleCard.Child = simple;
		root.Children.Add(simpleCard);
		simple.Children.Add(new TextBlock
		{
			Text = L("歌詞が少し早い / 遅い？", "Are the lyrics a little early or late?"),
			FontSize = 13,
			FontWeight = FontWeights.SemiBold,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0, 0, 0, 12)
		});
		Grid nudge = new();
		nudge.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		nudge.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		nudge.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		Button early = Button(L("◀  歌詞を早く", "◀  Earlier"));
		Button late = Button(L("歌詞を遅く  ▶", "Later  ▶"));
		early.Click += delegate { Change(profile => profile.OffsetSeconds -= 0.1); };
		late.Click += delegate { Change(profile => profile.OffsetSeconds += 0.1); };
		_offsetText = new TextBlock
		{
			Width = 86,
			TextAlignment = TextAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			FontSize = 18,
			FontWeight = FontWeights.Bold,
			Foreground = Accent()
		};
		Grid.SetColumn(early, 0);
		Grid.SetColumn(_offsetText, 1);
		Grid.SetColumn(late, 2);
		nudge.Children.Add(early);
		nudge.Children.Add(_offsetText);
		nudge.Children.Add(late);
		simple.Children.Add(nudge);

		_selectionText = new TextBlock
		{
			TextWrapping = TextWrapping.Wrap,
			TextAlignment = TextAlignment.Center,
			Foreground = Muted(),
			Margin = new Thickness(0, 14, 0, 7)
		};
		simple.Children.Add(_selectionText);
		_matchButton = Button(L("今ここに合わせる", "Match selected line here"));
		_matchButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		_matchButton.Click += MatchSelectedLine_Click;
		simple.Children.Add(_matchButton);

		WrapPanel history = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
		_undoButton = Button("UNDO");
		_redoButton = Button("REDO");
		Button reset = Button(L("元に戻す", "Reset adjustments"));
		_undoButton.Click += delegate { Undo(); };
		_redoButton.Click += delegate { Redo(); };
		reset.Click += delegate
		{
			Change(profile =>
			{
				profile.OffsetSeconds = 0;
				profile.Mode = PersonalSyncMode.None;
				profile.Anchors.Clear();
				profile.Segments.Clear();
			});
		};
		history.Children.Add(_undoButton);
		history.Children.Add(_redoButton);
		history.Children.Add(reset);
		simple.Children.Add(history);

		Button advancedToggle = Button(L("詳細調整", "Advanced sync"));
		advancedToggle.Margin = new Thickness(0, 14, 0, 0);
		advancedToggle.HorizontalAlignment = HorizontalAlignment.Stretch;
		simple.Children.Add(advancedToggle);

		_advancedPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 14, 0, 0) };
		root.Children.Add(_advancedPanel);
		advancedToggle.Click += delegate
		{
			_advancedPanel.Visibility = _advancedPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
			advancedToggle.Content = _advancedPanel.Visibility == Visibility.Visible
				? L("詳細調整を閉じる", "Close advanced sync") : L("詳細調整", "Advanced sync");
			DrawTimeline();
		};

		Border advancedCard = Card();
		StackPanel advanced = new() { Margin = new Thickness(16) };
		advancedCard.Child = advanced;
		_advancedPanel.Children.Add(advancedCard);
		advanced.Children.Add(new TextBlock
		{
			Text = L("位置と区間で調整", "Adjust with points and ranges"),
			FontSize = 14,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0, 0, 0, 8)
		});
		_timeline = new Canvas { Height = 92, Background = Brush(31, 29, 32), ClipToBounds = true };
		_timeline.SizeChanged += delegate { DrawTimeline(); };
		advanced.Children.Add(_timeline);

		WrapPanel actions = new() { Margin = new Thickness(0, 10, 0, 8) };
		Button anchor = Button(L("今ここに合わせる", "Add sync point"));
		anchor.Click += AddAnchor_Click;
		_holdButton = Button(L("ここから歌詞を止める", "Pause lyrics here"));
		_holdButton.Click += Hold_Click;
		actions.Children.Add(anchor);
		actions.Children.Add(_holdButton);
		advanced.Children.Add(actions);

		_pointsList = new ListBox
		{
			MinHeight = 105,
			MaxHeight = 190,
			Background = Brush(31, 29, 32),
			Foreground = Foreground,
			BorderBrush = Brush(75, 69, 76),
			Margin = new Thickness(0, 0, 0, 8)
		};
		advanced.Children.Add(_pointsList);
		Button deletePoint = Button(L("選択した点・区間を削除", "Delete selected point/range"));
		deletePoint.Click += DeleteSelectedPoint_Click;
		advanced.Children.Add(deletePoint);
		_trackScopeBox = new CheckBox
		{
			Content = L("この曲すべての再生元で使う", "Use for this track on every source"),
			IsChecked = _profile.Scope == PersonalSyncScope.Track,
			Margin = new Thickness(0, 13, 0, 0),
			Foreground = Foreground
		};
		_trackScopeBox.Checked += Scope_Changed;
		_trackScopeBox.Unchecked += Scope_Changed;
		advanced.Children.Add(_trackScopeBox);

		WrapPanel bottom = new() { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
		Button remove = Button(L("この曲の調整を解除", "Remove this sync"));
		remove.Click += Remove_Click;
		Button close = Button(L("閉じる", "Close"));
		close.Click += delegate { Close(); };
		bottom.Children.Add(remove);
		bottom.Children.Add(close);
		root.Children.Add(bottom);

		_previewTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
		_previewTimer.Tick += delegate { RefreshLiveUi(); };
		Loaded += delegate { _previewTimer.Start(); RefreshAll(); };
		Closing += PersonalSyncWindow_Closing;
		Closed += delegate { _previewTimer.Stop(); };
	}

	private void Change(Action<PersonalSyncProfile> mutation)
	{
		_undo.Push(_profile.Clone());
		_redo.Clear();
		mutation(_profile);
		if (_profile.Mode == PersonalSyncMode.None
			&& (Math.Abs(_profile.OffsetSeconds) > 0.0001 || _profile.Anchors.Count > 0 || _profile.Segments.Count > 0))
		{
			_profile.Mode = PersonalSyncMode.Offset;
		}
		_profile.OffsetSeconds = Math.Round(Math.Clamp(_profile.OffsetSeconds, -3600.0, 3600.0), 3);
		_profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
		_dirty = true;
		PreviewChanged?.Invoke(this, _profile.Clone());
		RefreshAll();
	}

	private void Undo()
	{
		if (_undo.Count == 0) return;
		_redo.Push(_profile.Clone());
		_profile = _undo.Pop();
		_dirty = true;
		PreviewChanged?.Invoke(this, _profile.Clone());
		RefreshAll();
	}

	private void Redo()
	{
		if (_redo.Count == 0) return;
		_undo.Push(_profile.Clone());
		_profile = _redo.Pop();
		_dirty = true;
		PreviewChanged?.Invoke(this, _profile.Clone());
		RefreshAll();
	}

	private void MatchSelectedLine_Click(object sender, RoutedEventArgs e)
	{
		int? selected = ValidSelectedLine();
		if (!selected.HasValue) return;
		double offset = PersonalSyncMapper.CalculateOffsetSeconds(_playbackPositionProvider(), _lines[selected.Value].Time);
		Change(profile =>
		{
			profile.Mode = PersonalSyncMode.Offset;
			profile.OffsetSeconds = offset;
			profile.Anchors.Clear();
			profile.Segments.Clear();
		});
	}

	private void AddAnchor_Click(object sender, RoutedEventArgs e)
	{
		int? selected = ValidSelectedLine();
		if (!selected.HasValue) return;
		double playback = Math.Max(0, _playbackPositionProvider().TotalSeconds);
		double lyrics = Math.Max(0, _lines[selected.Value].Time.TotalSeconds);
		Change(profile =>
		{
			profile.Mode = PersonalSyncMode.Advanced;
			profile.Anchors.RemoveAll(anchor => Math.Abs(anchor.PlaybackSeconds - playback) < 0.08);
			profile.Anchors.Add(new PersonalSyncAnchor { PlaybackSeconds = playback, LyricsSeconds = lyrics });
			profile.Anchors = profile.Anchors.OrderBy(anchor => anchor.PlaybackSeconds).ToList();
		});
	}

	private void Hold_Click(object sender, RoutedEventArgs e)
	{
		double playback = Math.Max(0, _playbackPositionProvider().TotalSeconds);
		if (!_pendingHoldStart.HasValue)
		{
			_pendingHoldStart = playback;
			_pendingHoldLyricsTime = PersonalSyncMapper.MapPlaybackToLyrics(TimeSpan.FromSeconds(playback), _profile).TotalSeconds;
			_holdButton.Content = L("ここから歌詞を再開", "Resume lyrics here");
			RefreshLiveUi();
			return;
		}
		double start = _pendingHoldStart.Value;
		_pendingHoldStart = null;
		_holdButton.Content = L("ここから歌詞を止める", "Pause lyrics here");
		if (playback <= start + 0.05) return;
		Change(profile =>
		{
			profile.Mode = PersonalSyncMode.Advanced;
			profile.Segments.Add(new PersonalSyncSegment
			{
				Type = PersonalSyncSegmentType.Hold,
				PlaybackStartSeconds = start,
				PlaybackEndSeconds = playback,
				LyricsTimeSeconds = _pendingHoldLyricsTime
			});
			profile.Segments = profile.Segments.OrderBy(segment => segment.PlaybackStartSeconds).ToList();
		});
	}

	private void DeleteSelectedPoint_Click(object sender, RoutedEventArgs e)
	{
		if (_pointsList.SelectedItem is not SyncPointListItem item) return;
		Change(profile =>
		{
			if (item.IsAnchor) profile.Anchors.RemoveAll(anchor => anchor.Id == item.Id);
			else profile.Segments.RemoveAll(segment => segment.Id == item.Id);
			if (profile.Anchors.Count == 0 && profile.Segments.Count == 0) profile.Mode = PersonalSyncMode.Offset;
		});
	}

	private void Scope_Changed(object sender, RoutedEventArgs e)
	{
		if (!IsLoaded) return;
		Change(profile => profile.Scope = _trackScopeBox.IsChecked == true ? PersonalSyncScope.Track : PersonalSyncScope.Source);
	}

	private async void Remove_Click(object sender, RoutedEventArgs e)
	{
		if (MessageBox.Show(this, L("この歌詞タイミング調整だけを解除しますか？", "Remove only this Personal Sync profile?"),
			"FlowLyrics", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
		try
		{
			await _store.DeleteAsync(_profile.Id);
			_deleted = true;
			_dirty = false;
			PreviewChanged?.Invoke(this, null);
			Close();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, "FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private async void PersonalSyncWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
	{
		_previewTimer.Stop();
		if (!_dirty || _deleted) return;
		try
		{
			_profile = await _store.UpsertAsync(_profile);
			_dirty = false;
			PreviewChanged?.Invoke(this, _profile.Clone());
		}
		catch (Exception ex)
		{
			e.Cancel = true;
			_previewTimer.Start();
			MessageBox.Show(this, L("歌詞タイミング調整を保存できませんでした。\n\n", "Could not save Personal Sync.\n\n") + ex.Message,
				"FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void RefreshAll()
	{
		_offsetText.Text = _profile.OffsetSeconds.ToString("+0.0;-0.0;0.0") + "s";
		_trackScopeBox.IsChecked = _profile.Scope == PersonalSyncScope.Track;
		_undoButton.IsEnabled = _undo.Count > 0;
		_redoButton.IsEnabled = _redo.Count > 0;
		RefreshPointList();
		RefreshLiveUi();
		DrawTimeline();
	}

	private void RefreshLiveUi()
	{
		int? selected = ValidSelectedLine();
		_matchButton.IsEnabled = selected.HasValue;
		_selectionText.Text = selected.HasValue
			? "●  " + _lines[selected.Value].Text + "  ·  " + Format(_lines[selected.Value].Time.TotalSeconds)
			: L("同期歌詞の行をクリックしてください", "Click a synced lyric line first");
		DrawTimeline();
	}

	private void RefreshPointList()
	{
		_pointsList.Items.Clear();
		foreach (PersonalSyncAnchor anchor in _profile.Anchors.OrderBy(item => item.PlaybackSeconds))
		{
			_pointsList.Items.Add(new SyncPointListItem(anchor.Id, true,
				$"SYNC   {Format(anchor.PlaybackSeconds)}  →  {Format(anchor.LyricsSeconds)}"));
		}
		foreach (PersonalSyncSegment segment in _profile.Segments.OrderBy(item => item.PlaybackStartSeconds))
		{
			_pointsList.Items.Add(new SyncPointListItem(segment.Id, false,
				$"PAUSE  {Format(segment.PlaybackStartSeconds)}  —  {Format(segment.PlaybackEndSeconds)}"));
		}
	}

	private void DrawTimeline()
	{
		if (_timeline.ActualWidth <= 1) return;
		_timeline.Children.Clear();
		double width = _timeline.ActualWidth;
		double duration = Math.Max(1.0, _context.Track.DurationSeconds);
		Line rail = new() { X1 = 16, X2 = width - 16, Y1 = 46, Y2 = 46, Stroke = Brush(100, 95, 102), StrokeThickness = 3 };
		_timeline.Children.Add(rail);
		foreach (PersonalSyncSegment segment in _profile.Segments.Where(item => item.Type == PersonalSyncSegmentType.Hold))
		{
			double x1 = X(segment.PlaybackStartSeconds, duration, width);
			double x2 = X(segment.PlaybackEndSeconds, duration, width);
			Rectangle hold = new() { Width = Math.Max(3, x2 - x1), Height = 16, Fill = Brush(255, 170, 67), Opacity = .52 };
			Canvas.SetLeft(hold, x1);
			Canvas.SetTop(hold, 38);
			_timeline.Children.Add(hold);
		}
		foreach (PersonalSyncAnchor anchor in _profile.Anchors)
		{
			Ellipse point = new() { Width = 11, Height = 11, Fill = Accent(), Stroke = Brushes.White, StrokeThickness = 1 };
			Canvas.SetLeft(point, X(anchor.PlaybackSeconds, duration, width) - 5.5);
			Canvas.SetTop(point, 40.5);
			_timeline.Children.Add(point);
		}
		double now = Math.Clamp(_playbackPositionProvider().TotalSeconds, 0, duration);
		Line marker = new() { X1 = X(now, duration, width), X2 = X(now, duration, width), Y1 = 20, Y2 = 70, Stroke = Brushes.White, StrokeThickness = 1.2 };
		_timeline.Children.Add(marker);
		if (_pendingHoldStart.HasValue)
		{
			Line pending = new() { X1 = X(_pendingHoldStart.Value, duration, width), X2 = X(_pendingHoldStart.Value, duration, width), Y1 = 30, Y2 = 62, Stroke = Accent(), StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 2, 2 } };
			_timeline.Children.Add(pending);
		}
	}

	private int? ValidSelectedLine()
	{
		int? selected = _selectedLineProvider();
		return selected is >= 0 && selected < _lines.Count ? selected : null;
	}

	private static PersonalSyncProfile CreateProfile(PersonalSyncContext context) => new()
	{
		Mode = PersonalSyncMode.Offset,
		Scope = PersonalSyncScope.Source,
		Track = context.Track,
		Source = context.Source,
		Lyrics = context.Lyrics
	};

	private static double X(double seconds, double duration, double width) => 16 + Math.Clamp(seconds / duration, 0, 1) * Math.Max(1, width - 32);
	private static string Format(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f");
	private string L(string ja, string en) => _language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? ja : en;
	private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
	private static SolidColorBrush Accent() => Brush(255, 107, 44);
	private static SolidColorBrush Muted() => Brush(181, 176, 181);

	private static Border Card() => new()
	{
		Background = Brush(36, 33, 37),
		BorderBrush = Brush(69, 64, 70),
		BorderThickness = new Thickness(1),
		CornerRadius = new CornerRadius(12),
		Margin = new Thickness(0, 0, 0, 10)
	};

	private static Button Button(string text) => new()
	{
		Content = text,
		Padding = new Thickness(12, 7, 12, 7),
		Margin = new Thickness(3),
		Background = Brush(49, 46, 50),
		Foreground = Brush(238, 235, 237),
		BorderBrush = Brush(89, 83, 91),
		BorderThickness = new Thickness(1),
		Cursor = System.Windows.Input.Cursors.Hand
	};

	private sealed record SyncPointListItem(Guid Id, bool IsAnchor, string Label)
	{
		public override string ToString() => Label;
	}
}
