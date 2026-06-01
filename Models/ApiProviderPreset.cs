namespace AIDebateStudio.Models;

public sealed record ApiProviderPreset(
	string Id,
	string DisplayName,
	string Region,
	AiProviderKind Kind,
	string BaseUrl,
	string DefaultModel,
	string HelpText,
	IReadOnlyList<string>? SuggestedModels = null)
{
	public bool IsManual => Id == "manual";
	public IReadOnlyList<string> Models => SuggestedModels ?? [];
}
