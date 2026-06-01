using System.Text;
using System.Text.Json;
using AIDebateStudio.Models;
using AIDebateStudio.Services;

namespace AIDebateStudio;

public partial class MainPage : ContentPage
{
	private const string SettingsKey = "ai_debate_studio_state_v1";
	private readonly AiChatClient _chatClient = new();
	private readonly ContextComposer _contextComposer = new();
	private readonly FileParsingService _fileParsingService = new();
	private readonly OcrService _ocrService = new();
	private readonly SiliconFlowModelService _siliconFlowModelService = new();
	private readonly List<DebateMessage> _messages = [];
	private readonly List<AttachmentContext> _attachments = [];
	private readonly Queue<string> _interjectionQueue = new();
	private readonly List<DebaterConfig> _debaters = [];
	private readonly DebateSettings _settings = new();
	private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
	private bool _isBusy;
	private bool _isLoading;
	private bool _isAutoLoopRunning;
	private CancellationTokenSource? _turnCts;
	private CancellationTokenSource? _saveCts;
	private Style CaptionStyle => (Style)Application.Current!.Resources["Caption"];
	private Color PanelAltColor => (Color)Application.Current!.Resources["PanelAlt"];
	private Color PanelAltDarkColor => (Color)Application.Current!.Resources["PanelAltDark"];
	private Color PanelBgColor => (Color)Application.Current!.Resources["PanelBg"];
	private Color PanelBgDarkColor => (Color)Application.Current!.Resources["PanelBgDark"];
	private Color ControlBgColor => (Color)Application.Current!.Resources["ControlBg"];
	private Color ControlBgDarkColor => (Color)Application.Current!.Resources["ControlBgDark"];
	private Color TextStrongColor => (Color)Application.Current!.Resources["TextStrong"];
	private Color TextStrongDarkColor => (Color)Application.Current!.Resources["TextStrongDark"];
	private Color PrimaryColor => (Color)Application.Current!.Resources["Primary"];
	private Color ChipTextDarkColor => (Color)Application.Current!.Resources["ChipTextDark"];

	public MainPage()
	{
		InitializeComponent();
		InitializePickers();
		LoadState();
		ApplyStateToUi();
		Loaded += OnPageLoaded;
		SizeChanged += OnPageSizeChanged;
	}

	private async void OnPageLoaded(object? sender, EventArgs e)
	{
		await Task.WhenAll(
			FadeInAsync(LeftPanel, 0),
			FadeInAsync(ChatHeader, 80),
			FadeInAsync(ChatSurface, 140),
			FadeInAsync(ComposerPanel, 200),
			FadeInAsync(RightPanel, 260));
	}

	private async Task FadeInAsync(View view, uint delay)
	{
		await Task.Delay((int)delay);
		await Task.WhenAll(view.FadeTo(1, 260, Easing.CubicOut), view.TranslateTo(0, 0, 260, Easing.CubicOut));
	}

	private void OnPageSizeChanged(object? sender, EventArgs e)
	{
		if (Width <= 920)
		{
			RootGrid.ColumnDefinitions[0].Width = 0;
			RootGrid.ColumnDefinitions[2].Width = 0;
			RootGrid.Padding = 10;
		}
		else if (Width <= 1180)
		{
			RootGrid.ColumnDefinitions[0].Width = 280;
			RootGrid.ColumnDefinitions[2].Width = 320;
			RootGrid.Padding = 12;
		}
		else
		{
			RootGrid.ColumnDefinitions[0].Width = 320;
			RootGrid.ColumnDefinitions[2].Width = 360;
			RootGrid.Padding = 18;
		}
	}

	private void InitializePickers()
	{
		foreach (var preset in ApiPresetCatalog.All)
		{
			ProviderPicker.Items.Add($"{preset.Region} · {preset.DisplayName}");
		}
	}

	private void LoadState()
	{
		var json = Preferences.Default.Get(SettingsKey, string.Empty);
		if (!string.IsNullOrWhiteSpace(json))
		{
			try
			{
				var state = JsonSerializer.Deserialize<PersistedState>(json, _jsonOptions);
				if (state is not null)
				{
					CopySettings(state.Settings, _settings);
					_debaters.Clear();
					_debaters.AddRange(state.Debaters);
					_attachments.Clear();
					_attachments.AddRange(state.Attachments);
					_settings.MemorySummary = state.Settings.MemorySummary;
				}
			}
			catch
			{
				// 损坏的本地状态不阻止应用启动。
			}
		}

		if (_debaters.Count == 0)
		{
			_debaters.AddRange(CreateDefaultDebaters());
		}

		MigrateDebatersToSiliconFlow();
		MigrateDebaterSides();
	}

	private static IEnumerable<DebaterConfig> CreateDefaultDebaters()
	{
		var siliconFlow = ApiPresetCatalog.Find("siliconflow");
		return
		[
			FromPreset(siliconFlow, "Pro/deepseek-ai/DeepSeek-V3.2", DebateSide.Pro, "重视产业实践和应用案例，擅长把抽象观点落到具体场景。"),
			FromPreset(siliconFlow, "deepseek-ai/DeepSeek-R1", DebateSide.Con, "强调逻辑漏洞、成本、可靠性和长期外部性，发言尖锐但克制。"),
			FromPreset(siliconFlow, "Qwen/Qwen3-32B", DebateSide.Neutral, "负责补充关键追问、比较双方论证强弱，并推动辩论回到核心问题。")
		];
	}

	private static DebaterConfig FromPreset(ApiProviderPreset preset, string model, DebateSide side, string persona)
	{
		return new DebaterConfig
		{
			Name = model,
			Position = side == DebateSide.Neutral ? string.Empty : GetSideName(side),
			Persona = persona,
			Side = side,
			PresetId = preset.Id,
			ProviderKind = preset.Kind,
			BaseUrl = preset.BaseUrl,
			Model = model,
			Temperature = 0.7,
			MaxTokens = 900,
			Enabled = true
		};
	}

	private void ApplyStateToUi()
	{
		_isLoading = true;
		TopicEditor.Text = _settings.Topic;
		RulesEditor.Text = _settings.Rules;
		ProPromptEditor.Text = _settings.ProSidePrompt;
		ConPromptEditor.Text = _settings.ConSidePrompt;
		RoundSlider.Value = _settings.MaxRounds;
		CompressionSwitch.IsToggled = _settings.CompressionEnabled;
		DebaterPicker.Items.Clear();
		foreach (var debater in _debaters)
		{
			DebaterPicker.Items.Add(GetDebaterDisplayName(debater));
		}
		DebaterPicker.SelectedIndex = Math.Clamp(_settings.NextDebaterIndex, 0, Math.Max(0, _debaters.Count - 1));
		_isLoading = false;
		LoadSelectedDebater();
		RefreshAiPoolCards();
		RefreshRosterAssignments();
		RefreshAll();
	}

