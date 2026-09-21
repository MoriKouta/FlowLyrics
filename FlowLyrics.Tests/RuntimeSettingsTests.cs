using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FlowLyrics.Core;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class RuntimeSettingsTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-runtime-ui-" + Guid.NewGuid().ToString("N"));
	private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	[Theory]
	[InlineData("en-US")]
	[InlineData("ja-JP")]
	public async Task LoadedSettings_ColorControlsSurviveTabOrderLocalizationPaletteAndReopen(string language)
	{
		AppSettings? saved = null;
		Sta(() =>
		{
			using LyricsService lyrics = new(_directory);
			using MediaSessionService media = new(new EmptyProvider());
			var window = Create(new AppSettings { Language = language }, lyrics, media);
			try
			{
				window.ShowActivated = false; window.Show(); Pump();
				var tabs = Read<TabControl>(window, "SettingsTabs");
				var outline = Read<Slider>(window, "OutlineSlider");
				TabItem color = Ancestor<TabItem>(outline);
				// Neither translated headers nor reordered tabs may be the contract.
				tabs.Items.Remove(color); tabs.Items.Insert(0, color); color.Header = "Changed header";
				tabs.SelectedItem = color; Pump();
				Assert.True(Read<bool>(window, "_glowControlsInitialized"));
				Assert.True(Read<bool>(window, "_paletteManagerInitialized"));
				Assert.True(Read<bool>(window, "_reverseColorsControlInitialized"));
				Assert.True(Read<bool>(window, "_mediaSessionControlsInitialized"));
				Assert.True(Read<bool>(window, "_personalSyncProfilesInitialized"));
				Assert.Single(Logical<Border>(window), item => item.Name == "TextEffectsCard");
				Assert.Single(Logical<Border>(window), item => item.Name == "SurfaceCard");
				Slider blur = Read<Slider>(window, "_glowStrengthSlider"), opacity = Read<Slider>(window, "_glowOpacitySlider");
				Assert.True(blur.IsVisible); Assert.True(opacity.IsVisible);
				UiUxRuntimeTests.Capture(window, "settings-color-" + language);
				Assert.Contains(Logical<TextBlock>(window), item => item.Text == LocalizationService.Translate(language, "Glow Blur"));
				var glow = Read<TextBox>(window, "_glowColorBox");
				glow.Text = "#FF123456"; blur.Value = 17.5; opacity.Value = 0.65;
				Assert.Equal(17.5, window.ResultSettings.GlowStrength);
				Assert.Equal(0.65, window.ResultSettings.GlowOpacity);
				Read<TextBox>(window, "_paletteNameBox").Text = "Runtime palette";
				Invoke(window, "SaveCurrentPalette_Click", window, new RoutedEventArgs());
				glow.Text = "#FFFFFFFF"; blur.Value = 1; opacity.Value = 0.1;
				Invoke(window, "ApplySavedPalette_Click", window, new RoutedEventArgs());
				Assert.Equal("#FF123456", glow.Text); Assert.Equal(17.5, blur.Value); Assert.Equal(0.65, opacity.Value);
				// Same JSON format used by palette export/import, including all glow fields.
				SavedColorPalette palette = window.ResultSettings.SavedColorPalettes.Single();
				SavedColorPalette imported = JsonSerializer.Deserialize<SavedColorPalette>(JsonSerializer.Serialize(palette))!;
				Assert.Equal(palette.GlowColor, imported.GlowColor); Assert.Equal(17.5, imported.GlowStrength); Assert.Equal(0.65, imported.GlowOpacity);
				Invoke(window, "RandomColors_Click", window, new RoutedEventArgs());
				Assert.Equal(window.ResultSettings.GlowColor, glow.Text);
				Invoke(window, "ApplySavedPalette_Click", window, new RoutedEventArgs());
				window.ApplyAndClose(); saved = window.ResultSettings;
			}
			finally { window.Close(); }
		});
		SettingsService settings = new(_directory);
		await settings.SaveAsync(saved!);
		AppSettings restored = new SettingsService(_directory).Load();
		Assert.Equal("#FF123456", restored.GlowColor); Assert.Equal(17.5, restored.GlowStrength); Assert.Equal(0.65, restored.GlowOpacity);
		Sta(() =>
		{
			using LyricsService lyrics = new(_directory);
			using MediaSessionService media = new(new EmptyProvider());
			var window = Create(restored, lyrics, media);
			try { window.ShowActivated = false; window.Show(); Pump(); Assert.Equal(17.5, Read<Slider>(window, "_glowStrengthSlider").Value); }
			finally { window.Close(); }
		});
	}

	[Fact]
	public void NewLabels_AreAvailableForAllSupportedLanguages()
	{
		foreach (var language in LocalizationService.Languages.Where(item => item.Code != "en-US"))
			foreach (string key in new[] { "Global offset", "Timing editor", "Adjustment points", "Align to now", "Resume here", "Start lyric hold", "Hold in progress. Choose the lyric to resume.", "Drag to the current position to align", "Changes are saved when you close.", "Not set", "Stop using", "Adjust lyric timing", "Use Local LRC for this track", "Lyrics source and details", "Sync history", "Glow Blur", "Glow Opacity", "TEXT EFFECTS", "SURFACE", "Load ID", "Enter a positive LRCLIB ID.",
				"LRCLIB search results may remain cached after a new submission. If you know the LRCLIB ID, load it directly." })
				Assert.NotEqual(key, LocalizationService.Translate(language.Code, key));
	}

	private SettingsWindow Create(AppSettings settings, LyricsService lyrics, MediaSessionService media) =>
		new(settings, _directory, lyrics, media, new PersonalSyncStore(_directory), () => null,
			() => new TrackInfo("Song", "Artist", "Album", TimeSpan.FromSeconds(240)), () => null, () => null, () => Task.CompletedTask);

	internal static T Read<T>(object instance, string field) => (T)instance.GetType().GetField(field, Private)!.GetValue(instance)!;
	internal static object? Invoke(object instance, string method, params object[] args) => instance.GetType().GetMethod(method, Private)!.Invoke(instance, args);
	internal static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
	{
		foreach (object child in LogicalTreeHelper.GetChildren(root))
		{
			if (child is T match) yield return match;
			if (child is DependencyObject element) foreach (T item in Logical<T>(element)) yield return item;
		}
	}
	private static T Ancestor<T>(FrameworkElement element) where T : FrameworkElement => element.Parent is T match ? match : Ancestor<T>((FrameworkElement)element.Parent);
	internal static void Pump()
	{
		DispatcherFrame frame = new();
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
		Dispatcher.PushFrame(frame);
	}
	internal static void Sta(Action action)
	{
		Exception? failure = null;
		Thread thread = new(() =>
		{
			SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
			try { action(); } catch (Exception ex) { failure = ex; } finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
		}) { IsBackground = true };
		thread.SetApartmentState(ApartmentState.STA); thread.Start();
		Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Runtime UI test timed out.");
		Assert.Null(failure);
	}
	internal sealed class EmptyProvider : IMediaSessionProvider
	{
		public event EventHandler? SessionsChanged { add { } remove { } }
		public Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MediaSessionInfo>>(Array.Empty<MediaSessionInfo>());
		public Task<bool> TryTogglePlayPauseAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
		public Task<bool> TrySkipNextAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
		public Task<bool> TrySkipPreviousAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
		public Task<bool> TrySeekAsync(string sessionId, TimeSpan position, CancellationToken cancellationToken = default) => Task.FromResult(false);
		public void Dispose() { }
	}
	public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
