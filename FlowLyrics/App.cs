using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using FlowLyrics.Services;

namespace FlowLyrics;

public class App : Application
{
	private bool _contentLoaded;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		base.DispatcherUnhandledException += delegate(object _, DispatcherUnhandledExceptionEventArgs args)
		{
			MessageBox.Show(string.Format(LocalizationService.TranslateCurrent("Unexpected error.\n\n{0}"), args.Exception.Message), "FlowLyrics", MessageBoxButton.OK, MessageBoxImage.Hand);
			args.Handled = true;
		};
		Window window = (base.MainWindow = new MainWindow());
		MainWindow mainWindow = (MainWindow)window;
		mainWindow.Show();
		if (e.Args.Any((string arg) => string.Equals(arg, "--hidden", StringComparison.OrdinalIgnoreCase)))
		{
			mainWindow.HideOverlay();
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.10.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/FlowLyrics;component/flowlyrics.app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.10.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run();
	}
}
