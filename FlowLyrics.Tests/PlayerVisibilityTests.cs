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
	public void ExistingSettings_DefaultToVisible_ExplicitChoicesRoundTrip()
	{
		var legacy = JsonSerializer.Deserialize<AppSettings>("{\"SettingsSchemaVersion\":17,\"ShowPlaybackControls\":true}")!;
		Assert.True(legacy.ShowRepeatButton && legacy.ShowReverseButton && legacy.ShowPersonalSyncButton && legacy.ShowVolumeButton);
		legacy.ShowRepeatButton = legacy.ShowReverseButton = legacy.ShowPersonalSyncButton = legacy.ShowVolumeButton = false;
		var copy = legacy.Clone(); copy.Normalize();
		Assert.False(copy.ShowRepeatButton || copy.ShowReverseButton || copy.ShowPersonalSyncButton || copy.ShowVolumeButton);
		Assert.Equal(17, copy.SettingsSchemaVersion); Assert.True(copy.ShowPlaybackControls);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void UtilityControls_PreviewIndependently_ThenCancelOrSave(bool save)
	{
		string directory = Path.Combine(Path.GetTempPath(), "FlowLyrics-visibility-" + Guid.NewGuid().ToString("N"));
		try
		{
			Sta(() =>
			{
				using MediaSessionService media = new(new EmptyProvider());
				SettingsService settingsService = new(directory);
				MainWindow main = new(settingsService, media);
				try
				{
					main.ShowActivated = false; main.Show(); Pump(); StopTimers();
					Invoke(main, "OpenSettings"); Pump();
					var settings = Read<SettingsWindow>(main, "_settingsWindow");
					string[] fields = ["_repeatButton", "_reverseColorsButton", "_personalSyncButton", "VolumeButton"];
					string[] options = ["_showRepeatButtonBox", "_showReverseButtonBox", "_showPersonalSyncButtonBox", "_showVolumeButtonBox"];
					for (int i = 0; i < fields.Length; i++)
					{
						Read<CheckBox>(settings, options[i]).IsChecked = false; Pump();
						for (int j = 0; j < fields.Length; j++) Assert.Equal(j <= i ? Visibility.Collapsed : Visibility.Visible, Read<Button>(main, fields[j]).Visibility);
						Assert.Equal(Visibility.Visible, Read<Button>(main, "LockButton").Visibility);
						Assert.Equal(Visibility.Visible, Read<Button>(main, "SettingsButton").Visibility);
						Assert.True(Read<AppSettings>(main, "_settings").ShowPlaybackControls);
					}
					Assert.Contains(Logical<TextBlock>(settings), text => text.Text == "PLAYER CONTROLS");
					if (save) settings.ApplyAndClose();
					else
					{
						string cancelText = LocalizationService.Translate(settings.ResultSettings.Language, "Cancel");
						Button cancel = Assert.Single(Logical<Button>(settings), button => button.Content is string content && content == cancelText);
						Assert.True(cancel.IsVisible); cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
					}
					Pump(); Assert.Equal(save, settings.Accepted);
					foreach (string field in fields) Assert.Equal(save ? Visibility.Collapsed : Visibility.Visible, Read<Button>(main, field).Visibility);
					if (save) WaitUntil(() => !settingsService.Load().ShowVolumeButton);
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
