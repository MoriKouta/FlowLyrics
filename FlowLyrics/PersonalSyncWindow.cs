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
	private bool _saveOnClosePending;
	private bool _closeAfterSave;
	private bool _refreshing;
	private bool _timelineCoordinatesDirty = true;
	private int _selectedLineIndex = -1;
	private int _activeLineIndex = -1;
	private double? _pendingHoldStart;
	private double _pendingHoldLyricsTime;

	private readonly TextBlock _offsetText;
	private readonly Grid _offsetNudges;
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
	private readonly CheckBox _wholeTrackBox;
	private readonly ListBox _lyricsList;
	private readonly Border _actionCard;
	private readonly TextBlock _currentMarker;
	private readonly StackPanel _currentMarkerHost;
	private readonly Expander _advancedActions;
	private readonly ListBox _pointsList;
	private readonly TextBlock _pointTypeText;
	private readonly TimeEditorRow _pointA;
	private readonly TimeEditorRow _pointB;
	private readonly TimeEditorRow _pointLyrics;
	private readonly Button _deletePointButton;

	public event EventHandler<PersonalSyncProfile?>? PreviewChanged;
	public event EventHandler<TimeSpan>? SeekRequested;
	private Point _dragStart;
	private int _dragLine = -1;
	private bool _dragging;
	private readonly string _dragToken = Guid.NewGuid().ToString("N");

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
		// A fixed drop target remains available even when the active lyric is offscreen.
		_nowText.AllowDrop = true;
		_nowText.ToolTip = T("Drag to the current position to align");
		_nowText.DragOver += (_, e) => { e.Effects = IsOwnLyricDrag(e) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
		_nowText.Drop += (_, e) => { if (IsOwnLyricDrag(e)) AlignDraggedLyric(_dragLine, _playbackPositionProvider().TotalSeconds); e.Handled = true; };
		Grid.SetColumn(_nowText, 1);
		header.Children.Add(_nowText);
		root.Children.Add(header);

		Grid body = new();
		body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		Grid.SetRow(body, 1);
		root.Children.Add(body);

		DockPanel offset = new() { Margin = new Thickness(0, 0, 0, 10) };
		StackPanel offsetHeading = new() { Width = 200 };
		offsetHeading.Children.Add(SectionTitle(T("Global offset")));
		_offsetText = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 20, Foreground = Foreground };
		offsetHeading.Children.Add(_offsetText);
		offset.Children.Add(offsetHeading);
		_offsetNudges = CreateNudgeButtons(Nudge);
		_offsetNudges.MaxWidth = 420;
		_offsetNudges.HorizontalAlignment = HorizontalAlignment.Left;
		offset.Children.Add(_offsetNudges);
		body.Children.Add(offset);

		StackPanel timelinePanel = new();
		Expander pointDetails = new() { Header = T("Adjustment points"), Content = new ScrollViewer { Content = timelinePanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 215 }, MaxHeight = 245 };
		Grid.SetRow(pointDetails, 2);
		body.Children.Add(pointDetails);

		_pointsList = new ListBox { Name = "TimingChangesList", MinHeight = 60, MaxHeight = 150, Margin = new Thickness(0, 10, 0, 6) };
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
			Text = L("上の補正点を選んで調整します", "Select a change above to adjust its timing"),
			FontWeight = FontWeights.SemiBold, Foreground = Muted(), TextWrapping = TextWrapping.Wrap
		};
		editPanel.Children.Add(_pointTypeText);
		_pointA = CreateTimeEditorRow(editPanel, TimeField.A);
		_pointB = CreateTimeEditorRow(editPanel, TimeField.B);
		_pointLyrics = CreateTimeEditorRow(editPanel, TimeField.Lyrics);
		_deletePointButton = DangerButton(L("選択した補正を削除", "Delete selected change"));
		_deletePointButton.HorizontalAlignment = HorizontalAlignment.Left;
		_deletePointButton.Click += DeleteSelectedPoint_Click;
		editPanel.Children.Add(_deletePointButton);

		Border lyricsCard = Card();
		lyricsCard.Name = "LyricsEditingCard";
		Grid lyricsPanel = new() { Margin = new Thickness(12) };
		lyricsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		lyricsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		lyricsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		lyricsCard.Child = lyricsPanel;
		Grid.SetRow(lyricsCard, 1);
		body.Children.Add(lyricsCard);
		DockPanel lyricsHeader = new() { Margin = new Thickness(2, 0, 2, 8) };
		_followNowBox = new CheckBox { Content = L("再生中の行を追う", "Follow current line"), IsChecked = true };
		DockPanel.SetDock(_followNowBox, Dock.Right);
		lyricsHeader.Children.Add(_followNowBox);
		lyricsHeader.Children.Add(SectionTitle(T("Timing editor")));
		lyricsPanel.Children.Add(lyricsHeader);
		_lyricsList = new ListBox { Name = "SyncLyricsList", BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
		_lyricsList.SelectionChanged += LyricsList_SelectionChanged;
		_lyricsList.PreviewMouseWheel += delegate { _followNowBox.IsChecked = false; };
		Grid.SetRow(_lyricsList, 1);
		lyricsPanel.Children.Add(_lyricsList);
		// One action surface travels with the selected lyric, rather than living
		// in a disconnected panel. The time axis is part of each lyric row.
		_actionCard = new Border { Name = "LyricActionPanel", Padding = new Thickness(0, 6, 0, 4) };
		StackPanel actions = new();
		_actionCard.Child = actions;
		_currentMarkerHost = new StackPanel { AllowDrop = true, Background = Brushes.Transparent };
		_currentMarker = new TextBlock { Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 5, 0, 5) };
		_currentMarkerHost.Children.Add(_currentMarker);
		_currentMarkerHost.DragOver += (_, e) => { e.Effects = IsOwnLyricDrag(e) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
		_currentMarkerHost.Drop += (_, e) => { if (IsOwnLyricDrag(e)) AlignDraggedLyric(_dragLine, _playbackPositionProvider().TotalSeconds); e.Handled = true; };

		_selectionText = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxHeight = 48, TextTrimming = TextTrimming.CharacterEllipsis,
			FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
		// Selection is already visible in its lyric row.
		_matchButton = PrimaryButton(string.Empty);
		_matchButton.Name = "AlignSelectedLyricButton";
		_matchButton.HorizontalAlignment = HorizontalAlignment.Left;
		_matchButton.MinWidth = 200;
		_matchButton.Click += MatchSelectedLine_Click;
		actions.Children.Add(_matchButton);
		_wholeTrackBox = new CheckBox { Content = L("曲全体にも適用", "Apply to the whole track"), Margin = new Thickness(4, 7, 4, 7),
			ToolTip = L("OFF: この位置から先を補正。ON: 曲全体のずれを調整。", "Off: align from here onward. On: shift the whole track.") };
		_wholeTrackBox.Checked += delegate { RefreshLiveUi(); };
		_wholeTrackBox.Unchecked += delegate { RefreshLiveUi(); };
		StackPanel more = new();
		more.Children.Add(_wholeTrackBox);
		_holdButton = Button(T("Start lyric hold"));
		_holdButton.Name = "HoldLyricDisplayButton";
		_holdButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		_holdButton.Click += Hold_Click;
		more.Children.Add(_holdButton);
		_advancedActions = new Expander { Header = T("More"), Content = more, Margin = new Thickness(0, 4, 0, 0) };
		actions.Children.Add(_advancedActions);
		_advancedActions.Expanded += (_, _) => RevealRowAction();
		_workflowHint = new TextBlock { Foreground = Muted(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 6, 4, 2), FontSize = 12 };
		actions.Children.Add(_workflowHint);
		_cancelHoldButton = Button(L("一時停止を取り消す", "Cancel hold"));
		_cancelHoldButton.Visibility = Visibility.Collapsed;
		_cancelHoldButton.Click += delegate { CancelPendingHold(); };
		actions.Children.Add(_cancelHoldButton);

		StackPanel footer = new() { Margin = new Thickness(0, 14, 0, 0) };
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
		_undoButton = Button(L("元に戻す", "Undo"));
		_redoButton = Button(L("やり直す", "Redo"));
		Button reset = Button(L("調整をゼロに戻す", "Reset timing"));
		Button remove = DangerButton(L("保存済み調整を削除", "Delete saved sync"));
		Button close = Button(T("Close"));
		close.ToolTip = T("Changes are saved when you close.");
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

		InputBindings.Add(new KeyBinding(ApplicationCommands.Undo, new KeyGesture(Key.Z, ModifierKeys.Control)));
		InputBindings.Add(new KeyBinding(ApplicationCommands.Redo, new KeyGesture(Key.Y, ModifierKeys.Control)));
		CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (_, e) => { Undo(); e.Handled = true; }, (_, e) => e.CanExecute = _undo.Count > 0));
		CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (_, e) => { Redo(); e.Handled = true; }, (_, e) => e.CanExecute = _redo.Count > 0));
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
			int lineIndex = index;
			LyricLine line = _lines[index];
			StackPanel content = new();
			Grid row = new() { Margin = new Thickness(4, 5, 6, 5), MinHeight = 28 };
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			TextBlock marker = new() { Text = "○", Foreground = Muted(), FontSize = 14, VerticalAlignment = VerticalAlignment.Center,
				Cursor = Cursors.SizeAll, ToolTip = T("Drag to the current position to align") };
			marker.PreviewMouseLeftButtonDown += (_, e) => { _dragStart = e.GetPosition(this); _dragLine = lineIndex; };
			marker.PreviewMouseMove += (_, e) =>
			{
				if (_dragging || _pendingHoldStart.HasValue || e.LeftButton != MouseButtonState.Pressed || _dragLine != lineIndex) return;
				Point at = e.GetPosition(this);
				if (Math.Abs(at.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance && Math.Abs(at.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance) return;
				_dragging = true;
				try { DragDrop.DoDragDrop(marker, new DataObject("FlowLyrics.SyncLyric", _dragToken), DragDropEffects.Move); }
				finally { _dragging = false; _dragLine = -1; RefreshLiveUi(); }
			};
			TextBlock time = new() { FontFamily = new FontFamily("Consolas"), Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center,
				Cursor = Cursors.Hand, ToolTip = T("Seek to this lyric") };
			time.MouseLeftButtonUp += (_, e) => { SeekToLyric(lineIndex); e.Handled = true; };
			TextBlock lyric = new() { Text = line.Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
			Grid.SetColumn(time, 1); Grid.SetColumn(lyric, 2);
			row.Children.Add(new Border { Width = 1, Background = Brush(78, 74, 80), HorizontalAlignment = HorizontalAlignment.Center });
			row.Children.Add(marker); row.Children.Add(time); row.Children.Add(lyric);
			content.Children.Add(row);
			StackPanel inline = new() { Margin = new Thickness(116, 0, 6, 0) };
			content.Children.Add(inline);
			TextBlock holdRange = new() { Foreground = Brush(168, 205, 223), Margin = new Thickness(28, 0, 0, 5), Visibility = Visibility.Collapsed };
			content.Children.Add(holdRange);
			ListBoxItem item = new() { Content = content, Tag = index, Padding = new Thickness(5, 1, 5, 1), HorizontalContentAlignment = HorizontalAlignment.Stretch };
			_lyricsList.Items.Add(item);
			_lyricRows.Add(new LyricRow(marker, time, lyric, content, inline, holdRange));
		}
	}

	private bool IsOwnLyricDrag(DragEventArgs e) => !_pendingHoldStart.HasValue && Equals(e.Data.GetData("FlowLyrics.SyncLyric"), _dragToken) && _dragLine >= 0;

	private void AlignDraggedLyric(int index, double playback)
	{
		if (_pendingHoldStart.HasValue || index < 0 || index >= _lines.Count || !double.IsFinite(playback)) return;
		_selectedLineIndex = index;
		_lyricsList.SelectedIndex = index;
		if (_wholeTrackBox.IsChecked == true)
		{
			double delta = PersonalSyncMapper.MapPlaybackToLyrics(playback, _profile) - _lines[index].Time.TotalSeconds;
			Change(profile => ShiftWholeTrack(profile, delta));
			return;
		}
		AlignLineAt(index, Math.Max(0, playback));
	}

	private void AlignLineAt(int index, double playback)
	{
		Guid id = Guid.NewGuid();
		Change(profile =>
		{
			profile.Mode = PersonalSyncMode.Advanced;
			PersonalSyncAnchor? nearby = profile.Anchors.FirstOrDefault(a => Math.Abs(a.PlaybackSeconds - playback) < .12);
			if (nearby == null) profile.Anchors.Add(new PersonalSyncAnchor { Id = id, PlaybackSeconds = playback, LyricsSeconds = _lines[index].Time.TotalSeconds });
			else { id = nearby.Id; nearby.PlaybackSeconds = playback; nearby.LyricsSeconds = _lines[index].Time.TotalSeconds; }
			profile.Anchors = profile.Anchors.OrderBy(a => a.PlaybackSeconds).ToList();
		});
		SelectPoint(id);
	}

	private void SeekToLyric(int index)
	{
		if (index < 0 || index >= _lines.Count) return;
		double? playback = PersonalSyncTimeline.PlaybackForLyric(_lines[index].Time.TotalSeconds, _profile);
		if (playback.HasValue) SeekRequested?.Invoke(this, TimeSpan.FromSeconds(playback.Value));
	}

	private void Nudge(double delta) => Change(profile => ShiftWholeTrack(profile, delta));

	private static void ShiftWholeTrack(PersonalSyncProfile profile, double delta)
	{
		if (profile.Mode == PersonalSyncMode.None) profile.Mode = PersonalSyncMode.Offset;
		profile.OffsetSeconds = Round(profile.OffsetSeconds + delta);
		foreach (PersonalSyncAnchor anchor in profile.Anchors) anchor.LyricsSeconds = Math.Max(0, anchor.LyricsSeconds - delta);
		foreach (PersonalSyncSegment hold in profile.Segments) hold.LyricsTimeSeconds = Math.Max(0, hold.LyricsTimeSeconds - delta);
	}

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
		if (_pendingHoldStart.HasValue) { Hold_Click(sender, e); return; }
		if (_wholeTrackBox.IsChecked != true) { AddAnchor_Click(sender, e); return; }
		if (!ValidSelectedLine(out int selected)) return;
		double delta = PersonalSyncMapper.MapPlaybackToLyrics(_playbackPositionProvider(), _profile).TotalSeconds - _lines[selected].Time.TotalSeconds;
		Change(profile => ShiftWholeTrack(profile, delta));
	}

	private void AddAnchor_Click(object sender, RoutedEventArgs e)
	{
		if (!ValidSelectedLine(out int selected)) return;
		AlignLineAt(selected, Math.Max(0, _playbackPositionProvider().TotalSeconds));
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
			RefreshLyricRowVisuals();
			RefreshLiveUi();
			RevealRowAction();
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
		RefreshLyricRowVisuals();
		SelectPoint(holdId);
	}

	private void CancelPendingHold()
	{
		if (!_pendingHoldStart.HasValue) return;
		_pendingHoldStart = null;
		RefreshLyricRowVisuals();
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
		if (_activeLineIndex >= 0) _followNowBox.IsChecked = false;
		RefreshLyricRowVisuals();
		DrawTimeline();
		RefreshSelectionText();
		RefreshPointEditor();
		RefreshPendingWorkflow();
		RevealRowAction();
	}

	private void RevealRowAction() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { if (IsVisible && _actionCard.IsVisible) _actionCard.BringIntoView(); }));

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
			_pointTypeText.Text = L("上の補正点を選んで調整します", "Select a change above to adjust its timing");
			SetEditorRow(_pointA, false, string.Empty, 0, string.Empty, false);
			SetEditorRow(_pointB, false, string.Empty, 0, string.Empty, false);
			SetEditorRow(_pointLyrics, false, string.Empty, 0, string.Empty, false);
			return;
		}

		if (item.IsAnchor)
		{
			PersonalSyncAnchor? anchor = _profile.Anchors.FirstOrDefault(candidate => candidate.Id == item.Id);
			if (anchor == null) return;
			_pointTypeText.Text = L("補正点：ここから先の歌詞を合わせます", "Alignment point: timing changes from here onward");
			SetEditorRow(_pointA, true, L("切替を始める再生位置", "Playback time where the switch begins"), anchor.PlaybackSeconds,
				L("現在の再生位置に合わせる", "Use current playback position"), true);
			SetEditorRow(_pointB, true, L("切替先の歌詞位置", "Lyric position to switch to"), anchor.LyricsSeconds,
				T("Use selected lyric"), ValidSelectedLine(out _));
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
				T("Use selected lyric"), ValidSelectedLine(out _));
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
		if (_closeAfterSave) return;
		_previewTimer.Stop();
		if (_pendingHoldStart.HasValue)
		{
			_pendingHoldStart = null;
			PreviewChanged?.Invoke(this, _dirty || _hadStoredProfile ? _profile.Clone() : null);
		}
		if (!_dirty || _deleted) return;
		// Cancel synchronously: setting Cancel after an awaited write is too late.
		e.Cancel = true;
		if (_saveOnClosePending) return;
		_saveOnClosePending = true;
		try
		{
			_profile = await _store.UpsertAsync(_profile);
			_dirty = false;
			PreviewChanged?.Invoke(this, _profile.Mode == PersonalSyncMode.None ? null : _profile.Clone());
			_closeAfterSave = true;
			_ = Dispatcher.BeginInvoke(new Action(Close));
		}
		catch (Exception ex)
		{
			e.Cancel = true;
			_previewTimer.Start();
			MessageBox.Show(this, L("歌詞タイミング調整を保存できませんでした。\n\n", "Could not save Personal Sync.\n\n") + ex.Message,
				"FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		finally { _saveOnClosePending = false; }
	}

	private void RefreshAll()
	{
		_timelineCoordinatesDirty = true;
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
		if (_lines.Count == 0) { RefreshPendingWorkflow(); return; }
		double playback = Math.Max(0, _playbackPositionProvider().TotalSeconds);
		double lyrics = _pendingHoldStart.HasValue ? _pendingHoldLyricsTime : PersonalSyncMapper.MapPlaybackToLyrics(playback, _profile);
		int active = FindActiveLine(lyrics);
		_nowText.Text = (_dragging ? T("Align to now") : L("再生", "PLAY")) + "  " + Format(playback) + "\n" + L("歌詞", "LYRIC") + "  " + Format(lyrics);

		if (active != _activeLineIndex)
		{
			_activeLineIndex = active;
			RefreshLyricRowVisuals();
			if (_followNowBox.IsChecked == true && active >= 0 && active < _lyricsList.Items.Count) _lyricsList.ScrollIntoView(_lyricsList.Items[active]);
		}
		RefreshPendingWorkflow();
		DrawTimeline();
	}

	private void RefreshPendingWorkflow()
	{
		bool pending = _pendingHoldStart.HasValue;
		bool selected = ValidSelectedLine(out _);
		_offsetNudges.IsEnabled = !pending;
		_cancelHoldButton.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
		_holdButton.Visibility = pending ? Visibility.Collapsed : Visibility.Visible;
		_advancedActions.Visibility = pending ? Visibility.Collapsed : Visibility.Visible;
		_holdButton.IsEnabled = _activeLineIndex >= 0;
		_wholeTrackBox.Visibility = pending ? Visibility.Collapsed : Visibility.Visible;
		_matchButton.Content = pending ? T("Resume here") : T("Align to now");
		_matchButton.IsEnabled = selected && (!pending || _playbackPositionProvider().TotalSeconds > _pendingHoldStart!.Value + .05);
		_workflowHint.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
		_workflowHint.Text = T("Hold in progress. Choose the lyric to resume.");
		_workflowHint.Foreground = pending ? Brush(168, 205, 223) : Muted();
	}

	private void RefreshLyricRowVisuals()
	{
		for (int index = 0; index < _lyricRows.Count; index++)
		{
			LyricRow row = _lyricRows[index];
			bool active = index == _activeLineIndex;
			double lyric = _lines[index].Time.TotalSeconds;
			bool held = (active && _pendingHoldStart.HasValue) || _profile.Segments.Any(h => Math.Abs(h.LyricsTimeSeconds - lyric) < .05);
			row.Marker.Text = held ? "Ⅱ" : active ? "●" : _profile.Anchors.Any(a => Math.Abs(a.LyricsSeconds - lyric) < .05) ? "◆" : "○";
			row.Time.Foreground = active ? Accent() : Muted();
			row.Lyric.Foreground = active ? Brushes.White : Brush(210, 206, 210);
			row.Lyric.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
		}
	}

	private void RefreshSelectionText()
	{
		if (ValidSelectedLine(out int selected))
		{
			_selectionText.Text = L("選択中  ", "SELECTED  ") + "[" + Format(_lines[selected].Time.TotalSeconds) + "]  " + _lines[selected].Text;
			_selectionText.Foreground = Accent();
		}
		else
		{
			_selectionText.Text = T("Select a lyric");
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
				$"●  {L("補正", "ALIGN")}  {Format(anchor.PlaybackSeconds)} → {Format(anchor.LyricsSeconds)}  ({offset:+0.0;-0.0;0.0}s)\n    {NearestLyricText(anchor.LyricsSeconds)}");
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
		if (_lyricRows.Count == 0) return;
		if (ValidSelectedLine(out int selected) && _actionCard.Parent != _lyricRows[selected].Inline)
		{
			if (_actionCard.Parent is Panel previous) previous.Children.Remove(_actionCard);
			_lyricRows[selected].Inline.Children.Add(_actionCard);
		}
		int active = Math.Clamp(_activeLineIndex, 0, _lyricRows.Count - 1);
		if (_currentMarkerHost.Parent != _lyricRows[active].Content)
		{
			if (_currentMarkerHost.Parent is Panel old) old.Children.Remove(_currentMarkerHost);
			_lyricRows[active].Content.Children.Insert(0, _currentMarkerHost);
		}
		_currentMarker.Text = "────▶ " + T(_dragging ? "Align to now" : "Now") + " " + Format(_playbackPositionProvider().TotalSeconds) + " ────";
		// Playback ticks only move the marker. Recompute row coordinates after edits.
		if (!_timelineCoordinatesDirty) return;
		_timelineCoordinatesDirty = false;
		RefreshLyricRowVisuals();
		for (int i = 0; i < _lyricRows.Count; i++)
		{
			LyricRow row = _lyricRows[i];
			double lyric = _lines[i].Time.TotalSeconds;
			double? playback = PersonalSyncTimeline.PlaybackForLyric(lyric, _profile);
			row.Time.Text = playback.HasValue ? Format(playback.Value) : "—";
			bool hold = _profile.Segments.Any(h => Math.Abs(h.LyricsTimeSeconds - lyric) < .05);
			row.Content.Background = hold ? Brush(39, 52, 59) : Brushes.Transparent;
			row.HoldRange.Visibility = hold ? Visibility.Visible : Visibility.Collapsed;
			row.HoldRange.Text = hold ? string.Join("\n", _profile.Segments.Where(h => Math.Abs(h.LyricsTimeSeconds - lyric) < .05)
				.Select(h => T("Lyric hold") + " " + Format(h.PlaybackStartSeconds) + " — " + Format(h.PlaybackEndSeconds))) : string.Empty;
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
	private static Button DangerButton(string text) => new() { Content = text, Foreground = Brush(238, 158, 157) };
	private static double Y(double seconds, double duration, double top, double bottom) => top + Math.Clamp(seconds / duration, 0, 1) * Math.Max(1, bottom - top);
	private static double Round(double value) => Math.Round(Math.Clamp(value, -3600.0, 3600.0), 3);
	private static string Format(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f", CultureInfo.InvariantCulture);
	private static string ShortLyric(string text) => text.Length <= 28 ? text : text[..27] + "…";
	private string T(string key) => LocalizationService.Translate(_language, key);
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
	private sealed record LyricRow(TextBlock Marker, TextBlock Time, TextBlock Lyric, StackPanel Content, StackPanel Inline, TextBlock HoldRange);
	private sealed record TimeEditorRow(Border Surface, TextBlock Label, TextBlock Value, Button SetButton);
	private sealed record SyncPointListItem(Guid Id, bool IsAnchor, string Label) { public override string ToString() => Label; }
}
