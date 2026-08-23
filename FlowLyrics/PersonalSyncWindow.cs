using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

/// <summary>Movable, lyrics-first non-destructive Personal Sync editor.</summary>
public sealed class PersonalSyncWindow : Window
{
	private readonly PersonalSyncStore _store;
	private readonly PersonalSyncContext _context;
	private readonly IReadOnlyList<LyricLine> _lines;
	private readonly Func<int?> _initialLineProvider;
	private readonly Func<TimeSpan> _playbackPositionProvider;
	private readonly string _language;
	private readonly bool _hadStoredProfile;
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
	private readonly TextBlock _workflowHint;
	private readonly Button _matchButton;
	private readonly Button _undoButton;
	private readonly Button _redoButton;
	private readonly Button _holdButton;
	private readonly Button _cancelHoldButton;
	private readonly CheckBox _trackScopeBox;
	private readonly CheckBox _followNowBox;
	private readonly ListBox _lyricsList;
	private readonly Canvas _timeline;
	private readonly ListBox _pointsList;
	private readonly TextBlock _pointTypeText;
	private readonly TimeEditorRow _pointA;
	private readonly TimeEditorRow _pointB;
	private readonly TimeEditorRow _pointLyrics;
	private readonly Button _deletePointButton;

	public event EventHandler<PersonalSyncProfile?>? PreviewChanged;

