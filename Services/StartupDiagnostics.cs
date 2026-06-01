namespace AIDebateStudio.Services;

public static class StartupDiagnostics
{
	public static void Write(Exception exception, string stage)
	{
		try
		{
			var path = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"AIDebateStudio",
				"startup-error.log");
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {stage}{Environment.NewLine}{exception}{Environment.NewLine}");
		}
		catch
		{
			// Startup diagnostics must never become the startup failure.
		}
	}
}
