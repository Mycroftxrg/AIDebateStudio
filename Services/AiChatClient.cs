using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIDebateStudio.Models;

namespace AIDebateStudio.Services;

public sealed class AiChatClient
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly HttpClient _httpClient = new()
	{
		Timeout = TimeSpan.FromSeconds(120)
	};

	public async Task<string> CompleteAsync(DebaterConfig config, IReadOnlyList<ChatRequestMessage> messages, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(config.ApiKey))
		{
			throw new InvalidOperationException($"{config.Name} 尚未填写 API Key。");
		}

		if (string.IsNullOrWhiteSpace(config.Model))
		{
			throw new InvalidOperationException($"{config.Name} 尚未填写模型名。");
		}

		return config.ProviderKind switch
		{
			AiProviderKind.Anthropic => await CompleteAnthropicAsync(config, messages, cancellationToken),
			AiProviderKind.Gemini => await CompleteGeminiAsync(config, messages, cancellationToken),
			_ => await CompleteOpenAiCompatibleAsync(config, messages, cancellationToken)
		};
	}

	private async Task<string> CompleteOpenAiCompatibleAsync(DebaterConfig config, IReadOnlyList<ChatRequestMessage> messages, CancellationToken cancellationToken)
	{
		var baseUrl = NormalizeBaseUrl(config.BaseUrl);
		var endpoint = baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
			? baseUrl
			: $"{baseUrl.TrimEnd('/')}/chat/completions";

		using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey.Trim());
		request.Content = JsonContent(new
		{
			model = config.Model.Trim(),
			messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
			temperature = config.Temperature,
			max_tokens = config.MaxTokens
		});

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		EnsureSuccess(response, json);

		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
		{
			var first = choices[0];
			if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
			{
				return content.GetString()?.Trim() ?? string.Empty;
			}

			if (first.TryGetProperty("text", out var text))
			{
				return text.GetString()?.Trim() ?? string.Empty;
			}
		}

		throw new InvalidOperationException("接口返回成功，但没有找到可用的回答内容。");
	}

	private async Task<string> CompleteAnthropicAsync(DebaterConfig config, IReadOnlyList<ChatRequestMessage> messages, CancellationToken cancellationToken)
	{
		var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://api.anthropic.com" : config.BaseUrl.TrimEnd('/');
		var systemText = string.Join("\n\n", messages.Where(m => m.Role == "system").Select(m => m.Content));
		var conversation = messages
			.Where(m => m.Role != "system")
			.Select(m => new
			{
				role = m.Role == "assistant" ? "assistant" : "user",
				content = m.Content
			})
			.ToArray();

		using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages");
		request.Headers.Add("x-api-key", config.ApiKey.Trim());
		request.Headers.Add("anthropic-version", "2023-06-01");
		request.Content = JsonContent(new
		{
			model = config.Model.Trim(),
			max_tokens = config.MaxTokens,
			temperature = config.Temperature,
			system = systemText,
			messages = conversation
		});

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		EnsureSuccess(response, json);

		using var document = JsonDocument.Parse(json);
		if (document.RootElement.TryGetProperty("content", out var content))
		{
			var parts = content.EnumerateArray()
				.Where(p => p.TryGetProperty("type", out var type) && type.GetString() == "text")
				.Select(p => p.TryGetProperty("text", out var text) ? text.GetString() : null)
				.Where(text => !string.IsNullOrWhiteSpace(text));
			return string.Join("\n", parts).Trim();
		}

		throw new InvalidOperationException("Anthropic 返回成功，但没有找到文本内容。");
	}

	private async Task<string> CompleteGeminiAsync(DebaterConfig config, IReadOnlyList<ChatRequestMessage> messages, CancellationToken cancellationToken)
	{
		var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://generativelanguage.googleapis.com" : config.BaseUrl.TrimEnd('/');
		var endpoint = $"{baseUrl}/v1beta/models/{Uri.EscapeDataString(config.Model.Trim())}:generateContent?key={Uri.EscapeDataString(config.ApiKey.Trim())}";
		var systemText = string.Join("\n\n", messages.Where(m => m.Role == "system").Select(m => m.Content));
		var contents = messages
			.Where(m => m.Role != "system")
			.Select(m => new
			{
				role = m.Role == "assistant" ? "model" : "user",
				parts = new[] { new { text = m.Content } }
			})
			.ToArray();

		using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		request.Content = JsonContent(new
		{
			systemInstruction = new { parts = new[] { new { text = systemText } } },
			contents,
			generationConfig = new
			{
				temperature = config.Temperature,
				maxOutputTokens = config.MaxTokens
			}
		});

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		EnsureSuccess(response, json);

		using var document = JsonDocument.Parse(json);
		if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
		{
			var parts = candidates[0].GetProperty("content").GetProperty("parts").EnumerateArray()
				.Select(p => p.TryGetProperty("text", out var text) ? text.GetString() : null)
				.Where(text => !string.IsNullOrWhiteSpace(text));
			return string.Join("\n", parts).Trim();
		}

		throw new InvalidOperationException("Gemini 返回成功，但没有找到文本内容。");
	}

	private static StringContent JsonContent(object body)
	{
		return new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
	}

	private static string NormalizeBaseUrl(string baseUrl)
	{
		if (string.IsNullOrWhiteSpace(baseUrl))
		{
			throw new InvalidOperationException("请填写 API Base URL。");
		}

		return baseUrl.Trim().TrimEnd('/');
	}

	private static void EnsureSuccess(HttpResponseMessage response, string content)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		var detail = content.Length > 800 ? content[..800] + "..." : content;
		throw new InvalidOperationException($"接口请求失败：{(int)response.StatusCode} {response.ReasonPhrase}\n{detail}");
	}
}
