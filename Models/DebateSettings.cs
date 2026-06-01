namespace AIDebateStudio.Models;

public sealed class DebateSettings
{
	public string Topic { get; set; } = "人工智能是否会显著提升普通人的创造力？";
	public string Rules { get; set; } = "围绕论题展开攻防。每次发言先回应上一位关键观点，再提出新的论据或反驳。避免空泛表态，必要时指出证据不足。";
	public int MaxRounds { get; set; } = 4;
	public int CurrentRound { get; set; }
	public int NextDebaterIndex { get; set; }
	public bool AutoContinue { get; set; }
	public bool CompressionEnabled { get; set; } = true;
	public int KeepRecentMessages { get; set; } = 10;
	public int CompressAfterMessages { get; set; } = 16;
	public string MemorySummary { get; set; } = string.Empty;
}