	private void RefreshAll()
	{
		RefreshMessages();
		RefreshAttachmentList();
		RefreshStatus();
		SaveState();
	}

	private async void OnNextTurnClicked(object? sender, EventArgs e)
	{
		await RunNextTurnAsync();
	}

	private async Task RunNextTurnAsync()
	{
		if (_isBusy)
		{
			AddLog("当前已有 AI 正在发言。");
			return;
		}

		var enabled = GetParticipants();
		if (enabled.Count == 0)
		{
			await DisplayAlert("无法开始", "请至少在正方或反方勾选一位 AI。", "知道了");
			return;
		}

		if (!enabled.Any(d => d.Side == DebateSide.Pro) || !enabled.Any(d => d.Side == DebateSide.Con))
		{
			await DisplayAlert("站位不完整", "请至少为正方和反方各勾选一位 AI。", "知道了");
			return;
		}

		var debater = enabled[_settings.NextDebaterIndex % enabled.Count];
		if (string.IsNullOrWhiteSpace(debater.ApiKey))
		{
			await DisplayAlert("缺少 API Key", $"请先为 {GetDebaterDisplayName(debater)} 填写 API Key。", "知道了");
			return;
		}

		_isBusy = true;
		_turnCts = new CancellationTokenSource();
		SetBusy(true, $"{GetDebaterDisplayName(debater)} 正在发言...");

		try
		{
			var context = _contextComposer.BuildDebateMessages(_settings, debater, _messages, _attachments, _interjectionQueue.ToList(), enabled);
			var content = await _chatClient.CompleteAsync(debater, context, _turnCts.Token);
			if (string.IsNullOrWhiteSpace(content))
			{
				content = "（接口返回了空内容）";
			}

			_messages.Add(new DebateMessage
			{
				Role = DebateRole.Debater,
				Speaker = GetDebaterDisplayName(debater),
				ModelName = debater.Model,
				Phase = $"第 {_settings.CurrentRound + 1} 轮",
				Content = content
			});

			await FlushQueuedInterjectionsAsync();
			AdvanceTurn(enabled.Count);
			await MaybeCompressContextAsync(debater, _turnCts.Token);
			AddLog($"{GetDebaterDisplayName(debater)} 已完成发言。");
		}
		catch (OperationCanceledException)
		{
			AddLog("本次发言已取消。");
		}
		catch (Exception ex)
		{
			AddSystemMessage($"请求失败：{ex.Message}");
			AddLog($"请求失败：{ex.Message}");
		}
		finally
		{
			_isBusy = false;
			SetBusy(false, "准备就绪");
			RefreshAll();
		}
	}

	private async Task FlushQueuedInterjectionsAsync()
	{
		while (_interjectionQueue.Count > 0)
		{
			var text = _interjectionQueue.Dequeue();
			_messages.Add(new DebateMessage
			{
				Role = DebateRole.Human,
				Speaker = "用户插话",
				Content = text,
				IsQueuedHumanInterjection = true
			});
			await Task.Delay(40);
		}
	}

	private void AdvanceTurn(int enabledCount)
	{
		_settings.NextDebaterIndex++;
		if (_settings.NextDebaterIndex >= enabledCount)
		{
			_settings.NextDebaterIndex = 0;
			_settings.CurrentRound++;
		}

		if (_settings.CurrentRound >= _settings.MaxRounds)
		{
			_settings.AutoContinue = false;
		}
	}

	private async Task MaybeCompressContextAsync(DebaterConfig debater, CancellationToken cancellationToken)
	{
		if (!_settings.CompressionEnabled || _messages.Count < _settings.CompressAfterMessages)
		{
			return;
		}

		var keep = Math.Max(4, _settings.KeepRecentMessages);
		var toCompress = _messages.Take(Math.Max(0, _messages.Count - keep)).ToList();
		if (toCompress.Count == 0)
		{
			return;
		}

		var compressionMessages = _contextComposer.BuildCompressionMessages(_settings.MemorySummary, toCompress);
		try
		{
			var summary = await _chatClient.CompleteAsync(debater, compressionMessages, cancellationToken);
			if (!string.IsNullOrWhiteSpace(summary))
			{
				_settings.MemorySummary = summary.Trim();
				_messages.RemoveRange(0, toCompress.Count);
				AddLog("已更新长对话压缩记忆。");
			}
		}
		catch (Exception ex)
		{
			AddLog($"自动压缩失败：{ex.Message}");
		}
	}

	private async void OnSendInterjectionClicked(object? sender, EventArgs e)
	{
		var text = UserInputEditor.Text?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		UserInputEditor.Text = string.Empty;
		if (_isBusy)
		{
			_interjectionQueue.Enqueue(text);
			AddQueuedMessage(text);
			AddLog("人工插话已进入等待队列。");
		}
		else
		{
			_messages.Add(new DebateMessage { Role = DebateRole.Human, Speaker = "用户", Content = text });
			AddLog("人工插话已插入对话。");
		}

		RefreshAll();
		await ScrollToBottomAsync();
	}

	private async void OnInsertNowClicked(object? sender, EventArgs e)
	{
		var text = UserInputEditor.Text?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		UserInputEditor.Text = string.Empty;
		_messages.Add(new DebateMessage { Role = DebateRole.Human, Speaker = "用户", Content = text });
		AddLog(_isBusy ? "已强制插入，但当前 AI 请求不会读取这条新内容。" : "已立即插入。");
		RefreshAll();
		await ScrollToBottomAsync();
	}

	private async void OnAttachFileClicked(object? sender, EventArgs e)
	{
		try
		{
			var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "选择要解析的文件" });
			if (file is null)
			{
				return;
			}

