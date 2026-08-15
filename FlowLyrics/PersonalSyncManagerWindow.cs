using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowLyrics.Models;
using FlowLyrics.Services;

namespace FlowLyrics;

public sealed class PersonalSyncManagerWindow : Window
{
	private readonly PersonalSyncStore _store;
	private readonly string _language;
	private readonly ListBox _list;
	private readonly TextBlock _details;
	private PersonalSyncProfile[] _profiles = Array.Empty<PersonalSyncProfile>();

	public PersonalSyncManagerWindow(PersonalSyncStore store, string language)
	{
		_store = store;
		_language = language;
		Title = "FlowLyrics · " + L("歌詞タイミング調整", "Personal Sync profiles");
		Width = 760;
		Height = 540;
		MinWidth = 620;
		MinHeight = 420;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ShowInTaskbar = false;
		Background = Brush(25, 23, 26);
		Foreground = Brush(235, 232, 234);

		Grid root = new() { Margin = new Thickness(18) };
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		Content = root;

		TextBlock heading = new() { Text = "PERSONAL SYNC", Foreground = Accent(), FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) };
		Grid.SetColumnSpan(heading, 3);
		root.Children.Add(heading);
		_list = new ListBox { Background = Brush(35, 33, 36), Foreground = Foreground, BorderBrush = Brush(74, 69, 76), Padding = new Thickness(4) };
		_list.SelectionChanged += delegate { RefreshDetails(); };
		Grid.SetRow(_list, 1);
		root.Children.Add(_list);
		_details = new TextBlock { Background = Brush(31, 29, 32), Padding = new Thickness(14), TextWrapping = TextWrapping.Wrap, Foreground = Foreground };
		Grid.SetRow(_details, 1);
		Grid.SetColumn(_details, 2);
		root.Children.Add(_details);

		WrapPanel buttons = new() { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
		Button earlier = Button(L("0.1秒 早く", "0.1s earlier"));
		Button later = Button(L("0.1秒 遅く", "0.1s later"));
		Button reset = Button(L("RESET", "RESET"));
		Button delete = Button(L("DELETE", "DELETE"));
		Button close = Button(L("閉じる", "Close"));
		earlier.Click += async delegate { await EditOffsetAsync(-0.1); };
		later.Click += async delegate { await EditOffsetAsync(0.1); };
		reset.Click += Reset_Click;
		delete.Click += Delete_Click;
		close.Click += delegate { Close(); };
		buttons.Children.Add(earlier);
		buttons.Children.Add(later);
		buttons.Children.Add(reset);
		buttons.Children.Add(delete);
		buttons.Children.Add(close);
		Grid.SetRow(buttons, 2);
		Grid.SetColumnSpan(buttons, 3);
		root.Children.Add(buttons);
		Loaded += async delegate { await RefreshAsync(); };
	}

	private async Task RefreshAsync(Guid? selectedId = null)
	{
		_profiles = (await _store.ListAsync()).ToArray();
		_list.Items.Clear();
		foreach (PersonalSyncProfile profile in _profiles)
		{
			ListBoxItem item = new()
			{
				Tag = profile.Id,
				Content = profile.Track.Title + "\n" + profile.Track.Artist + "\n" +
					(profile.Scope == PersonalSyncScope.Source ? profile.Source.Source : L("すべての再生元", "Every source")) + "  ·  " +
					(profile.Mode == PersonalSyncMode.Advanced ? "Advanced" : profile.OffsetSeconds.ToString("+0.0;-0.0;0.0") + "s"),
				Padding = new Thickness(5)
			};
			_list.Items.Add(item);
			if (selectedId == profile.Id) _list.SelectedItem = item;
		}
		if (_list.SelectedItem == null && _list.Items.Count > 0) _list.SelectedIndex = 0;
		RefreshDetails();
	}

	private PersonalSyncProfile? Selected() => _list.SelectedItem is ListBoxItem item && item.Tag is Guid id
		? _profiles.FirstOrDefault(profile => profile.Id == id) : null;

	private void RefreshDetails()
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null) { _details.Text = L("保存済み調整はありません。", "No saved profiles."); return; }
		_details.Text = $"{profile.Track.Title}\n{profile.Track.Artist}\n{profile.Track.Album}\n\n" +
			$"Source: {(profile.Scope == PersonalSyncScope.Source ? profile.Source.Source : "ALL")}\n" +
			$"Lyrics: {profile.Lyrics.DisplayName}\nMode: {profile.Mode}\nOffset: {profile.OffsetSeconds:+0.000;-0.000;0.000}s\n" +
			$"Sync points: {profile.Anchors.Count}\nHold ranges: {profile.Segments.Count}\n\n" +
			L("ここでの0.1秒調整は同期点・停止区間を保持します。", "The 0.1s controls keep existing sync points and hold ranges.");
	}

	private async Task EditOffsetAsync(double delta)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null) return;
		profile.OffsetSeconds = Math.Round(Math.Clamp(profile.OffsetSeconds + delta, -3600, 3600), 3);
		await _store.UpsertAsync(profile);
		await RefreshAsync(profile.Id);
	}

	private async void Reset_Click(object sender, RoutedEventArgs e)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null) return;
		profile.Mode = PersonalSyncMode.Offset;
		profile.OffsetSeconds = 0;
		profile.Anchors.Clear();
		profile.Segments.Clear();
		await _store.UpsertAsync(profile);
		await RefreshAsync(profile.Id);
	}

	private async void Delete_Click(object sender, RoutedEventArgs e)
	{
		PersonalSyncProfile? profile = Selected();
		if (profile == null || MessageBox.Show(this, L("このProfileを削除しますか？", "Delete this profile?"), "FlowLyrics", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
		await _store.DeleteAsync(profile.Id);
		await RefreshAsync();
	}

	private string L(string ja, string en) => _language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? ja : en;
	private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
	private static SolidColorBrush Accent() => Brush(255, 107, 44);
	private static Button Button(string text) => new()
	{
		Content = text,
		Padding = new Thickness(11, 6, 11, 6),
		Margin = new Thickness(3),
		Background = Brush(49, 46, 50),
		Foreground = Brush(238, 235, 237),
		BorderBrush = Brush(89, 83, 91),
		BorderThickness = new Thickness(1)
	};
}
