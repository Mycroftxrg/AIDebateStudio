using AIDebateStudio.Models;

namespace AIDebateStudio.Services;

public static class ApiPresetCatalog
{
	public static IReadOnlyList<ApiProviderPreset> All { get; } =
	[
		new(
			"siliconflow",
			"硅基流动",
			"国内",
			AiProviderKind.OpenAiCompatible,
			"https://api.siliconflow.cn/v1",
			"Pro/deepseek-ai/DeepSeek-V3.2",
			"硅基流动 OpenAI-compatible API。支持 /v1/chat/completions；填写 API Key 后点击“刷新硅基流动模型”，会从 /v1/models?type=text&sub_type=chat 读取账号可用模型。",
			[
				"Pro/deepseek-ai/DeepSeek-V3.2",
				"deepseek-ai/DeepSeek-V3.2",
				"Pro/deepseek-ai/DeepSeek-R1",
				"deepseek-ai/DeepSeek-R1",
				"Pro/zai-org/GLM-4.7",
				"Pro/zai-org/GLM-5",
				"zai-org/GLM-4.6",
				"Qwen/Qwen3-32B",
				"Qwen/Qwen3-30B-A3B",
				"Qwen/Qwen3-14B",
				"Qwen/Qwen3-8B",
				"Qwen/Qwen3-4B",
				"Qwen/Qwen3-1.7B",
				"Qwen/Qwen3.5-35B-A3B",
				"Qwen/Qwen3.5-14B",
				"Qwen/QwQ-32B",
				"Qwen/Qwen2.5-72B-Instruct",
				"Qwen/Qwen2.5-32B-Instruct",
				"Qwen/Qwen2.5-14B-Instruct",
				"Qwen/Qwen2.5-7B-Instruct",
				"THUDM/GLM-4-9B-0414",
				"internlm/internlm3-8b-instruct",
				"meta-llama/Llama-3.3-70B-Instruct",
				"meta-llama/Meta-Llama-3.1-8B-Instruct",
				"mistralai/Mistral-7B-Instruct-v0.3",
				"google/gemma-2-27b-it",
				"TeleAI/TeleChat2",
				"01-ai/Yi-1.5-34B-Chat-16K"
			])
	];

	public static ApiProviderPreset Find(string id)
	{
		return All.FirstOrDefault(p => p.Id == id) ?? All[0];
	}
}
