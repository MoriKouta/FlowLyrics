using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FlowLyrics.Models;
using FlowLyrics.Services;
using Xunit;
using static FlowLyrics.Tests.RuntimeSettingsTests;
using static FlowLyrics.Tests.PersonalSyncRuntimeTests;

namespace FlowLyrics.Tests;

[Collection("WPF UI")]
public sealed class PlayerVisibilityTests
{
	[Fact]
	public void MissingSettings_DefaultToHidden_VolumeUnchanged()
	{
		var legacy = JsonSerializer.Deserialize<AppSettings>("{\"SettingsSchemaVersion\":17,\"ShowPlaybackControls\":true}")!;
		Assert.False(legacy.ShowShuffleButton || legacy.ShowRepeatButton || legacy.ShowReverseButton || legacy.ShowPersonalSyncButton);
		Assert.True(legacy.ShowVolumeButton);
		var fresh = new AppSettings();
		Assert.False(fresh.ShowShuffleButton || fresh.ShowRepeatButton || fresh.ShowReverseButton || fresh.ShowPersonalSyncButton);
		Assert.True(fresh.ShowVolumeButton);
		legacy.ShowShuffleButton = legacy.ShowRepeatButton = legacy.ShowReverseButton = legacy.ShowPersonalSyncButton = true;
		var copy = legacy.Clone(); copy.Normalize();
		Assert.True(copy.ShowShuffleButton && copy.ShowRepeatButton && copy.ShowReverseButton && copy.ShowPersonalSyncButton && copy.ShowVolumeButton);
		Assert.Equal(17, copy.SettingsSchemaVersion); Assert.True(copy.ShowPlaybackControls);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async System.Threading.Tasks.Task ExplicitVisibility_SurvivesSettingsLoadSave(bool visible)
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-defaults-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			SettingsService service = new(directory);
			File.WriteAllText(service.SettingsPath, "{\"SettingsSchemaVersion\":16,\"ShowRepeatButton\":" + visible.ToString().ToLowerInvariant() +
				",\"ShowReverseButton\":" + visible.ToString().ToLowerInvariant() + ",\"ShowPersonalSyncButton\":" + visible.ToString().ToLowerInvariant() + "}");
			var settings = service.Load();
			Assert.False(settings.ShowShuffleButton);
			Assert.Equal(visible, settings.ShowRepeatButton); Assert.Equal(visible, settings.ShowReverseButton); Assert.Equal(visible, settings.ShowPersonalSyncButton);
			settings.ShowShuffleButton = visible; await service.SaveAsync(settings);
			var reopened = service.Load();
			Assert.Equal(visible, reopened.ShowShuffleButton); Assert.Equal(visible, reopened.ShowRepeatButton);
			Assert.Equal(visible, reopened.ShowReverseButton); Assert.Equal(visible, reopened.ShowPersonalSyncButton); Assert.True(reopened.ShowVolumeButton);
		}
		finally { Directory.Delete(directory, true); }
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void UtilityControls_PreviewIndependently_ThenCommitOnButtonOrTitleBarClose(bool save)
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-visibility-" + Guid.NewGuid().ToString("N"));
		try
		{
			Sta(() =>
			{
				using MediaSessionService media = new(new EmptyProvider());
				SettingsService settingsService = new(directory);
				System.Threading.Tasks.Task.Run(() => settingsService.SaveAsync(new() { ShowShuffleButton = true, ShowRepeatButton = true, ShowReverseButton = true, ShowPersonalSyncButton = true })).GetAwaiter().GetResult();
				MainWindow main = new(settingsService, media);
				try
				{
					main.ShowActivated = false; main.Show(); Pump(); StopTimers();
					Invoke(main, "OpenSettings"); Pump();
					var settings = Read<SettingsWindow>(main, "_settingsWindow");
					string[] fields = ["_shuffleButton", "_repeatButton", "_reverseColorsButton", "_personalSyncButton", "VolumeButton"];
					string[] options = ["_showShuffleButtonBox", "_showRepeatButtonBox", "_showReverseButtonBox", "_showPersonalSyncButtonBox", "_showVolumeButtonBox"];
					for (int i = 0; i < fields.Length; i++)
					{
						Read<CheckBox>(settings, options[i]).IsChecked = false; Pump();
						for (int j = 0; j < fields.Length; j++) Assert.Equal(j <= i ? Visibility.Collapsed : Visibility.Visible, Read<Button>(main, fields[j]).Visibility);
						Assert.Equal(Visibility.Visible, Read<Button>(main, "LockButton").Visibility);
						Assert.Equal(Visibility.Visible, Read<Button>(main, "SettingsButton").Visibility);
						Assert.True(Read<AppSettings>(main, "_settings").ShowPlaybackControls);
					}
					Assert.Contains(Logical<TextBlock>(settings), text => text.Text == "PLAYER CONTROLS");
					Assert.DoesNotContain(Logical<Button>(settings), button => button.IsVisible && button.Content?.ToString() == LocalizationService.Translate(settings.ResultSettings.Language, "Cancel"));
					if (save) Assert.Single(Logical<Button>(settings), button => button.Content?.ToString() == "CLOSE").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					else
					{
						settings.Close();
					}
					Pump(); Assert.True(settings.Accepted);
					foreach (string field in fields) Assert.Equal(Visibility.Collapsed, Read<Button>(main, field).Visibility);
					WaitUntil(() => !settingsService.Load().ShowVolumeButton);
				}
				finally
				{
					Read<SettingsWindow?>(main, "_settingsWindow")?.Close(); StopTimers();
					Read<IDisposable?>(main, "_hotkeys")?.Dispose(); Read<IDisposable?>(main, "_tray")?.Dispose(); Read<LyricsService>(main, "_lyricsService").Dispose();
					typeof(MainWindow).GetField("_allowClose", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true); main.Close();
				}
				void StopTimers()
				{
					foreach (FieldInfo field in typeof(MainWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
						if (field.GetValue(main) is DispatcherTimer timer) timer.Stop();
				}
			});
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}
}
