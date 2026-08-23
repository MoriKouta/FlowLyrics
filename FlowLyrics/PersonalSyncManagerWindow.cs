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
	private readonly Canvas _timeline;
	private readonly ListBox _points;
	private readonly TextBlock _editTitle;
	private readonly TextBox _aBox;
	private readonly TextBox _bBox;
	private readonly TextBox _lyricsBox;
	private readonly Button _applyPointButton;
	private PersonalSyncProfile[] _profiles = Array.Empty<PersonalSyncProfile>();

	public PersonalSyncManagerWindow(PersonalSyncStore store, string language, Guid? initialProfileId = null)
	{
		_store = store;
		_language = language;
		_initialProfileId = initialProfileId;
		Title = "FlowLyrics · " + L("Personal Sync 履歴", "Personal Sync history");
		Width = 900;
		Height = 650;
		MinWidth = 720;
		MinHeight = 500;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ShowInTaskbar = false;
		Background = Brush(25, 23, 26);
		Foreground = Brush(235, 232, 234);

		Grid root = new() { Margin = new Thickness(18) };
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		Content = root;

		Grid heading = new();
		heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
		TextBlock title = new() { Text = "PERSONAL SYNC HISTORY", Foreground = Accent(), FontSize = 21, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) };
		heading.Children.Add(title);
		_searchBox = new TextBox
		{
			Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 12),
			Background = Brush(245, 244, 245), Foreground = Brush(26, 24, 27),
			BorderBrush = Brush(92, 86, 94), ToolTip = L("曲名・アーティスト・再生元で検索", "Search title, artist, or source")
		};
		_searchBox.TextChanged += delegate { PopulateList(); };
		Grid.SetColumn(_searchBox, 1);
		heading.Children.Add(_searchBox);
		Grid.SetColumnSpan(heading, 3);
		root.Children.Add(heading);

		_list = new ListBox
		{
			Background = Brush(35, 33, 36), Foreground = Foreground, BorderBrush = Brush(74, 69, 76),
			Padding = new Thickness(4), HorizontalContentAlignment = HorizontalAlignment.Stretch
		};
		_list.SelectionChanged += delegate { RefreshDetails(); };
		Grid.SetRow(_list, 1);
		root.Children.Add(_list);

		ScrollViewer detailsScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
		StackPanel right = new();
		detailsScroll.Content = right;
		Grid.SetRow(detailsScroll, 1);
		Grid.SetColumn(detailsScroll, 2);
		root.Children.Add(detailsScroll);
		_details = new TextBlock { Padding = new Thickness(14), TextWrapping = TextWrapping.Wrap, Foreground = Foreground, Background = Brush(31, 29, 32) };
		right.Children.Add(_details);
		_timeline = new Canvas { Height = 92, Background = Brush(31, 29, 32), Margin = new Thickness(0, 10, 0, 8), ClipToBounds = true };
		_timeline.SizeChanged += delegate { DrawTimeline(); };
		right.Children.Add(_timeline);
		_points = new ListBox { MinHeight = 120, MaxHeight = 190, Background = Brush(31, 29, 32), Foreground = Foreground, BorderBrush = Brush(74, 69, 76) };
		_points.SelectionChanged += delegate { RefreshPointEditor(); };
		right.Children.Add(_points);

		Border editor = new() { Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(12), CornerRadius = new CornerRadius(9), Background = Brush(35, 32, 36), BorderBrush = Brush(74, 69, 76), BorderThickness = new Thickness(1) };
		StackPanel editorPanel = new();
		editor.Child = editorPanel;
		right.Children.Add(editor);
		_editTitle = new TextBlock { Text = L("変更点を選択すると秒数を編集できます", "Select a change to edit its times"), Foreground = Muted(), FontWeight = FontWeights.SemiBold };
		editorPanel.Children.Add(_editTitle);
		Grid editGrid = new() { Margin = new Thickness(0, 7, 0, 0) };
		editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
		editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		for (int i = 0; i < 3; i++) editGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		AddLabel(editGrid, 0, L("再生 / 開始", "Playback / Start"));
		AddLabel(editGrid, 1, L("歌詞 / 終了", "Lyric / End"));
		AddLabel(editGrid, 2, L("固定位置", "Held lyric"));
		_aBox = AddBox(editGrid, 0);
		_bBox = AddBox(editGrid, 1);
		_lyricsBox = AddBox(editGrid, 2);
		editorPanel.Children.Add(editGrid);
		_applyPointButton = Button(L("変更点を更新", "Update change"));
		_applyPointButton.Click += ApplyPoint_Click;
		editorPanel.Children.Add(_applyPointButton);

		WrapPanel buttons = new() { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
		foreach (double delta in new[] { -0.5, -0.1, 0.1, 0.5 })
		{
			double captured = delta;
			Button nudge = Button(delta.ToString("+0.0;-0.0", CultureInfo.InvariantCulture));
			nudge.Click += async delegate { await EditOffsetAsync(captured); };
			buttons.Children.Add(nudge);
		}
		Button reset = Button(L("RESET", "RESET"));
		Button delete = Button(L("DELETE", "DELETE"));
		Button close = Button(L("閉じる", "Close"));
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

	private async Task RefreshAsync(Guid? selectedId = null)
	{
		_profiles = (await _store.ListAsync()).ToArray();
		PopulateList(selectedId);
	}

	private void PopulateList(Guid? selectedId = null)
	{
		selectedId ??= Selected()?.Id;
		string query = _searchBox.Text.Trim();
		_list.Items.Clear();
		foreach (PersonalSyncProfile profile in _profiles.Where(profile => Matches(profile, query)))
		{
			StackPanel content = new();
			content.Children.Add(new TextBlock { Text = profile.Track.Title, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
			content.Children.Add(new TextBlock { Text = profile.Track.Artist, Foreground = Muted(), Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
			content.Children.Add(new TextBlock
			{
				Text = (profile.Scope == PersonalSyncScope.Source ? profile.Source.Source : L("すべての再生元", "Every source")) + "  ·  " + Summary(profile),
				Foreground = Accent(), FontFamily = new FontFamily("Consolas"), FontSize = 10, Margin = new Thickness(0, 3, 0, 0)
			});
			ListBoxItem item = new() { Tag = profile.Id, Content = content, Padding = new Thickness(8), HorizontalContentAlignment = HorizontalAlignment.Stretch };
			_list.Items.Add(item);
			if (selectedId == profile.Id) _list.SelectedItem = item;
		}
		if (_list.SelectedItem == null && _list.Items.Count > 0) _list.SelectedIndex = 0;
		RefreshDetails();
	}

	private static bool Matches(PersonalSyncProfile profile, string query)
	{
		if (string.IsNullOrWhiteSpace(query)) return true;
		return new[] { profile.Track.Title, profile.Track.Artist, profile.Track.Album, profile.Source.Source, profile.Lyrics.DisplayName }
			.Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase));
	}

	private PersonalSyncProfile? Selected() => _list.SelectedItem is ListBoxItem { Tag: Guid id }
		? _profiles.FirstOrDefault(profile => profile.Id == id) : null;

	private void RefreshDetails()
	{
		PersonalSyncProfile? profile = Selected();
		_points.Items.Clear();
		if (profile == null)
		{
			_details.Text = L("保存済み調整はありません。", "No saved profiles.");
			DrawTimeline();
			RefreshPointEditor();
			return;
		}
		_details.Text = $"{profile.Track.Title}\n{profile.Track.Artist}\n{profile.Track.Album}\n\n" +
			$"Source  {(profile.Scope == PersonalSyncScope.Source ? profile.Source.Source : "ALL")}\n" +
			$"Lyrics  {profile.Lyrics.DisplayName}\nMode    {profile.Mode}\nOffset  {profile.OffsetSeconds:+0.000;-0.000;0.000} s\n" +
			$"Updated {profile.UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
		foreach (PersonalSyncAnchor anchor in profile.Anchors.OrderBy(item => item.PlaybackSeconds))
			_points.Items.Add(new PointItem(anchor.Id, true, $"● SYNC  {Format(anchor.PlaybackSeconds)} → {Format(anchor.LyricsSeconds)}"));
		foreach (PersonalSyncSegment segment in profile.Segments.OrderBy(item => item.PlaybackStartSeconds))
			_points.Items.Add(new PointItem(segment.Id, false, $"▰ HOLD  {Format(segment.PlaybackStartSeconds)} — {Format(segment.PlaybackEndSeconds)}  @ {Format(segment.LyricsTimeSeconds)}"));
		DrawTimeline();
		RefreshPointEditor();
	}

	private void RefreshPointEditor()
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null || _points.SelectedItem is not PointItem item)
		{
			_editTitle.Text = L("変更点を選択すると秒数を編集できます", "Select a change to edit its times");
			_aBox.Text = _bBox.Text = _lyricsBox.Text = string.Empty;
			_applyPointButton.IsEnabled = false;
			return;
		}
		_applyPointButton.IsEnabled = true;
		if (item.IsAnchor)
		{
			PersonalSyncAnchor? anchor = profile.Anchors.FirstOrDefault(value => value.Id == item.Id);
			if (anchor == null) return;
			_editTitle.Text = L("同期点：再生時刻 → 歌詞時刻", "Sync point: playback → lyric");
			_aBox.Text = Format(anchor.PlaybackSeconds);
			_bBox.Text = Format(anchor.LyricsSeconds);
			_lyricsBox.Text = string.Empty;
			_lyricsBox.IsEnabled = false;
		}
		else
		{
			PersonalSyncSegment? segment = profile.Segments.FirstOrDefault(value => value.Id == item.Id);
			if (segment == null) return;
			_editTitle.Text = L("停止区間：開始 / 終了 / 固定する歌詞時刻", "Hold: start / end / held lyric time");
			_aBox.Text = Format(segment.PlaybackStartSeconds);
			_bBox.Text = Format(segment.PlaybackEndSeconds);
			_lyricsBox.Text = Format(segment.LyricsTimeSeconds);
			_lyricsBox.IsEnabled = true;
		}
	}

	private async void ApplyPoint_Click(object sender, RoutedEventArgs e)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null || _points.SelectedItem is not PointItem item || !TryParse(_aBox.Text, out double a) || !TryParse(_bBox.Text, out double b)) return;
		if (item.IsAnchor)
		{
			PersonalSyncAnchor? anchor = profile.Anchors.FirstOrDefault(value => value.Id == item.Id);
			if (anchor == null) return;
			anchor.PlaybackSeconds = a;
			anchor.LyricsSeconds = b;
			profile.Anchors = profile.Anchors.OrderBy(value => value.PlaybackSeconds).ToList();
		}
		else
		{
			if (!TryParse(_lyricsBox.Text, out double lyrics) || b <= a) return;
			PersonalSyncSegment? segment = profile.Segments.FirstOrDefault(value => value.Id == item.Id);
			if (segment == null) return;
			segment.PlaybackStartSeconds = a;
			segment.PlaybackEndSeconds = b;
			segment.LyricsTimeSeconds = lyrics;
			profile.Segments = profile.Segments.OrderBy(value => value.PlaybackStartSeconds).ToList();
		}
		await _store.UpsertAsync(profile);
		await RefreshAsync(profile.Id);
	}

	private async Task EditOffsetAsync(double delta)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null) return;
		profile.OffsetSeconds = Math.Round(Math.Clamp(profile.OffsetSeconds + delta, -3600, 3600), 3);
		if (profile.Mode == PersonalSyncMode.None) profile.Mode = PersonalSyncMode.Offset;
		await _store.UpsertAsync(profile);
		await RefreshAsync(profile.Id);
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
		if (profile == null || _timeline.ActualWidth <= 1) return;
		double width = _timeline.ActualWidth;
		double duration = Math.Max(1, profile.Track.DurationSeconds);
		_timeline.Children.Add(new Line { X1 = 16, X2 = width - 16, Y1 = 48, Y2 = 48, Stroke = Brush(99, 94, 101), StrokeThickness = 3 });
		foreach (PersonalSyncSegment segment in profile.Segments)
		{
			double x1 = X(segment.PlaybackStartSeconds, duration, width), x2 = X(segment.PlaybackEndSeconds, duration, width);
			Rectangle range = new() { Width = Math.Max(3, x2 - x1), Height = 18, Fill = Accent(), Opacity = .38 };
			Canvas.SetLeft(range, x1);
			Canvas.SetTop(range, 39);
			_timeline.Children.Add(range);
		}
		foreach (PersonalSyncAnchor anchor in profile.Anchors)
		{
			double x = X(anchor.PlaybackSeconds, duration, width);
			_timeline.Children.Add(new Line { X1 = x, X2 = x, Y1 = 24, Y2 = 66, Stroke = Accent(), StrokeThickness = 1.5 });
			Ellipse point = new() { Width = 12, Height = 12, Fill = Accent(), Stroke = Brushes.White, StrokeThickness = 1 };
			Canvas.SetLeft(point, x - 6);
			Canvas.SetTop(point, 42);
			_timeline.Children.Add(point);
		}
		TextBlock start = new() { Text = "0:00", Foreground = Muted(), FontFamily = new FontFamily("Consolas"), FontSize = 9 };
		TextBlock end = new() { Text = Format(duration), Foreground = Muted(), FontFamily = new FontFamily("Consolas"), FontSize = 9 };
		Canvas.SetLeft(start, 12); Canvas.SetTop(start, 67);
		Canvas.SetLeft(end, Math.Max(12, width - 58)); Canvas.SetTop(end, 67);
		_timeline.Children.Add(start); _timeline.Children.Add(end);
	}

	private static string Summary(PersonalSyncProfile profile) => profile.Mode == PersonalSyncMode.Advanced
		? $"{profile.Anchors.Count} POINT / {profile.Segments.Count} HOLD"
		: profile.OffsetSeconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " s";
	private static double X(double seconds, double duration, double width) => 16 + Math.Clamp(seconds / duration, 0, 1) * Math.Max(1, width - 32);
	private static string Format(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f", CultureInfo.InvariantCulture);

	private static bool TryParse(string text, out double seconds)
	{
		seconds = 0;
		string value = text.Trim();
		if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)) return seconds >= 0 && double.IsFinite(seconds);
		string[] parts = value.Split(':');
		if (parts.Length is < 2 or > 3) return false;
		double total = 0;
		foreach (string part in parts)
		{
			if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return false;
			total = total * 60 + number;
		}
		seconds = total;
		return seconds >= 0 && double.IsFinite(seconds);
	}

	private static void AddLabel(Grid grid, int row, string text)
	{
		TextBlock label = new() { Text = text, Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center };
		Grid.SetRow(label, row); grid.Children.Add(label);
	}

	private static TextBox AddBox(Grid grid, int row)
	{
		TextBox box = new() { Margin = new Thickness(3), Padding = new Thickness(7, 5, 7, 5), Background = Brush(245, 244, 245), Foreground = Brush(26, 24, 27), FontFamily = new FontFamily("Consolas") };
		Grid.SetRow(box, row); Grid.SetColumn(box, 1); grid.Children.Add(box); return box;
	}

	private string L(string ja, string en) => _language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? ja : en;
	private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
	private static SolidColorBrush Accent() => Brush(255, 107, 44);
	private static SolidColorBrush Muted() => Brush(181, 176, 181);
	private static Button Button(string text) => new() { Content = text, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(3), Background = Brush(49, 46, 50), Foreground = Brush(238, 235, 237), BorderBrush = Brush(89, 83, 91), BorderThickness = new Thickness(1) };
	private sealed record PointItem(Guid Id, bool IsAnchor, string Label) { public override string ToString() => Label; }
}
