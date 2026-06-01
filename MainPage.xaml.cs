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
	private CancellationTokenSource? _turnCts;
	private Style CaptionStyle => (Style)Application.Current!.Resources["Caption"];

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

		MigrateDebaterSides();
	}

	private static IEnumerable<DebaterConfig> CreateDefaultDebaters()
	{
		var qwen = ApiPresetCatalog.Find("dashscope");
		var deepseek = ApiPresetCatalog.Find("deepseek");
		var openai = ApiPresetCatalog.Find("openai");
		return
		[
			FromPreset(qwen, "qwen-plus", DebateSide.Pro, "重视产业实践和应用案例，擅长把抽象观点落到具体场景。"),
			FromPreset(deepseek, "deepseek-chat", DebateSide.Con, "强调逻辑漏洞、成本、可靠性和长期外部性，发言尖锐但克制。"),
			FromPreset(openai, "gpt-4.1-mini", DebateSide.Neutral, "负责补充关键追问、比较双方论证强弱，并推动辩论回到核心问题。")
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
			if (_settings.AutoContinue && _settings.CurrentRound < _settings.MaxRounds)
			{
				await Task.Delay(600);
				await RunNextTurnAsync();
			}
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
		if (_settings.AutoContinue)
		{
			await RunNextTurnAsync();
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

		foreach (var message in _messages)
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
			BackgroundColor = isHuman
				? Color.FromArgb("#EEF4FA")
				: message.Role == DebateRole.Tool
					? Color.FromArgb("#F8FAFC")
					: Color.FromArgb("#FFFFFF"),
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
						TextColor = isAi ? Color.FromArgb("#2563EB") : Color.FromArgb("#0F172A")
					},
					new Label
					{
						Text = message.Content,
						FontSize = 14,
						LineHeight = 1.18
					},
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

	private View CreateInfoCard(string title, string body)
	{
		return new Border
		{
			Padding = 14,
			BackgroundColor = Color.FromArgb("#F8FAFC"),
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
			return;
		}

		_isLoading = true;
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
		if (!preset.IsManual)
		{
			debater.BaseUrl = preset.BaseUrl;
			debater.Model = preset.DefaultModel;
			debater.Name = preset.DefaultModel;
		}

		LoadSelectedDebater();
		RefreshDebaterPickerNames();
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

		debater.Name = string.IsNullOrWhiteSpace(DebaterNameEntry.Text) ? ModelEntry.Text?.Trim() ?? "未命名模型" : DebaterNameEntry.Text.Trim();
		debater.Persona = DebaterPersonaEditor.Text?.Trim() ?? string.Empty;
		debater.BaseUrl = BaseUrlEntry.Text?.Trim() ?? string.Empty;
		debater.Model = ModelEntry.Text?.Trim() ?? string.Empty;
		debater.ApiKey = ApiKeyEntry.Text?.Trim() ?? string.Empty;
		RefreshDebaterPickerNames();
		RefreshRosterAssignments();
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
		var preset = ApiPresetCatalog.Find("deepseek");
		_debaters.Add(FromPreset(preset, preset.DefaultModel, DebateSide.Neutral, string.Empty));
		RefreshDebaterPickerNames();
		DebaterPicker.SelectedIndex = _debaters.Count - 1;
		LoadSelectedDebater();
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
		SaveState();
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
		var state = new PersistedState
		{
			Settings = _settings,
			Debaters = _debaters,
			Attachments = _attachments
		};
		Preferences.Default.Set(SettingsKey, JsonSerializer.Serialize(state, _jsonOptions));
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