			SetBusy(true, "正在解析文件...");
			var attachment = await _fileParsingService.ParseAsync(file, CancellationToken.None);
			_attachments.Add(attachment);
			_messages.Add(new DebateMessage { Role = DebateRole.Tool, Speaker = "文件解析", Content = $"已加入资料：{attachment.FileName}\n{Preview(attachment.Content, 500)}" });
			AddLog($"已解析文件：{attachment.FileName}");
		}
		catch (Exception ex)
		{
			await DisplayAlert("文件解析失败", ex.Message, "知道了");
		}
		finally
		{
			SetBusy(_isBusy, _isBusy ? "AI 正在发言..." : "准备就绪");
			RefreshAll();
		}
	}

	private async void OnOcrClicked(object? sender, EventArgs e)
	{
		try
		{
			var file = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "选择要 OCR 的图片",
				FileTypes = FilePickerFileType.Images
			});
			if (file is null)
			{
				return;
			}

			SetBusy(true, "正在处理 OCR 入口...");
			var attachment = await _ocrService.RecognizeAsync(file, _debaters.FirstOrDefault(d => d.Enabled), CancellationToken.None);
			_attachments.Add(attachment);
			_messages.Add(new DebateMessage { Role = DebateRole.Tool, Speaker = "OCR", Content = $"已加入 OCR 资料：{attachment.FileName}\n{attachment.Content}" });
			AddLog($"OCR 入口已接收图片：{attachment.FileName}");
		}
		catch (Exception ex)
		{
			await DisplayAlert("OCR 失败", ex.Message, "知道了");
		}
		finally
		{
			SetBusy(_isBusy, _isBusy ? "AI 正在发言..." : "准备就绪");
			RefreshAll();
		}
	}

	private async void OnCompressClicked(object? sender, EventArgs e)
	{
		var debater = _debaters.FirstOrDefault(d => d.Enabled && !string.IsNullOrWhiteSpace(d.ApiKey));
		if (debater is null)
		{
			await DisplayAlert("无法压缩", "请至少配置一位可用辩手 API。", "知道了");
			return;
		}

		SetBusy(true, "正在压缩上下文...");
		await MaybeCompressContextAsync(debater, CancellationToken.None);
		SetBusy(false, "准备就绪");
		RefreshAll();
	}

	private async void OnAutoClicked(object? sender, EventArgs e)
	{
		_settings.AutoContinue = !_settings.AutoContinue;
		RefreshStatus();
		SaveState();
		if (_settings.AutoContinue && !_isAutoLoopRunning)
		{
			await RunAutoLoopAsync();
		}
	}

	private async Task RunAutoLoopAsync()
	{
		_isAutoLoopRunning = true;
		try
		{
			while (_settings.AutoContinue && _settings.CurrentRound < _settings.MaxRounds)
			{
				await RunNextTurnAsync();
				if (_settings.AutoContinue && _settings.CurrentRound < _settings.MaxRounds)
				{
					await Task.Delay(600);
				}
			}
		}
		finally
		{
			_isAutoLoopRunning = false;
			RefreshStatus();
			SaveState();
		}
	}

	private async void OnClearClicked(object? sender, EventArgs e)
	{
		var ok = await DisplayAlert("清空对话", "将清空当前消息、插话队列和轮次，但保留 API 配置。", "清空", "取消");
		if (!ok)
		{
			return;
		}

		_messages.Clear();
		_interjectionQueue.Clear();
		_settings.CurrentRound = 0;
		_settings.NextDebaterIndex = 0;
		_settings.MemorySummary = string.Empty;
		RefreshAll();
	}

	private async void OnExportClicked(object? sender, EventArgs e)
	{
		var path = Path.Combine(FileSystem.AppDataDirectory, $"AI辩论记录-{DateTime.Now:yyyyMMdd-HHmmss}.md");
		var builder = new StringBuilder();
		builder.AppendLine($"# {_settings.Topic}");
		builder.AppendLine();
		builder.AppendLine($"规则：{_settings.Rules}");
		builder.AppendLine();
		if (!string.IsNullOrWhiteSpace(_settings.MemorySummary))
		{
			builder.AppendLine("## 压缩记忆");
			builder.AppendLine(_settings.MemorySummary);
			builder.AppendLine();
		}

		builder.AppendLine("## 对话");
		foreach (var message in _messages)
		{
			builder.AppendLine($"### {message.Speaker} · {message.CreatedAt:yyyy-MM-dd HH:mm}");
			builder.AppendLine(message.Content);
			builder.AppendLine();
		}

		await File.WriteAllTextAsync(path, builder.ToString());
		await DisplayAlert("已导出", $"文件已保存：\n{path}", "知道了");
	}

	private async void OnTestApiClicked(object? sender, EventArgs e)
	{
		var debater = CurrentDebater;
		if (debater is null)
		{
			return;
		}

		SetBusy(true, "正在测试接口...");
		try
		{
			var answer = await _chatClient.CompleteAsync(debater,
			[
				new ChatRequestMessage("system", "你是接口连通性测试助手。"),
				new ChatRequestMessage("user", "请用一句中文回复：接口连接正常。")
			], CancellationToken.None);
			await DisplayAlert("测试成功", answer, "知道了");
		}
		catch (Exception ex)
		{
			await DisplayAlert("测试失败", ex.Message, "知道了");
		}
		finally
		{
			SetBusy(false, "准备就绪");
		}
	}

	private void RefreshMessages()
	{
		MessageStack.Children.Clear();
		if (_messages.Count == 0 && _interjectionQueue.Count == 0)
		{
			MessageStack.Children.Add(CreateInfoCard("欢迎使用 AI 辩论工作室", "配置右侧辩手和 API 后，点击“下一位发言”开始。你在底栏输入的插话会先排队，等当前 AI 说完后再插入正式对话。"));
			return;
		}

		var hiddenCount = Math.Max(0, _messages.Count - 80);
		if (hiddenCount > 0)
		{
			MessageStack.Children.Add(CreateInfoCard("已折叠较早消息", $"为保持界面流畅，当前只显示最近 80 条消息。较早 {hiddenCount} 条仍会参与压缩记忆和导出。"));
		}

		foreach (var message in _messages.Skip(hiddenCount))
		{
			MessageStack.Children.Add(CreateMessageCard(message));
		}

		foreach (var queued in _interjectionQueue)
		{
			MessageStack.Children.Add(CreateQueuedCard(queued));
		}
	}

	private View CreateMessageCard(DebateMessage message)
	{
		var isAi = message.Role == DebateRole.Debater;
		var isHuman = message.Role == DebateRole.Human;
		var border = new Border
		{
			Padding = 12,
			StrokeThickness = isAi ? 1 : 0,
			BackgroundColor = GetThemedColor(
				isHuman ? ControlBgColor : message.Role == DebateRole.Tool ? PanelAltColor : PanelBgColor,
				isHuman ? ControlBgDarkColor : message.Role == DebateRole.Tool ? PanelAltDarkColor : PanelBgDarkColor),
			Content = new VerticalStackLayout
			{
				Spacing = 6,
				Children =
				{
					new Label
					{
						Text = $"{message.Speaker}{(string.IsNullOrWhiteSpace(message.ModelName) ? string.Empty : $" · {message.ModelName}")}",
						FontFamily = "OpenSansSemibold",
						FontSize = 13,
						TextColor = isAi ? GetThemedColor(PrimaryColor, ChipTextDarkColor) : GetThemedColor(TextStrongColor, TextStrongDarkColor)
					},
					CreateMarkdownView(message.Content),
					new Label
					{
						Text = $"{message.CreatedAt:HH:mm} {message.Phase}",
						Style = CaptionStyle
					}
				}
			}
		};
		border.Loaded += async (_, _) =>
		{
			border.Opacity = 0;
			border.TranslationY = 12;
			await Task.WhenAll(border.FadeTo(1, 180, Easing.CubicOut), border.TranslateTo(0, 0, 180, Easing.CubicOut));
		};
		return border;
	}

	private View CreateQueuedCard(string text)
	{
		return CreateInfoCard("等待插入的人工插话", text);
	}

	private View CreateMarkdownView(string markdown)
	{
		var stack = new VerticalStackLayout { Spacing = 6 };
		var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');
		var inCodeBlock = false;
		var code = new StringBuilder();

		foreach (var rawLine in lines)
		{
			var line = rawLine.TrimEnd();
			if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
			{
				if (inCodeBlock)
				{
					stack.Children.Add(CreateCodeBlock(code.ToString().TrimEnd()));
					code.Clear();
					inCodeBlock = false;
				}
				else
				{
					inCodeBlock = true;
				}

				continue;
			}

			if (inCodeBlock)
			{
				code.AppendLine(line);
				continue;
			}

			if (string.IsNullOrWhiteSpace(line))
			{
				stack.Children.Add(new BoxView { HeightRequest = 2, Opacity = 0 });
				continue;
			}

			stack.Children.Add(CreateMarkdownLine(line));
		}

		if (inCodeBlock && code.Length > 0)
		{
			stack.Children.Add(CreateCodeBlock(code.ToString().TrimEnd()));
		}

		return stack;
	}

	private View CreateMarkdownLine(string line)
	{
		var trimmed = line.TrimStart();
		var fontSize = 14d;
		var prefix = string.Empty;
		var content = line;
		var semibold = false;

		if (trimmed.StartsWith("### ", StringComparison.Ordinal))
		{
			fontSize = 15;
			content = trimmed[4..];
			semibold = true;
		}
		else if (trimmed.StartsWith("## ", StringComparison.Ordinal))
		{
			fontSize = 16;
			content = trimmed[3..];
			semibold = true;
		}
		else if (trimmed.StartsWith("# ", StringComparison.Ordinal))
		{
			fontSize = 17;
			content = trimmed[2..];
			semibold = true;
		}
		else if (trimmed.StartsWith("> ", StringComparison.Ordinal))
		{
			prefix = "引用：";
			content = trimmed[2..];
		}
		else if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
		{
			prefix = "• ";
			content = trimmed[2..];
		}
		else
		{
			var dot = trimmed.IndexOf(". ", StringComparison.Ordinal);
			if (dot > 0 && dot <= 3 && trimmed[..dot].All(char.IsDigit))
			{
				prefix = $"{trimmed[..(dot + 1)]} ";
				content = trimmed[(dot + 2)..];
			}
		}

		var label = new Label
		{
			FontSize = fontSize,
			LineHeight = 1.18,
			TextColor = GetThemedColor(TextStrongColor, TextStrongDarkColor),
			FontFamily = semibold ? "OpenSansSemibold" : "OpenSansRegular",
			FormattedText = ParseInlineMarkdown(prefix, content)
		};
		return label;
	}

	private View CreateCodeBlock(string code)
	{
		return new Border
		{
			Padding = 10,
			StrokeThickness = 0,
			BackgroundColor = GetThemedColor(ControlBgColor, ControlBgDarkColor),
			Content = new Label
			{
				Text = code,
				FontFamily = "monospace",
				FontSize = 13,
				LineHeight = 1.15,
				TextColor = GetThemedColor(TextStrongColor, TextStrongDarkColor)
			}
		};
	}

	private FormattedString ParseInlineMarkdown(string prefix, string content)
	{
		var formatted = new FormattedString();
		if (!string.IsNullOrEmpty(prefix))
		{
			formatted.Spans.Add(new Span { Text = prefix, FontFamily = "OpenSansSemibold" });
		}

		var i = 0;
		while (i < content.Length)
		{
			if (TryReadInlineToken(content, i, "**", out var bold, out var boldNext))
			{
				formatted.Spans.Add(new Span { Text = bold, FontFamily = "OpenSansSemibold" });
				i = boldNext;
			}
			else if (TryReadInlineToken(content, i, "`", out var code, out var codeNext))
			{
				formatted.Spans.Add(new Span
				{
					Text = code,
					FontFamily = "monospace",
					BackgroundColor = GetThemedColor(ControlBgColor, ControlBgDarkColor)
				});
				i = codeNext;
			}
			else if (TryReadInlineToken(content, i, "*", out var italic, out var italicNext))
			{
				formatted.Spans.Add(new Span { Text = italic, FontAttributes = FontAttributes.Italic });
				i = italicNext;
			}
			else
			{
				var next = FindNextMarkdownToken(content, i);
				formatted.Spans.Add(new Span { Text = content[i..next] });
				i = next;
			}
		}

		return formatted;
	}

	private static bool TryReadInlineToken(string text, int start, string marker, out string value, out int next)
	{
		value = string.Empty;
		next = start;
		if (!text.AsSpan(start).StartsWith(marker, StringComparison.Ordinal))
		{
			return false;
		}

		var contentStart = start + marker.Length;
		var end = text.IndexOf(marker, contentStart, StringComparison.Ordinal);
		if (end < 0)
		{
			return false;
		}

		value = text[contentStart..end];
		next = end + marker.Length;
		return true;
	}

	private static int FindNextMarkdownToken(string text, int start)
	{
		var indexes = new[]
		{
			text.IndexOf("**", start, StringComparison.Ordinal),
			text.IndexOf('`', start),
			text.IndexOf('*', start)
		}.Where(index => index >= 0);
		return indexes.Any() ? indexes.Min() : text.Length;
	}

	private View CreateInfoCard(string title, string body)
	{
		return new Border
		{
			Padding = 14,
			BackgroundColor = GetThemedColor(PanelAltColor, PanelAltDarkColor),
			StrokeThickness = 0,
			Content = new VerticalStackLayout
			{
				Spacing = 6,
				Children =
				{
					new Label { Text = title, FontFamily = "OpenSansSemibold", FontSize = 16 },
					new Label { Text = body, Style = CaptionStyle }
				}
			}
		};
	}

	private Color GetThemedColor(Color light, Color dark)
	{
		return Application.Current?.RequestedTheme == AppTheme.Dark ? dark : light;
	}

	private void RefreshAttachmentList()
	{
		AttachmentStack.Children.Clear();
		AttachmentSummaryLabel.Text = _attachments.Count == 0 ? "暂无资料" : $"已加入 {_attachments.Count} 个资料";
		foreach (var item in _attachments.TakeLast(5))
		{
			AttachmentStack.Children.Add(new Label { Text = $"{item.Kind} · {item.FileName}", Style = CaptionStyle });
		}
	}

	private void RefreshStatus()
	{
		var proCount = _debaters.Count(d => d.Side == DebateSide.Pro);
		var conCount = _debaters.Count(d => d.Side == DebateSide.Con);
		FlowSummaryLabel.Text = $"第 {_settings.CurrentRound} / {_settings.MaxRounds} 轮 · 下位：{NextDebaterName()}";
		QueueBadgeLabel.Text = $"插话队列 {_interjectionQueue.Count}";
		DebaterSummaryLabel.Text = $"{_debaters.Count} 个 AI · 正方 {proCount} · 反方 {conCount}";
		AutoButton.Text = _settings.AutoContinue ? "停止自动" : "自动辩论";
		RoundCountLabel.Text = _settings.MaxRounds.ToString();
		MemoryLabel.Text = string.IsNullOrWhiteSpace(_settings.MemorySummary) ? "尚未生成压缩记忆。" : Preview(_settings.MemorySummary, 900);
		ChatSubtitleLabel.Text = _isBusy ? "当前 AI 正在发言；人工插话会等待本次发言结束后插入。" : "正反方 AI 按站位轮流发言，同队会承接队友论点。";
	}

	private string NextDebaterName()
	{
		var participants = GetParticipants();
		return participants.Count == 0 ? "无" : GetDebaterDisplayName(participants[_settings.NextDebaterIndex % participants.Count]);
	}

	private void AddQueuedMessage(string text)
	{
		MessageStack.Children.Add(CreateQueuedCard(text));
	}

	private void AddSystemMessage(string text)
	{
		_messages.Add(new DebateMessage { Role = DebateRole.System, Speaker = "系统", Content = text });
	}

	private void AddLog(string text)
	{
		LogLabel.Text = $"日志：{DateTime.Now:HH:mm:ss} {text}";
	}

	private void SetBusy(bool busy, string status)
	{
		BusyIndicator.IsVisible = busy;
		BusyIndicator.IsRunning = busy;
		ChatSubtitleLabel.Text = status;
	}

	private async Task ScrollToBottomAsync()
	{
		await Task.Delay(60);
		await ChatScrollView.ScrollToAsync(0, MessageStack.Height, true);
	}

	private DebaterConfig? CurrentDebater => DebaterPicker.SelectedIndex >= 0 && DebaterPicker.SelectedIndex < _debaters.Count ? _debaters[DebaterPicker.SelectedIndex] : null;

	private void OnDebaterSelected(object? sender, EventArgs e)
	{
		if (_isLoading)
		{
			return;
		}

		LoadSelectedDebater();
	}

	private void LoadSelectedDebater()
	{
		var debater = CurrentDebater;
		if (debater is null)
		{
			CurrentAiLabel.Text = "正在编辑：未选择";
			return;
		}

		_isLoading = true;
		CurrentAiLabel.Text = $"正在编辑：{GetDebaterDisplayName(debater)}";
		DebaterNameEntry.Text = debater.Name;
		DebaterPersonaEditor.Text = debater.Persona;
		BaseUrlEntry.Text = debater.BaseUrl;
		ModelEntry.Text = debater.Model;
		ApiKeyEntry.Text = debater.ApiKey;
		TemperatureSlider.Value = debater.Temperature;
		MaxTokensSlider.Value = debater.MaxTokens;
		TemperatureLabel.Text = debater.Temperature.ToString("0.0");
		MaxTokensLabel.Text = debater.MaxTokens.ToString();
		var presetIndex = Math.Max(0, ApiPresetCatalog.All.ToList().FindIndex(p => p.Id == debater.PresetId));
		ProviderPicker.SelectedIndex = presetIndex;
		RefreshModelPicker(ApiPresetCatalog.Find(debater.PresetId), debater.Model);
		_isLoading = false;
	}

	private void OnProviderSelected(object? sender, EventArgs e)
	{
		if (_isLoading || CurrentDebater is not { } debater || ProviderPicker.SelectedIndex < 0)
		{
			return;
		}

		var preset = ApiPresetCatalog.All[ProviderPicker.SelectedIndex];
		debater.PresetId = preset.Id;
		debater.ProviderKind = preset.Kind;
		debater.BaseUrl = preset.BaseUrl;
		debater.Model = preset.DefaultModel;
		debater.Name = preset.DefaultModel;

		LoadSelectedDebater();
		RefreshDebaterPickerNames();
		RefreshAiPoolCards();
		RefreshRosterAssignments();
		SaveState();
	}

	private void OnModelSelected(object? sender, EventArgs e)
	{
		if (_isLoading || CurrentDebater is not { } debater || ModelPicker.SelectedIndex < 0)
		{
			return;
		}

		var selected = ModelPicker.Items[ModelPicker.SelectedIndex];
		if (string.IsNullOrWhiteSpace(selected))
		{
			return;
		}

		debater.Model = selected;
		if (string.IsNullOrWhiteSpace(debater.Name) || debater.Name == ModelEntry.Text)
		{
			debater.Name = selected;
		}

		_isLoading = true;
		ModelEntry.Text = selected;
		DebaterNameEntry.Text = debater.Name;
		_isLoading = false;
		RefreshDebaterPickerNames();
		RefreshRosterAssignments();
		SaveState();
	}

	private async void OnRefreshModelsClicked(object? sender, EventArgs e)
	{
		if (CurrentDebater is not { } debater)
		{
			return;
		}

		if (!IsSiliconFlow(debater))
		{
			await DisplayAlert("无法刷新", "当前 AI 不是硅基流动预设。", "知道了");
			return;
		}

		SetBusy(true, "正在读取硅基流动模型列表...");
		try
		{
			var models = await _siliconFlowModelService.GetChatModelsAsync(debater.BaseUrl, debater.ApiKey, CancellationToken.None);
			if (models.Count == 0)
			{
				await DisplayAlert("没有模型", "硅基流动返回了空模型列表。", "知道了");
				return;
			}

			RefreshModelPicker(ApiPresetCatalog.Find(debater.PresetId), debater.Model, models);
			AddLog($"已读取硅基流动模型列表：{models.Count} 个。");
		}
		catch (Exception ex)
		{
			await DisplayAlert("模型列表失败", ex.Message, "知道了");
		}
		finally
		{
			SetBusy(_isBusy, _isBusy ? "AI 正在发言..." : "准备就绪");
		}
	}

	private void OnDebaterConfigChanged(object? sender, TextChangedEventArgs e)
	{
		if (_isLoading || CurrentDebater is not { } debater)
		{
			return;
		}

		var previousName = GetAiPoolName(debater);
		var previousModel = debater.Model;
		debater.Name = string.IsNullOrWhiteSpace(DebaterNameEntry.Text) ? ModelEntry.Text?.Trim() ?? "未命名模型" : DebaterNameEntry.Text.Trim();
		debater.Persona = DebaterPersonaEditor.Text?.Trim() ?? string.Empty;
		debater.BaseUrl = BaseUrlEntry.Text?.Trim() ?? string.Empty;
		debater.Model = ModelEntry.Text?.Trim() ?? string.Empty;
		debater.ApiKey = ApiKeyEntry.Text?.Trim() ?? string.Empty;

		if (!ReferenceEquals(sender, ApiKeyEntry))
		{
			if (!previousModel.Equals(debater.Model, StringComparison.OrdinalIgnoreCase))
			{
				RefreshModelPicker(ApiPresetCatalog.Find(debater.PresetId), debater.Model);
			}

			if (!previousName.Equals(GetAiPoolName(debater), StringComparison.Ordinal))
			{
				RefreshDebaterPickerNames();
				RefreshAiPoolCards();
				RefreshRosterAssignments();
			}
		}

		SaveState();
	}

	private void OnSidePromptChanged(object? sender, TextChangedEventArgs e)
	{
		if (_isLoading)
		{
			return;
		}

		_settings.ProSidePrompt = ProPromptEditor.Text?.Trim() ?? string.Empty;
		_settings.ConSidePrompt = ConPromptEditor.Text?.Trim() ?? string.Empty;
		SaveState();
	}

	private void OnAddAiClicked(object? sender, EventArgs e)
	{
		var preset = ApiPresetCatalog.Find("siliconflow");
		_debaters.Add(FromPreset(preset, preset.DefaultModel, DebateSide.Neutral, string.Empty));
		RefreshDebaterPickerNames();
		SelectDebater(_debaters.Count - 1);
		RefreshRosterAssignments();
		RefreshStatus();
		SaveState();
		AddLog("已添加一个未站位 AI，请填写 API Key 后勾选正方或反方。");
	}

	private async void OnGenerateSidePromptsClicked(object? sender, EventArgs e)
	{
		var topic = TopicEditor.Text?.Trim();
		if (string.IsNullOrWhiteSpace(topic))
		{
			await DisplayAlert("缺少辩题", "请先填写辩题，再生成正反方前置提示词。", "知道了");
			return;
		}

		var generator = FindDeepSeekPromptGenerator();
		if (generator is null)
		{
			await DisplayAlert("缺少 DeepSeek", "请先在 AI 池添加 DeepSeek，并填写 API Key。", "知道了");
			return;
		}

		SetBusy(true, "DeepSeek 正在生成正反方前置提示词...");
		try
		{
			var messages = _contextComposer.BuildSidePromptGenerationMessages(topic);
			var json = await _chatClient.CompleteAsync(generator, messages, CancellationToken.None);
			ApplyGeneratedSidePrompts(json);
			AddLog("DeepSeek 已生成正反方前置提示词。");
		}
		catch (Exception ex)
		{
			await DisplayAlert("生成失败", ex.Message, "知道了");
		}
		finally
		{
			SetBusy(_isBusy, _isBusy ? "AI 正在发言..." : "准备就绪");
			RefreshStatus();
			SaveState();
		}
	}

	private void OnTemperatureChanged(object? sender, ValueChangedEventArgs e)
	{
		TemperatureLabel.Text = e.NewValue.ToString("0.0");
		if (!_isLoading && CurrentDebater is { } debater)
		{
			debater.Temperature = Math.Round(e.NewValue, 1);
			SaveState();
		}
	}

	private void OnMaxTokensChanged(object? sender, ValueChangedEventArgs e)
	{
		var value = (int)Math.Round(e.NewValue / 100) * 100;
		MaxTokensLabel.Text = value.ToString();
		if (!_isLoading && CurrentDebater is { } debater)
		{
			debater.MaxTokens = value;
			SaveState();
		}
	}

	private void OnDebateSettingChanged(object? sender, TextChangedEventArgs e)
	{
		if (_isLoading)
		{
			return;
		}

		_settings.Topic = TopicEditor.Text?.Trim() ?? string.Empty;
		_settings.Rules = RulesEditor.Text?.Trim() ?? string.Empty;
		SaveState();
	}

	private void OnRoundSliderChanged(object? sender, ValueChangedEventArgs e)
	{
		_settings.MaxRounds = Math.Max(1, (int)Math.Round(e.NewValue));
		RoundCountLabel.Text = _settings.MaxRounds.ToString();
		RefreshStatus();
		SaveState();
	}

	private void OnCompressionToggled(object? sender, ToggledEventArgs e)
	{
		_settings.CompressionEnabled = e.Value;
		SaveState();
	}

	private void OnSaveClicked(object? sender, EventArgs e)
	{
		SaveStateNow();
		AddLog("配置已保存。");
	}

	private void RefreshDebaterPickerNames()
	{
		var selected = DebaterPicker.SelectedIndex;
		DebaterPicker.Items.Clear();
		foreach (var debater in _debaters)
		{
			DebaterPicker.Items.Add(GetDebaterDisplayName(debater));
		}
		DebaterPicker.SelectedIndex = Math.Clamp(selected, 0, Math.Max(0, _debaters.Count - 1));
		RefreshAiPoolCards();
	}

	private void SelectDebater(int index)
	{
		if (_debaters.Count == 0)
		{
			return;
		}

		_isLoading = true;
		DebaterPicker.SelectedIndex = Math.Clamp(index, 0, _debaters.Count - 1);
		_isLoading = false;
		LoadSelectedDebater();
		RefreshAiPoolCards();
	}

	private void RefreshAiPoolCards()
	{
		AiPoolStack.Children.Clear();
		if (_debaters.Count == 0)
		{
			AiPoolStack.Children.Add(CreateInfoCard("AI 池为空", "点击“新增 AI”添加硅基流动模型。"));
			return;
		}

		for (var i = 0; i < _debaters.Count; i++)
		{
			AiPoolStack.Children.Add(CreateAiPoolCard(_debaters[i], i));
		}
	}

	private View CreateAiPoolCard(DebaterConfig debater, int index)
	{
		var selected = index == DebaterPicker.SelectedIndex;
		var border = new Border
		{
			Padding = 10,
			StrokeThickness = selected ? 2 : 1,
			Stroke = new SolidColorBrush(selected ? PrimaryColor : GetThemedColor(PanelAltColor, PanelAltDarkColor)),
			BackgroundColor = GetThemedColor(PanelAltColor, PanelAltDarkColor)
		};

		var title = new Label
		{
			Text = GetDebaterDisplayName(debater),
			FontFamily = "OpenSansSemibold",
			FontSize = 13,
			TextColor = GetThemedColor(TextStrongColor, TextStrongDarkColor)
		};

		var details = new Label
		{
			Text = $"硅基流动 · {debater.Model}\nKey：{(string.IsNullOrWhiteSpace(debater.ApiKey) ? "未填写" : "已填写")} · Base：{Preview(debater.BaseUrl, 42)}",
			Style = CaptionStyle
		};

		var editButton = new Button
		{
			Text = "编辑",
			Padding = new Thickness(12, 6),
			MinimumHeightRequest = 34,
			BackgroundColor = GetThemedColor(ControlBgColor, ControlBgDarkColor),
			TextColor = GetThemedColor(TextStrongColor, TextStrongDarkColor)
		};
		editButton.Clicked += (_, _) => SelectDebater(index);

		var deleteButton = new Button
		{
			Text = "删除",
			Padding = new Thickness(12, 6),
			MinimumHeightRequest = 34,
			BackgroundColor = GetThemedColor(PanelBgColor, PanelBgDarkColor),
			TextColor = (Color)Application.Current!.Resources["Danger"]
		};
		deleteButton.Clicked += async (_, _) => await DeleteDebaterAsync(index);

		var buttonRow = new HorizontalStackLayout
		{
			Spacing = 8,
			Children = { editButton, deleteButton }
		};

		border.Content = new VerticalStackLayout
		{
			Spacing = 8,
			Children = { title, details, buttonRow }
		};

		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => SelectDebater(index);
		border.GestureRecognizers.Add(tap);
		return border;
	}

	private async Task DeleteDebaterAsync(int index)
	{
		if (index < 0 || index >= _debaters.Count)
		{
			return;
		}

		if (_debaters.Count == 1)
		{
			await DisplayAlert("无法删除", "AI 池至少保留一个硅基流动配置。", "知道了");
			return;
		}

		var name = GetDebaterDisplayName(_debaters[index]);
		var ok = await DisplayAlert("删除 AI", $"确定删除 {name}？", "删除", "取消");
		if (!ok)
		{
			return;
		}

		_debaters.RemoveAt(index);
		if (_settings.NextDebaterIndex >= _debaters.Count)
		{
			_settings.NextDebaterIndex = 0;
		}

		RefreshDebaterPickerNames();
		SelectDebater(Math.Min(index, _debaters.Count - 1));
		RefreshRosterAssignments();
		RefreshStatus();
		SaveStateNow();
		AddLog($"已删除 AI：{name}");
	}

	private void RefreshModelPicker(ApiProviderPreset preset, string currentModel, IReadOnlyList<string>? dynamicModels = null)
	{
		var models = new List<string>();
		if (dynamicModels is not null)
		{
			models.AddRange(dynamicModels);
		}

		models.AddRange(preset.Models);
		if (!string.IsNullOrWhiteSpace(preset.DefaultModel))
		{
			models.Add(preset.DefaultModel);
		}

		if (!string.IsNullOrWhiteSpace(currentModel))
		{
			models.Add(currentModel);
		}

		models = models
			.Where(model => !string.IsNullOrWhiteSpace(model))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		ModelPicker.Items.Clear();
		foreach (var model in models)
		{
			ModelPicker.Items.Add(model);
		}

		ModelPicker.IsEnabled = models.Count > 0;
		ModelPicker.SelectedIndex = string.IsNullOrWhiteSpace(currentModel)
			? -1
			: models.FindIndex(model => model.Equals(currentModel, StringComparison.OrdinalIgnoreCase));
		RefreshModelsButton.IsVisible = preset.Id == "siliconflow";
		RefreshModelsButton.IsEnabled = preset.Id == "siliconflow";
		ProviderHelpLabel.Text = preset.HelpText;
	}

	private void RefreshRosterAssignments()
	{
		ProRosterStack.Children.Clear();
		ConRosterStack.Children.Clear();
		foreach (var debater in _debaters)
		{
			ProRosterStack.Children.Add(CreateSideAssignmentRow(debater, DebateSide.Pro));
			ConRosterStack.Children.Add(CreateSideAssignmentRow(debater, DebateSide.Con));
		}
	}

	private View CreateSideAssignmentRow(DebaterConfig debater, DebateSide side)
	{
		var checkBox = new CheckBox
		{
			IsChecked = debater.Side == side,
			VerticalOptions = LayoutOptions.Center
		};
		checkBox.CheckedChanged += (_, args) =>
		{
			if (_isLoading)
			{
				return;
			}

			debater.Side = args.Value ? side : DebateSide.Neutral;
			debater.Position = debater.Side == DebateSide.Neutral ? string.Empty : GetSideName(debater.Side);
			debater.Enabled = debater.Side != DebateSide.Neutral;
			RefreshDebaterPickerNames();
			RefreshRosterAssignments();
			RefreshStatus();
			SaveState();
		};

		var label = new Label
		{
			Text = GetAiPoolName(debater),
			Style = CaptionStyle,
			VerticalOptions = LayoutOptions.Center
		};
		Grid.SetColumn(label, 1);

		var row = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition { Width = GridLength.Auto },
				new ColumnDefinition { Width = GridLength.Star }
			},
			ColumnSpacing = 6
		};
		row.Children.Add(checkBox);
		row.Children.Add(label);
		return row;
	}

	private List<DebaterConfig> GetParticipants()
	{
		var pro = _debaters.Where(d => d.Side == DebateSide.Pro).ToList();
		var con = _debaters.Where(d => d.Side == DebateSide.Con).ToList();
		var participants = new List<DebaterConfig>();
		var max = Math.Max(pro.Count, con.Count);
		for (var i = 0; i < max; i++)
		{
			if (i < pro.Count)
			{
				participants.Add(pro[i]);
			}

			if (i < con.Count)
			{
				participants.Add(con[i]);
			}
		}

		return participants;
	}

	private DebaterConfig? FindDeepSeekPromptGenerator()
	{
		return _debaters.FirstOrDefault(d =>
			!string.IsNullOrWhiteSpace(d.ApiKey) &&
			(d.PresetId.Equals("deepseek", StringComparison.OrdinalIgnoreCase) ||
			 d.BaseUrl.Contains("deepseek", StringComparison.OrdinalIgnoreCase) ||
			 d.Model.Contains("deepseek", StringComparison.OrdinalIgnoreCase)));
	}

	private void ApplyGeneratedSidePrompts(string raw)
	{
		var json = ExtractJsonObject(raw);
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		var pro = root.TryGetProperty("pro", out var proElement) ? proElement.GetString() : null;
		var con = root.TryGetProperty("con", out var conElement) ? conElement.GetString() : null;
		if (string.IsNullOrWhiteSpace(pro) || string.IsNullOrWhiteSpace(con))
		{
			throw new InvalidOperationException("DeepSeek 返回内容中没有找到 pro/con 字段。");
		}

		_settings.ProSidePrompt = pro.Trim();
		_settings.ConSidePrompt = con.Trim();
		_isLoading = true;
		ProPromptEditor.Text = _settings.ProSidePrompt;
		ConPromptEditor.Text = _settings.ConSidePrompt;
		_isLoading = false;
	}

	private static string ExtractJsonObject(string raw)
	{
		var start = raw.IndexOf('{');
		var end = raw.LastIndexOf('}');
		if (start < 0 || end <= start)
		{
			throw new InvalidOperationException("DeepSeek 没有返回可解析的 JSON。");
		}

		return raw[start..(end + 1)];
	}

	private void MigrateDebaterSides()
	{
		foreach (var debater in _debaters)
		{
			if (debater.Side != DebateSide.Neutral)
			{
				debater.Position = GetSideName(debater.Side);
				debater.Enabled = true;
				continue;
			}

			if (debater.Position.Contains("正方", StringComparison.OrdinalIgnoreCase))
			{
				debater.Side = DebateSide.Pro;
			}
			else if (debater.Position.Contains("反方", StringComparison.OrdinalIgnoreCase))
			{
				debater.Side = DebateSide.Con;
			}

			debater.Position = debater.Side == DebateSide.Neutral ? string.Empty : GetSideName(debater.Side);
			debater.Enabled = debater.Side != DebateSide.Neutral;
		}
	}

	private void MigrateDebatersToSiliconFlow()
	{
		var preset = ApiPresetCatalog.Find("siliconflow");
		foreach (var debater in _debaters)
		{
			debater.PresetId = preset.Id;
			debater.ProviderKind = preset.Kind;
			debater.BaseUrl = preset.BaseUrl;
			if (string.IsNullOrWhiteSpace(debater.Model))
			{
				debater.Model = preset.DefaultModel;
			}

			if (string.IsNullOrWhiteSpace(debater.Name))
			{
				debater.Name = debater.Model;
			}
		}
	}

	private static string GetDebaterDisplayName(DebaterConfig debater)
	{
		var model = string.IsNullOrWhiteSpace(debater.Model) ? "未设置模型" : debater.Model;
		var name = string.IsNullOrWhiteSpace(debater.Name) || debater.Name == model ? model : $"{debater.Name} / {model}";
		return $"{name}（{GetSideName(debater.Side)}）";
	}

	private static string GetAiPoolName(DebaterConfig debater)
	{
		var model = string.IsNullOrWhiteSpace(debater.Model) ? "未设置模型" : debater.Model;
		return string.IsNullOrWhiteSpace(debater.Name) || debater.Name == model ? model : $"{debater.Name} / {model}";
	}

	private static string GetSideName(DebateSide side)
	{
		return side switch
		{
			DebateSide.Pro => "正方",
			DebateSide.Con => "反方",
			_ => "未站位"
		};
	}

	private static bool IsSiliconFlow(DebaterConfig debater)
	{
		return debater.PresetId.Equals("siliconflow", StringComparison.OrdinalIgnoreCase) ||
			debater.BaseUrl.Contains("siliconflow", StringComparison.OrdinalIgnoreCase);
	}

	private void SaveState()
	{
		_saveCts?.Cancel();
		_saveCts = new CancellationTokenSource();
		var token = _saveCts.Token;
		_ = SaveStateDebouncedAsync(token);
	}

	private async Task SaveStateDebouncedAsync(CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(300, cancellationToken);
			var json = CreateStateJson();
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}

			Preferences.Default.Set(SettingsKey, json);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			AddLog($"保存配置失败：{ex.Message}");
		}
	}

	private void SaveStateNow()
	{
		_saveCts?.Cancel();
		Preferences.Default.Set(SettingsKey, CreateStateJson());
	}

	private string CreateStateJson()
	{
		var state = new PersistedState
		{
			Settings = _settings,
			Debaters = _debaters.Select(CloneDebater).ToList(),
			Attachments = _attachments.Select(CloneAttachment).ToList()
		};
		return JsonSerializer.Serialize(state, _jsonOptions);
	}

	private static DebaterConfig CloneDebater(DebaterConfig source)
	{
		return new DebaterConfig
		{
			Name = source.Name,
			Position = source.Position,
			Persona = source.Persona,
			Side = source.Side,
			PresetId = source.PresetId,
			ProviderKind = source.ProviderKind,
			BaseUrl = source.BaseUrl,
			ApiKey = source.ApiKey,
			Model = source.Model,
			Temperature = source.Temperature,
			MaxTokens = source.MaxTokens,
			Enabled = source.Enabled
		};
	}

	private static AttachmentContext CloneAttachment(AttachmentContext source)
	{
		return new AttachmentContext
		{
			FileName = source.FileName,
			Kind = source.Kind,
			Content = source.Content,
			CreatedAt = source.CreatedAt
		};
	}

	private static void CopySettings(DebateSettings source, DebateSettings target)
	{
		target.Topic = source.Topic;
		target.Rules = source.Rules;
		target.ProSidePrompt = string.IsNullOrWhiteSpace(source.ProSidePrompt) ? target.ProSidePrompt : source.ProSidePrompt;
		target.ConSidePrompt = string.IsNullOrWhiteSpace(source.ConSidePrompt) ? target.ConSidePrompt : source.ConSidePrompt;
		target.MaxRounds = source.MaxRounds;
		target.CurrentRound = source.CurrentRound;
		target.NextDebaterIndex = source.NextDebaterIndex;
		target.AutoContinue = false;
		target.CompressionEnabled = source.CompressionEnabled;
		target.KeepRecentMessages = source.KeepRecentMessages;
		target.CompressAfterMessages = source.CompressAfterMessages;
		target.MemorySummary = source.MemorySummary;
	}

	private static string Preview(string text, int max)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}

		return text.Length <= max ? text : text[..max] + "...";
	}

	private async void OnButtonPressed(object? sender, EventArgs e)
	{
		if (sender is View view)
		{
			await view.ScaleTo(0.985, 70, Easing.CubicOut);
		}
	}

	private async void OnButtonReleased(object? sender, EventArgs e)
	{
		if (sender is View view)
		{
			await view.ScaleTo(1, 90, Easing.CubicOut);
		}
	}

	private sealed class PersistedState
	{
		public DebateSettings Settings { get; set; } = new();
		public List<DebaterConfig> Debaters { get; set; } = [];
		public List<AttachmentContext> Attachments { get; set; } = [];
	}
}
