using AIDebateStudio.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AIDebateStudio;

public partial class App : Application
{
	public App()
	{
		try
		{
			InitializeComponent();
		}
		catch (Exception ex)
		{
			StartupDiagnostics.Write(ex, "App.InitializeComponent");
			throw;
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		try
		{
			return new Window(new AppShell());
		}
		catch (Exception ex)
		{
			StartupDiagnostics.Write(ex, "App.CreateWindow");
			throw;
		}
	}
}
