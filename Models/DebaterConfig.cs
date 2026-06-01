namespace AIDebateStudio.Models;

public sealed class DebaterConfig
{
	public string Name { get; set; } = string.Empty;
	public string Position { get; set; } = string.Empty;
	public string Persona { get; set; } = string.Empty;
	public DebateSide Side { get; set; }
	public string PresetId { get; set; } = "openai";
	public AiProviderKind ProviderKind { get; set; } = AiProviderKind.OpenAiCompatible;
	public string BaseUrl { get; set; } = string.Empty;
	public string ApiKey { get; set; } = string.Empty;
	public string Model { get; set; } = string.Empty;
	public double Temperature { get; set; } = 0.7;
	public int MaxTokens { get; set; } = 900;
	public bool Enabled { get; set; } = true;
}
