namespace AIDebateStudio.Models;

public sealed class AttachmentContext
{
	public string FileName { get; init; } = string.Empty;
	public string Kind { get; init; } = string.Empty;
	public string Content { get; init; } = string.Empty;
	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
