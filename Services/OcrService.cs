using AIDebateStudio.Models;

namespace AIDebateStudio.Services;

public sealed class OcrService
{
	public async Task<AttachmentContext> RecognizeAsync(FileResult file, DebaterConfig? visionConfig, CancellationToken cancellationToken)
	{
		await using var stream = await file.OpenReadAsync();
		await using var memory = new MemoryStream();
		await stream.CopyToAsync(memory, cancellationToken);

		var note = visionConfig is null || string.IsNullOrWhiteSpace(visionConfig.ApiKey)
			? "OCR 入口已建立。当前 1.0 版本先把图片作为待识别资料加入上下文；填写视觉模型配置后可扩展到多模态识别。"
			: $"OCR 入口已接收图片。当前配置模型：{visionConfig.Model}。";

		return new AttachmentContext
		{
			FileName = file.FileName,
			Kind = "OCR",
			Content = $"{note}\n文件大小：{memory.Length} 字节。\n建议在人工插话中补充希望识别的区域或识别目标。"
		};
	}
}
