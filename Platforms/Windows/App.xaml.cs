using AIDebateStudio.Services;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AIDebateStudio.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			if (args.ExceptionObject is Exception ex)
			{
				StartupDiagnostics.Write(ex, "WinUI.AppDomain.UnhandledException");
			}
		};

		UnhandledException += (_, args) =>
		{
			StartupDiagnostics.Write(args.Exception, "WinUI.Application.UnhandledException");
		};

		try
		{
			this.InitializeComponent();
		}
		catch (Exception ex)
		{
			StartupDiagnostics.Write(ex, "WinUI.App.InitializeComponent");
			throw;
		}
	}

	protected override MauiApp CreateMauiApp()
	{
		try
		{
			return MauiProgram.CreateMauiApp();
		}
		catch (Exception ex)
		{
			StartupDiagnostics.Write(ex, "WinUI.App.CreateMauiApp");
			throw;
		}
	}
}
