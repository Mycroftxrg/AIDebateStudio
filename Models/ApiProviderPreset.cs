namespace AIDebateStudio.Models;

public sealed record ApiProviderPreset(
	string Id,
	string DisplayName,
	string Region,
	AiProviderKind Kind,
	string BaseUrl,
	string DefaultModel,
	string HelpText)
{
	public bool IsManual => Id == "manual";
}
