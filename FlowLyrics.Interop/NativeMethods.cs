using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FlowLyrics.Interop;

internal static class NativeMethods
{
	internal struct Point
	{
		internal int X;

		internal int Y;
	}

	internal struct Rect
	{
		internal int Left;
		internal int Top;
		internal int Right;
		internal int Bottom;
	}

	internal const int GwlExStyle = -20;

	internal const long WsExTransparent = 32L;

	internal const long WsExToolWindow = 128L;

	internal const long WsExLayered = 524288L;

	internal const long WsExNoActivate = 134217728L;

	internal const int WmHotkey = 786;

	internal const int WmNcHitTest = 132;

	internal const int WmNcLButtonDown = 161;

	internal const int HtTopLeft = 13;

	internal const int HtTopRight = 14;

	internal const int HtBottomLeft = 16;

	internal const int HtBottomRight = 17;

	internal const int HtTransparent = -1;

	internal const uint ModAlt = 1u;

	internal const uint ModControl = 2u;

	internal const uint ModNoRepeat = 16384u;

	[DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
	private static extern int GetWindowLong32(nint hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
	private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
	private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
	private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool UnregisterHotKey(nint hWnd, int id);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool GetCursorPos(out Point point);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool GetWindowRect(nint hWnd, out Rect rect);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	internal static extern nint SendMessage(nint hWnd, int message, nint wParam, nint lParam);

	internal static long GetExtendedStyle(nint handle)
	{
		if (IntPtr.Size != 8)
		{
			return GetWindowLong32(handle, -20);
		}
		return ((IntPtr)GetWindowLongPtr64(handle, -20)).ToInt64();
	}

	internal static void SetExtendedStyle(nint handle, long style)
	{
		Marshal.SetLastPInvokeError(0);
		if (IntPtr.Size == 8)
		{
			if (SetWindowLongPtr64(handle, -20, new IntPtr(style)) == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}
		else if (SetWindowLong32(handle, -20, (int)style) == 0 && Marshal.GetLastWin32Error() != 0)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}
}
