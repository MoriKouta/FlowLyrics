using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;
using static FlowLyrics.Tests.PersonalSyncRuntimeTests;
using static FlowLyrics.Tests.CandidateRuntimeTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class LocalizationRuntimeTests
{
	[Fact]
	public void EmbeddedDotFont_ResolvesToActualGlyphs()
	{
		Sta(() =>
		{
			_ = new Window(); // Initialize WPF's pack URI parser before resolving a resource font.
			var family = (FontFamily)typeof(LocalizationService).Assembly.GetType("FlowLyrics.Services.LocalizedUiFont")!.GetProperty("EnglishDotFont")!.GetValue(null)!;
			var details = family.GetTypefaces().Select(face =>
				(face.TryGetGlyphTypeface(out var glyph) ? glyph.FontUri.ToString() : "composite")).ToArray();
			string? audit = Environment.GetEnvironmentVariable("FLOWLYRICS_UI_CAPTURE_DIR");
			if (audit != null) File.WriteAllLines(Path.Combine(audit, "font-resolution.txt"), details);
			Assert.Contains(details, detail => detail.Contains("flowdots.ttf", StringComparison.OrdinalIgnoreCase) && !detail.EndsWith("composite"));
		});
	}
	public static IEnumerable<object[]> Languages => LocalizationService.Languages.Select(language => new object[] { language.Code });

	[Fact]
	public void WindowKeys_HaveExplicitTranslationsInEverySupportedLanguage()
	{
		Assert.Equal(10, LocalizationService.Languages.Count);
		DirectoryInfo root = new(AppContext.BaseDirectory);
		while (!File.Exists(Path.Combine(root.FullName, "FlowLyrics.csproj"))) root = root.Parent!;
		var keys = Directory.GetFiles(Path.Combine(root.FullName, "FlowLyrics"), "*Window.cs")
			.SelectMany(file => Regex.Matches(File.ReadAllText(file), "\\bT\\(\"([^\"\\r\\n]+)\"\\)").Select(match => Regex.Unescape(match.Groups[1].Value)))
			.Concat(Regex.Matches(File.ReadAllText(Path.Combine(root.FullName, "FlowLyrics.Services", "EditorUiTranslations.cs")), "\\[\"([^\"]+)\"\\] = ").Select(match => match.Groups[1].Value)).Distinct().ToArray();
		var missing = from language in LocalizationService.Languages.Skip(1) from key in keys
			where !LocalizationService.HasTranslation(language.Code, key) select language.Code + ": " + key;
		string[] absent = missing.ToArray();
		string? audit = Environment.GetEnvironmentVariable("FLOWLYRICS_UI_CAPTURE_DIR");
		if (audit != null) { Directory.CreateDirectory(audit); File.WriteAllLines(Path.Combine(audit, "missing-keys.txt"), absent); }
		Assert.Empty(absent);
		Assert.False(LocalizationService.HasTranslation("ja-JP", "not-a-translation-key"));
	}

	[Theory]
	[MemberData(nameof(Languages))]
	public void NativeWindows_UseLocalizedFontsAndCompleteEditorLabels(string language)
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-localization-" + Guid.NewGuid().ToString("N"));
		try
		{
			Sta(() =>
			{
				_ = new Window();
				Type fontResolver = typeof(LocalizationService).Assembly.GetType("FlowLyrics.Services.LocalizedUiFont")!;
				FontFamily dotFont = (FontFamily)fontResolver.GetProperty("EnglishDotFont")!.GetValue(null)!;
				FontFamily expected = (FontFamily)fontResolver.GetMethod("Resolve")!.Invoke(null, [language, dotFont])!;
				Assert.Contains(language switch { "en-US" => "Flow Dots", "ja-JP" => "Yu Gothic UI", "zh-CN" => "Microsoft YaHei UI",
					"zh-TW" => "Microsoft JhengHei UI", "ko-KR" => "Malgun Gothic", _ => "Segoe UI" }, expected.Source);
				if (language == "en-US") Assert.Contains(expected.GetTypefaces(), typeface => typeface.TryGetGlyphTypeface(out GlyphTypeface glyph) && glyph.FontUri.ToString().Contains("flowdots", StringComparison.OrdinalIgnoreCase));
				using LyricsService lyrics = new(directory, new LrclibRefreshTests.Handler());
				using MediaSessionService media = new(new EmptyProvider());
				TrackInfo track = new("Title", "Artist", "Album", TimeSpan.FromSeconds(120));
				LyricLine[] lines = [new(TimeSpan.FromSeconds(20), "A line to align with the current position"), new(TimeSpan.FromSeconds(24), "次の歌詞 / The next lyric"), new(TimeSpan.FromSeconds(28), "One more line")];
				LyricsLookupResult lookup = new() { Status = LyricsLookupStatus.LrclibAuto, Lyrics = new(lines, null, "LRCLIB"), LrclibRecord = new() { Id = 123 } };
				PlaybackSnapshot snapshot = new(track, TimeSpan.FromSeconds(36), false, DateTimeOffset.UtcNow);
				PersonalSyncContext context = PersonalSyncIdentity.Create(snapshot, lookup);
				PersonalSyncStore store = new(directory);
				PersonalSyncProfile profile = new() { Track = context.Track, Source = context.Source, Lyrics = context.Lyrics, Mode = PersonalSyncMode.Advanced,
					OffsetSeconds = 16, Anchors = [new() { PlaybackSeconds = 40, LyricsSeconds = 24 }], Segments = [new() { PlaybackStartSeconds = 30, PlaybackEndSeconds = 34, LyricsTimeSeconds = 14 }] };
				Task.Run(() => store.UpsertAsync(profile)).GetAwaiter().GetResult();
				PersonalSyncWindow sync = new(store, context, profile, lines, () => 0, () => snapshot.Position, language);
				Show(sync, "sync", () =>
				{
					Assert.Equal(expected.Source, sync.FontFamily.Source);
					Assert.Equal("ALIGN FROM HERE", Read<Button>(sync, "_resyncButton").Content);
					AssertHeading(sync, "PERSONAL SYNC"); AssertHeading(sync, "TIMING EDITOR");
					Assert.Contains("Flow Dots", Logical<Button>(sync).First(button => Equals(button.Content, "+0.5s")).FontFamily.Source);
					Assert.Contains(Logical<TextBlock>(sync), label => label.Text == LocalizationService.Translate(language, "Global offset"));
					sync.Width = 760; Pump();
					foreach (Button action in Visuals<Button>(sync).Where(button => button.Name == "AlignLyricButton")) Assert.InRange(action.ActualWidth, 1, 148);
					Visuals<Button>(sync).Single(button => button.Name == "TimingDetailsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
					Capture(sync, "sync-narrow");
					sync.Width = 1120; Pump();
				});
				Show(new PersonalSyncManagerWindow(store, language), "manager");
				Show(new MediaSessionDiagnosticsWindow(media, () => snapshot, () => null, language), "diagnostics");
				Show(new CandidateSearchWindow(track, lyrics, language, true, "#FFFF6B2C", false), "candidates", () =>
					WaitUntil(() => !Read<bool>(lastWindow!, "_isSearching")));
				Show(new LyricsPreviewWindow(track, new() { Record = new() { Id = 123, TrackName = track.Title, ArtistName = track.Artist,
					AlbumName = track.Album, Duration = 120, SyncedLyrics = "[00:20]First lyric\n[00:24]次の歌詞" } }, language, "#FFFF6B2C"), "preview");
				SettingsWindow settings = new(new() { Language = language }, directory, lyrics, media, store, () => snapshot, () => track, () => lookup, () => null, () => Task.CompletedTask);
				Show(settings, "settings", () =>
				{
					AssertHeading(settings, "PLAYER"); AssertHeading(settings, "CURRENT TRACK"); AssertHeading(settings, "PERSONAL SYNC");
					Assert.Equal(track.Title, Read<TextBlock>(settings, "CurrentTrackTitleText").Text);
					Assert.Equal(track.Artist, Read<TextBlock>(settings, "CurrentTrackArtistText").Text);
					Assert.Equal("AUTO", Read<TextBlock>(settings, "SelectionModeText").Text);
					Assert.Contains("Flow Dots", Read<TextBlock>(settings, "SelectionModeText").FontFamily.Source);
					var missing = new List<string>();
					foreach (string field in new[] { "_localizedText", "_localizedContent", "_localizedHeaders" })
					{
						var values = (System.Collections.IDictionary)typeof(SettingsWindow).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(settings)!;
						foreach (string key in values.Values)
							if (!LocalizationService.HasTranslation(language, key) && Regex.IsMatch(key, "[A-Za-z]{3}")) missing.Add(key);
					}
					string? audit = Environment.GetEnvironmentVariable("FLOWLYRICS_UI_CAPTURE_DIR");
					if (audit != null) { Directory.CreateDirectory(audit); File.WriteAllLines(Path.Combine(audit, "settings-keys-" + language + ".txt"), missing.Distinct()); }
					var tabs = Read<TabControl>(settings, "SettingsTabs");
					foreach (TabItem tab in tabs.Items) { tabs.SelectedItem = tab; Pump(); Capture(settings, "settings-" + tabs.SelectedIndex); }
					// Reusing the same window must update runtime-created labels and fonts.
					foreach (string selectedLanguage in new[] { "ja-JP", "en-US", "ja-JP" })
					{
						Invoke(settings, "ApplyLanguage", selectedLanguage); Pump();
						Assert.Contains("Flow Dots", ((FontFamily)settings.Resources["DotFont"]).Source);
						AssertHeading(settings, "PLAYER"); AssertHeading(settings, "CURRENT TRACK");
					}
					Assert.Contains("Yu Gothic UI", Read<Button>(settings, "_openSyncButton").FontFamily.Source);
					Invoke(settings, "ApplyLanguage", language); Pump();
					Assert.Equal(track.Title, Read<TextBlock>(settings, "CurrentTrackTitleText").Text);
					Assert.Equal(LocalizationService.Translate(language, "Sync history"), Logical<Button>(settings).Single(button => button.Content?.ToString() == LocalizationService.Translate(language, "Sync history")).Content);
				});
				MainWindow main = new(new SettingsService(directory), media);
				try
				{
					main.ShowActivated = false; main.Show(); Pump();
					Read<AppSettings>(main, "_settings").Language = language; Invoke(main, "ApplyVisualSettings");
					Assert.Equal("MEDIA SESSION / " + LocalizationService.Translate(language, "Waiting"), Read<TextBlock>(main, "TrackStatusText").Text);
					foreach ((string status, string label) in new[] { ("LRCLIB — AUTO SELECTED", "LRCLIB — AUTO"), ("CACHE", "CACHE"), ("LRCLIB — MANUALLY SELECTED", "LRCLIB — MANUAL"), ("LRCLIB — BEST MATCH", "LRCLIB — BEST MATCH"), ("LOCAL LRC", "LOCAL LRC") })
					{
						Invoke(main, "SetTrackStatus", status, Colors.White);
						Assert.Equal("MEDIA SESSION / " + label, Read<TextBlock>(main, "TrackStatusText").Text);
					}
					foreach (FieldInfo field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
						if (field.GetValue(main) is DispatcherTimer timer) timer.Stop();
					main.WindowState = WindowState.Normal; main.Width = 856; main.Height = 616; main.Left = 80; main.Top = 80; main.Show(); Pump();
					Assert.True(main.ActualWidth > 400 && main.ActualHeight > 300);
					Assert.Contains("Flow Dots", ((FontFamily)main.Resources["DotFont"]).Source);
					Assert.Equal(expected.Source, ((FontFamily)main.Resources["UiFont"]).Source);
					main.Background = Brushes.Black; Pump(); Capture(main, "main");
				}
				finally
				{
					Read<IDisposable?>(main, "_hotkeys")?.Dispose(); Read<IDisposable?>(main, "_tray")?.Dispose(); Read<LyricsService>(main, "_lyricsService").Dispose();
					typeof(MainWindow).GetField("_allowClose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true); main.Close();
				}

				void Capture(Window window, string name)
				{
					window.UpdateLayout(); UiUxRuntimeTests.Capture(window, name + "-" + language);
					GlowOverlayTests.CaptureNative(window, name + "-" + language);
				}
				void Show(Window window, string name, Action? inspect = null)
				{
					lastWindow = window;
					try { window.ShowActivated = false; window.Show(); Pump(); inspect?.Invoke(); if (language == "en-US") Assert.Contains("Flow Dots", window.FontFamily.Source); else Assert.Equal(expected.Source, window.FontFamily.Source); Capture(window, name); }
					finally { window.Close(); Pump(); }
				}
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
	private Window? lastWindow;
	private static void AssertHeading(Window window, string english)
	{
		TextBlock heading = Assert.Single(Logical<TextBlock>(window), text => text.Text == english && Equals(text.Tag, "VisualHeading"));
		Assert.Contains("Flow Dots", heading.FontFamily.Source);
		Assert.Contains(heading.FontFamily.GetTypefaces(), face => face.TryGetGlyphTypeface(out GlyphTypeface glyph) && glyph.FontUri.ToString().Contains("flowdots", StringComparison.OrdinalIgnoreCase));
	}
}
