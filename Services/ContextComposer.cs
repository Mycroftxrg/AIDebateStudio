using System.Text;
using AIDebateStudio.Models;

namespace AIDebateStudio.Services;

public sealed class ContextComposer
{
	public IReadOnlyList<ChatRequestMessage> BuildDebateMessages(
		DebateSettings settings,
		DebaterConfig debater,
		IReadOnlyList<DebateMessage> history,
		IReadOnlyList<AttachmentContext> attachments,
		IReadOnlyList<string> queuedInterjections,
		IReadOnlyList<DebaterConfig> allDebaters)
	{
		var messages = new List<ChatRequestMessage>();
		var roster = string.Join("\n", allDebaters.Where(d => d.Enabled).Select(d => $"- {d.Name}：{d.Position}。{d.Persona}"));

		var system = new StringBuilder();
		system.AppendLine("你正在参加一场中文 AI 辩论。");
		system.AppendLine($"当前辩题：{settings.Topic}");
		system.AppendLine($"辩论规则：{settings.Rules}");
		system.AppendLine($"你的身份：{debater.Name}");
		system.AppendLine($"你的立场：{debater.Position}");
		system.AppendLine($"你的风格和任务：{debater.Persona}");
		system.AppendLine("辩手名单：");
		system.AppendLine(roster);
		system.AppendLine("发言要求：");
		system.AppendLine("1. 先回应上一位发言者最关键的论点，再推进自己的论证。");
		system.AppendLine("2. 允许质疑资料可靠性，但不要编造来源。");
		system.AppendLine("3. 每次发言保持 2 到 5 段，给出清晰结论。");
		system.AppendLine("4. 不要代替其他辩手发言，不要输出舞台说明。");
		system.AppendLine("5. 如果用户刚插话，必须优先回应用户插话。");
		messages.Add(new ChatRequestMessage("system", system.ToString().Trim()));

		if (!string.IsNullOrWhiteSpace(settings.MemorySummary))
		{
			messages.Add(new ChatRequestMessage("system", $"以下是较早辩论记录的压缩记忆，请持续遵守：\n{settings.MemorySummary}"));
		}

		if (attachments.Count > 0)
		{
			var attachmentText = new StringBuilder();
			attachmentText.AppendLine("用户提供的资料上下文：");
			foreach (var attachment in attachments.TakeLast(8))
			{
				attachmentText.AppendLine($"【{attachment.Kind}】{attachment.FileName}");
				attachmentText.AppendLine(TrimForContext(attachment.Content, 6000));
				attachmentText.AppendLine();
			}

			messages.Add(new ChatRequestMessage("system", attachmentText.ToString().Trim()));
		}

		if (queuedInterjections.Count > 0)
		{
			messages.Add(new ChatRequestMessage("system", $"当前还有 {queuedInterjections.Count} 条用户插话正在等待排队进入正式记录。你现在只能基于已经进入记录的内容发言。"));
		}

		foreach (var item in history.TakeLast(Math.Max(4, settings.KeepRecentMessages)))
		{
			var role = item.Role == DebateRole.Debater ? "assistant" : "user";
			var prefix = item.Role == DebateRole.Debater ? $"{item.Speaker}： " : $"用户/{item.Speaker}： ";
			messages.Add(new ChatRequestMessage(role, prefix + item.Content));
		}

		messages.Add(new ChatRequestMessage("user", $"现在轮到 {debater.Name} 发言。请围绕“{settings.Topic}”继续辩论。"));
		return messages;
	}

	public IReadOnlyList<ChatRequestMessage> BuildCompressionMessages(string previousSummary, IReadOnlyList<DebateMessage> messages)
	{
		var text = new StringBuilder();
		if (!string.IsNullOrWhiteSpace(previousSummary))
		{
			text.AppendLine("已有压缩记忆：");
			text.AppendLine(previousSummary);
			text.AppendLine();
		}

		text.AppendLine("需要纳入压缩记忆的新记录：");
		foreach (var message in messages)
		{
			text.AppendLine($"[{message.CreatedAt:MM-dd HH:mm}] {message.Speaker}：{message.Content}");
		}

		return
		[
			new ChatRequestMessage("system", "你是辩论记录整理器。请用中文压缩长对话，只保留立场、关键论据、已达成或未解决的分歧、用户明确要求、资料引用线索和后续应遵守的上下文。不要加入新观点。"),
			new ChatRequestMessage("user", text.ToString())
		];
	}

	private static string TrimForContext(string value, int maxChars)
	{
		if (value.Length <= maxChars)
		{
			return value;
		}

		return value[..maxChars] + "\n...[内容过长，已截断]";
	}
}
