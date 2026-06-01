using System.Net.Http.Headers;
using System.Text.Json;

namespace AIDebateStudio.Services;

public sealed class SiliconFlowModelService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly HttpClient _httpClient = new()
	{
		Timeout = TimeSpan.FromSeconds(30)
	};

	public async Task<IReadOnlyList<string>> GetChatModelsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(baseUrl))
		{
			throw new InvalidOperationException("请先填写硅基流动 Base URL。");
		}

		if (string.IsNullOrWhiteSpace(apiKey))
		{
			throw new InvalidOperationException("请先填写硅基流动 API Key。");
		}

		var endpoint = $"{baseUrl.Trim().TrimEnd('/')}/models?type=text&sub_type=chat";
		using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			var detail = json.Length > 800 ? json[..800] + "..." : json;
			throw new InvalidOperationException($"硅基流动模型列表获取失败：{(int)response.StatusCode} {response.ReasonPhrase}\n{detail}");
		}

		using var document = JsonDocument.Parse(json);
		return ExtractModelIds(document.RootElement)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static IEnumerable<string> ExtractModelIds(JsonElement root)
	{
		if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in data.EnumerateArray())
			{
				var id = ExtractId(item);
				if (!string.IsNullOrWhiteSpace(id))
				{
					yield return id;
				}
			}
		}
		else if (root.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in root.EnumerateArray())
			{
				var id = ExtractId(item);
				if (!string.IsNullOrWhiteSpace(id))
				{
					yield return id;
				}
			}
		}
	}

	private static string? ExtractId(JsonElement item)
	{
		if (item.ValueKind == JsonValueKind.String)
		{
			return item.GetString();
		}

		foreach (var property in new[] { "id", "name", "model" })
		{
			if (item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
			{
				return value.GetString();
			}
		}

		return null;
	}
}
