namespace AIDebateStudio.Models;

public sealed class DebateMessage
{
	public string Id { get; init; } = Guid.NewGuid().ToString("N");
	public DebateRole Role { get; init; }
	public string Speaker { get; init; } = string.Empty;
	public string Content { get; init; } = string.Empty;
	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
	public string? ModelName { get; init; }
	public string? Phase { get; init; }
	public bool IsQueuedHumanInterjection { get; init; }
}
