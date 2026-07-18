using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace FlowLyrics.Services;

public sealed class TrayIconService : IDisposable
{
	private readonly NotifyIcon _notifyIcon;

	private readonly ToolStripMenuItem _showItem;

	private readonly ToolStripMenuItem _lockItem;

	private readonly ToolStripMenuItem _settingsItem;

	private readonly ToolStripMenuItem _exitItem;

	private string _language;

	private bool _isVisible = true;

	private bool _isLocked;

	public event Action? ToggleVisibilityRequested;

	public event Action? ToggleLockRequested;

	public event Action? SettingsRequested;

	public event Action? ExitRequested;

	public TrayIconService(string language)
	{
		_language = LocalizationService.NormalizeLanguage(language);
		_showItem = new ToolStripMenuItem(T("Show / Hide"));
		_showItem.Click += delegate
		{
			this.ToggleVisibilityRequested?.Invoke();
		};
		_lockItem = new ToolStripMenuItem(T("Lock"));
		_lockItem.Click += delegate
		{
			this.ToggleLockRequested?.Invoke();
		};
		_settingsItem = new ToolStripMenuItem(T("Settings..."));
		_settingsItem.Click += delegate
		{
			this.SettingsRequested?.Invoke();
		};
		_exitItem = new ToolStripMenuItem(T("Exit"));
		_exitItem.Click += delegate
		{
			this.ExitRequested?.Invoke();
		};
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip
		{
			Items = 
			{
				(ToolStripItem)_showItem,
				(ToolStripItem)_lockItem,
				(ToolStripItem)_settingsItem,
				(ToolStripItem)new ToolStripSeparator(),
				(ToolStripItem)_exitItem
			}
		};
		Icon trayIcon = SystemIcons.Information;
		try
		{
			Stream? iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("assets/branding/FlowLyrics_logoF.ico");
			if (iconStream != null)
			{
				trayIcon = new Icon(iconStream);
			}
		}
		catch
		{
		}
		_notifyIcon = new NotifyIcon
		{
			Text = "FlowLyrics",
			Icon = trayIcon,
			ContextMenuStrip = contextMenuStrip,
			Visible = true
		};
		_notifyIcon.DoubleClick += delegate
		{
			this.ToggleVisibilityRequested?.Invoke();
		};
	}

	public void UpdateState(bool isVisible, bool isLocked)
	{
		_isVisible = isVisible;
		_isLocked = isLocked;
		_showItem.Text = T(isVisible ? "Hide" : "Show");
		_lockItem.Text = T(isLocked ? "Unlock" : "Lock") + "  (Ctrl+Alt+L)";
	}

	public void UpdateLanguage(string language)
	{
		_language = LocalizationService.NormalizeLanguage(language);
		_settingsItem.Text = T("Settings...");
		_exitItem.Text = T("Exit");
		UpdateState(_isVisible, _isLocked);
	}

	private string T(string key)
	{
		return LocalizationService.Translate(_language, key);
	}

	public void ShowMessage(string title, string message)
	{
		_notifyIcon.ShowBalloonTip(2500, title, message, ToolTipIcon.Info);
	}

	public void Dispose()
	{
		_notifyIcon.Visible = false;
		_notifyIcon.ContextMenuStrip?.Dispose();
		_notifyIcon.Dispose();
	}
}
