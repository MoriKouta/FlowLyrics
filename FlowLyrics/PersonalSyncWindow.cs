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
using FlowLyrics.Controls;
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
	private readonly ListBox _lyricsList;
	private readonly PersonalSyncRail _rail;
	private readonly Border _inspector;
	private readonly TextBlock _inspectorSummary;
	private readonly ColumnDefinition _inspectorColumn;
	private readonly Func<bool> _canSeek;
	private DateTimeOffset _followSuspendedUntil;
	private int _lastFollowedLine = -1;
	private Guid? _selectedHoldId;
	private PersonalSyncProfile? _railEditBefore;
	private bool _railEditChanged;
	private readonly ListBox _pointsList;
	private readonly TextBlock _pointTypeText;
	private readonly TimeEditorRow _pointA;
	private readonly TimeEditorRow _pointB;
	private readonly TimeEditorRow _pointLyrics;
	private readonly Button _deletePointButton;
	private readonly Button _resyncButton;
	private readonly Button _resumeButton;
	private readonly TextBlock _backToLyric;
	private bool _showInspector;

	public event EventHandler<PersonalSyncProfile?>? PreviewChanged;
	public event EventHandler<TimeSpan>? SeekRequested;
	public event EventHandler? PlayPauseRequested;
	private Point _dragStart;
	private int _dragLine = -1;
	private bool _dragging;
	private readonly string _dragToken = Guid.NewGuid().ToString("N");

	public PersonalSyncWindow(PersonalSyncStore store, PersonalSyncContext context, PersonalSyncProfile? profile,
		IReadOnlyList<LyricLine> lines, Func<int?> selectedLineProvider, Func<TimeSpan> playbackPositionProvider, string language, Func<bool>? canSeek = null)
	{
		_canSeek = canSeek ?? (() => true);
		_store = store;
		_context = context;
		_lines = lines;
		_initialLineProvider = selectedLineProvider;
		_playbackPositionProvider = playbackPositionProvider;
		_language = language;
		_hadStoredProfile = profile != null;
		_profile = profile?.Clone() ?? CreateProfile(context);

		Title = "FlowLyrics · " + T("Personal Sync");
		Width = 1120;
		Height = 780;
		MinWidth = 760;
		MinHeight = 640;
		WindowStartupLocation = WindowStartupLocation.Manual;
		Background = Brush(25, 23, 26);
		Foreground = Brush(235, 232, 234);
		ShowInTaskbar = false;
		PersonalSyncUiTheme.Apply(this, _language);

		Grid root = new() { Margin = new Thickness(22) };
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		Content = root;

		Grid header = new() { Margin = new Thickness(0, 0, 0, 14) };
		header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		StackPanel heading = new();
		heading.Children.Add(LocalizedUiFont.Heading("PERSONAL SYNC", 23, Accent()));
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
			FontFamily = LocalizedUiFont.EnglishDotFont, FontSize = 10, TextAlignment = TextAlignment.Right,
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
		body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		_inspectorColumn = new ColumnDefinition { Width = new GridLength(260) };
		body.ColumnDefinitions.Add(_inspectorColumn);
		body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		Grid.SetRow(body, 1);
		root.Children.Add(body);

		WrapPanel offset = new() { Margin = new Thickness(0, 0, 0, 10) };
		StackPanel offsetHeading = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
		offsetHeading.Children.Add(new TextBlock { Text = T("Global offset"), FontSize = 14, FontWeight = FontWeights.SemiBold });
		_offsetText = new TextBlock { FontFamily = LocalizedUiFont.EnglishDotFont, FontSize = 16, Foreground = Foreground, Margin = new Thickness(12, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
		offsetHeading.Children.Add(_offsetText);
		offset.Children.Add(offsetHeading);
		_offsetNudges = CreateNudgeButtons(Nudge);
		_offsetNudges.MaxWidth = 420;
		_offsetNudges.HorizontalAlignment = HorizontalAlignment.Left;
		offset.Children.Add(_offsetNudges);
		Grid.SetColumnSpan(offset, 2);
		body.Children.Add(offset);

		StackPanel timelinePanel = new();
		_inspector = new Border { Background = Brush(30, 28, 31), Padding = new Thickness(12), Margin = new Thickness(12, 0, 0, 0), CornerRadius = new CornerRadius(8),
			Child = new ScrollViewer { Content = timelinePanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
		Grid.SetRow(_inspector, 1); Grid.SetColumn(_inspector, 1); body.Children.Add(_inspector);
		_selectionText = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
		_inspectorSummary = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Muted(), Margin = new Thickness(0, 0, 0, 10) };
		timelinePanel.Children.Add(_selectionText); timelinePanel.Children.Add(_inspectorSummary);
		_pointsList = new ListBox(); // Selection model only; the rail replaces the duplicate visible point list.
		_pointsList.SelectionChanged += delegate { RefreshPointEditor(); };
		SizeChanged += (_, _) => UpdateInspectorLayout();
		_resyncButton = Button(T("Re-sync from here"));
		_resyncButton.Name = "ResyncFromHereButton";
		_resyncButton.Click += (_, _) => { if (ValidSelectedLine(out int line)) AlignLineAt(line, Math.Max(0, _playbackPositionProvider().TotalSeconds)); };
		timelinePanel.Children.Add(_resyncButton);
		_resumeButton = Button(T("Resume here")); _resumeButton.Name = "ResumeSelectedHoldButton";
		_resumeButton.Click += (_, _) => ResumeSelectedHold();
		timelinePanel.Children.Add(_resumeButton);
		_backToLyric = new TextBlock { Margin = new Thickness(3, 3, 3, 8) };
		var back = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(T("Back to selected lyric"))) { Foreground = Muted() };
		back.Click += (_, _) => { _pointsList.SelectedItem = null; _rail.SelectedId = null; RefreshPointEditor(); };
		_backToLyric.Inlines.Add(back); timelinePanel.Children.Add(_backToLyric);

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
			Text = T("Select a point or range on the rail to refine it"),
			FontWeight = FontWeights.SemiBold, Foreground = Muted(), TextWrapping = TextWrapping.Wrap
		};
		editPanel.Children.Add(_pointTypeText);
		_pointA = CreateTimeEditorRow(editPanel, TimeField.A);
		_pointB = CreateTimeEditorRow(editPanel, TimeField.B);
		_pointLyrics = CreateTimeEditorRow(editPanel, TimeField.Lyrics);
		_deletePointButton = DangerButton(T("Delete selected change"));
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
		WrapPanel lyricsHeader = new() { Margin = new Thickness(2, 0, 2, 8) };
		_followNowBox = new CheckBox { Content = T("Follow current line"), Margin = new Thickness(0, 0, 12, 0), IsChecked = true };
		lyricsHeader.Children.Add(_followNowBox);
		Button details = Button(T("Edit details")); details.Name = "TimingDetailsButton";
		details.MinHeight = 28; details.Padding = new Thickness(7, 4, 7, 4);
		details.Click += (_, _) => { _showInspector = !_showInspector; UpdateInspectorLayout(); };
		SizeChanged += (_, _) => details.Visibility = ActualWidth >= 1080 ? Visibility.Collapsed : Visibility.Visible;
		lyricsHeader.Children.Add(details);
		lyricsHeader.Children.Add(SectionTitle("TIMING EDITOR"));
		lyricsPanel.Children.Add(lyricsHeader);
		_lyricsList = new ListBox { Name = "SyncLyricsList", BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
		_lyricsList.SelectionChanged += LyricsList_SelectionChanged;
		_lyricsList.PreviewMouseWheel += delegate { SuspendFollow(); };
		Grid editingSurface = new();
		editingSurface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
		editingSurface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		_rail = new PersonalSyncRail { Name = "PlaybackRail", LabelFont = LocalizedUiFont.EnglishDotFont, Margin = new Thickness(0, 0, 6, 0), AllowDrop = true };
		_rail.DescribePosition = seconds => Format(seconds) + "\n" + NearestLyricText(PersonalSyncMapper.MapPlaybackToLyrics(seconds, _profile));
		_rail.SeekRequested += seconds => SeekRequested?.Invoke(this, TimeSpan.FromSeconds(seconds));
		_rail.InteractionStarted += SuspendFollow;
		_rail.PointSelected += SelectPoint;
		_rail.EditStarted += _ => { _railEditBefore = _profile.Clone(); _railEditChanged = false; };
		_rail.EditPreview += PreviewRailEdit;
		_rail.EditFinished += FinishRailEdit;
		_rail.DragOver += (_, e) => { e.Effects = IsOwnLyricDrag(e) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
		_rail.Drop += (_, e) => { if (IsOwnLyricDrag(e)) AlignDraggedLyric(_dragLine, _playbackPositionProvider().TotalSeconds); e.Handled = true; };
		editingSurface.Children.Add(_rail);
		Grid.SetColumn(_lyricsList, 1); editingSurface.Children.Add(_lyricsList);
		Grid.SetRow(editingSurface, 1); lyricsPanel.Children.Add(editingSurface);
		_matchButton = Button(T("Align to now"));
		_matchButton.HorizontalAlignment = HorizontalAlignment.Left;
		_matchButton.MinHeight = 28; _matchButton.Padding = new Thickness(8, 4, 8, 4);
		_matchButton.Name = "AlignSelectedLyricButton";
		_matchButton.Click += MatchSelectedLine_Click;
		// Primary alignment belongs to the lyric row; retained as the keyboard/hold command target.
		_holdButton = Button("+ " + T("Lyric hold"));
		_holdButton.Name = "AddLyricHoldButton";
		_holdButton.Click += AddHoldRange_Click;
		lyricsHeader.Children.Insert(0, _holdButton);
		_workflowHint = new TextBlock { Foreground = Muted(), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
		_cancelHoldButton = Button(T("Cancel hold"));
		_cancelHoldButton.Visibility = Visibility.Collapsed;
		_cancelHoldButton.Click += delegate { CancelPendingHold(); };
		StackPanel workflow = new(); workflow.Children.Add(_workflowHint); workflow.Children.Add(_cancelHoldButton);
		Grid.SetRow(workflow, 2); lyricsPanel.Children.Add(workflow);

		StackPanel footer = new() { Margin = new Thickness(0, 14, 0, 0) };
		Grid.SetRow(footer, 2);
		root.Children.Add(footer);
		_trackScopeBox = new CheckBox
		{
			Content = T("Use for this track on every source"),
			IsChecked = _profile.Scope == PersonalSyncScope.Track
		};
		_trackScopeBox.Checked += Scope_Changed;
		_trackScopeBox.Unchecked += Scope_Changed;
		footer.Children.Add(_trackScopeBox);
		WrapPanel bottomButtons = new() { HorizontalAlignment = HorizontalAlignment.Right };
		DockPanel.SetDock(bottomButtons, Dock.Right);
		_undoButton = Button(T("Undo"));
		_redoButton = Button(T("Redo"));
		Button reset = Button(T("Reset timing"));
		Button remove = DangerButton(T("Delete saved sync"));
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
		PreviewKeyDown += Editor_KeyDown;
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
		_lyricsList.Items.Clear(); _lyricRows.Clear();
		for (int index = 0; index < _lines.Count; index++)
		{
			int i = index;
			Grid row = new() { Height = 38, Margin = new Thickness(2, 0, 2, 0) };
			foreach (var width in new[] { new GridLength(24), new GridLength(68), new GridLength(1, GridUnitType.Star), new GridLength(160) }) row.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
			TextBlock marker = new() { Text = "○", Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.SizeAll, ToolTip = T("Drag to the current position to align") };
			marker.PreviewMouseLeftButtonDown += (_, e) => { _dragStart = e.GetPosition(this); _dragLine = i; };
			marker.PreviewMouseMove += (_, e) =>
			{
				if (_dragging || _pendingHoldStart.HasValue || e.LeftButton != MouseButtonState.Pressed || _dragLine != i) return;
				if ((e.GetPosition(this) - _dragStart).Length < SystemParameters.MinimumVerticalDragDistance) return;
				_dragging = true; SuspendFollow();
				try { DragDrop.DoDragDrop(marker, new DataObject("FlowLyrics.SyncLyric", _dragToken), DragDropEffects.Move); }
				finally { _dragging = false; _dragLine = -1; RefreshLiveUi(); }
			};
			TextBlock time = new() { FontFamily = LocalizedUiFont.EnglishDotFont, FontSize = 10, Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = T("Seek to this lyric") };
			time.MouseLeftButtonUp += (_, e) => { SeekToLyric(i); e.Handled = true; };
			TextBlock lyric = new() { Text = _lines[i].Text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = _lines[i].Text };
			TextBlock correction = new() { FontSize = 10, Foreground = Muted(), Visibility = Visibility.Hidden };
			StackPanel lyricCell = new() { VerticalAlignment = VerticalAlignment.Center };
			lyricCell.Children.Add(lyric); lyricCell.Children.Add(correction);
			StackPanel actions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Hidden };
			Button align = Button(T("Align to now")); align.Name = "AlignLyricButton"; align.MinHeight = 26; align.MaxWidth = 148; align.Padding = new Thickness(7, 3, 7, 3); align.FontSize = 11;
			align.Click += (_, e) => { _lyricsList.SelectedIndex = i; MatchSelectedLine_Click(align, e); };
			actions.Children.Add(align);
			Grid.SetColumn(time, 1); Grid.SetColumn(lyricCell, 2); Grid.SetColumn(actions, 3);
			row.Children.Add(marker); row.Children.Add(time); row.Children.Add(lyricCell); row.Children.Add(actions);
			ListBoxItem item = new() { Content = row, Tag = i, Padding = new Thickness(2, 0, 2, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
			item.MouseEnter += (_, _) => ShowRowActions(i, true);
			item.MouseLeave += (_, _) => ShowRowActions(i, item.IsSelected || item.IsKeyboardFocusWithin);
			item.IsKeyboardFocusWithinChanged += (_, _) => ShowRowActions(i, item.IsSelected || item.IsMouseOver || item.IsKeyboardFocusWithin);
			_lyricsList.Items.Add(item); _lyricRows.Add(new(marker, time, lyric, correction, actions, align, item));
		}
	}

	private void ShowRowActions(int index, bool visible)
	{
		var row = _lyricRows[index];
		row.Actions.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
		double original = _lines[index].Time.TotalSeconds;
		double? mapped = PersonalSyncTimeline.PlaybackForLyric(original, _profile);
		row.Correction.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
		row.Correction.Text = T("Correction") + " " + ((mapped ?? original) - original).ToString("+0.0;-0.0;0.0") + " s";
		row.Lyric.ToolTip = _lines[index].Text + "\n" + T("Original") + " " + Format(original)
			+ " · " + T("Correction") + " " + ((mapped ?? original) - original).ToString("+0.0;-0.0;0.0") + " s"
			+ "\n" + T("Difference now") + " " + (_playbackPositionProvider().TotalSeconds - (mapped ?? original)).ToString("+0.0;-0.0;0.0") + " s";
	}

	private bool IsOwnLyricDrag(DragEventArgs e) => !_pendingHoldStart.HasValue && Equals(e.Data.GetData("FlowLyrics.SyncLyric"), _dragToken) && _dragLine >= 0;

	private void AlignDraggedLyric(int index, double playback)
	{
		if (_pendingHoldStart.HasValue || index < 0 || index >= _lines.Count || !double.IsFinite(playback)) return;
		_selectedLineIndex = index;
		_lyricsList.SelectedIndex = index;
		AlignProgressively(index, Math.Max(0, playback));
	}

	private void AlignProgressively(int index, double playback)
	{
		double lyric = _lines[index].Time.TotalSeconds;
		double? current = _profile.Mode != PersonalSyncMode.Advanced
			? lyric + (_profile.Mode == PersonalSyncMode.None ? 0 : _profile.OffsetSeconds)
			: PersonalSyncTimeline.PlaybackForLyric(lyric, _profile);
		if (!current.HasValue)
		{
			_workflowHint.Text = T("This line is skipped by the current edits. Use Re-sync from here.");
			return;
		}
		double delta = playback - current.Value;
		if (Math.Abs(delta) > .0001) Change(profile => ShiftWholeTrack(profile, delta));
	}

	private void AlignLineAt(int index, double playback)
	{
		Guid id = Guid.NewGuid();
		Change(profile =>
		{
			profile.Mode = PersonalSyncMode.Advanced;
			PersonalSyncAnchor? nearby = profile.Anchors.FirstOrDefault(a => Math.Abs(a.PlaybackSeconds - playback) < .12);
			if (nearby == null) profile.Anchors.Add(new PersonalSyncAnchor { Id = id, PlaybackSeconds = playback, LyricsSeconds = _lines[index].Time.TotalSeconds });
			else { id = nearby.Id; MoveAnchorPlayback(profile, nearby, playback); nearby.LyricsSeconds = _lines[index].Time.TotalSeconds; }
			profile.Anchors = profile.Anchors.OrderBy(a => a.PlaybackSeconds).ToList();
		});
		SelectPoint(id);
	}

	private void SeekToLyric(int index)
	{
		if (!_canSeek() || _context.Track.DurationSeconds <= 0 || index < 0 || index >= _lines.Count) return;
		double? playback = PersonalSyncTimeline.PlaybackForLyric(_lines[index].Time.TotalSeconds, _profile);
		if (playback.HasValue) SeekRequested?.Invoke(this, TimeSpan.FromSeconds(playback.Value));
	}

	private void Nudge(double delta) => Change(profile => ShiftWholeTrack(profile, delta));

	private static void ShiftWholeTrack(PersonalSyncProfile profile, double delta)
	{
		PersonalSyncTimeline.ShiftWholeTrack(profile, delta);
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
		if (ValidSelectedLine(out int selected)) AlignProgressively(selected, Math.Max(0, _playbackPositionProvider().TotalSeconds));
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
		if (_activeLineIndex >= 0) SuspendFollow();
		// A selected point/range remains the edit target while choosing its lyric.
		RefreshLyricRowVisuals();
		DrawTimeline();
		RefreshSelectionText();
		RefreshPointEditor();
		RefreshPendingWorkflow();
		RevealRowAction();
	}

	private void RevealRowAction() { /* Actions reserve space; selection never changes row geometry. */ }

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
			FontFamily = LocalizedUiFont.EnglishDotFont, FontSize = 16, FontWeight = FontWeights.Bold,
			Foreground = Accent(), HorizontalAlignment = HorizontalAlignment.Right
		};
		DockPanel.SetDock(value, Dock.Right);
		heading.Children.Add(value);
		TextBlock label = new() { Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
		heading.Children.Add(label);
		stack.Children.Add(heading);
		stack.Children.Add(CreateNudgeButtons(delta => AdjustSelectedPoint(field, delta), compact: true));
		Button setButton = Button(string.Empty);
		setButton.HorizontalAlignment = HorizontalAlignment.Stretch;
		setButton.Click += delegate { SetSelectedPointField(field); };
		stack.Children.Add(setButton);
		parent.Children.Add(surface);
		return new TimeEditorRow(surface, label, value, setButton);
	}

	private void RefreshPointEditor()
	{
		RefreshInspectorSummary();
		bool has = _pointsList.SelectedItem is SyncPointListItem;
		_deletePointButton.IsEnabled = has;
		_deletePointButton.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
		_matchButton.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
		_resyncButton.Visibility = !has && ValidSelectedLine(out _) ? Visibility.Visible : Visibility.Collapsed;
		_resumeButton.Visibility = _pointsList.SelectedItem is SyncPointListItem { IsAnchor: false } ? Visibility.Visible : Visibility.Collapsed;
		_resumeButton.IsEnabled = ValidSelectedLine(out _);
		_resumeButton.ToolTip = ValidSelectedLine(out int selectedLyric) ? _lines[selectedLyric].Text : T("Select a lyric");
		_backToLyric.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
		UpdateInspectorLayout();
		if (_pointsList.SelectedItem is not SyncPointListItem item)
		{
			_pointTypeText.Text = T("Select a point or range on the rail to refine it");
			SetEditorRow(_pointA, false, string.Empty, 0, string.Empty, false);
			SetEditorRow(_pointB, false, string.Empty, 0, string.Empty, false);
			SetEditorRow(_pointLyrics, false, string.Empty, 0, string.Empty, false);
			return;
		}

		if (item.IsAnchor)
		{
			PersonalSyncAnchor? anchor = _profile.Anchors.FirstOrDefault(candidate => candidate.Id == item.Id);
			if (anchor == null) return;
			_pointTypeText.Text = T("Alignment point: timing changes from here onward");
			SetEditorRow(_pointA, true, T("Playback"), anchor.PlaybackSeconds,
				T("Use current position"), true);
			SetEditorRow(_pointB, true, T("Lyrics"), anchor.LyricsSeconds,
				T("Use selected lyric"), ValidSelectedLine(out _));
			SetEditorRow(_pointLyrics, false, string.Empty, 0, string.Empty, false);
		}
		else
		{
			PersonalSyncSegment? hold = _profile.Segments.FirstOrDefault(candidate => candidate.Id == item.Id);
			if (hold == null) return;
			_pointTypeText.Text = T("Hold range: keep one lyric displayed until the resume point");
			SetEditorRow(_pointA, true, T("Start"), hold.PlaybackStartSeconds,
				T("Use current position"), true);
			SetEditorRow(_pointB, true, T("End / resume"), hold.PlaybackEndSeconds,
				T("Use current position"), true);
			SetEditorRow(_pointLyrics, true, T("Held lyric"), hold.LyricsTimeSeconds,
				T("Use selected lyric"), ValidSelectedLine(out _));
		}
	}

	private static void SetEditorRow(TimeEditorRow row, bool visible, string label, double seconds, string action, bool enabled)
	{
		row.Surface.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
		if (!visible) return;
		row.Label.Text = label;
		row.Value.Text = Format(seconds);
		row.SetButton.Content = new TextBlock { Text = action, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
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
				if (field == TimeField.A) MoveAnchorPlayback(profile, anchor, Round(Math.Max(0, anchor.PlaybackSeconds + delta)));
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
				if (field == TimeField.A) MoveAnchorPlayback(profile, anchor, now);
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
			if (item.IsAnchor)
			{
				foreach (var hold in profile.Segments.Where(h => h.ResumeAnchorId == item.Id)) hold.ResumeAnchorId = null;
				profile.Anchors.RemoveAll(anchor => anchor.Id == item.Id);
			}
			else
			{
				PersonalSyncSegment? hold = profile.Segments.FirstOrDefault(segment => segment.Id == item.Id);
				if (hold != null)
				{
					PersonalSyncAnchor? resume = FindResumeAnchor(profile, hold, hold.PlaybackEndSeconds);
					if (resume != null && !profile.Segments.Any(h => h.Id != hold.Id && h.ResumeAnchorId == resume.Id)) profile.Anchors.Remove(resume);
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
		if (MessageBox.Show(this, T("Remove only this Personal Sync profile?"),
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
		// An unfinished pointer gesture is only a preview, including during close.
		_rail.EndInteraction(cancel: true);
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
			MessageBox.Show(this, T("Could not save Personal Sync.") + "\n\n" + ex.Message,
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
		_nowText.Text = (_rail.IsSeeking ? "SEEK" : _dragging ? "ALIGN" : "PLAY") + "  " + Format(_rail.IsSeeking ? _rail.PreviewSeconds ?? playback : playback) + "\nLYRICS  " + Format(lyrics);

		if (active != _activeLineIndex)
		{
			_activeLineIndex = active;
			RefreshLyricRowVisuals();
		}
		if (_followNowBox.IsChecked == true && DateTimeOffset.UtcNow >= _followSuspendedUntil && !_dragging && !_rail.IsInteracting
			&& active != _lastFollowedLine && active >= 0 && active < _lyricsList.Items.Count)
		{ _lyricsList.ScrollIntoView(_lyricsList.Items[active]); _lastFollowedLine = active; }
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
		_holdButton.IsEnabled = _activeLineIndex >= 0 && _context.Track.DurationSeconds > 0;
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
			row.Marker.Text = held ? "Ⅱ" : _profile.Anchors.Any(a => Math.Abs(a.LyricsSeconds - lyric) < .05) ? "◆" : active ? "▶" : "○";
			row.Time.Foreground = active ? Accent() : Muted();
			row.Lyric.Foreground = active ? Brushes.White : Brush(210, 206, 210);
			row.Lyric.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
			row.Marker.Foreground = active ? Accent() : held ? Brushes.LightBlue : Muted();
			row.Align.Content = _pendingHoldStart.HasValue ? T("Resume here") : T("Align to now");
			ShowRowActions(index, row.Item.IsSelected || row.Item.IsMouseOver || row.Item.IsKeyboardFocusWithin);
		}
	}

	private void RefreshSelectionText()
	{
		if (ValidSelectedLine(out int selected))
		{
			_selectionText.Text = T("Selected lyric") + "  " + "[" + Format(_lines[selected].Time.TotalSeconds) + "]  " + _lines[selected].Text;
			_selectionText.Foreground = Foreground;
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
				.Select(anchor => NearestLyricText(anchor.LyricsSeconds)).FirstOrDefault() ?? T("automatic continuation");
			SyncPointListItem item = new(segment.Id, false,
				$"▰  {T("Lyric hold")}  {Format(segment.PlaybackStartSeconds)} — {Format(segment.PlaybackEndSeconds)}\n    {T("Resume here")}  {resume}");
			_pointsList.Items.Add(item);
			if (selectedId == segment.Id) _pointsList.SelectedItem = item;
		}
		foreach (PersonalSyncAnchor anchor in _profile.Anchors.OrderBy(item => item.PlaybackSeconds))
		{
			double offset = anchor.PlaybackSeconds - anchor.LyricsSeconds;
			SyncPointListItem item = new(anchor.Id, true,
				$"●  {T("Align to now")}  {Format(anchor.PlaybackSeconds)} → {Format(anchor.LyricsSeconds)}  ({offset:+0.0;-0.0;0.0}s)\n    {NearestLyricText(anchor.LyricsSeconds)}");
			_pointsList.Items.Add(item);
			if (selectedId == anchor.Id) _pointsList.SelectedItem = item;
		}
	}

	private void SelectPoint(Guid id)
	{
		_rail.SelectedId = id;
		if (_profile.Segments.Any(h => h.Id == id)) _selectedHoldId = id;
		foreach (object candidate in _pointsList.Items)
		{
			if (candidate is SyncPointListItem item && item.Id == id)
			{
				_pointsList.SelectedItem = item;

				break;
			}
		}
	}

	private void SuspendFollow() { _followSuspendedUntil = DateTimeOffset.UtcNow.AddSeconds(5); _lastFollowedLine = -1; }

	private void UpdateInspectorLayout()
	{
		bool wide = ActualWidth >= 1080;
		bool visible = wide || _showInspector || _pointsList.SelectedItem != null;
		_inspector.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
		_inspectorColumn.Width = new GridLength(wide ? 260 : 0);
		Grid.SetRow(_inspector, wide ? 1 : 2); Grid.SetColumn(_inspector, wide ? 1 : 0); Grid.SetColumnSpan(_inspector, wide ? 1 : 2);
		_inspector.MaxHeight = wide ? double.PositiveInfinity : 220;
		_inspector.Margin = wide ? new Thickness(12, 0, 0, 0) : new Thickness(0, 10, 0, 0);
	}

	private void ResumeSelectedHold()
	{
		if (_pointsList.SelectedItem is not SyncPointListItem { IsAnchor: false } point || !ValidSelectedLine(out int index)) return;
		var hold = _profile.Segments.FirstOrDefault(h => h.Id == point.Id);
		if (hold == null) return;
		Change(profile =>
		{
			var resume = FindResumeAnchor(profile, hold, hold.PlaybackEndSeconds);
			if (resume == null) { resume = new() { PlaybackSeconds = hold.PlaybackEndSeconds }; profile.Anchors.Add(resume); }
			resume.LyricsSeconds = _lines[index].Time.TotalSeconds; hold.ResumeAnchorId = resume.Id;
		});
		SelectPoint(hold.Id);
	}

	private void RefreshInspectorSummary()
	{
		if (_pointsList.SelectedItem is SyncPointListItem point)
		{
			_selectionText.Text = point.IsAnchor ? T("Sync point") : T("Lyric hold");
			var anchor = _profile.Anchors.FirstOrDefault(a => a.Id == point.Id);
			var hold = _profile.Segments.FirstOrDefault(h => h.Id == point.Id);
			_inspectorSummary.Text = anchor != null ? T("Correction") + " " + (anchor.PlaybackSeconds - anchor.LyricsSeconds).ToString("+0.0;-0.0;0.0") + " s"
				: hold != null ? T("Duration") + " " + (hold.PlaybackEndSeconds - hold.PlaybackStartSeconds).ToString("0.0") + " s" : "";
		}
		else if (ValidSelectedLine(out int selected))
		{
			double original = _lines[selected].Time.TotalSeconds;
			double? playback = PersonalSyncTimeline.PlaybackForLyric(original, _profile);
			_selectionText.Text = _lines[selected].Text;
			_inspectorSummary.Text = T("Original") + "  " + Format(original) + "\n" + T("Playback") + "  "
				+ (playback.HasValue ? Format(playback.Value) : "—") + "\n" + T("Correction") + "  "
				+ ((playback ?? original) - original).ToString("+0.0;-0.0;0.0") + " s\n" + T("Difference now") + "  "
				+ (_playbackPositionProvider().TotalSeconds - (playback ?? original)).ToString("+0.0;-0.0;0.0") + " s";
		}
		else
		{
			_selectionText.Text = T("Playback");
			_inspectorSummary.Text = Format(_playbackPositionProvider().TotalSeconds) + " / " + Format(_context.Track.DurationSeconds)
				+ "\n" + T("Global offset") + " " + _profile.OffsetSeconds.ToString("+0.0;-0.0;0.0") + " s";
		}
	}

	private void AddHoldRange_Click(object sender, RoutedEventArgs e)
	{
		double duration = _context.Track.DurationSeconds;
		if (!double.IsFinite(duration) || duration < .1) return;
		double start = Math.Clamp(_playbackPositionProvider().TotalSeconds, 0, duration - .1);
		double end = Math.Min(duration, start + 5);
		double mapped = PersonalSyncMapper.MapPlaybackToLyrics(start, _profile);
		int active = FindActiveLine(mapped);
		double lyric = active >= 0 ? _lines[active].Time.TotalSeconds : mapped;
		// Preserve the existing mapping at the provisional end until the user chooses another resume lyric.
		double resumeLyric = PersonalSyncMapper.MapPlaybackToLyrics(end, _profile);
		Guid id = Guid.NewGuid(), resumeId = Guid.NewGuid();
		Change(profile =>
		{
			profile.Mode = PersonalSyncMode.Advanced;
			profile.Anchors.Add(new() { Id = resumeId, PlaybackSeconds = end, LyricsSeconds = resumeLyric });
			profile.Segments.Add(new() { Id = id, Type = PersonalSyncSegmentType.Hold, ResumeAnchorId = resumeId,
				PlaybackStartSeconds = start, PlaybackEndSeconds = end, LyricsTimeSeconds = lyric });
		});
		SelectPoint(id);
	}

	private void PreviewRailEdit(SyncRailEdit edit)
	{
		if (_railEditBefore == null) return;
		_railEditChanged = true;
		if (edit.Field == SyncRailField.Anchor)
		{
			var anchor = _profile.Anchors.FirstOrDefault(a => a.Id == edit.Id);
			if (anchor == null) return;
			MoveAnchorPlayback(_profile, anchor, edit.Seconds);
		}
		else
		{
			var hold = _profile.Segments.FirstOrDefault(h => h.Id == edit.Id);
			if (hold == null) return;
			if (edit.Field == SyncRailField.HoldStart) hold.PlaybackStartSeconds = Math.Clamp(edit.Seconds, 0, Math.Max(0, hold.PlaybackEndSeconds - .1));
			else
			{
				var resume = FindResumeAnchor(_profile, hold, hold.PlaybackEndSeconds);
				hold.PlaybackEndSeconds = Math.Clamp(edit.Seconds, hold.PlaybackStartSeconds + .1, Math.Max(_context.Track.DurationSeconds, hold.PlaybackStartSeconds + .1));
				if (resume != null) resume.PlaybackSeconds = hold.PlaybackEndSeconds;
			}
		}
		_profile.Anchors = _profile.Anchors.OrderBy(a => a.PlaybackSeconds).ToList();
		_profile.Segments = _profile.Segments.OrderBy(h => h.PlaybackStartSeconds).ToList();
		PreviewChanged?.Invoke(this, _profile.Clone());
		RefreshPointEditor(); RefreshInspectorSummary(); // Only selected values, not every lyric row/control.
	}

	private void FinishRailEdit(bool cancel)
	{
		if (_railEditBefore == null) return;
		if (!_railEditChanged) { _railEditBefore = null; RefreshPointEditor(); RefreshInspectorSummary(); return; }
		if (cancel) _profile = _railEditBefore;
		else { _undo.Push(_railEditBefore); TrimStack(_undo, 80); _redo.Clear(); _dirty = true; _profile.UpdatedAtUtc = DateTimeOffset.UtcNow; }
		_railEditBefore = null;
		PreviewChanged?.Invoke(this, _profile.Clone());
		RefreshAll();
	}

	private void Editor_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.OriginalSource is TextBox || e.OriginalSource == _rail) return;
		if (e.Key == Key.Space && e.OriginalSource is not System.Windows.Controls.Button && e.OriginalSource is not CheckBox) { PlayPauseRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
		else if (e.Key == Key.Enter && e.OriginalSource is not System.Windows.Controls.Button) { MatchSelectedLine_Click(this, new RoutedEventArgs()); e.Handled = true; }
		else if (e.Key == Key.Delete && _pointsList.SelectedItem != null) { DeleteSelectedPoint_Click(this, new RoutedEventArgs()); e.Handled = true; }
		else if (e.Key is Key.Up or Key.Down && _lines.Count > 0)
		{
			_lyricsList.SelectedIndex = Math.Clamp(_selectedLineIndex + (e.Key == Key.Up ? -1 : 1), 0, _lines.Count - 1);
			_lyricsList.ScrollIntoView(_lyricsList.SelectedItem); SuspendFollow(); e.Handled = true;
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

	private static void MoveAnchorPlayback(PersonalSyncProfile profile, PersonalSyncAnchor anchor, double seconds)
	{
		var linked = profile.Segments.Where(h => h.ResumeAnchorId == anchor.Id
			|| (h.ResumeAnchorId == null && Math.Abs(h.PlaybackEndSeconds - anchor.PlaybackSeconds) < .15)).ToArray();
		anchor.PlaybackSeconds = Math.Max(linked.Select(h => h.PlaybackStartSeconds + .1).DefaultIfEmpty(0).Max(), seconds);
		foreach (var hold in linked) { hold.PlaybackEndSeconds = anchor.PlaybackSeconds; hold.ResumeAnchorId = anchor.Id; }
	}

	private void DrawTimeline()
	{
		_rail.CanSeek = _canSeek();
		_rail.SetCurrent(_playbackPositionProvider().TotalSeconds);
		RefreshInspectorSummary();
		if (!_timelineCoordinatesDirty) return;
		_timelineCoordinatesDirty = false;
		RefreshLyricRowVisuals();
		List<double> times = new();
		for (int i = 0; i < _lyricRows.Count; i++)
		{
			double original = _lines[i].Time.TotalSeconds;
			double? playback = PersonalSyncTimeline.PlaybackForLyric(original, _profile);
			_lyricRows[i].Time.Text = playback.HasValue ? Format(playback.Value) : "—";
			if (playback.HasValue) times.Add(playback.Value);
		}
		_rail.SetTimeline(_context.Track.DurationSeconds, _profile, times);
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

	private static Grid CreateNudgeButtons(Action<double> action, bool compact = false)
	{
		Grid grid = new() { Margin = new Thickness(-3, 4, -3, 2) };
		double[] amounts = { -0.5, -0.1, 0.1, 0.5 };
		for (int i = 0; i < amounts.Length; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		for (int i = 0; i < amounts.Length; i++)
		{
			double delta = amounts[i];
			Button button = Button(delta.ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + "s");
			if (compact) { button.Content = delta.ToString("+0.0;-0.0", CultureInfo.InvariantCulture); button.Padding = new Thickness(2, 3, 2, 3); button.MinHeight = 28; button.FontSize = 11; button.Margin = new Thickness(2); }
			button.Click += delegate { action(delta); };
			Grid.SetColumn(button, i);
			grid.Children.Add(button);
		}
		return grid;
	}

	private static TextBlock SectionTitle(string text) => LocalizedUiFont.Heading(text);
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
	private string SourceContext(PersonalSyncSourceIdentity source)
	{
		string label = string.IsNullOrWhiteSpace(source.ContextLabel) ? source.Source : source.ContextLabel;
		return source.ProviderInferred ? label + " " + T("(inferred)") : label;
	}
	private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
	private static SolidColorBrush Accent() => Brush(255, 107, 44);
	private static SolidColorBrush Muted() => Brush(181, 176, 181);

	private enum TimeField { A, B, Lyrics }
	private sealed record LyricRow(TextBlock Marker, TextBlock Time, TextBlock Lyric, TextBlock Correction, StackPanel Actions, Button Align, ListBoxItem Item);
	private sealed record TimeEditorRow(Border Surface, TextBlock Label, TextBlock Value, Button SetButton);
	private sealed record SyncPointListItem(Guid Id, bool IsAnchor, string Label) { public override string ToString() => Label; }
}
