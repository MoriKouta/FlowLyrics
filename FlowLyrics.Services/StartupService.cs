using System;
using Microsoft.Win32;

namespace FlowLyrics.Services;

public static class StartupService
{
	private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string ValueName = "FlowLyrics";

	public static bool TrySetEnabled(bool enabled, out string? error)
	{
		error = null;
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true) ?? Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (enabled)
			{
				string processPath = Environment.ProcessPath;
				if (string.IsNullOrWhiteSpace(processPath))
				{
					throw new InvalidOperationException(LocalizationService.TranslateCurrent("Could not locate the executable."));
				}
				registryKey.SetValue("FlowLyrics", "\"" + processPath + "\"");
			}
			else
			{
				registryKey.DeleteValue("FlowLyrics", throwOnMissingValue: false);
			}
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}
}
