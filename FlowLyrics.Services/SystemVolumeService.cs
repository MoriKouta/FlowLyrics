using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FlowLyrics.Services;

public sealed class SystemVolumeService
{
	private enum EDataFlow
	{
		Render,
		Capture,
		All
	}

	[ComImport]
	[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
	private sealed class MMDeviceEnumeratorComObject
	{
	}

	[ComImport]
	[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDeviceEnumerator
	{
		[PreserveSig]
		int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection devices);

		[PreserveSig]
		int GetDefaultAudioEndpoint(EDataFlow dataFlow, int role, out IMMDevice device);

		[PreserveSig]
		int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

		[PreserveSig]
		int RegisterEndpointNotificationCallback(nint client);

		[PreserveSig]
		int UnregisterEndpointNotificationCallback(nint client);
	}

	[ComImport]
	[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDeviceCollection
	{
		[PreserveSig]
		int GetCount(out uint count);

		[PreserveSig]
		int Item(uint index, out IMMDevice device);
	}

	[ComImport]
	[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDevice
	{
		[PreserveSig]
		int Activate(ref Guid interfaceId, int classContext, nint activationParameters, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);

		[PreserveSig]
		int OpenPropertyStore(int access, out nint properties);

		[PreserveSig]
		int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

		[PreserveSig]
		int GetState(out int state);
	}

	[ComImport]
	[Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioSessionManager2
	{
		[PreserveSig]
		int GetAudioSessionControl(ref Guid sessionGuid, uint streamFlags, out IAudioSessionControl sessionControl);

		[PreserveSig]
		int GetSimpleAudioVolume(ref Guid sessionGuid, uint streamFlags, out ISimpleAudioVolume audioVolume);

		[PreserveSig]
		int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);

		[PreserveSig]
		int RegisterSessionNotification(nint sessionNotification);

		[PreserveSig]
		int UnregisterSessionNotification(nint sessionNotification);

		[PreserveSig]
		int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, nint duckNotification);

		[PreserveSig]
		int UnregisterDuckNotification(nint duckNotification);
	}

	[ComImport]
	[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioSessionEnumerator
	{
		[PreserveSig]
		int GetCount(out int count);

		[PreserveSig]
		int GetSession(int index, out IAudioSessionControl sessionControl);
	}

	[ComImport]
	[Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioSessionControl
	{
		[PreserveSig]
		int GetState(out int state);

		[PreserveSig]
		int GetDisplayName(out nint displayName);

		[PreserveSig]
		int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid context);

		[PreserveSig]
		int GetIconPath(out nint iconPath);

		[PreserveSig]
		int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid context);

		[PreserveSig]
		int GetGroupingParam(out Guid groupingId);

		[PreserveSig]
		int SetGroupingParam(ref Guid groupingId, ref Guid context);

		[PreserveSig]
		int RegisterAudioSessionNotification(nint client);

		[PreserveSig]
		int UnregisterAudioSessionNotification(nint client);
	}

	[ComImport]
	[Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioSessionControl2
	{
		[PreserveSig]
		int GetState(out int state);

		[PreserveSig]
		int GetDisplayName(out nint displayName);

		[PreserveSig]
		int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid context);

		[PreserveSig]
		int GetIconPath(out nint iconPath);

		[PreserveSig]
		int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid context);

		[PreserveSig]
		int GetGroupingParam(out Guid groupingId);

		[PreserveSig]
		int SetGroupingParam(ref Guid groupingId, ref Guid context);

		[PreserveSig]
		int RegisterAudioSessionNotification(nint client);

		[PreserveSig]
		int UnregisterAudioSessionNotification(nint client);

		[PreserveSig]
		int GetSessionIdentifier(out nint sessionIdentifier);

		[PreserveSig]
		int GetSessionInstanceIdentifier(out nint sessionInstanceIdentifier);

		[PreserveSig]
		int GetProcessId(out uint processId);

		[PreserveSig]
		int IsSystemSoundsSession();

		[PreserveSig]
		int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
	}

	[ComImport]
	[Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface ISimpleAudioVolume
	{
		[PreserveSig]
		int SetMasterVolume(float level, ref Guid context);

		[PreserveSig]
		int GetMasterVolume(out float level);

		[PreserveSig]
		int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);

		[PreserveSig]
		int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
	}

	private const int DeviceStateActive = 1;

	private const int ClsctxAll = 23;

	private const uint ProcessQueryLimitedInformation = 0x1000;

	private const int ErrorInsufficientBuffer = 122;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetApplicationUserModelId(nint process, ref uint applicationUserModelIdLength, [Out] StringBuilder? applicationUserModelId);

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(nint handle);

	public bool TryGetVolume(string? sourceAppUserModelId, out double volume, out bool muted)
	{
		double total = 0.0;
		int count = 0;
		bool allMuted = true;
		VisitPreferredSourceSessions(sourceAppUserModelId, delegate(ISimpleAudioVolume session)
		{
			if (Failed(session.GetMasterVolume(out var level)) || Failed(session.GetMute(out var muted2)))
			{
				return false;
			}
			total += Math.Clamp(level, 0f, 1f);
			count++;
			allMuted &= muted2;
			return true;
		});
		volume = ((count == 0) ? 0.0 : (total / (double)count));
		muted = count > 0 && allMuted;
		return count > 0;
	}

	public bool TrySetVolume(string? sourceAppUserModelId, double volume)
	{
		float level = (float)Math.Clamp(volume, 0.0, 1.0);
		return VisitPreferredSourceSessions(sourceAppUserModelId, delegate(ISimpleAudioVolume session)
		{
			Guid context = Guid.Empty;
			if (Failed(session.SetMasterVolume(level, ref context)))
			{
				return false;
			}
			return level <= 0f || Succeeded(session.SetMute(muted: false, ref context));
		});
	}

	public bool TryToggleMute(string? sourceAppUserModelId)
	{
		if (!TryGetVolume(sourceAppUserModelId, out var _, out var muted))
		{
			return false;
		}
		bool newMuted = !muted;
		return VisitPreferredSourceSessions(sourceAppUserModelId, delegate(ISimpleAudioVolume session)
		{
			Guid context = Guid.Empty;
			return Succeeded(session.SetMute(newMuted, ref context));
		});
	}

	internal static bool IsSpotifyProcessName(string? processName)
	{
		if (!string.IsNullOrWhiteSpace(processName))
		{
			return processName.StartsWith("Spotify", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	internal static bool IsSpotifySessionIdentity(string? processName, string? sessionIdentifier, string? sessionInstanceIdentifier, string? displayName)
	{
		return IsSourceSessionIdentity("spotify", processName, null, sessionIdentifier, sessionInstanceIdentifier, displayName, null);
	}

	internal static bool IsSourceSessionIdentity(
		string? sourceAppUserModelId,
		string? processName,
		string? processAppUserModelId,
		string? sessionIdentifier,
		string? sessionInstanceIdentifier,
		string? displayName,
		string? iconPath)
	{
		string normalizedSource = NormalizeIdentity(sourceAppUserModelId);
		string normalizedProcessAppId = NormalizeIdentity(processAppUserModelId);
		if (normalizedSource.Length > 0 && normalizedProcessAppId.Length > 0
			&& (string.Equals(normalizedSource, normalizedProcessAppId, StringComparison.Ordinal)
				|| normalizedSource.Contains(normalizedProcessAppId, StringComparison.Ordinal)
				|| normalizedProcessAppId.Contains(normalizedSource, StringComparison.Ordinal)))
		{
			return true;
		}

		string[] tokens = GetSourceIdentityTokens(sourceAppUserModelId);
		if (tokens.Length == 0)
		{
			return false;
		}

		foreach (string candidate in new[] { processName, processAppUserModelId, sessionIdentifier, sessionInstanceIdentifier, displayName, iconPath })
		{
			string normalized = NormalizeIdentity(candidate);
			if (normalized.Length == 0) continue;
			if (tokens.Any(token => normalized.Contains(token, StringComparison.Ordinal))) return true;
		}
		return false;
	}

	private static bool VisitPreferredSourceSessions(string? sourceAppUserModelId, Func<ISimpleAudioVolume, bool> action)
	{
		if (string.IsNullOrWhiteSpace(sourceAppUserModelId)) return false;
		if (!VisitSourceSessions(sourceAppUserModelId, action, activeOnly: true))
		{
			return VisitSourceSessions(sourceAppUserModelId, action, activeOnly: false);
		}
		return true;
	}

	private static bool VisitSourceSessions(string sourceAppUserModelId, Func<ISimpleAudioVolume, bool> action, bool activeOnly)
	{
		IMMDeviceEnumerator iMMDeviceEnumerator = null;
		IMMDeviceCollection devices = null;
		int num = 0;
		try
		{
			iMMDeviceEnumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
			if (Failed(iMMDeviceEnumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceStateActive, out devices)) || Failed(devices.GetCount(out var count)))
			{
				return false;
			}
			for (uint num2 = 0u; num2 < count; num2++)
			{
				IMMDevice device = null;
				object endpoint = null;
				IAudioSessionEnumerator sessionEnumerator = null;
				try
				{
					if (Failed(devices.Item(num2, out device)))
					{
						continue;
					}
					Guid interfaceId = typeof(IAudioSessionManager2).GUID;
					if (Failed(device.Activate(ref interfaceId, ClsctxAll, IntPtr.Zero, out endpoint)) || !(endpoint is IAudioSessionManager2 audioSessionManager) || Failed(audioSessionManager.GetSessionEnumerator(out sessionEnumerator)) || Failed(sessionEnumerator.GetCount(out var count2)))
					{
						continue;
					}
					for (int i = 0; i < count2; i++)
					{
						IAudioSessionControl sessionControl = null;
						try
						{
							if (Failed(sessionEnumerator.GetSession(i, out sessionControl)))
							{
								continue;
							}
							IAudioSessionControl2 control;
							ISimpleAudioVolume arg;
							try
							{
								control = (IAudioSessionControl2)sessionControl;
								arg = (ISimpleAudioVolume)sessionControl;
							}
							catch (InvalidCastException)
							{
								goto end_IL_00c9;
							}
							if (Succeeded(sessionControl.GetState(out var state)))
							{
								if (state != 2 && (!activeOnly || state == 1))
								{
									goto IL_0119;
								}
							}
							else if (!activeOnly)
							{
								goto IL_0119;
							}
							goto end_IL_00c9;
							IL_0119:
							if (IsSourceSession(sourceAppUserModelId, sessionControl, control) && action(arg))
							{
								num++;
							}
							end_IL_00c9:;
						}
						finally
						{
							Release(sessionControl);
						}
					}
				}
				finally
				{
					Release(sessionEnumerator);
					Release(endpoint);
					Release(device);
				}
			}
			return num > 0;
		}
		catch
		{
			return false;
		}
		finally
		{
			Release(devices);
			Release(iMMDeviceEnumerator);
		}
	}

	private static bool IsSourceSession(string sourceAppUserModelId, IAudioSessionControl control, IAudioSessionControl2 control2)
	{
		string processName = null;
		string processAppUserModelId = null;
		if (Succeeded(control2.GetProcessId(out var processId)) && processId != 0)
		{
			processAppUserModelId = TryGetProcessApplicationUserModelId(processId);
			try
			{
				using Process process = Process.GetProcessById(checked((int)processId));
				processName = process.ProcessName;
			}
			catch
			{
			}
		}
		return IsSourceSessionIdentity(
			sourceAppUserModelId,
			processName,
			processAppUserModelId,
			ReadSessionIdentifier(control2),
			ReadSessionInstanceIdentifier(control2),
			ReadDisplayName(control),
			ReadIconPath(control));
	}

	private static string[] GetSourceIdentityTokens(string? sourceAppUserModelId)
	{
		string normalized = NormalizeIdentity(sourceAppUserModelId);
		if (normalized.Length == 0) return Array.Empty<string>();
		if (normalized.Contains("spotify", StringComparison.Ordinal)) return new[] { "spotify" };
		if (normalized.Contains("applemusic", StringComparison.Ordinal) || normalized.Contains("itunes", StringComparison.Ordinal)) return new[] { "applemusic", "itunes", "ampmediaplayer", "amplibraryagent" };
		if (normalized.Contains("tidal", StringComparison.Ordinal)) return new[] { "tidal" };
		if (normalized.Contains("videolan", StringComparison.Ordinal) || normalized.Contains("vlc", StringComparison.Ordinal)) return new[] { "videolan", "vlc" };
		if (normalized.Contains("msedge", StringComparison.Ordinal) || normalized.Contains("microsoftedge", StringComparison.Ordinal)) return new[] { "msedge", "microsoftedge" };
		if (normalized.Contains("chrome", StringComparison.Ordinal) || normalized.Contains("chromium", StringComparison.Ordinal)) return new[] { "chrome", "chromium" };
		if (normalized.Contains("firefox", StringComparison.Ordinal)) return new[] { "firefox" };
		if (normalized.Contains("zunemusic", StringComparison.Ordinal) || normalized.Contains("mediaplayer", StringComparison.Ordinal)) return new[] { "zunemusic", "musicui", "mediaplayer", "wmplayer" };

		return (sourceAppUserModelId ?? string.Empty)
			.Split(new[] { '.', '_', '!', '-', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(NormalizeIdentity)
			.Where(token => token.Length >= 4 && token is not "microsoft" and not "windows" and not "application" and not "package" and not "app" and not "exe")
			.Distinct(StringComparer.Ordinal)
			.ToArray();
	}

	private static string NormalizeIdentity(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
	}

	internal static string? TryGetProcessApplicationUserModelId(uint processId)
	{
		if (processId == 0) return null;
		nint processHandle = IntPtr.Zero;
		try
		{
			processHandle = OpenProcess(ProcessQueryLimitedInformation, inheritHandle: false, processId);
			if (processHandle == IntPtr.Zero) return null;
			uint length = 0;
			int result = GetApplicationUserModelId(processHandle, ref length, null);
			if (result != ErrorInsufficientBuffer || length == 0 || length > 32768) return null;
			StringBuilder value = new StringBuilder(checked((int)length));
			result = GetApplicationUserModelId(processHandle, ref length, value);
			return result == 0 ? value.ToString().Trim() : null;
		}
		catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException or OverflowException)
		{
			return null;
		}
		finally
		{
			if (processHandle != IntPtr.Zero) CloseHandle(processHandle);
		}
	}

	private static string? ReadSessionIdentifier(IAudioSessionControl2 control)
	{
		if (!Succeeded(control.GetSessionIdentifier(out var sessionIdentifier)))
		{
			return null;
		}
		return ReadAndFreeString(sessionIdentifier);
	}

	private static string? ReadSessionInstanceIdentifier(IAudioSessionControl2 control)
	{
		if (!Succeeded(control.GetSessionInstanceIdentifier(out var sessionInstanceIdentifier)))
		{
			return null;
		}
		return ReadAndFreeString(sessionInstanceIdentifier);
	}

	private static string? ReadDisplayName(IAudioSessionControl control)
	{
		if (!Succeeded(control.GetDisplayName(out var displayName)))
		{
			return null;
		}
		return ReadAndFreeString(displayName);
	}

	private static string? ReadIconPath(IAudioSessionControl control)
	{
		if (!Succeeded(control.GetIconPath(out var iconPath)))
		{
			return null;
		}
		return ReadAndFreeString(iconPath);
	}

	private static string? ReadAndFreeString(nint pointer)
	{
		if (pointer == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			return Marshal.PtrToStringUni(pointer);
		}
		finally
		{
			Marshal.FreeCoTaskMem(pointer);
		}
	}

	internal static bool Succeeded(int hresult)
	{
		return hresult >= 0;
	}

	private static bool Failed(int hresult)
	{
		return hresult < 0;
	}

	private static void Release(object? value)
	{
		if (value != null && Marshal.IsComObject(value))
		{
			Marshal.ReleaseComObject(value);
		}
	}
}
