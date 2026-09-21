using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;
using static FlowLyrics.Tests.PersonalSyncRuntimeTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class CandidateRuntimeTests
{
	[Fact]
	public void LoadedIdControls_PreviewAndManualUseWithoutSearch()
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-id-runtime-" + Guid.NewGuid().ToString("N"));
		try
		{
			Sta(() =>
			{
				using var handler = new LrclibRefreshTests.Handler();
				using LyricsService service = new(directory, handler);
				CandidateSearchWindow window = new(new("Different title", "Artist", "Album", TimeSpan.FromSeconds(240)), service, "en-US", true, "#FFFF6B2C", false);
				window.ShowActivated = false; window.Show(); Pump();
				try
				{
					WaitUntil(() => !Read<bool>(window, "_isSearching"));
					Assert.Contains(Visuals<TextBlock>(window), item => item.Text.StartsWith("LRCLIB search results may remain cached"));
					TextBox id = Read<TextBox>(window, "_recordIdBox"); Button load = Read<Button>(window, "_loadIdButton");
					Assert.True(id.IsVisible); Assert.True(load.IsVisible);
					Assert.Same(Read<Button>(window, "SearchButton").Template, load.Template);
					Assert.Equal(Read<Button>(window, "SearchButton").MinHeight, load.MinHeight);
					int requests = handler.Urls.Count;
					id.Text = "invalid"; load.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					Assert.Equal("Enter a positive LRCLIB ID.", Read<TextBlock>(window, "StatusText").Text);
					Assert.Equal(requests, handler.Urls.Count);
					id.Text = "123"; load.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					WaitUntil(() => load.IsEnabled);
					Assert.Equal("/api/get/123", Assert.Single(handler.Urls.Skip(requests)));
					ItemsControl results = Read<ItemsControl>(window, "ResultsList");
					Assert.Single(results.Items); Pump();
					UiUxRuntimeTests.Capture(window, "lrclib-direct-id");
					Button preview = Visuals<Button>(window).Single(button => button.Content?.ToString() == "Preview");
					Assert.Same(load.Template, preview.Template);
					Assert.DoesNotContain(Visuals<TextBlock>(window), item => item.Text.Contains("System.Windows.Documents.Run"));
					preview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
					Window previewWindow = Assert.Single(window.OwnedWindows.Cast<Window>());
					Assert.IsType<LyricsPreviewWindow>(previewWindow); Assert.True(previewWindow.IsVisible);
					UiUxRuntimeTests.Capture(previewWindow, "lyrics-preview"); previewWindow.Close();
					Button use = Visuals<Button>(window).Single(button => button.Content?.ToString() == "Use these lyrics");
					Assert.True(use.IsEnabled); use.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					WaitUntil(() => !window.IsVisible);
					Assert.True(window.SelectedResult!.SelectedManually);
					Assert.Equal(123, window.SelectedResult.LrclibRecord!.Id);
					Assert.All(handler.Urls.Skip(requests), url => Assert.Equal("/api/get/123", url));
				}
				finally { window.Close(); }
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	internal static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
	{
		if (root is T match) yield return match;
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
			foreach (T child in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
	}
}