	public PersonalSyncWindow(PersonalSyncStore store, PersonalSyncContext context, PersonalSyncProfile? profile,
		IReadOnlyList<LyricLine> lines, Func<int?> selectedLineProvider, Func<TimeSpan> playbackPositionProvider, string language)
	{
		_store = store;
		_context = context;
		_lines = lines;
		_initialLineProvider = selectedLineProvider;
		_playbackPositionProvider = playbackPositionProvider;
		_language = language;
		_hadStoredProfile = profile != null;
		_profile = profile?.Clone() ?? CreateProfile(context);

		Title = "FlowLyrics · " + L("歌詞タイミング", "Personal Sync");
		Width = 1120;
		Height = 780;
		MinWidth = 900;
		MinHeight = 640;
		WindowStartupLocation = WindowStartupLocation.Manual;
		Background = Brush(25, 23, 26);
		Foreground = Brush(235, 232, 234);
		ShowInTaskbar = false;
		PersonalSyncUiTheme.Apply(this);

		Grid root = new() { Margin = new Thickness(22) };
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
			FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
		});
		heading.Children.Add(new TextBlock
		{
			Text = SourceContext(context.Source) + "  ·  " + context.Lyrics.DisplayName,
			Foreground = Muted(), Margin = new Thickness(0, 3, 0, 0)
		});
		header.Children.Add(heading);
		_nowText = new TextBlock
		{
			FontFamily = new FontFamily("Consolas"), FontSize = 12, TextAlignment = TextAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center, Foreground = Brush(224, 220, 223)
		};
		Grid.SetColumn(_nowText, 1);
		header.Children.Add(_nowText);
		root.Children.Add(header);

		Grid body = new();
		body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.98, GridUnitType.Star) });
		body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
		body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.12, GridUnitType.Star) });
		Grid.SetRow(body, 1);
		root.Children.Add(body);

		ScrollViewer leftScroll = new()
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Padding = new Thickness(0, 0, 5, 0)
		};
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
			Text = L("＋で遅く、－で早く表示します。入力は不要です。", "Plus displays lyrics later; minus displays them earlier. No typing needed."),
			Foreground = Muted(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 9)
		});
		_offsetText = new TextBlock
		{
			HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Consolas"), FontSize = 25,
			FontWeight = FontWeights.Bold, Foreground = Accent(), Margin = new Thickness(0, 0, 0, 8)
		};
		offsetPanel.Children.Add(_offsetText);
		offsetPanel.Children.Add(CreateNudgeButtons(Nudge));

		Border timelineCard = Card();
		StackPanel timelinePanel = CardContent();
		timelineCard.Child = timelinePanel;
		left.Children.Add(timelineCard);
		timelinePanel.Children.Add(SectionTitle(L("曲の途中で合わせ直す", "Re-sync during the track")));
		timelinePanel.Children.Add(new TextBlock
		{
			Text = L("上から下へ曲が進みます。●は切替、帯は歌詞を止める区間、白線は現在位置です。", "The song flows top to bottom. ● is a switch, the band is a lyric hold, and the white line is now."),
			Foreground = Muted(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8)
		});
		_timeline = new Canvas { Height = 270, Background = Brush(31, 29, 32), ClipToBounds = true };
		_timeline.SizeChanged += delegate { DrawTimeline(); };
		timelinePanel.Children.Add(_timeline);
		_selectionText = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 5) };
		timelinePanel.Children.Add(_selectionText);
		_matchButton = Button(L("選択した歌詞を現在位置に設定", "Set selected lyric to current time"));
		_matchButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		_matchButton.Click += MatchSelectedLine_Click;
		timelinePanel.Children.Add(_matchButton);

		_holdButton = PrimaryButton(L("ここから歌詞を止める", "Hold lyrics from here"));
		_holdButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		_holdButton.Click += Hold_Click;
		timelinePanel.Children.Add(_holdButton);
		_workflowHint = new TextBlock
		{
			Text = L("止めるときに押し、再開したい歌詞を右で選び、その歌詞が聞こえた瞬間にもう一度押します。", "Press to hold, select the resume lyric on the right, then press again the instant you hear it."),
			Foreground = Muted(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 3, 4, 5)
		};
		timelinePanel.Children.Add(_workflowHint);
		_cancelHoldButton = Button(L("停止設定をキャンセル", "Cancel pending hold"));
		_cancelHoldButton.Visibility = Visibility.Collapsed;
		_cancelHoldButton.Click += delegate { CancelPendingHold(); };
		timelinePanel.Children.Add(_cancelHoldButton);

		Button anchorButton = Button(L("ここから選択したタイミングへ切り替える", "Switch to selected timing from here"));
		anchorButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		anchorButton.Click += AddAnchor_Click;
		timelinePanel.Children.Add(anchorButton);

		_pointsList = new ListBox { MinHeight = 96, MaxHeight = 180, Margin = new Thickness(0, 10, 0, 6) };
		_pointsList.SelectionChanged += delegate { RefreshPointEditor(); };
		timelinePanel.Children.Add(_pointsList);

		Border editCard = new()
		{
			Background = Brush(30, 28, 31), BorderBrush = Brush(69, 64, 70), BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(8), Padding = new Thickness(11), Margin = new Thickness(0, 3, 0, 0)
		};
		StackPanel editPanel = new();
		editCard.Child = editPanel;
		timelinePanel.Children.Add(editCard);
		_pointTypeText = new TextBlock
		{
			Text = L("変更点を選ぶと、ボタンだけで時刻を直せます", "Select a change, then adjust it with buttons only"),
			FontWeight = FontWeights.SemiBold, Foreground = Muted(), TextWrapping = TextWrapping.Wrap
		};
		editPanel.Children.Add(_pointTypeText);
		_pointA = CreateTimeEditorRow(editPanel, TimeField.A);
		_pointB = CreateTimeEditorRow(editPanel, TimeField.B);
		_pointLyrics = CreateTimeEditorRow(editPanel, TimeField.Lyrics);
		_deletePointButton = Button(L("この変更点を削除", "Delete this change"));
		_deletePointButton.HorizontalAlignment = HorizontalAlignment.Left;
		_deletePointButton.Click += DeleteSelectedPoint_Click;
		editPanel.Children.Add(_deletePointButton);

		Border lyricsCard = Card();
		Grid lyricsPanel = new() { Margin = new Thickness(12) };
		lyricsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		lyricsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		lyricsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		lyricsCard.Child = lyricsPanel;
		Grid.SetColumn(lyricsCard, 2);
		body.Children.Add(lyricsCard);
		DockPanel lyricsHeader = new() { Margin = new Thickness(2, 0, 2, 8) };
		_followNowBox = new CheckBox { Content = L("再生中の行を追う", "Follow current line"), IsChecked = true };
		DockPanel.SetDock(_followNowBox, Dock.Right);
		lyricsHeader.Children.Add(_followNowBox);
		lyricsHeader.Children.Add(SectionTitle(L("時刻付き歌詞", "Lyrics with timestamps")));
		lyricsPanel.Children.Add(lyricsHeader);
		_lyricsList = new ListBox { BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
		_lyricsList.SelectionChanged += LyricsList_SelectionChanged;
		_lyricsList.PreviewMouseWheel += delegate { _followNowBox.IsChecked = false; };
		Grid.SetRow(_lyricsList, 1);
		lyricsPanel.Children.Add(_lyricsList);
		TextBlock lyricsHint = new()
		{
			Text = L("行をクリックして選択。● が現在表示中です。停止中も再開したい行を選べます。", "Click a line to select it. ● is the displayed line. You can choose a resume line while held."),
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
			IsChecked = _profile.Scope == PersonalSyncScope.Track
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
		Button close = PrimaryButton(L("閉じる", "Close"));
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
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			TextBlock marker = new() { Foreground = Accent(), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
			TextBlock time = new() { Text = Format(line.Time.TotalSeconds), FontFamily = new FontFamily("Consolas"), Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center };
			TextBlock lyric = new() { Text = line.Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
			Grid.SetColumn(time, 1);
			Grid.SetColumn(lyric, 2);
			row.Children.Add(marker);
			row.Children.Add(time);
			row.Children.Add(lyric);
			ListBoxItem item = new() { Content = row, Tag = index, HorizontalContentAlignment = HorizontalAlignment.Stretch };
			_lyricsList.Items.Add(item);
			_lyricRows.Add(new LyricRow(marker, time, lyric));
		}
	}

	private void Nudge(double delta) => Change(profile =>
	{
		if (profile.Mode == PersonalSyncMode.None) profile.Mode = PersonalSyncMode.Offset;
		profile.OffsetSeconds = Round(profile.OffsetSeconds + delta);
	});

	private void Change(Action<PersonalSyncProfile> mutation)
	{
		_undo.Push(_profile.Clone());
		while (_undo.Count > 80) TrimStack(_undo, 80);
		_redo.Clear();
		mutation(_profile);
		if (_profile.Mode == PersonalSyncMode.None && (Math.Abs(_profile.OffsetSeconds) > 0.0001 || _profile.Anchors.Count > 0 || _profile.Segments.Count > 0))
			_profile.Mode = _profile.Anchors.Count > 0 || _profile.Segments.Count > 0 ? PersonalSyncMode.Advanced : PersonalSyncMode.Offset;
		_profile.OffsetSeconds = Round(_profile.OffsetSeconds);
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
		CancelPendingHold();
		_redo.Push(_profile.Clone());
		_profile = _undo.Pop();
		_dirty = true;
		PreviewChanged?.Invoke(this, _profile.Clone());
		RefreshAll();
	}

	private void Redo()
	{
		if (_redo.Count == 0) return;
		CancelPendingHold();
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
		Guid anchorId = Guid.NewGuid();
		Change(profile =>
		{
			profile.Mode = PersonalSyncMode.Advanced;
			PersonalSyncAnchor? nearby = profile.Anchors.FirstOrDefault(anchor => Math.Abs(anchor.PlaybackSeconds - playback) < 0.12);
			if (nearby == null) profile.Anchors.Add(new PersonalSyncAnchor { Id = anchorId, PlaybackSeconds = playback, LyricsSeconds = lyrics });
			else { anchorId = nearby.Id; nearby.PlaybackSeconds = playback; nearby.LyricsSeconds = lyrics; }
			profile.Anchors = profile.Anchors.OrderBy(anchor => anchor.PlaybackSeconds).ToList();
		});
		SelectPoint(anchorId);
	}

	private void Hold_Click(object sender, RoutedEventArgs e)
	{
		double playback = Math.Max(0, _playbackPositionProvider().TotalSeconds);
		if (!_pendingHoldStart.HasValue)
		{
			_pendingHoldStart = playback;
			double mapped = PersonalSyncMapper.MapPlaybackToLyrics(playback, _profile);
			int active = FindActiveLine(mapped);
			_pendingHoldLyricsTime = active >= 0 && active < _lines.Count ? _lines[active].Time.TotalSeconds : mapped;
			EmitPendingHoldPreview();
			RefreshLiveUi();
			return;
		}

		if (!ValidSelectedLine(out int selected)) return;
		double start = _pendingHoldStart.Value;
		if (playback <= start + 0.05) return;
		double heldLyrics = _pendingHoldLyricsTime;
		double resumeLyrics = Math.Max(0, _lines[selected].Time.TotalSeconds);
		_pendingHoldStart = null;
		Guid holdId = Guid.NewGuid();
		Guid resumeAnchorId = Guid.NewGuid();
		Change(profile =>
		{
			profile.Mode = PersonalSyncMode.Advanced;
			PersonalSyncAnchor? nearby = profile.Anchors.FirstOrDefault(anchor => Math.Abs(anchor.PlaybackSeconds - playback) < 0.12);
			if (nearby == null) profile.Anchors.Add(new PersonalSyncAnchor { Id = resumeAnchorId, PlaybackSeconds = playback, LyricsSeconds = resumeLyrics });
			else { resumeAnchorId = nearby.Id; nearby.PlaybackSeconds = playback; nearby.LyricsSeconds = resumeLyrics; }
			profile.Segments.Add(new PersonalSyncSegment
			{
				Id = holdId, ResumeAnchorId = resumeAnchorId, Type = PersonalSyncSegmentType.Hold,
				PlaybackStartSeconds = start, PlaybackEndSeconds = playback, LyricsTimeSeconds = heldLyrics
			});
			profile.Segments = profile.Segments.OrderBy(segment => segment.PlaybackStartSeconds).ToList();
			profile.Anchors = profile.Anchors.OrderBy(anchor => anchor.PlaybackSeconds).ToList();
		});
		SelectPoint(holdId);
	}

	private void CancelPendingHold()
	{
		if (!_pendingHoldStart.HasValue) return;
		_pendingHoldStart = null;
		PreviewChanged?.Invoke(this, _dirty || _hadStoredProfile ? _profile.Clone() : null);
		RefreshLiveUi();
	}

	private void EmitPendingHoldPreview()
	{
		if (!_pendingHoldStart.HasValue) return;
		PersonalSyncProfile preview = _profile.Clone();
		preview.Mode = PersonalSyncMode.Advanced;
		preview.Segments.Add(new PersonalSyncSegment
		{
			Type = PersonalSyncSegmentType.Hold,
			PlaybackStartSeconds = _pendingHoldStart.Value,
			PlaybackEndSeconds = Math.Max(_context.Track.DurationSeconds + 60, _pendingHoldStart.Value + 86400),
			LyricsTimeSeconds = _pendingHoldLyricsTime
		});
		PreviewChanged?.Invoke(this, preview);
	}

	private void LyricsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_refreshing) return;
		_selectedLineIndex = _lyricsList.SelectedItem is ListBoxItem { Tag: int index } ? index : -1;
		RefreshSelectionText();
		RefreshPointEditor();
		RefreshPendingWorkflow();
	}

	private TimeEditorRow CreateTimeEditorRow(StackPanel parent, TimeField field)
	{
		Border surface = new()
		{
			Background = Brush(35, 32, 36), BorderBrush = Brush(66, 61, 67), BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(7), Padding = new Thickness(9), Margin = new Thickness(0, 7, 0, 0), Visibility = Visibility.Collapsed
		};
		StackPanel stack = new();
		surface.Child = stack;
		DockPanel heading = new();
		TextBlock value = new()
		{
			FontFamily = new FontFamily("Consolas"), FontSize = 16, FontWeight = FontWeights.Bold,
			Foreground = Accent(), HorizontalAlignment = HorizontalAlignment.Right
		};
		DockPanel.SetDock(value, Dock.Right);
		heading.Children.Add(value);
		TextBlock label = new() { Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
		heading.Children.Add(label);
		stack.Children.Add(heading);
		stack.Children.Add(CreateNudgeButtons(delta => AdjustSelectedPoint(field, delta)));
		Button setButton = Button(string.Empty);
		setButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		setButton.Click += delegate { SetSelectedPointField(field); };
		stack.Children.Add(setButton);
		parent.Children.Add(surface);
		return new TimeEditorRow(surface, label, value, setButton);
	}

	private void RefreshPointEditor()
	{
		bool has = _pointsList.SelectedItem is SyncPointListItem;
		_deletePointButton.IsEnabled = has;
		if (_pointsList.SelectedItem is not SyncPointListItem item)
		{
			_pointTypeText.Text = L("変更点を選ぶと、ボタンだけで時刻を直せます", "Select a change, then adjust it with buttons only");
			SetEditorRow(_pointA, false, string.Empty, 0, string.Empty, false);
			SetEditorRow(_pointB, false, string.Empty, 0, string.Empty, false);
			SetEditorRow(_pointLyrics, false, string.Empty, 0, string.Empty, false);
			return;
		}

		if (item.IsAnchor)
		{
			PersonalSyncAnchor? anchor = _profile.Anchors.FirstOrDefault(candidate => candidate.Id == item.Id);
			if (anchor == null) return;
			_pointTypeText.Text = L("切替点：この再生位置から、指定した歌詞位置へ切り替えます", "Switch point: jump to the chosen lyric position at this playback time");
			SetEditorRow(_pointA, true, L("切替を始める再生位置", "Playback time where the switch begins"), anchor.PlaybackSeconds,
				L("現在の再生位置に合わせる", "Use current playback position"), true);
			SetEditorRow(_pointB, true, L("切替先の歌詞位置", "Lyric position to switch to"), anchor.LyricsSeconds,
				L("右で選んだ歌詞に合わせる", "Use selected lyric on the right"), ValidSelectedLine(out _));
			SetEditorRow(_pointLyrics, false, string.Empty, 0, string.Empty, false);
		}
		else
		{
			PersonalSyncSegment? hold = _profile.Segments.FirstOrDefault(candidate => candidate.Id == item.Id);
			if (hold == null) return;
			_pointTypeText.Text = L("停止区間：開始から再開まで同じ歌詞を表示します", "Hold range: keep one lyric displayed until the resume point");
			SetEditorRow(_pointA, true, L("停止を始める再生位置", "Playback time where the hold begins"), hold.PlaybackStartSeconds,
				L("現在の再生位置に合わせる", "Use current playback position"), true);
			SetEditorRow(_pointB, true, L("選択歌詞から再開する再生位置", "Playback time where the selected lyric resumes"), hold.PlaybackEndSeconds,
				L("現在の再生位置に合わせる", "Use current playback position"), true);
			SetEditorRow(_pointLyrics, true, L("停止中に表示する歌詞位置", "Lyric position shown while held"), hold.LyricsTimeSeconds,
				L("右で選んだ歌詞に合わせる", "Use selected lyric on the right"), ValidSelectedLine(out _));
		}
	}

	private static void SetEditorRow(TimeEditorRow row, bool visible, string label, double seconds, string action, bool enabled)
	{
		row.Surface.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
		if (!visible) return;
		row.Label.Text = label;
		row.Value.Text = Format(seconds);
		row.SetButton.Content = action;
		row.SetButton.IsEnabled = enabled;
	}

	private void AdjustSelectedPoint(TimeField field, double delta)
	{
		if (_pointsList.SelectedItem is not SyncPointListItem item) return;
		Change(profile =>
		{
			if (item.IsAnchor)
			{
				PersonalSyncAnchor? anchor = profile.Anchors.FirstOrDefault(candidate => candidate.Id == item.Id);
				if (anchor == null) return;
				if (field == TimeField.A) anchor.PlaybackSeconds = Round(Math.Max(0, anchor.PlaybackSeconds + delta));
				else if (field == TimeField.B) anchor.LyricsSeconds = Round(Math.Max(0, anchor.LyricsSeconds + delta));
				profile.Anchors = profile.Anchors.OrderBy(candidate => candidate.PlaybackSeconds).ToList();
			}
			else
			{
				PersonalSyncSegment? hold = profile.Segments.FirstOrDefault(candidate => candidate.Id == item.Id);
				if (hold == null) return;
				if (field == TimeField.A) hold.PlaybackStartSeconds = Round(Math.Clamp(hold.PlaybackStartSeconds + delta, 0, Math.Max(0, hold.PlaybackEndSeconds - 0.05)));
				else if (field == TimeField.B)
				{
					double previousEnd = hold.PlaybackEndSeconds;
					hold.PlaybackEndSeconds = Round(Math.Max(hold.PlaybackStartSeconds + 0.05, hold.PlaybackEndSeconds + delta));
					PersonalSyncAnchor? resume = FindResumeAnchor(profile, hold, previousEnd);
					if (resume != null) resume.PlaybackSeconds = hold.PlaybackEndSeconds;
				}
				else hold.LyricsTimeSeconds = Round(Math.Max(0, hold.LyricsTimeSeconds + delta));
				profile.Segments = profile.Segments.OrderBy(candidate => candidate.PlaybackStartSeconds).ToList();
			}
		});
		SelectPoint(item.Id);
	}

	private void SetSelectedPointField(TimeField field)
	{
		if (_pointsList.SelectedItem is not SyncPointListItem item) return;
		double now = Math.Max(0, _playbackPositionProvider().TotalSeconds);
		bool hasLyric = ValidSelectedLine(out int selected);
		double lyric = hasLyric ? _lines[selected].Time.TotalSeconds : 0;
		Change(profile =>
		{
			if (item.IsAnchor)
			{
				PersonalSyncAnchor? anchor = profile.Anchors.FirstOrDefault(candidate => candidate.Id == item.Id);
				if (anchor == null) return;
				if (field == TimeField.A) anchor.PlaybackSeconds = now;
				else if (field == TimeField.B && hasLyric) anchor.LyricsSeconds = lyric;
				profile.Anchors = profile.Anchors.OrderBy(candidate => candidate.PlaybackSeconds).ToList();
			}
			else
			{
				PersonalSyncSegment? hold = profile.Segments.FirstOrDefault(candidate => candidate.Id == item.Id);
				if (hold == null) return;
				if (field == TimeField.A) hold.PlaybackStartSeconds = Math.Max(0, Math.Min(now, hold.PlaybackEndSeconds - 0.05));
				else if (field == TimeField.B)
				{
					double previousEnd = hold.PlaybackEndSeconds;
					hold.PlaybackEndSeconds = Math.Max(hold.PlaybackStartSeconds + 0.05, now);
					PersonalSyncAnchor? resume = FindResumeAnchor(profile, hold, previousEnd);
					if (resume != null) resume.PlaybackSeconds = hold.PlaybackEndSeconds;
				}
				else if (hasLyric) hold.LyricsTimeSeconds = lyric;
				profile.Segments = profile.Segments.OrderBy(candidate => candidate.PlaybackStartSeconds).ToList();
			}
		});
		SelectPoint(item.Id);
	}

	private void DeleteSelectedPoint_Click(object sender, RoutedEventArgs e)
	{
		if (_pointsList.SelectedItem is not SyncPointListItem item) return;
		Change(profile =>
		{
			if (item.IsAnchor) profile.Anchors.RemoveAll(anchor => anchor.Id == item.Id);
			else
			{
				PersonalSyncSegment? hold = profile.Segments.FirstOrDefault(segment => segment.Id == item.Id);
				if (hold != null)
				{
					PersonalSyncAnchor? resume = FindResumeAnchor(profile, hold, hold.PlaybackEndSeconds);
					if (resume != null) profile.Anchors.Remove(resume);
				}
				profile.Segments.RemoveAll(segment => segment.Id == item.Id);
			}
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
		CancelPendingHold();
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
		if (_pendingHoldStart.HasValue)
		{
			_pendingHoldStart = null;
			PreviewChanged?.Invoke(this, _dirty || _hadStoredProfile ? _profile.Clone() : null);
		}
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
		double lyrics = _pendingHoldStart.HasValue ? _pendingHoldLyricsTime : PersonalSyncMapper.MapPlaybackToLyrics(playback, _profile);
		int active = FindActiveLine(lyrics);
		_nowText.Text = L("再生", "PLAY") + "  " + Format(playback) + "\n" + L("歌詞", "LYRIC") + "  " + Format(lyrics);
		_matchButton.Content = ValidSelectedLine(out _)
			? L($"選択した歌詞を現在の {Format(playback)} に設定", $"Set selected lyric to current {Format(playback)}")
			: L("右の歌詞を選択してください", "Select a lyric on the right");
		_matchButton.IsEnabled = _selectedLineIndex >= 0 && !_pendingHoldStart.HasValue;
		RefreshPendingWorkflow();
		if (active != _activeLineIndex)
		{
			_activeLineIndex = active;
			RefreshLyricRowVisuals();
			if (_followNowBox.IsChecked == true && active >= 0 && active < _lyricsList.Items.Count) _lyricsList.ScrollIntoView(_lyricsList.Items[active]);
		}
		DrawTimeline();
	}

	private void RefreshPendingWorkflow()
	{
		bool pending = _pendingHoldStart.HasValue;
		_cancelHoldButton.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
		if (!pending)
		{
			_holdButton.Content = L("ここから歌詞を止める", "Hold lyrics from here");
			_holdButton.IsEnabled = true;
			_workflowHint.Text = L("止めるときに押し、再開したい歌詞を右で選び、その歌詞が聞こえた瞬間にもう一度押します。", "Press to hold, select the resume lyric on the right, then press again the instant you hear it.");
			_workflowHint.Foreground = Muted();
			return;
		}
		_holdButton.Content = ValidSelectedLine(out int selected)
			? L($"「{ShortLyric(_lines[selected].Text)}」から今すぐ再開", $"Resume now from “{ShortLyric(_lines[selected].Text)}”")
			: L("右で再開する歌詞を選択", "Select the resume lyric on the right");
		_holdButton.IsEnabled = ValidSelectedLine(out _);
		_workflowHint.Text = L(
			$"歌詞を {Format(_pendingHoldStart!.Value)} から固定中。再開する行を選び、聞こえた瞬間にオレンジのボタンを押してください。",
			$"Lyrics are held from {Format(_pendingHoldStart!.Value)}. Select the resume line and press the orange button when you hear it.");
		_workflowHint.Foreground = Accent();
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
		foreach (PersonalSyncSegment segment in _profile.Segments.OrderBy(item => item.PlaybackStartSeconds))
		{
			string resume = _profile.Anchors.Where(anchor => Math.Abs(anchor.PlaybackSeconds - segment.PlaybackEndSeconds) < 0.15)
				.Select(anchor => NearestLyricText(anchor.LyricsSeconds)).FirstOrDefault() ?? L("自動継続", "automatic continuation");
			SyncPointListItem item = new(segment.Id, false,
				$"▰  {L("停止", "HOLD")}  {Format(segment.PlaybackStartSeconds)} — {Format(segment.PlaybackEndSeconds)}\n    {L("再開", "RESUME")}  {resume}");
			_pointsList.Items.Add(item);
			if (selectedId == segment.Id) _pointsList.SelectedItem = item;
		}
		foreach (PersonalSyncAnchor anchor in _profile.Anchors.OrderBy(item => item.PlaybackSeconds))
		{
			double offset = anchor.PlaybackSeconds - anchor.LyricsSeconds;
			SyncPointListItem item = new(anchor.Id, true,
				$"●  {L("切替", "SWITCH")}  {Format(anchor.PlaybackSeconds)} → {Format(anchor.LyricsSeconds)}  ({offset:+0.0;-0.0;0.0}s)\n    {NearestLyricText(anchor.LyricsSeconds)}");
			_pointsList.Items.Add(item);
			if (selectedId == anchor.Id) _pointsList.SelectedItem = item;
		}
	}

	private void SelectPoint(Guid id)
	{
		foreach (object candidate in _pointsList.Items)
		{
			if (candidate is SyncPointListItem item && item.Id == id)
			{
				_pointsList.SelectedItem = item;
				_pointsList.ScrollIntoView(item);
				break;
			}
		}
	}

	private string NearestLyricText(double seconds)
	{
		if (_lines.Count == 0) return string.Empty;
		int index = Math.Clamp(FindActiveLine(seconds), 0, _lines.Count - 1);
		return _lines[index].Text;
	}

	private static PersonalSyncAnchor? FindResumeAnchor(PersonalSyncProfile profile, PersonalSyncSegment hold, double fallbackEnd)
	{
		return hold.ResumeAnchorId.HasValue
			? profile.Anchors.FirstOrDefault(anchor => anchor.Id == hold.ResumeAnchorId.Value)
			: profile.Anchors.FirstOrDefault(anchor => Math.Abs(anchor.PlaybackSeconds - fallbackEnd) < 0.15);
	}

	private void DrawTimeline()
	{
		if (_timeline.ActualWidth <= 1 || _timeline.ActualHeight <= 1) return;
		_timeline.Children.Clear();
		double width = _timeline.ActualWidth;
		double height = _timeline.ActualHeight;
		double duration = Math.Max(1.0, _context.Track.DurationSeconds);
		double axisX = 38, top = 22, bottom = height - 22;
		_timeline.Children.Add(new Line { X1 = axisX, X2 = axisX, Y1 = top, Y2 = bottom, Stroke = Brush(100, 95, 102), StrokeThickness = 3 });
		AddTimelineText("0:00", 4, 3, Muted(), 9);
		AddTimelineText(Format(duration), 4, height - 18, Muted(), 9);

		double labelY = 10;
		foreach (TimelineEvent item in TimelineEvents().OrderBy(item => item.PlaybackSeconds))
		{
			double markerY = Y(item.PlaybackSeconds, duration, top, bottom);
			double desired = Math.Clamp(markerY - 18, 8, Math.Max(8, height - 42));
			labelY = Math.Min(Math.Max(desired, labelY), Math.Max(8, height - 42));
			_timeline.Children.Add(new Line { X1 = axisX + 7, X2 = 67, Y1 = markerY, Y2 = labelY + 16, Stroke = item.IsHold ? Accent() : Brush(166, 158, 166), StrokeThickness = 1, Opacity = .7 });
			if (item.IsHold)
			{
				double endY = Y(item.EndSeconds, duration, top, bottom);
				Rectangle range = new() { Width = 16, Height = Math.Max(4, endY - markerY), Fill = Accent(), Opacity = .42, RadiusX = 5, RadiusY = 5 };
				Canvas.SetLeft(range, axisX - 8);
				Canvas.SetTop(range, markerY);
				_timeline.Children.Add(range);
			}
			else
			{
				Ellipse point = new() { Width = 13, Height = 13, Fill = Accent(), Stroke = Brushes.White, StrokeThickness = 1 };
				Canvas.SetLeft(point, axisX - 6.5);
				Canvas.SetTop(point, markerY - 6.5);
				_timeline.Children.Add(point);
			}
			AddTimelineText(item.Label, 72, labelY, Brushes.White, 10, Math.Max(80, width - 82));
			labelY += 42;
		}

		double now = Math.Clamp(_playbackPositionProvider().TotalSeconds, 0, duration);
		double nowY = Y(now, duration, top, bottom);
		_timeline.Children.Add(new Line { X1 = 13, X2 = width - 9, Y1 = nowY, Y2 = nowY, Stroke = Brushes.White, StrokeThickness = 1.2 });
		TextBlock nowLabel = AddTimelineText(L("現在 ", "NOW ") + Format(now), Math.Max(65, width - 105), Math.Clamp(nowY - 16, 1, height - 18), Brushes.White, 9);
		nowLabel.Background = Brush(31, 29, 32);
		if (_pendingHoldStart.HasValue)
		{
			double startY = Y(_pendingHoldStart.Value, duration, top, bottom);
			Rectangle pending = new() { Width = 20, Height = Math.Max(3, nowY - startY), Fill = Accent(), Opacity = .62, RadiusX = 6, RadiusY = 6 };
			Canvas.SetLeft(pending, axisX - 10);
			Canvas.SetTop(pending, startY);
			_timeline.Children.Add(pending);
		}
	}

	private IEnumerable<TimelineEvent> TimelineEvents()
	{
		foreach (PersonalSyncSegment hold in _profile.Segments)
		{
			PersonalSyncAnchor? resume = _profile.Anchors.FirstOrDefault(anchor => Math.Abs(anchor.PlaybackSeconds - hold.PlaybackEndSeconds) < 0.15);
			string resumeText = resume == null ? L("そのまま再開", "resume continuously") : ShortLyric(NearestLyricText(resume.LyricsSeconds));
			yield return new TimelineEvent(hold.PlaybackStartSeconds, hold.PlaybackEndSeconds, true,
				$"{L("歌詞停止", "HOLD")} {Format(hold.PlaybackStartSeconds)} → {Format(hold.PlaybackEndSeconds)}\n{L("再開", "RESUME")}: {resumeText}");
		}
		foreach (PersonalSyncAnchor anchor in _profile.Anchors)
			yield return new TimelineEvent(anchor.PlaybackSeconds, anchor.PlaybackSeconds, false,
				$"{L("切替", "SWITCH")} {Format(anchor.PlaybackSeconds)} → {Format(anchor.LyricsSeconds)}\n{ShortLyric(NearestLyricText(anchor.LyricsSeconds))}");
	}

	private TextBlock AddTimelineText(string text, double x, double y, Brush foreground, double size, double maxWidth = double.PositiveInfinity)
	{
		TextBlock label = new()
		{
			Text = text, Foreground = foreground, FontFamily = new FontFamily("Consolas"), FontSize = size,
			TextWrapping = TextWrapping.Wrap, MaxWidth = maxWidth
		};
		Canvas.SetLeft(label, x);
		Canvas.SetTop(label, y);
		_timeline.Children.Add(label);
		return label;
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

	private static Grid CreateNudgeButtons(Action<double> action)
	{
		Grid grid = new() { Margin = new Thickness(-3, 4, -3, 2) };
		double[] amounts = { -0.5, -0.1, 0.1, 0.5 };
		for (int i = 0; i < amounts.Length; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		for (int i = 0; i < amounts.Length; i++)
		{
			double delta = amounts[i];
			Button button = Button(delta.ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + "s");
			button.Click += delegate { action(delta); };
			Grid.SetColumn(button, i);
			grid.Children.Add(button);
		}
		return grid;
	}

	private static TextBlock SectionTitle(string text) => new() { Text = text, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4) };
	private static StackPanel CardContent() => new() { Margin = new Thickness(14) };
	private static Border Card() => new() { Background = Brush(36, 33, 37), BorderBrush = Brush(69, 64, 70), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 0, 11) };
	private static Button Button(string text) => new() { Content = text };
	private static Button PrimaryButton(string text) => new() { Content = text, Background = Accent(), Foreground = Brush(28, 25, 28), BorderBrush = Accent(), FontWeight = FontWeights.SemiBold };
	private static double Y(double seconds, double duration, double top, double bottom) => top + Math.Clamp(seconds / duration, 0, 1) * Math.Max(1, bottom - top);
	private static double Round(double value) => Math.Round(Math.Clamp(value, -3600.0, 3600.0), 3);
	private static string Format(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f", CultureInfo.InvariantCulture);
	private static string ShortLyric(string text) => text.Length <= 28 ? text : text[..27] + "…";
	private string L(string ja, string en) => _language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? ja : en;
	private string SourceContext(PersonalSyncSourceIdentity source)
	{
		string label = string.IsNullOrWhiteSpace(source.ContextLabel) ? source.Source : source.ContextLabel;
		return source.ProviderInferred ? label + L("（推定）", " (inferred)") : label;
	}
	private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
	private static SolidColorBrush Accent() => Brush(255, 107, 44);
	private static SolidColorBrush Muted() => Brush(181, 176, 181);

	private enum TimeField { A, B, Lyrics }
	private sealed record LyricRow(TextBlock Marker, TextBlock Time, TextBlock Lyric);
	private sealed record TimeEditorRow(Border Surface, TextBlock Label, TextBlock Value, Button SetButton);
	private sealed record TimelineEvent(double PlaybackSeconds, double EndSeconds, bool IsHold, string Label);
	private sealed record SyncPointListItem(Guid Id, bool IsAnchor, string Label) { public override string ToString() => Label; }
}
