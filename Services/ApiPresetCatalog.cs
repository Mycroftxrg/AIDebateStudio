using AIDebateStudio.Models;

namespace AIDebateStudio.Services;

public static class ApiPresetCatalog
{
	public static IReadOnlyList<ApiProviderPreset> All { get; } =
	[
		new("openai", "OpenAI", "国外", AiProviderKind.OpenAiCompatible, "https://api.openai.com/v1", "gpt-4.1-mini", "官方 OpenAI Chat Completions 兼容接口。"),
		new("anthropic", "Anthropic Claude", "国外", AiProviderKind.Anthropic, "https://api.anthropic.com", "claude-sonnet-4-20250514", "Anthropic Messages API。"),
		new("gemini", "Google Gemini", "国外", AiProviderKind.Gemini, "https://generativelanguage.googleapis.com", "gemini-2.5-flash", "Gemini generateContent REST API。"),
		new("openrouter", "OpenRouter", "国外", AiProviderKind.OpenAiCompatible, "https://openrouter.ai/api/v1", "openai/gpt-4.1-mini", "多模型聚合，兼容 OpenAI Chat Completions。"),
		new("groq", "Groq", "国外", AiProviderKind.OpenAiCompatible, "https://api.groq.com/openai/v1", "llama-3.3-70b-versatile", "高速推理平台，兼容 OpenAI。"),
		new("mistral", "Mistral AI", "国外", AiProviderKind.OpenAiCompatible, "https://api.mistral.ai/v1", "mistral-large-latest", "Mistral Chat Completions API。"),
		new("xai", "xAI", "国外", AiProviderKind.OpenAiCompatible, "https://api.x.ai/v1", "grok-3-mini", "xAI OpenAI-compatible API。"),
		new("deepseek", "DeepSeek", "国内", AiProviderKind.OpenAiCompatible, "https://api.deepseek.com", "deepseek-chat", "DeepSeek OpenAI-compatible API。"),
		new("dashscope", "阿里云百炼 / 通义千问", "国内", AiProviderKind.OpenAiCompatible, "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen-plus", "DashScope OpenAI 兼容模式。"),
		new("doubao", "火山方舟 / 豆包", "国内", AiProviderKind.OpenAiCompatible, "https://ark.cn-beijing.volces.com/api/v3", "doubao-1-5-pro-32k-250115", "火山方舟 OpenAI-compatible Endpoint。"),
		new("zhipu", "智谱 GLM", "国内", AiProviderKind.OpenAiCompatible, "https://open.bigmodel.cn/api/paas/v4", "glm-4-plus", "智谱 OpenAI 兼容接口。"),
		new("moonshot", "Moonshot Kimi", "国内", AiProviderKind.OpenAiCompatible, "https://api.moonshot.cn/v1", "moonshot-v1-32k", "Moonshot OpenAI-compatible API。"),
		new("baidu", "百度千帆 / 文心", "国内", AiProviderKind.OpenAiCompatible, "https://qianfan.baidubce.com/v2", "ernie-4.5-turbo-128k", "千帆 OpenAI-compatible API。"),
		new("hunyuan", "腾讯混元", "国内", AiProviderKind.OpenAiCompatible, "https://api.hunyuan.cloud.tencent.com/v1", "hunyuan-turbos-latest", "腾讯混元 OpenAI-compatible API。"),
		new(
			"siliconflow",
			"硅基流动",
			"国内",
			AiProviderKind.OpenAiCompatible,
			"https://api.siliconflow.cn/v1",
			"Pro/deepseek-ai/DeepSeek-V3.2",
			"硅基流动 OpenAI-compatible API。支持 /v1/chat/completions；填写 API Key 后可刷新 /v1/models 获取账号可用 chat 模型。",
			[
				"Pro/deepseek-ai/DeepSeek-V3.2",
				"deepseek-ai/DeepSeek-V3.2",
				"Pro/deepseek-ai/DeepSeek-R1",
				"deepseek-ai/DeepSeek-R1",
				"Pro/zai-org/GLM-4.7",
				"Pro/zai-org/GLM-5",
				"zai-org/GLM-4.6",
				"Qwen/Qwen3-32B",
				"Qwen/Qwen3-14B",
				"Qwen/Qwen3-8B",
				"Qwen/Qwen3.5-35B-A3B",
				"Qwen/Qwen3.5-14B"
			]),
		new("stepfun", "阶跃星辰", "国内", AiProviderKind.OpenAiCompatible, "https://api.stepfun.com/v1", "step-2-mini", "阶跃星辰 OpenAI-compatible API。"),
		new("yi", "零一万物 Yi", "国内", AiProviderKind.OpenAiCompatible, "https://api.lingyiwanwu.com/v1", "yi-large", "零一万物 OpenAI-compatible API。"),
		new("manual", "手动填写", "自定义", AiProviderKind.OpenAiCompatible, "", "", "用于自建网关、代理或未列出的兼容服务。")
	];

	public static ApiProviderPreset Find(string id)
	{
		return All.FirstOrDefault(p => p.Id == id) ?? All[0];
	}
}
