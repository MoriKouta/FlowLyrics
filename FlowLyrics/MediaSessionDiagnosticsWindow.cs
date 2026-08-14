using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;

namespace FlowLyrics;

public sealed class MediaSessionDiagnosticsWindow : Window
{
	private readonly MediaSessionService _mediaSessionService;

	private readonly Func<PlaybackSnapshot?> _currentSnapshotProvider;

	private readonly string _language;

	private readonly ListBox _sessionList;

	private readonly TextBox _detailBox;

	private readonly TextBlock _summaryText;

	private readonly DispatcherTimer _liveTimer;

	private IReadOnlyList<MediaSessionInfo> _sessions = Array.Empty<MediaSessionInfo>();

	private bool _refreshing;

	public MediaSessionDiagnosticsWindow(MediaSessionService mediaSessionService, Func<PlaybackSnapshot?> currentSnapshotProvider, string language)
	{
		_mediaSessionService = mediaSessionService;
		_currentSnapshotProvider = currentSnapshotProvider;
		_language = LocalizationService.NormalizeLanguage(language);
		Title = "FlowLyrics · Media Session Diagnostics";
		Width = 920.0;
		Height = 620.0;
		MinWidth = 700.0;
		MinHeight = 460.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 23, 25));
		Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 231, 232));

		Grid root = new Grid { Margin = new Thickness(16.0) };
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(310.0) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12.0) });
		root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

		DockPanel header = new DockPanel { LastChildFill = true, Margin = new Thickness(0.0, 0.0, 0.0, 12.0) };
		WrapPanel buttons = new WrapPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
		Button refreshButton = CreateButton(T("Refresh"));
		refreshButton.Click += async delegate { await RefreshAsync(); };
		Button copyButton = CreateButton(T("Copy diagnostics"));
		copyButton.Margin = new Thickness(7.0, 0.0, 0.0, 0.0);
		copyButton.Click += CopyDiagnostics_Click;
		buttons.Children.Add(refreshButton);
		buttons.Children.Add(copyButton);
		DockPanel.SetDock(buttons, Dock.Right);
		header.Children.Add(buttons);
		_summaryText = new TextBlock
		{
			FontSize = 13.0,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center,
			TextWrapping = TextWrapping.Wrap
		};
		header.Children.Add(_summaryText);
		Grid.SetColumnSpan(header, 3);
		root.Children.Add(header);

		_sessionList = new ListBox
		{
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 33, 36)),
			Foreground = Foreground,
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 69, 76)),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(4.0)
		};
		_sessionList.SelectionChanged += delegate { RefreshDetail(); };
		Grid.SetRow(_sessionList, 1);
		root.Children.Add(_sessionList);

		_detailBox = new TextBox
		{
			IsReadOnly = true,
			AcceptsReturn = true,
			TextWrapping = TextWrapping.Wrap,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
			FontFamily = new System.Windows.Media.FontFamily("Consolas"),
			FontSize = 12.0,
			Padding = new Thickness(12.0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 29, 32)),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 229, 231)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 69, 76)),
			BorderThickness = new Thickness(1.0)
		};
		Grid.SetRow(_detailBox, 1);
		Grid.SetColumn(_detailBox, 2);
		root.Children.Add(_detailBox);
		Content = root;

		_liveTimer = new DispatcherTimer(DispatcherPriority.Background)
		{
			Interval = TimeSpan.FromSeconds(1)
		};
		_liveTimer.Tick += async delegate { await RefreshAsync(); };
		_mediaSessionService.SessionsChanged += MediaSessionService_SessionsChanged;
		Loaded += async delegate
		{
			_liveTimer.Start();
			await RefreshAsync();
		};
		Closed += delegate
		{
			_liveTimer.Stop();
			_mediaSessionService.SessionsChanged -= MediaSessionService_SessionsChanged;
		};
	}

	private async Task RefreshAsync()
	{
		if (_refreshing) return;
		_refreshing = true;
		try
		{
			string selectedId = (_sessionList.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? string.Empty;
			_sessions = await _mediaSessionService.GetSessionsAsync();
			_sessionList.Items.Clear();
			foreach (MediaSessionInfo session in _sessions)
			{
				string flags = string.Join(" · ", new[]
				{
					session.PlaybackState.ToString(),
					session.IsSelectedByFlowLyrics ? "SELECTED" : string.Empty,
					session.IsCurrentSession ? "WINDOWS CURRENT" : string.Empty,
					session.IsIgnored ? "IGNORED" : string.Empty
				}.Where(value => value.Length > 0));
				ListBoxItem item = new ListBoxItem
				{
					Tag = session.SessionId,
					Content = new TextBlock
					{
						Text = session.DisplaySourceName + "\n" + ValueOrDash(session.Metadata.TitleRaw) + "\n" + flags,
						TextWrapping = TextWrapping.Wrap,
						Margin = new Thickness(4.0)
					},
					ToolTip = session.SourceAppUserModelId
				};
				_sessionList.Items.Add(item);
				if (string.Equals(session.SessionId, selectedId, StringComparison.Ordinal)) _sessionList.SelectedItem = item;
			}
			if (_sessionList.SelectedItem == null && _sessionList.Items.Count > 0)
			{
				_sessionList.SelectedItem = _sessionList.Items.OfType<ListBoxItem>().FirstOrDefault(item =>
					_sessions.First(session => string.Equals(session.SessionId, item.Tag?.ToString(), StringComparison.Ordinal)).IsSelectedByFlowLyrics)
					?? _sessionList.Items[0];
			}

			PlaybackSnapshot? selected = _currentSnapshotProvider();
			_summaryText.Text = _sessions.Count + " " + T("session(s)") + " · " + T("Selected") + ": " + (selected?.SourceDisplayName ?? T("None"));
			RefreshDetail();
		}
		catch (Exception ex)
		{
			_detailBox.Text = T("Could not read Windows Media Sessions.") + Environment.NewLine + ex.GetType().Name + ": " + ex.Message;
		}
		finally
		{
			_refreshing = false;
		}
	}

	private void RefreshDetail()
	{
		string id = (_sessionList.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? string.Empty;
		MediaSessionInfo? session = _sessions.FirstOrDefault(candidate => string.Equals(candidate.SessionId, id, StringComparison.Ordinal));
		_detailBox.Text = session == null ? T("Select a Media Session to inspect it.") : BuildSessionDiagnostics(session);
	}

	private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			StringBuilder text = new();
			text.AppendLine("FlowLyrics Media Session Diagnostics");
			text.AppendLine("App Version: " + BuildInfo.Version);
			text.AppendLine("Windows Version: " + Environment.OSVersion.VersionString);
			text.AppendLine("Captured UTC: " + DateTimeOffset.UtcNow.ToString("O"));
			text.AppendLine("Session Count: " + _sessions.Count);
			text.AppendLine();
			foreach (MediaSessionInfo session in _sessions)
			{
				text.AppendLine(BuildSessionDiagnostics(session));
				text.AppendLine(new string('-', 72));
			}
			Clipboard.SetText(text.ToString());
			_summaryText.Text = T("Diagnostics copied to the clipboard.");
		}
		catch (Exception ex)
		{
			_summaryText.Text = T("Could not copy diagnostics.") + " " + ex.Message;
		}
	}

	private static string BuildSessionDiagnostics(MediaSessionInfo session)
	{
		SearchMetadataCandidate normalized = MetadataNormalizer.NormalizeForSearch(
			session.Metadata.TitleRaw,
			session.Metadata.ArtistRaw,
			session.Metadata.AlbumRaw);
		StringBuilder text = new();
		text.AppendLine("Display Source: " + session.DisplaySourceName);
		text.AppendLine("SourceAppUserModelId: " + ValueOrDash(session.SourceAppUserModelId));
		text.AppendLine("Session Identifier: " + session.SessionId);
		text.AppendLine("Playback State: " + session.PlaybackState);
		text.AppendLine("Position: " + FormatTime(session.Position));
		text.AppendLine("Duration: " + FormatTime(session.Metadata.Duration));
		text.AppendLine("Timeline Updated UTC: " + FormatDate(session.TimelineUpdatedAtUtc));
		text.AppendLine("Last Activity UTC: " + FormatDate(session.LastActivityUtc));
		text.AppendLine("CurrentSession: " + YesNo(session.IsCurrentSession));
		text.AppendLine("SelectedByFlowLyrics: " + YesNo(session.IsSelectedByFlowLyrics));
		text.AppendLine("Ignored: " + YesNo(session.IsIgnored));
		text.AppendLine();
		text.AppendLine("RAW METADATA");
		text.AppendLine("Title: " + ValueOrDash(session.Metadata.TitleRaw));
		text.AppendLine("Artist: " + ValueOrDash(session.Metadata.ArtistRaw));
		text.AppendLine("Album: " + ValueOrDash(session.Metadata.AlbumRaw));
		text.AppendLine();
		text.AppendLine("NORMALIZED SEARCH METADATA");
		text.AppendLine("Title: " + ValueOrDash(normalized.Title));
		text.AppendLine("Artist: " + ValueOrDash(normalized.Artist));
		text.AppendLine("Album: " + ValueOrDash(normalized.Album));
		text.AppendLine();
		text.AppendLine("CAPABILITIES");
		text.AppendLine("CanPlay: " + YesNo(session.Capabilities.CanPlay));
		text.AppendLine("CanPause: " + YesNo(session.Capabilities.CanPause));
		text.AppendLine("CanTogglePlayPause: " + YesNo(session.Capabilities.CanTogglePlayPause));
		text.AppendLine("CanPrevious: " + YesNo(session.Capabilities.CanPrevious));
		text.AppendLine("CanNext: " + YesNo(session.Capabilities.CanNext));
		text.AppendLine("CanSeek: " + YesNo(session.Capabilities.CanSeek));
		return text.ToString().TrimEnd();
	}

	private void MediaSessionService_SessionsChanged(object? sender, EventArgs e)
	{
		if (!Dispatcher.CheckAccess())
		{
			Dispatcher.BeginInvoke((Action)(async () => await RefreshAsync()));
			return;
		}
		_ = RefreshAsync();
	}

	private static Button CreateButton(string content)
	{
		return new Button
		{
			Content = content,
			Padding = new Thickness(12.0, 7.0, 12.0, 7.0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 45, 49)),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 232, 234)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(89, 83, 91)),
			BorderThickness = new Thickness(1.0)
		};
	}

	private string T(string key) => LocalizationService.Translate(_language, key);

	private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

	private static string YesNo(bool value) => value ? "YES" : "NO";

	private static string FormatTime(TimeSpan value) => value <= TimeSpan.Zero ? "--:--.---" : value.ToString(@"hh\:mm\:ss\.fff");

	private static string FormatDate(DateTimeOffset value) => value == default ? "—" : value.ToUniversalTime().ToString("O");
}
