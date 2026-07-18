using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowLyrics.Models;

namespace FlowLyrics.Services;

public sealed class SettingsService
{
	private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

	private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	public string AppDataDirectory { get; }

	public string SettingsPath { get; }

	public SettingsService()
	{
		AppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlowLyrics");
		SettingsPath = Path.Combine(AppDataDirectory, "settings.json");
	}

	public AppSettings Load()
	{
		Directory.CreateDirectory(AppDataDirectory);
		if (!File.Exists(SettingsPath))
		{
			return new AppSettings();
		}
		try
		{
			string json = File.ReadAllText(SettingsPath);
			int sourceVersion = 0;
			using (JsonDocument jsonDocument = JsonDocument.Parse(json))
			{
				if (jsonDocument.RootElement.TryGetProperty("SettingsSchemaVersion", out var value) && value.TryGetInt32(out var value2))
				{
					sourceVersion = value2;
				}
			}
			AppSettings? obj = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
			UpgradeSettings(obj, sourceVersion);
			obj.Normalize();
			return obj;
		}
		catch
		{
			try
			{
				string destFileName = Path.Combine(AppDataDirectory, $"settings.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");
				File.Copy(SettingsPath, destFileName, overwrite: false);
			}
			catch
			{
			}
			return new AppSettings();
		}
	}

	private static void UpgradeSettings(AppSettings settings, int sourceVersion)
	{
		if (sourceVersion < 2)
		{
			if (settings.DisplayLines == 2)
			{
				settings.DisplayLines = 7;
			}
			if (settings.WindowHeight <= 160.0)
			{
				settings.WindowHeight = 520.0;
			}
			if (settings.BackgroundOpacity <= 0.001 && string.Equals(settings.BackgroundColor, "#FF111318", StringComparison.OrdinalIgnoreCase))
			{
				settings.BackgroundOpacity = 0.58;
			}
			if (string.Equals(settings.NextTextColor, "#99FFFFFF", StringComparison.OrdinalIgnoreCase))
			{
				settings.NextTextColor = "#FFE3E7EF";
			}
		}
		if (sourceVersion < 3)
		{
			settings.WrapLongLines = true;
			settings.AutoFitText = true;
			settings.MinimumFontSize = Math.Min(settings.MinimumFontSize, 8.0);
			settings.MaximumWrapLines = Math.Max(settings.MaximumWrapLines, 4);
		}
		if (sourceVersion < 4)
		{
			settings.ShowUnlockedBadge = false;
		}
		if (sourceVersion < 5 && string.IsNullOrWhiteSpace(settings.UiColor))
		{
			settings.UiColor = "#FFFF6B2C";
		}
		settings.SettingsSchemaVersion = 14;
	}

	public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default(CancellationToken))
	{
		settings.Normalize();
		Directory.CreateDirectory(AppDataDirectory);
		await _writeLock.WaitAsync(cancellationToken);
		try
		{
			string contents = JsonSerializer.Serialize(settings, _jsonOptions);
			string temporaryPath = SettingsPath + ".tmp";
			await File.WriteAllTextAsync(temporaryPath, contents, cancellationToken);
			File.Move(temporaryPath, SettingsPath, overwrite: true);
		}
		finally
		{
			_writeLock.Release();
		}
	}
}
