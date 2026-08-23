using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowLyrics.Models;
using FlowLyrics.Services;

namespace FlowLyrics;

public sealed class PersonalSyncManagerWindow : Window
{
	private readonly PersonalSyncStore _store;
	private readonly string _language;
	private readonly Guid? _initialProfileId;
	private readonly TextBox _searchBox;
	private readonly ListBox _list;
	private readonly TextBlock _details;
	private readonly TextBlock _offsetValue;
	private readonly Canvas _timeline;
	private readonly ListBox _points;
	private readonly TextBlock _editTitle;
	private readonly HistoryEditorRow _aRow;
	private readonly HistoryEditorRow _bRow;
	private readonly HistoryEditorRow _lyricsRow;
	private readonly Button _deletePointButton;
	private PersonalSyncProfile[] _profiles = Array.Empty<PersonalSyncProfile>();

	public PersonalSyncManagerWindow(PersonalSyncStore store, string language, Guid? initialProfileId = null)
	{
		_store = store;
		_language = language;
		_initialProfileId = initialProfileId;
		Title = "FlowLyrics · " + L("Personal Sync 履歴", "Personal Sync history");
		Width = 980;
		Height = 720;
		MinWidth = 780;
		MinHeight = 560;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ShowInTaskbar = false;
		Background = Brush(25, 23, 26);
		Foreground = Brush(235, 232, 234);
		PersonalSyncUiTheme.Apply(this);

		Grid root = new() { Margin = new Thickness(22) };
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		Content = root;

		Grid heading = new() { Margin = new Thickness(0, 0, 0, 13) };
		heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
		heading.Children.Add(new TextBlock { Text = "PERSONAL SYNC HISTORY", Foreground = Accent(), FontSize = 22, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
		_searchBox = new TextBox
		{
			Margin = new Thickness(0), ToolTip = L("曲名・アーティスト・サービス・再生アプリで検索", "Search title, artist, service, or playback app")
		};
		_searchBox.TextChanged += delegate { PopulateList(); };
		Grid.SetColumn(_searchBox, 1);
		heading.Children.Add(_searchBox);
		Grid.SetColumnSpan(heading, 3);
		root.Children.Add(heading);

		Border listCard = Card();
		listCard.Margin = new Thickness(0);
		Grid.SetRow(listCard, 1);
		root.Children.Add(listCard);
		Grid listGrid = new() { Margin = new Thickness(12) };
		listGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		listGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		listGrid.Children.Add(SectionTitle(L("同期した曲", "Synced tracks")));
		_list = new ListBox { Margin = new Thickness(0, 8, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
		_list.SelectionChanged += delegate { RefreshDetails(); };
		Grid.SetRow(_list, 1);
		listGrid.Children.Add(_list);
		listCard.Child = listGrid;

		ScrollViewer detailsScroll = new()
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			Padding = new Thickness(0, 0, 5, 0)
		};
		StackPanel right = new();
		detailsScroll.Content = right;
		Grid.SetRow(detailsScroll, 1);
		Grid.SetColumn(detailsScroll, 2);
		root.Children.Add(detailsScroll);

		Border detailsCard = Card();
		StackPanel detailsPanel = CardContent();
		detailsCard.Child = detailsPanel;
		right.Children.Add(detailsCard);
		detailsPanel.Children.Add(SectionTitle(L("同期の詳細", "Sync details")));
		_details = new TextBlock { Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap, Foreground = Foreground };
		detailsPanel.Children.Add(_details);

		Border offsetCard = Card();
		StackPanel offsetPanel = CardContent();
		offsetCard.Child = offsetPanel;
		right.Children.Add(offsetCard);
		offsetPanel.Children.Add(SectionTitle(L("曲全体の調整", "Whole-track adjustment")));
		_offsetValue = new TextBlock
		{
			HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Consolas"),
			FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Accent(), Margin = new Thickness(0, 4, 0, 5)
		};
		offsetPanel.Children.Add(_offsetValue);
		offsetPanel.Children.Add(CreateNudgeButtons(async delta => await EditOffsetAsync(delta)));

		Border flowCard = Card();
		StackPanel flowPanel = CardContent();
		flowCard.Child = flowPanel;
		right.Children.Add(flowCard);
		flowPanel.Children.Add(SectionTitle(L("曲中の変更", "Changes during the track")));
		flowPanel.Children.Add(new TextBlock
		{
			Text = L("上から下へ曲が進みます。帯は停止区間、●は歌詞の切替点です。", "The song flows top to bottom. Bands are holds and ● marks lyric switches."),
			Foreground = Muted(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8)
		});
		_timeline = new Canvas { Height = 250, Background = Brush(31, 29, 32), ClipToBounds = true };
		_timeline.SizeChanged += delegate { DrawTimeline(); };
		flowPanel.Children.Add(_timeline);
		_points = new ListBox { MinHeight = 110, MaxHeight = 190, Margin = new Thickness(0, 9, 0, 0) };
		_points.SelectionChanged += delegate { RefreshPointEditor(); };
		flowPanel.Children.Add(_points);

		Border editor = new()
		{
			Margin = new Thickness(0, 9, 0, 0), Padding = new Thickness(11), CornerRadius = new CornerRadius(9),
			Background = Brush(30, 28, 31), BorderBrush = Brush(74, 69, 76), BorderThickness = new Thickness(1)
		};
		StackPanel editorPanel = new();
		editor.Child = editorPanel;
		flowPanel.Children.Add(editor);
		_editTitle = new TextBlock
		{
			Text = L("変更点を選ぶと、ボタンだけで時刻を直せます", "Select a change, then adjust it with buttons only"),
			Foreground = Muted(), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap
		};
		editorPanel.Children.Add(_editTitle);
		_aRow = CreateHistoryEditorRow(editorPanel, HistoryField.A);
		_bRow = CreateHistoryEditorRow(editorPanel, HistoryField.B);
		_lyricsRow = CreateHistoryEditorRow(editorPanel, HistoryField.Lyrics);
		_deletePointButton = Button(L("この変更点を削除", "Delete this change"));
		_deletePointButton.Click += DeletePoint_Click;
		_deletePointButton.HorizontalAlignment = HorizontalAlignment.Left;
		editorPanel.Children.Add(_deletePointButton);

		WrapPanel buttons = new() { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 13, 0, 0) };
		Button reset = Button("RESET");
		Button delete = Button("DELETE");
		Button close = PrimaryButton(L("閉じる", "Close"));
		reset.Click += Reset_Click;
		delete.Click += Delete_Click;
		close.Click += delegate { Close(); };
		buttons.Children.Add(reset);
		buttons.Children.Add(delete);
		buttons.Children.Add(close);
		Grid.SetRow(buttons, 2);
		Grid.SetColumnSpan(buttons, 3);
		root.Children.Add(buttons);
		Loaded += async delegate { await RefreshAsync(_initialProfileId); };
	}

	private async Task RefreshAsync(Guid? selectedId = null, Guid? selectedPointId = null)
	{
		_profiles = (await _store.ListAsync()).ToArray();
		PopulateList(selectedId, selectedPointId);
	}

	private void PopulateList(Guid? selectedId = null, Guid? selectedPointId = null)
	{
		selectedId ??= Selected()?.Id;
		string query = _searchBox.Text.Trim();
		_list.Items.Clear();
		foreach (PersonalSyncProfile profile in _profiles.Where(profile => Matches(profile, query))
			.OrderBy(profile => SourceContext(profile.Source)).ThenBy(profile => profile.Track.Artist).ThenBy(profile => profile.Track.Title))
		{
			StackPanel content = new();
			content.Children.Add(new TextBlock
			{
				Text = SourceContext(profile.Source).ToUpperInvariant(), Foreground = Accent(),
				FontFamily = new FontFamily("Consolas"), FontSize = 10, FontWeight = FontWeights.Bold
			});
			content.Children.Add(new TextBlock { Text = profile.Track.Title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
			content.Children.Add(new TextBlock { Text = profile.Track.Artist, Foreground = Muted(), Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
			content.Children.Add(new TextBlock
			{
				Text = (profile.Scope == PersonalSyncScope.Track ? L("全再生元", "EVERY SOURCE") : profile.Source.Source) + "  ·  " + Summary(profile),
				Foreground = Muted(), FontFamily = new FontFamily("Consolas"), FontSize = 10, Margin = new Thickness(0, 3, 0, 0)
			});
			ListBoxItem item = new() { Tag = profile.Id, Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch };
			_list.Items.Add(item);
			if (selectedId == profile.Id) _list.SelectedItem = item;
		}
		if (_list.SelectedItem == null && _list.Items.Count > 0) _list.SelectedIndex = 0;
		RefreshDetails(selectedPointId);
	}

	private static bool Matches(PersonalSyncProfile profile, string query)
	{
		if (string.IsNullOrWhiteSpace(query)) return true;
		return new[]
		{
			profile.Track.Title, profile.Track.Artist, profile.Track.Album, profile.Source.Source,
			profile.Source.Provider, profile.Source.ContextLabel, profile.Source.OriginalMediaTitle,
			profile.Source.OriginalMediaArtist, profile.Lyrics.DisplayName
		}.Any(value => (value ?? string.Empty).Contains(query, StringComparison.CurrentCultureIgnoreCase));
	}

	private PersonalSyncProfile? Selected() => _list.SelectedItem is ListBoxItem { Tag: Guid id }
		? _profiles.FirstOrDefault(profile => profile.Id == id) : null;

	private void RefreshDetails(Guid? selectedPointId = null)
	{
		PersonalSyncProfile? profile = Selected();
		_points.Items.Clear();
		if (profile == null)
		{
			_details.Text = L("保存済み調整はありません。", "No saved profiles.");
			_offsetValue.Text = "0.0 s";
			DrawTimeline();
			RefreshPointEditor();
			return;
		}

		string inferred = profile.Source.ProviderInferred ? L("（メタデータから推定）", " (inferred from metadata)") : string.Empty;
		_details.Text = $"{profile.Track.Title}\n{profile.Track.Artist}\n{profile.Track.Album}\n\n" +
			$"{L("サービス", "Service")}  {Provider(profile.Source)}{inferred}\n" +
			$"{L("再生アプリ", "Playback app")}  {profile.Source.Source}\n" +
			$"{L("元タイトル", "Original title")}  {profile.Source.OriginalMediaTitle}\n" +
			$"{L("元投稿者 / アーティスト", "Original channel / artist")}  {profile.Source.OriginalMediaArtist}\n" +
			$"{L("歌詞", "Lyrics")}  {profile.Lyrics.DisplayName}\n{L("範囲", "Scope")}  {profile.Scope}\n" +
			$"{L("更新", "Updated")}  {profile.UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
		_offsetValue.Text = profile.OffsetSeconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
		foreach (PersonalSyncSegment segment in profile.Segments.OrderBy(item => item.PlaybackStartSeconds))
		{
			PointItem item = new(segment.Id, false, $"▰ {L("停止", "HOLD")}  {Format(segment.PlaybackStartSeconds)} — {Format(segment.PlaybackEndSeconds)}  @ {Format(segment.LyricsTimeSeconds)}");
			_points.Items.Add(item);
			if (selectedPointId == segment.Id) _points.SelectedItem = item;
		}
		foreach (PersonalSyncAnchor anchor in profile.Anchors.OrderBy(item => item.PlaybackSeconds))
		{
			PointItem item = new(anchor.Id, true, $"● {L("切替", "SWITCH")}  {Format(anchor.PlaybackSeconds)} → {Format(anchor.LyricsSeconds)}");
			_points.Items.Add(item);
			if (selectedPointId == anchor.Id) _points.SelectedItem = item;
		}
		DrawTimeline();
		RefreshPointEditor();
	}

	private HistoryEditorRow CreateHistoryEditorRow(StackPanel parent, HistoryField field)
	{
		Border surface = new()
		{
			Background = Brush(35, 32, 36), BorderBrush = Brush(66, 61, 67), BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(7), Padding = new Thickness(9), Margin = new Thickness(0, 7, 0, 0), Visibility = Visibility.Collapsed
		};
		StackPanel stack = new();
		surface.Child = stack;
		DockPanel heading = new();
		TextBlock value = new() { FontFamily = new FontFamily("Consolas"), FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Accent() };
		DockPanel.SetDock(value, Dock.Right);
		heading.Children.Add(value);
		TextBlock label = new() { Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
		heading.Children.Add(label);
		stack.Children.Add(heading);
		stack.Children.Add(CreateNudgeButtons(async delta => await AdjustPointAsync(field, delta)));
		parent.Children.Add(surface);
		return new HistoryEditorRow(surface, label, value);
	}

	private void RefreshPointEditor()
	{
		PersonalSyncProfile? profile = Selected();
		_deletePointButton.IsEnabled = profile != null && _points.SelectedItem is PointItem;
		if (profile == null || _points.SelectedItem is not PointItem item)
		{
			_editTitle.Text = L("変更点を選ぶと、ボタンだけで時刻を直せます", "Select a change, then adjust it with buttons only");
			SetRow(_aRow, false, string.Empty, 0);
			SetRow(_bRow, false, string.Empty, 0);
			SetRow(_lyricsRow, false, string.Empty, 0);
			return;
		}
		if (item.IsAnchor)
		{
			PersonalSyncAnchor? anchor = profile.Anchors.FirstOrDefault(value => value.Id == item.Id);
			if (anchor == null) return;
			_editTitle.Text = L("切替点：再生位置から指定歌詞へ切り替え", "Switch point: playback position to chosen lyric");
			SetRow(_aRow, true, L("切替を始める再生位置", "Playback switch position"), anchor.PlaybackSeconds);
			SetRow(_bRow, true, L("切替先の歌詞位置", "Destination lyric position"), anchor.LyricsSeconds);
			SetRow(_lyricsRow, false, string.Empty, 0);
		}
		else
		{
			PersonalSyncSegment? segment = profile.Segments.FirstOrDefault(value => value.Id == item.Id);
			if (segment == null) return;
			_editTitle.Text = L("停止区間：開始から再開まで同じ歌詞を表示", "Hold range: keep one lyric until resume");
			SetRow(_aRow, true, L("停止を始める再生位置", "Hold start playback position"), segment.PlaybackStartSeconds);
			SetRow(_bRow, true, L("選択歌詞から再開する再生位置", "Resume playback position"), segment.PlaybackEndSeconds);
			SetRow(_lyricsRow, true, L("停止中に表示する歌詞位置", "Lyric position shown while held"), segment.LyricsTimeSeconds);
		}
	}

	private static void SetRow(HistoryEditorRow row, bool visible, string label, double seconds)
	{
		row.Surface.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
		if (!visible) return;
		row.Label.Text = label;
		row.Value.Text = Format(seconds);
	}

	private async Task AdjustPointAsync(HistoryField field, double delta)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null || _points.SelectedItem is not PointItem item) return;
		if (item.IsAnchor)
		{
			PersonalSyncAnchor? anchor = profile.Anchors.FirstOrDefault(value => value.Id == item.Id);
			if (anchor == null) return;
			if (field == HistoryField.A) anchor.PlaybackSeconds = Round(Math.Max(0, anchor.PlaybackSeconds + delta));
			else if (field == HistoryField.B) anchor.LyricsSeconds = Round(Math.Max(0, anchor.LyricsSeconds + delta));
			profile.Anchors = profile.Anchors.OrderBy(value => value.PlaybackSeconds).ToList();
		}
		else
		{
			PersonalSyncSegment? segment = profile.Segments.FirstOrDefault(value => value.Id == item.Id);
			if (segment == null) return;
			if (field == HistoryField.A) segment.PlaybackStartSeconds = Round(Math.Clamp(segment.PlaybackStartSeconds + delta, 0, Math.Max(0, segment.PlaybackEndSeconds - 0.05)));
			else if (field == HistoryField.B)
			{
				double previousEnd = segment.PlaybackEndSeconds;
				segment.PlaybackEndSeconds = Round(Math.Max(segment.PlaybackStartSeconds + 0.05, segment.PlaybackEndSeconds + delta));
				PersonalSyncAnchor? resume = FindResumeAnchor(profile, segment, previousEnd);
				if (resume != null) resume.PlaybackSeconds = segment.PlaybackEndSeconds;
			}
			else segment.LyricsTimeSeconds = Round(Math.Max(0, segment.LyricsTimeSeconds + delta));
			profile.Segments = profile.Segments.OrderBy(value => value.PlaybackStartSeconds).ToList();
		}
		await _store.UpsertAsync(profile);
		await RefreshAsync(profile.Id, item.Id);
	}

	private async void DeletePoint_Click(object sender, RoutedEventArgs e)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null || _points.SelectedItem is not PointItem item) return;
		if (item.IsAnchor) profile.Anchors.RemoveAll(value => value.Id == item.Id);
		else
		{
			PersonalSyncSegment? hold = profile.Segments.FirstOrDefault(value => value.Id == item.Id);
			if (hold != null)
			{
				PersonalSyncAnchor? resume = FindResumeAnchor(profile, hold, hold.PlaybackEndSeconds);
				if (resume != null) profile.Anchors.Remove(resume);
			}
			profile.Segments.RemoveAll(value => value.Id == item.Id);
		}
		if (profile.Anchors.Count == 0 && profile.Segments.Count == 0)
			profile.Mode = Math.Abs(profile.OffsetSeconds) > 0.0001 ? PersonalSyncMode.Offset : PersonalSyncMode.None;
		await _store.UpsertAsync(profile);
		await RefreshAsync(profile.Id);
	}

	private async Task EditOffsetAsync(double delta)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null) return;
		profile.OffsetSeconds = Round(profile.OffsetSeconds + delta);
		if (profile.Mode == PersonalSyncMode.None) profile.Mode = PersonalSyncMode.Offset;
		await _store.UpsertAsync(profile);
		await RefreshAsync(profile.Id);
	}

	private static PersonalSyncAnchor? FindResumeAnchor(PersonalSyncProfile profile, PersonalSyncSegment hold, double fallbackEnd)
	{
		return hold.ResumeAnchorId.HasValue
			? profile.Anchors.FirstOrDefault(anchor => anchor.Id == hold.ResumeAnchorId.Value)
			: profile.Anchors.FirstOrDefault(anchor => Math.Abs(anchor.PlaybackSeconds - fallbackEnd) < 0.15);
	}

	private async void Reset_Click(object sender, RoutedEventArgs e)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null) return;
		profile.Mode = PersonalSyncMode.None;
		profile.OffsetSeconds = 0;
		profile.Anchors.Clear();
		profile.Segments.Clear();
		await _store.UpsertAsync(profile);
		await RefreshAsync();
	}

	private async void Delete_Click(object sender, RoutedEventArgs e)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null || MessageBox.Show(this, L("この同期履歴を削除しますか？", "Delete this Personal Sync profile?"), "FlowLyrics", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
		await _store.DeleteAsync(profile.Id);
		await RefreshAsync();
	}

	private void DrawTimeline()
	{
		_timeline.Children.Clear();
		PersonalSyncProfile? profile = Selected();
		if (profile == null || _timeline.ActualWidth <= 1 || _timeline.ActualHeight <= 1) return;
		double width = _timeline.ActualWidth, height = _timeline.ActualHeight;
		double duration = Math.Max(1, profile.Track.DurationSeconds);
		double axisX = 38, top = 22, bottom = height - 22;
		_timeline.Children.Add(new Line { X1 = axisX, X2 = axisX, Y1 = top, Y2 = bottom, Stroke = Brush(99, 94, 101), StrokeThickness = 3 });
		AddTimelineText("0:00", 4, 3, Muted(), 9);
		AddTimelineText(Format(duration), 4, height - 18, Muted(), 9);
		double labelY = 10;
		foreach (PersonalSyncSegment segment in profile.Segments.OrderBy(value => value.PlaybackStartSeconds))
		{
			double startY = Y(segment.PlaybackStartSeconds, duration, top, bottom);
			double endY = Y(segment.PlaybackEndSeconds, duration, top, bottom);
			Rectangle range = new() { Width = 16, Height = Math.Max(4, endY - startY), Fill = Accent(), Opacity = .42, RadiusX = 5, RadiusY = 5 };
			Canvas.SetLeft(range, axisX - 8); Canvas.SetTop(range, startY); _timeline.Children.Add(range);
			double y = Math.Min(Math.Max(startY - 12, labelY), Math.Max(8, height - 35));
			_timeline.Children.Add(new Line { X1 = axisX + 8, X2 = 67, Y1 = startY, Y2 = y + 10, Stroke = Accent(), StrokeThickness = 1 });
			AddTimelineText($"{L("停止", "HOLD")} {Format(segment.PlaybackStartSeconds)} → {Format(segment.PlaybackEndSeconds)}", 72, y, Brushes.White, 10, Math.Max(80, width - 82));
			labelY = y + 35;
		}
		foreach (PersonalSyncAnchor anchor in profile.Anchors.OrderBy(value => value.PlaybackSeconds))
		{
			double markerY = Y(anchor.PlaybackSeconds, duration, top, bottom);
			Ellipse point = new() { Width = 13, Height = 13, Fill = Accent(), Stroke = Brushes.White, StrokeThickness = 1 };
			Canvas.SetLeft(point, axisX - 6.5); Canvas.SetTop(point, markerY - 6.5); _timeline.Children.Add(point);
			double y = Math.Min(Math.Max(markerY - 12, labelY), Math.Max(8, height - 35));
			_timeline.Children.Add(new Line { X1 = axisX + 7, X2 = 67, Y1 = markerY, Y2 = y + 10, Stroke = Brush(166, 158, 166), StrokeThickness = 1 });
			AddTimelineText($"{L("切替", "SWITCH")} {Format(anchor.PlaybackSeconds)} → {Format(anchor.LyricsSeconds)}", 72, y, Brushes.White, 10, Math.Max(80, width - 82));
			labelY = y + 35;
		}
	}

	private TextBlock AddTimelineText(string text, double x, double y, Brush foreground, double size, double maxWidth = double.PositiveInfinity)
	{
		TextBlock label = new() { Text = text, Foreground = foreground, FontFamily = new FontFamily("Consolas"), FontSize = size, TextWrapping = TextWrapping.Wrap, MaxWidth = maxWidth };
		Canvas.SetLeft(label, x); Canvas.SetTop(label, y); _timeline.Children.Add(label); return label;
	}

	private static Grid CreateNudgeButtons(Func<double, Task> action)
	{
		Grid grid = new() { Margin = new Thickness(-3, 4, -3, 2) };
		double[] amounts = { -0.5, -0.1, 0.1, 0.5 };
		for (int i = 0; i < amounts.Length; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		for (int i = 0; i < amounts.Length; i++)
		{
			double delta = amounts[i];
			Button button = Button(delta.ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + "s");
			button.Click += async delegate { await action(delta); };
			Grid.SetColumn(button, i); grid.Children.Add(button);
		}
		return grid;
	}

	private static string Summary(PersonalSyncProfile profile) => profile.Mode == PersonalSyncMode.Advanced
		? $"{profile.Anchors.Count} SWITCH / {profile.Segments.Count} HOLD"
		: profile.OffsetSeconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
	private string SourceContext(PersonalSyncSourceIdentity source)
	{
		string label = string.IsNullOrWhiteSpace(source.ContextLabel) ? source.Source : source.ContextLabel;
		return source.ProviderInferred ? label + L("（推定）", " (inferred)") : label;
	}
	private static string Provider(PersonalSyncSourceIdentity source) => string.IsNullOrWhiteSpace(source.Provider) ? source.Source : source.Provider;
	private static double Y(double seconds, double duration, double top, double bottom) => top + Math.Clamp(seconds / duration, 0, 1) * Math.Max(1, bottom - top);
	private static double Round(double value) => Math.Round(Math.Clamp(value, -3600, 3600), 3);
	private static string Format(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f", CultureInfo.InvariantCulture);
	private static TextBlock SectionTitle(string text) => new() { Text = text, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
	private static StackPanel CardContent() => new() { Margin = new Thickness(14) };
	private static Border Card() => new() { Background = Brush(36, 33, 37), BorderBrush = Brush(69, 64, 70), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 0, 11) };
	private static Button Button(string text) => new() { Content = text };
	private static Button PrimaryButton(string text) => new() { Content = text, Background = Accent(), Foreground = Brush(28, 25, 28), BorderBrush = Accent(), FontWeight = FontWeights.SemiBold };
	private string L(string ja, string en) => _language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? ja : en;
	private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
	private static SolidColorBrush Accent() => Brush(255, 107, 44);
	private static SolidColorBrush Muted() => Brush(181, 176, 181);

	private enum HistoryField { A, B, Lyrics }
	private sealed record HistoryEditorRow(Border Surface, TextBlock Label, TextBlock Value);
	private sealed record PointItem(Guid Id, bool IsAnchor, string Label) { public override string ToString() => Label; }
}
