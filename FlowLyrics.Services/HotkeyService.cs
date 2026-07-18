using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using FlowLyrics.Interop;

namespace FlowLyrics.Services;

public sealed class HotkeyService : IDisposable
{
	private const int ToggleLockId = 16641;

	private const int ToggleVisibilityId = 16642;

	private readonly nint _handle;

	private readonly HwndSource _source;

	private bool _lockRegistered;

	private bool _visibilityRegistered;

	public event Action? ToggleLockRequested;

	public event Action? ToggleVisibilityRequested;

	public HotkeyService(nint handle)
	{
		_handle = handle;
		_source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException(LocalizationService.TranslateCurrent("Could not get the window handle."));
		_source.AddHook(WindowProcedure);
		uint fsModifiers = 16387u;
		_lockRegistered = NativeMethods.RegisterHotKey(_handle, 16641, fsModifiers, 76u);
		_visibilityRegistered = NativeMethods.RegisterHotKey(_handle, 16642, fsModifiers, 75u);
		if (!_lockRegistered && !_visibilityRegistered)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error(), LocalizationService.TranslateCurrent("Could not register global shortcuts."));
		}
	}

	public void Dispose()
	{
		_source.RemoveHook(WindowProcedure);
		if (_lockRegistered)
		{
			NativeMethods.UnregisterHotKey(_handle, 16641);
			_lockRegistered = false;
		}
		if (_visibilityRegistered)
		{
			NativeMethods.UnregisterHotKey(_handle, 16642);
			_visibilityRegistered = false;
		}
	}

	private nint WindowProcedure(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
	{
		if (message != 786)
		{
			return IntPtr.Zero;
		}
		nint num = wParam;
		switch (((IntPtr)num).ToInt32())
		{
		case 16641:
			this.ToggleLockRequested?.Invoke();
			handled = true;
			break;
		case 16642:
			this.ToggleVisibilityRequested?.Invoke();
			handled = true;
			break;
		}
		return IntPtr.Zero;
	}
}
