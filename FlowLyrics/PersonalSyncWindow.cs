using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;

namespace FlowLyrics;

/// <summary>
/// Movable, lyrics-first editor for non-destructive Personal Sync profiles.
/// Opening and closing this window without an edit never creates a profile.
/// </summary>
public sealed class PersonalSyncWindow : Window
{
	private readonly PersonalSyncStore _store;
	private readonly PersonalSyncContext _context;
	private readonly IReadOnlyList<LyricLine> _lines;
	private readonly Func<int?> _initialLineProvider;
	private readonly Func<TimeSpan> _playbackPositionProvider;
	private readonly string _language;
	private readonly Stack<PersonalSyncProfile> _undo = new();
	private readonly Stack<PersonalSyncProfile> _redo = new();
	private readonly DispatcherTimer _previewTimer;
	private readonly List<LyricRow> _lyricRows = new();
	private PersonalSyncProfile _profile;
	private bool _dirty;
	private bool _deleted;
	private bool _refreshing;
	private int _selectedLineIndex = -1;
	private int _activeLineIndex = -1;
	private double? _pendingHoldStart;
	private double _pendingHoldLyricsTime;

	private readonly TextBlock _offsetText;
	private readonly TextBlock _nowText;
	private readonly TextBlock _selectionText;
	private readonly Button _matchButton;
	private readonly Button _undoButton;
	private readonly Button _redoButton;
	private readonly Button _holdButton;
	private readonly CheckBox _trackScopeBox;
	private readonly CheckBox _followNowBox;
	private readonly ListBox _lyricsList;
	private readonly Canvas _timeline;
	private readonly ListBox _pointsList;
	private readonly TextBlock _pointTypeText;
	private readonly TextBlock _pointALabel;
	private readonly TextBlock _pointBLabel;
	private readonly TextBlock _pointLyricsLabel;
	private readonly TextBox _pointABox;
	private readonly TextBox _pointBBox;
	private readonly TextBox _pointLyricsBox;
	private readonly Button _applyPointButton;
	private readonly Button _pointNowButton;
	private readonly Button _pointLyricButton;

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
		_initialLineProvider = selectedLineProvider;
		_playbackPositionProvider = playbackPositionProvider;
		_language = language;
		_profile = profile?.Clone() ?? CreateProfile(context);

		Title = "FlowLyrics · " + L("歌詞タイミング", "Personal Sync");
		Width = 980;
		Height = 720;
		MinWidth = 820;
		MinHeight = 600;
		WindowStartupLocation = WindowStartupLocation.Manual;
		Background = Brush(25, 23, 26);
		Foreground = Brush(235, 232, 234);
		ShowInTaskbar = false;

		Grid root = new() { Margin = new Thickness(20) };
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		Content = root;

