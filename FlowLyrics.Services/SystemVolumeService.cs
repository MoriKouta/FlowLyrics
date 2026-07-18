using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

	public bool TryGetVolume(out double volume, out bool muted)
	{
		double total = 0.0;
		int count = 0;
		bool allMuted = true;
		VisitPreferredSpotifySessions(delegate(ISimpleAudioVolume session)
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

	public bool TrySetVolume(double volume)
	{
		float level = (float)Math.Clamp(volume, 0.0, 1.0);
		return VisitPreferredSpotifySessions(delegate(ISimpleAudioVolume session)
		{
			Guid context = Guid.Empty;
			if (Failed(session.SetMasterVolume(level, ref context)))
			{
				return false;
			}
			return level <= 0f || Succeeded(session.SetMute(muted: false, ref context));
		});
	}

	public bool TryToggleMute()
	{
		if (!TryGetVolume(out var _, out var muted))
		{
			return false;
		}
		bool newMuted = !muted;
		return VisitPreferredSpotifySessions(delegate(ISimpleAudioVolume session)
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
		if (!IsSpotifyProcessName(processName) && !ContainsSpotify(sessionIdentifier) && !ContainsSpotify(sessionInstanceIdentifier))
		{
			return ContainsSpotify(displayName);
		}
		return true;
	}

	private static bool VisitPreferredSpotifySessions(Func<ISimpleAudioVolume, bool> action)
	{
		if (!VisitSpotifySessions(action, activeOnly: true))
		{
			return VisitSpotifySessions(action, activeOnly: false);
		}
		return true;
	}

	private static bool VisitSpotifySessions(Func<ISimpleAudioVolume, bool> action, bool activeOnly)
	{
		IMMDeviceEnumerator iMMDeviceEnumerator = null;
		IMMDeviceCollection devices = null;
		int num = 0;
		try
		{
			iMMDeviceEnumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
			if (Failed(iMMDeviceEnumerator.EnumAudioEndpoints(EDataFlow.Render, 1, out devices)) || Failed(devices.GetCount(out var count)))
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
					if (Failed(device.Activate(ref interfaceId, 23, IntPtr.Zero, out endpoint)) || !(endpoint is IAudioSessionManager2 audioSessionManager) || Failed(audioSessionManager.GetSessionEnumerator(out sessionEnumerator)) || Failed(sessionEnumerator.GetCount(out var count2)))
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
							if (IsSpotifySession(sessionControl, control) && action(arg))
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

	private static bool IsSpotifySession(IAudioSessionControl control, IAudioSessionControl2 control2)
	{
		string processName = null;
		if (Succeeded(control2.GetProcessId(out var processId)) && processId != 0)
		{
			try
			{
				using Process process = Process.GetProcessById(checked((int)processId));
				processName = process.ProcessName;
			}
			catch
			{
			}
		}
		if (IsSpotifyProcessName(processName))
		{
			return true;
		}
		if (ContainsSpotify(ReadSessionIdentifier(control2)))
		{
			return true;
		}
		if (ContainsSpotify(ReadSessionInstanceIdentifier(control2)))
		{
			return true;
		}
		if (ContainsSpotify(ReadDisplayName(control)))
		{
			return true;
		}
		return ContainsSpotify(ReadIconPath(control));
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

	private static bool ContainsSpotify(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value.Contains("spotify", StringComparison.OrdinalIgnoreCase);
		}
		return false;
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