		Grid header = new() { Margin = new Thickness(0, 0, 0, 14) };
		header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		StackPanel heading = new();
		heading.Children.Add(new TextBlock { Text = "PERSONAL SYNC", FontSize = 23, FontWeight = FontWeights.Bold, Foreground = Accent() });
		heading.Children.Add(new TextBlock
		{
			Text = context.Track.Title + (string.IsNullOrWhiteSpace(context.Track.Artist) ? string.Empty : " — " + context.Track.Artist),
			FontSize = 14,
			FontWeight = FontWeights.SemiBold,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, 4, 0, 0)
		});
		heading.Children.Add(new TextBlock
		{
			Text = context.Source.Source + "  ·  " + context.Lyrics.DisplayName,
			Foreground = Muted(),
			Margin = new Thickness(0, 3, 0, 0)
		});
		header.Children.Add(heading);
		_nowText = new TextBlock
		{
			FontFamily = new FontFamily("Consolas"),
			FontSize = 12,
			TextAlignment = TextAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Foreground = Brush(224, 220, 223)
		};
		Grid.SetColumn(_nowText, 1);
		header.Children.Add(_nowText);
		root.Children.Add(header);

		Grid body = new();
		body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
		body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
		body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
		Grid.SetRow(body, 1);
		root.Children.Add(body);

		ScrollViewer leftScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
		StackPanel left = new();
		leftScroll.Content = left;
		body.Children.Add(leftScroll);

		Border offsetCard = Card();
		StackPanel offsetPanel = CardContent();
		offsetCard.Child = offsetPanel;
		left.Children.Add(offsetCard);
		offsetPanel.Children.Add(SectionTitle(L("曲全体のタイミング", "Whole-track timing")));
		offsetPanel.Children.Add(new TextBlock
		{
			Text = L("数値だけで表示タイミングを変更します。＋は遅く、－は早く表示します。", "Adjust by seconds. Plus displays lyrics later; minus displays them earlier."),
			Foreground = Muted(),
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, 2, 0, 9)
		});
		_offsetText = new TextBlock
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			FontFamily = new FontFamily("Consolas"),
			FontSize = 25,
			FontWeight = FontWeights.Bold,
			Foreground = Accent(),
			Margin = new Thickness(0, 0, 0, 8)
		};
		offsetPanel.Children.Add(_offsetText);
		Grid nudge = new();
		for (int i = 0; i < 4; i++) nudge.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		double[] amounts = { -0.5, -0.1, 0.1, 0.5 };
		for (int i = 0; i < amounts.Length; i++)
		{
			double delta = amounts[i];
			Button button = Button(delta.ToString("+0.0;-0.0", CultureInfo.InvariantCulture));
			button.Click += delegate { Nudge(delta); };
			Grid.SetColumn(button, i);
			nudge.Children.Add(button);
		}
		offsetPanel.Children.Add(nudge);

		Border timelineCard = Card();
		StackPanel timelinePanel = CardContent();
		timelineCard.Child = timelinePanel;
		left.Children.Add(timelineCard);
		timelinePanel.Children.Add(SectionTitle(L("途中でタイミングを変える", "Changes during the track")));
		timelinePanel.Children.Add(new TextBlock
		{
			Text = L("歌詞を右から選び、現在の再生位置から切り替える同期点を追加します。", "Choose a lyric on the right, then add a point where the new timing starts."),
			Foreground = Muted(),
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0, 2, 0, 8)
		});
		_timeline = new Canvas { Height = 90, Background = Brush(31, 29, 32), ClipToBounds = true };
		_timeline.SizeChanged += delegate { DrawTimeline(); };
		timelinePanel.Children.Add(_timeline);
		_selectionText = new TextBlock { Foreground = Brush(222, 218, 221), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 5) };
		timelinePanel.Children.Add(_selectionText);
		_matchButton = Button(L("選択した歌詞を現在位置に設定", "Set selected lyric to current time"));
		_matchButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		_matchButton.Click += MatchSelectedLine_Click;
		timelinePanel.Children.Add(_matchButton);
		Button anchorButton = Button(L("ここから選択したタイミングへ切り替える", "Change to selected timing from here"));
		anchorButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		anchorButton.Click += AddAnchor_Click;
		timelinePanel.Children.Add(anchorButton);
		_holdButton = Button(L("ここから歌詞を止める", "Hold lyrics from here"));
		_holdButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		_holdButton.Click += Hold_Click;
		timelinePanel.Children.Add(_holdButton);

		_pointsList = new ListBox
		{
			MinHeight = 90,
			MaxHeight = 150,
			Background = Brush(31, 29, 32),
			Foreground = Foreground,
			BorderBrush = Brush(75, 69, 76),
			Margin = new Thickness(0, 10, 0, 6)
		};
		_pointsList.SelectionChanged += PointsList_SelectionChanged;
		timelinePanel.Children.Add(_pointsList);

		Border editCard = new()
		{
			Background = Brush(30, 28, 31), BorderBrush = Brush(69, 64, 70), BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 3, 0, 0)
		};
		StackPanel editPanel = new();
		editCard.Child = editPanel;
		timelinePanel.Children.Add(editCard);
		_pointTypeText = new TextBlock { Text = L("変更点を選択すると編集できます", "Select a change to edit it"), FontWeight = FontWeights.SemiBold, Foreground = Muted() };
		editPanel.Children.Add(_pointTypeText);
		Grid editGrid = new() { Margin = new Thickness(0, 7, 0, 0) };
		editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
		editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		for (int i = 0; i < 3; i++) editGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		_pointALabel = EditLabel(editGrid, 0);
		_pointBLabel = EditLabel(editGrid, 1);
		_pointLyricsLabel = EditLabel(editGrid, 2);
		_pointABox = EditBox(editGrid, 0);
		_pointBBox = EditBox(editGrid, 1);
		_pointLyricsBox = EditBox(editGrid, 2);
		editPanel.Children.Add(editGrid);
		WrapPanel editActions = new() { Margin = new Thickness(-3, 4, 0, 0) };
		_applyPointButton = Button(L("時刻を適用", "Apply times"));
		_applyPointButton.Click += ApplyPoint_Click;
		_pointNowButton = Button(L("再生時刻＝現在", "Playback = now"));
		_pointNowButton.Click += PointNow_Click;
		_pointLyricButton = Button(L("歌詞時刻＝選択行", "Lyric = selected"));
		_pointLyricButton.Click += PointLyric_Click;
		Button deletePoint = Button(L("削除", "Delete"));
		deletePoint.Click += DeleteSelectedPoint_Click;
		editActions.Children.Add(_applyPointButton);
		editActions.Children.Add(_pointNowButton);
		editActions.Children.Add(_pointLyricButton);
		editActions.Children.Add(deletePoint);
		editPanel.Children.Add(editActions);

		Border lyricsCard = Card();
		Grid lyricsPanel = new() { Margin = new Thickness(12) };
		lyricsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		lyricsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		lyricsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		lyricsCard.Child = lyricsPanel;
		Grid.SetColumn(lyricsCard, 2);
		body.Children.Add(lyricsCard);
		DockPanel lyricsHeader = new() { Margin = new Thickness(2, 0, 2, 8) };
		_followNowBox = new CheckBox { Content = L("再生中の行を追う", "Follow current line"), IsChecked = true, Foreground = Foreground, VerticalAlignment = VerticalAlignment.Center };
		DockPanel.SetDock(_followNowBox, Dock.Right);
		lyricsHeader.Children.Add(_followNowBox);
		lyricsHeader.Children.Add(SectionTitle(L("時刻付き歌詞", "Lyrics with timestamps")));
		lyricsPanel.Children.Add(lyricsHeader);
		_lyricsList = new ListBox
		{
			Background = Brush(28, 26, 29), Foreground = Foreground, BorderThickness = new Thickness(0),
			HorizontalContentAlignment = HorizontalAlignment.Stretch
		};
		ScrollViewer.SetHorizontalScrollBarVisibility(_lyricsList, ScrollBarVisibility.Disabled);
		_lyricsList.SelectionChanged += LyricsList_SelectionChanged;
		_lyricsList.PreviewMouseWheel += delegate { _followNowBox.IsChecked = false; };
		Grid.SetRow(_lyricsList, 1);
		lyricsPanel.Children.Add(_lyricsList);
		TextBlock lyricsHint = new()
		{
			Text = L("行をクリックすると選択。● が今再生中の位置です。", "Click a line to select it. ● marks the current playback position."),
			Foreground = Muted(), Margin = new Thickness(2, 8, 2, 0), TextWrapping = TextWrapping.Wrap
		};
		Grid.SetRow(lyricsHint, 2);
		lyricsPanel.Children.Add(lyricsHint);

		DockPanel footer = new() { Margin = new Thickness(0, 14, 0, 0) };
		Grid.SetRow(footer, 2);
		root.Children.Add(footer);
		_trackScopeBox = new CheckBox
		{
			Content = L("この曲をすべての再生元で使う", "Use for this track on every source"),
			IsChecked = _profile.Scope == PersonalSyncScope.Track, Foreground = Foreground, VerticalAlignment = VerticalAlignment.Center
		};
		_trackScopeBox.Checked += Scope_Changed;
		_trackScopeBox.Unchecked += Scope_Changed;
		footer.Children.Add(_trackScopeBox);
		WrapPanel bottomButtons = new() { HorizontalAlignment = HorizontalAlignment.Right };
		DockPanel.SetDock(bottomButtons, Dock.Right);
		_undoButton = Button("UNDO");
		_redoButton = Button("REDO");
		Button reset = Button(L("リセット", "Reset"));
		Button remove = Button(L("この曲の調整を解除", "Remove sync"));
		Button close = Button(L("閉じる", "Close"));
		_undoButton.Click += delegate { Undo(); };
		_redoButton.Click += delegate { Redo(); };
		reset.Click += Reset_Click;
		remove.Click += Remove_Click;
		close.Click += delegate { Close(); };
		bottomButtons.Children.Add(_undoButton);
		bottomButtons.Children.Add(_redoButton);
		bottomButtons.Children.Add(reset);
		bottomButtons.Children.Add(remove);
		bottomButtons.Children.Add(close);
		footer.Children.Add(bottomButtons);

		BuildLyricsList();
		_previewTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
		_previewTimer.Tick += delegate { RefreshLiveUi(); };
		Loaded += PersonalSyncWindow_Loaded;
		Closing += PersonalSyncWindow_Closing;
		Closed += delegate { _previewTimer.Stop(); };
	}

	private void PersonalSyncWindow_Loaded(object sender, RoutedEventArgs e)
	{
		PlaceBesideOwner();
		int? initial = _initialLineProvider();
		_selectedLineIndex = initial is >= 0 && initial < _lines.Count ? initial.Value : -1;
		if (_selectedLineIndex >= 0) _lyricsList.SelectedIndex = _selectedLineIndex;
		_previewTimer.Start();
		RefreshAll();
	}

	private void BuildLyricsList()
	{
		_lyricsList.Items.Clear();
		_lyricRows.Clear();
		for (int index = 0; index < _lines.Count; index++)
		{
			LyricLine line = _lines[index];
			Grid row = new() { Margin = new Thickness(4, 3, 6, 3) };
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			TextBlock marker = new() { Foreground = Accent(), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
			TextBlock time = new() { Text = Format(line.Time.TotalSeconds), FontFamily = new FontFamily("Consolas"), Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center };
			TextBlock lyric = new() { Text = line.Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
			Grid.SetColumn(time, 1);
			Grid.SetColumn(lyric, 2);
			row.Children.Add(marker);
			row.Children.Add(time);
			row.Children.Add(lyric);
			ListBoxItem item = new() { Content = row, Tag = index, Padding = new Thickness(2), HorizontalContentAlignment = HorizontalAlignment.Stretch };
			_lyricsList.Items.Add(item);
			_lyricRows.Add(new LyricRow(marker, time, lyric));
		}
	}

	private void Nudge(double delta)
	{
		Change(profile =>
		{
			if (profile.Mode == PersonalSyncMode.None) profile.Mode = PersonalSyncMode.Offset;
			profile.OffsetSeconds = Math.Round(Math.Clamp(profile.OffsetSeconds + delta, -3600.0, 3600.0), 3);
		});
	}

	private void Change(Action<PersonalSyncProfile> mutation)
	{
		_undo.Push(_profile.Clone());
		while (_undo.Count > 80) TrimStack(_undo, 80);
		_redo.Clear();
		mutation(_profile);
		if (_profile.Mode == PersonalSyncMode.None && (Math.Abs(_profile.OffsetSeconds) > 0.0001 || _profile.Anchors.Count > 0 || _profile.Segments.Count > 0))
		{
			_profile.Mode = _profile.Anchors.Count > 0 || _profile.Segments.Count > 0 ? PersonalSyncMode.Advanced : PersonalSyncMode.Offset;
		}
		_profile.OffsetSeconds = Math.Round(Math.Clamp(_profile.OffsetSeconds, -3600.0, 3600.0), 3);
		_profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
		_dirty = true;
		PreviewChanged?.Invoke(this, _profile.Clone());
		RefreshAll();
	}

	private static void TrimStack(Stack<PersonalSyncProfile> stack, int keep)
	{
		PersonalSyncProfile[] items = stack.Take(keep).Reverse().ToArray();
		stack.Clear();
		foreach (PersonalSyncProfile item in items) stack.Push(item);
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
		if (!ValidSelectedLine(out int selected)) return;
		double offset = PersonalSyncMapper.CalculateOffsetSeconds(_playbackPositionProvider(), _lines[selected].Time);
		Change(profile =>
		{
			profile.Mode = profile.Anchors.Count > 0 || profile.Segments.Count > 0 ? PersonalSyncMode.Advanced : PersonalSyncMode.Offset;
			profile.OffsetSeconds = offset;
		});
	}

	private void AddAnchor_Click(object sender, RoutedEventArgs e)
	{
		if (!ValidSelectedLine(out int selected)) return;
		double playback = Math.Max(0, _playbackPositionProvider().TotalSeconds);
		double lyrics = Math.Max(0, _lines[selected].Time.TotalSeconds);
		Change(profile =>
		{
			profile.Mode = PersonalSyncMode.Advanced;
			PersonalSyncAnchor? nearby = profile.Anchors.FirstOrDefault(anchor => Math.Abs(anchor.PlaybackSeconds - playback) < 0.12);
			if (nearby == null) profile.Anchors.Add(new PersonalSyncAnchor { PlaybackSeconds = playback, LyricsSeconds = lyrics });
			else { nearby.PlaybackSeconds = playback; nearby.LyricsSeconds = lyrics; }
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
			_holdButton.Content = L("ここで歌詞を再開", "Resume lyrics here");
			RefreshLiveUi();
			return;
		}
		double start = _pendingHoldStart.Value;
		_pendingHoldStart = null;
		_holdButton.Content = L("ここから歌詞を止める", "Hold lyrics from here");
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

	private void LyricsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_refreshing) return;
		_selectedLineIndex = _lyricsList.SelectedItem is ListBoxItem { Tag: int index } ? index : -1;
		RefreshSelectionText();
	}

	private void PointsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPointEditor();

	private void RefreshPointEditor()
	{
		bool hasItem = _pointsList.SelectedItem is SyncPointListItem;
		_pointABox.IsEnabled = hasItem;
		_pointBBox.IsEnabled = hasItem;
		_pointLyricsBox.IsEnabled = hasItem;
		_applyPointButton.IsEnabled = hasItem;
		_pointNowButton.IsEnabled = hasItem;
		_pointLyricButton.IsEnabled = hasItem && _selectedLineIndex >= 0;
		if (_pointsList.SelectedItem is not SyncPointListItem item)
		{
			_pointTypeText.Text = L("変更点を選択すると編集できます", "Select a change to edit it");
			_pointALabel.Text = L("再生", "Playback");
			_pointBLabel.Text = L("歌詞", "Lyric");
			_pointLyricsLabel.Text = string.Empty;
			_pointABox.Text = _pointBBox.Text = _pointLyricsBox.Text = string.Empty;
			_pointLyricsBox.Visibility = Visibility.Collapsed;
			return;
		}
		if (item.IsAnchor)
		{
			PersonalSyncAnchor? anchor = _profile.Anchors.FirstOrDefault(candidate => candidate.Id == item.Id);
			if (anchor == null) return;
			_pointTypeText.Text = L("同期点：ここから歌詞タイミングを変更", "Sync point: timing changes from here");
			_pointALabel.Text = L("再生時刻", "Playback");
			_pointBLabel.Text = L("歌詞時刻", "Lyric");
			_pointLyricsLabel.Text = string.Empty;
			_pointABox.Text = Format(anchor.PlaybackSeconds);
			_pointBBox.Text = Format(anchor.LyricsSeconds);
			_pointLyricsBox.Text = string.Empty;
			_pointLyricsBox.Visibility = Visibility.Collapsed;
		}
		else
		{
			PersonalSyncSegment? hold = _profile.Segments.FirstOrDefault(candidate => candidate.Id == item.Id);
			if (hold == null) return;
			_pointTypeText.Text = L("停止区間：この間は同じ歌詞位置を保持", "Hold range: lyric position stays fixed");
			_pointALabel.Text = L("開始", "Start");
			_pointBLabel.Text = L("終了", "End");
			_pointLyricsLabel.Text = L("歌詞位置", "Lyric");
			_pointABox.Text = Format(hold.PlaybackStartSeconds);
			_pointBBox.Text = Format(hold.PlaybackEndSeconds);
			_pointLyricsBox.Text = Format(hold.LyricsTimeSeconds);
			_pointLyricsBox.Visibility = Visibility.Visible;
		}
	}

	private void ApplyPoint_Click(object sender, RoutedEventArgs e)
	{
		if (_pointsList.SelectedItem is not SyncPointListItem item || !TryParseTime(_pointABox.Text, out double a) || !TryParseTime(_pointBBox.Text, out double b)) return;
		if (item.IsAnchor)
		{
			Change(profile =>
			{
				PersonalSyncAnchor? anchor = profile.Anchors.FirstOrDefault(candidate => candidate.Id == item.Id);
				if (anchor == null) return;
				anchor.PlaybackSeconds = Math.Max(0, a);
				anchor.LyricsSeconds = Math.Max(0, b);
				profile.Anchors = profile.Anchors.OrderBy(candidate => candidate.PlaybackSeconds).ToList();
			});
		}
		else if (TryParseTime(_pointLyricsBox.Text, out double lyrics) && b > a)
		{
			Change(profile =>
			{
				PersonalSyncSegment? hold = profile.Segments.FirstOrDefault(candidate => candidate.Id == item.Id);
				if (hold == null) return;
				hold.PlaybackStartSeconds = Math.Max(0, a);
				hold.PlaybackEndSeconds = Math.Max(hold.PlaybackStartSeconds + 0.05, b);
				hold.LyricsTimeSeconds = Math.Max(0, lyrics);
				profile.Segments = profile.Segments.OrderBy(candidate => candidate.PlaybackStartSeconds).ToList();
			});
		}
	}

	private void PointNow_Click(object sender, RoutedEventArgs e)
	{
		if (_pointsList.SelectedItem is not SyncPointListItem item) return;
		double now = Math.Max(0, _playbackPositionProvider().TotalSeconds);
		_pointABox.Text = Format(now);
		if (!item.IsAnchor && TryParseTime(_pointBBox.Text, out double end) && end <= now) _pointBBox.Text = Format(now + 1.0);
	}

	private void PointLyric_Click(object sender, RoutedEventArgs e)
	{
		if (!ValidSelectedLine(out int selected) || _pointsList.SelectedItem is not SyncPointListItem item) return;
		string value = Format(_lines[selected].Time.TotalSeconds);
		if (item.IsAnchor) _pointBBox.Text = value;
		else _pointLyricsBox.Text = value;
	}

	private void DeleteSelectedPoint_Click(object sender, RoutedEventArgs e)
	{
		if (_pointsList.SelectedItem is not SyncPointListItem item) return;
		Change(profile =>
		{
			if (item.IsAnchor) profile.Anchors.RemoveAll(anchor => anchor.Id == item.Id);
			else profile.Segments.RemoveAll(segment => segment.Id == item.Id);
			if (profile.Anchors.Count == 0 && profile.Segments.Count == 0)
				profile.Mode = Math.Abs(profile.OffsetSeconds) > 0.0001 ? PersonalSyncMode.Offset : PersonalSyncMode.None;
		});
	}

	private void Scope_Changed(object sender, RoutedEventArgs e)
	{
		if (!IsLoaded || _refreshing) return;
		Change(profile => profile.Scope = _trackScopeBox.IsChecked == true ? PersonalSyncScope.Track : PersonalSyncScope.Source);
	}

	private void Reset_Click(object sender, RoutedEventArgs e)
	{
		Change(profile => { profile.OffsetSeconds = 0; profile.Mode = PersonalSyncMode.None; profile.Anchors.Clear(); profile.Segments.Clear(); });
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
		catch (Exception ex) { MessageBox.Show(this, ex.Message, "FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Exclamation); }
	}

	private async void PersonalSyncWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
	{
		_previewTimer.Stop();
		if (!_dirty || _deleted) return;
		try
		{
			_profile = await _store.UpsertAsync(_profile);
			_dirty = false;
			PreviewChanged?.Invoke(this, _profile.Mode == PersonalSyncMode.None ? null : _profile.Clone());
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
		_refreshing = true;
		try
		{
			_offsetText.Text = _profile.OffsetSeconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
			_trackScopeBox.IsChecked = _profile.Scope == PersonalSyncScope.Track;
			_undoButton.IsEnabled = _undo.Count > 0;
			_redoButton.IsEnabled = _redo.Count > 0;
			RefreshPointList();
			RefreshSelectionText();
			DrawTimeline();
		}
		finally { _refreshing = false; }
		RefreshPointEditor();
		RefreshLiveUi();
	}

	private void RefreshLiveUi()
	{
		if (_lines.Count == 0) return;
		double playback = Math.Max(0, _playbackPositionProvider().TotalSeconds);
		double lyrics = PersonalSyncMapper.MapPlaybackToLyrics(playback, _profile);
		int active = FindActiveLine(lyrics);
		_nowText.Text = L("再生", "PLAY") + "  " + Format(playback) + "\n" + L("歌詞", "LYRIC") + "  " + Format(lyrics);
		_matchButton.Content = ValidSelectedLine(out _)
			? L($"選択した歌詞を現在の {Format(playback)} に設定", $"Set selected lyric to current {Format(playback)}")
			: L("右の歌詞を選択してください", "Select a lyric on the right");
		_matchButton.IsEnabled = _selectedLineIndex >= 0;
		_pointLyricButton.IsEnabled = _pointsList.SelectedItem is SyncPointListItem && _selectedLineIndex >= 0;
		if (active != _activeLineIndex)
		{
			_activeLineIndex = active;
			RefreshLyricRowVisuals();
			if (_followNowBox.IsChecked == true && active >= 0 && active < _lyricsList.Items.Count) _lyricsList.ScrollIntoView(_lyricsList.Items[active]);
		}
		DrawTimeline();
	}

	private void RefreshLyricRowVisuals()
	{
		for (int index = 0; index < _lyricRows.Count; index++)
		{
			LyricRow row = _lyricRows[index];
			bool active = index == _activeLineIndex;
			row.Marker.Text = active ? "●" : string.Empty;
			row.Time.Foreground = active ? Accent() : Muted();
			row.Lyric.Foreground = active ? Brushes.White : Brush(210, 206, 210);
			row.Lyric.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
		}
	}

	private void RefreshSelectionText()
	{
		if (ValidSelectedLine(out int selected))
		{
			_selectionText.Text = "✓  [" + Format(_lines[selected].Time.TotalSeconds) + "]  " + _lines[selected].Text;
			_selectionText.Foreground = Accent();
		}
		else
		{
			_selectionText.Text = L("右の歌詞をクリックして選択", "Click a lyric on the right to select it");
			_selectionText.Foreground = Muted();
		}
	}

	private void RefreshPointList()
	{
		Guid? selectedId = _pointsList.SelectedItem is SyncPointListItem selected ? selected.Id : null;
		_pointsList.Items.Clear();
		foreach (PersonalSyncAnchor anchor in _profile.Anchors.OrderBy(item => item.PlaybackSeconds))
		{
			double offset = anchor.PlaybackSeconds - anchor.LyricsSeconds;
			SyncPointListItem item = new(anchor.Id, true,
				$"●  {Format(anchor.PlaybackSeconds)}  →  {Format(anchor.LyricsSeconds)}  ({offset:+0.0;-0.0;0.0}s)\n    {NearestLyricText(anchor.LyricsSeconds)}");
			_pointsList.Items.Add(item);
			if (selectedId == anchor.Id) _pointsList.SelectedItem = item;
		}
		foreach (PersonalSyncSegment segment in _profile.Segments.OrderBy(item => item.PlaybackStartSeconds))
		{
			SyncPointListItem item = new(segment.Id, false,
				$"▰  {Format(segment.PlaybackStartSeconds)} — {Format(segment.PlaybackEndSeconds)}\n    {L("歌詞を固定", "Hold lyric")} {Format(segment.LyricsTimeSeconds)}");
			_pointsList.Items.Add(item);
			if (selectedId == segment.Id) _pointsList.SelectedItem = item;
		}
	}

	private string NearestLyricText(double seconds)
	{
		if (_lines.Count == 0) return string.Empty;
		int index = Math.Clamp(FindActiveLine(seconds), 0, _lines.Count - 1);
		return _lines[index].Text;
	}

	private void DrawTimeline()
	{
		if (_timeline.ActualWidth <= 1) return;
		_timeline.Children.Clear();
		double width = _timeline.ActualWidth;
		double duration = Math.Max(1.0, _context.Track.DurationSeconds);
		_timeline.Children.Add(new Line { X1 = 16, X2 = width - 16, Y1 = 47, Y2 = 47, Stroke = Brush(100, 95, 102), StrokeThickness = 3 });
		foreach (PersonalSyncSegment segment in _profile.Segments.Where(item => item.Type == PersonalSyncSegmentType.Hold))
		{
			double x1 = X(segment.PlaybackStartSeconds, duration, width);
			double x2 = X(segment.PlaybackEndSeconds, duration, width);
			Rectangle hold = new() { Width = Math.Max(3, x2 - x1), Height = 18, Fill = Accent(), Opacity = .38, ToolTip = $"HOLD {Format(segment.PlaybackStartSeconds)} — {Format(segment.PlaybackEndSeconds)}" };
			Canvas.SetLeft(hold, x1);
			Canvas.SetTop(hold, 38);
			_timeline.Children.Add(hold);
		}
		foreach (PersonalSyncAnchor anchor in _profile.Anchors)
		{
			double x = X(anchor.PlaybackSeconds, duration, width);
			_timeline.Children.Add(new Line { X1 = x, X2 = x, Y1 = 24, Y2 = 62, Stroke = Accent(), StrokeThickness = 1.5, Opacity = 0.85 });
			Ellipse point = new() { Width = 12, Height = 12, Fill = Accent(), Stroke = Brushes.White, StrokeThickness = 1, ToolTip = $"{Format(anchor.PlaybackSeconds)} → {Format(anchor.LyricsSeconds)}" };
			Canvas.SetLeft(point, x - 6);
			Canvas.SetTop(point, 41);
			_timeline.Children.Add(point);
		}
		double now = Math.Clamp(_playbackPositionProvider().TotalSeconds, 0, duration);
		double nowX = X(now, duration, width);
		_timeline.Children.Add(new Line { X1 = nowX, X2 = nowX, Y1 = 15, Y2 = 72, Stroke = Brushes.White, StrokeThickness = 1.2 });
		TextBlock nowLabel = new() { Text = Format(now), Foreground = Brushes.White, FontFamily = new FontFamily("Consolas"), FontSize = 9 };
		Canvas.SetLeft(nowLabel, Math.Clamp(nowX - 22, 2, Math.Max(2, width - 48)));
		Canvas.SetTop(nowLabel, 2);
		_timeline.Children.Add(nowLabel);
		if (_pendingHoldStart.HasValue)
		{
			double pendingX = X(_pendingHoldStart.Value, duration, width);
			_timeline.Children.Add(new Line { X1 = pendingX, X2 = pendingX, Y1 = 28, Y2 = 66, Stroke = Accent(), StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 2, 2 } });
		}
	}

	private int FindActiveLine(double lyricsSeconds)
	{
		int low = 0, high = _lines.Count - 1, result = -1;
		while (low <= high)
		{
			int middle = low + (high - low) / 2;
			if (_lines[middle].Time.TotalSeconds <= lyricsSeconds) { result = middle; low = middle + 1; }
			else high = middle - 1;
		}
		return result;
	}

	private bool ValidSelectedLine(out int selected)
	{
		selected = _selectedLineIndex;
		return selected >= 0 && selected < _lines.Count;
	}

	private void PlaceBesideOwner()
	{
		if (Owner == null) return;
		Owner.UpdateLayout();
		System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(Owner).Handle);
		DpiScale dpi = VisualTreeHelper.GetDpi(Owner);
		Rect work = new(screen.WorkingArea.Left / dpi.DpiScaleX, screen.WorkingArea.Top / dpi.DpiScaleY,
			screen.WorkingArea.Width / dpi.DpiScaleX, screen.WorkingArea.Height / dpi.DpiScaleY);
		Rect owner = new(Owner.Left, Owner.Top, Math.Max(Owner.ActualWidth, Owner.Width), Math.Max(Owner.ActualHeight, Owner.Height));
		double gap = 12, width = ActualWidth > 0 ? ActualWidth : Width, height = ActualHeight > 0 ? ActualHeight : Height;
		double rightSpace = work.Right - owner.Right, leftSpace = owner.Left - work.Left;
		double belowSpace = work.Bottom - owner.Bottom, aboveSpace = owner.Top - work.Top;
		if (rightSpace >= width + gap) { Left = owner.Right + gap; Top = owner.Top; }
		else if (leftSpace >= width + gap) { Left = owner.Left - width - gap; Top = owner.Top; }
		else if (belowSpace >= height + gap) { Left = owner.Left + (owner.Width - width) / 2; Top = owner.Bottom + gap; }
		else if (aboveSpace >= height + gap) { Left = owner.Left + (owner.Width - width) / 2; Top = owner.Top - height - gap; }
		else { Left = rightSpace >= leftSpace ? owner.Right + gap : owner.Left - width - gap; Top = owner.Top; }
		Left = Math.Clamp(Left, work.Left, Math.Max(work.Left, work.Right - width));
		Top = Math.Clamp(Top, work.Top, Math.Max(work.Top, work.Bottom - height));
	}

	private static PersonalSyncProfile CreateProfile(PersonalSyncContext context) => new()
	{
		Mode = PersonalSyncMode.Offset, Scope = PersonalSyncScope.Source, Track = context.Track, Source = context.Source, Lyrics = context.Lyrics
	};

	private static TextBlock SectionTitle(string text) => new() { Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4) };
	private static StackPanel CardContent() => new() { Margin = new Thickness(13) };
	private static Border Card() => new() { Background = Brush(36, 33, 37), BorderBrush = Brush(69, 64, 70), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 0, 10) };
	private static Button Button(string text) => new() { Content = text, Padding = new Thickness(11, 7, 11, 7), Margin = new Thickness(3), Background = Brush(49, 46, 50), Foreground = Brush(238, 235, 237), BorderBrush = Brush(89, 83, 91), BorderThickness = new Thickness(1), Cursor = Cursors.Hand };

	private static TextBlock EditLabel(Grid grid, int row)
	{
		TextBlock label = new() { Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center };
		Grid.SetRow(label, row);
		grid.Children.Add(label);
		return label;
	}

	private static TextBox EditBox(Grid grid, int row)
	{
		TextBox box = new() { Margin = new Thickness(3), Padding = new Thickness(7, 5, 7, 5), Background = Brush(244, 243, 244), Foreground = Brush(26, 24, 27), BorderBrush = Brush(89, 83, 91), FontFamily = new FontFamily("Consolas") };
		Grid.SetRow(box, row);
		Grid.SetColumn(box, 1);
		grid.Children.Add(box);
		return box;
	}

	private static bool TryParseTime(string text, out double seconds)
	{
		seconds = 0;
		string value = text.Trim();
		if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)) return double.IsFinite(seconds) && seconds >= 0;
		string[] parts = value.Split(':');
		if (parts.Length is < 2 or > 3) return false;
		double total = 0;
		foreach (string part in parts)
		{
			if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return false;
			total = total * 60 + number;
		}
		seconds = total;
		return double.IsFinite(seconds) && seconds >= 0;
	}

	private static double X(double seconds, double duration, double width) => 16 + Math.Clamp(seconds / duration, 0, 1) * Math.Max(1, width - 32);
	private static string Format(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f", CultureInfo.InvariantCulture);
	private string L(string ja, string en) => _language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? ja : en;
	private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
	private static SolidColorBrush Accent() => Brush(255, 107, 44);
	private static SolidColorBrush Muted() => Brush(181, 176, 181);

	private sealed record LyricRow(TextBlock Marker, TextBlock Time, TextBlock Lyric);
	private sealed record SyncPointListItem(Guid Id, bool IsAnchor, string Label) { public override string ToString() => Label; }
}
