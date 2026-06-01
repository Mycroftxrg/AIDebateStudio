# AI 辩论工作室

AI 辩论工作室是一款中文优先的跨平台 AI 辩论软件，基于 .NET MAUI 构建，目标平台为 Windows 和 Android。应用采用类似主流 AI 聊天软件的中间聊天流加底栏输入结构，AI 辩手依次轮流发言，用户可以随时插话，插话会在当前 AI 发言结束后进入正式对话，避免扰乱上下文顺序。

## 主要能力

- AI 池与战队站位：先添加多个 API/模型进入 AI 池，再通过正方/反方列表勾选参赛站位。
- 多 AI 轮流辩论：已站位 AI 按正反方轮流发言，同队 AI 会承接队友论点并补强薄弱处。
- 战队提示词：正方、反方分别拥有独立前置提示词，可由 DeepSeek 根据辩题生成简短明确的战队提示词。
- 多厂商 API：内置 OpenAI、Anthropic、Gemini、OpenRouter、Groq、Mistral、xAI、DeepSeek、通义千问、豆包、智谱、Kimi、文心、混元、硅基流动、阶跃星辰、零一万物等快捷预设。
- 硅基流动增强：内置常用 chat 模型快捷选择，并支持用 API Key 刷新 `/v1/models` 获取账号可用模型。
- 手动接入：支持自定义 Base URL、模型名、API Key，适配 OpenAI-compatible 网关或自建代理。
- 人工插话队列：辩论运行中输入的内容进入等待队列，上一个辩手说完后再插入，后续 AI 会读取插话后的完整历史。
- 长对话压缩：保留近期原文，将更早对话滚动压缩成中文记忆，降低长辩论上下文压力。
- 文件解析：支持 txt、md、csv、json、xml、代码文本、PDF、docx、pptx、xlsx 等资料入口。
- OCR 入口：提供图片 OCR 资料入口，当前版本保留可扩展服务层，后续可接入多模态识别或本地 OCR。
- 中文主页与日志：界面、状态、日志、导出记录均为中文。
- 动画与过渡：页面进入、消息出现、按钮按压带有轻量动画。

## 使用方式

1. 打开项目：

   ```powershell
   cd "$HOME\Desktop\AIDebateStudio"
   ```

2. 运行 Windows 版本：

   ```powershell
   dotnet build .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0
   dotnet run --project .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0
   ```

3. 构建 Android 版本：

   ```powershell
   dotnet build .\AIDebateStudio.csproj -f net10.0-android
   ```

4. 在右侧先把多个 API/模型添加到 AI 池，再在正方/反方列表勾选站位；需要时点击“DeepSeek 生成”生成双方前置提示词，然后点击“下一位发言”或“自动辩论”。

## 1.2 版本说明

当前版本将 API 配置改造为 AI 池流程，并把正反方站位从单个辩手表单中拆出：用户可以先添加多个 API，再用列表勾选参赛阵营。正反方拥有独立前置提示词，DeepSeek 可根据辩题生成简短明确的战队提示词。硅基流动支持常用模型快捷选择和账号模型列表刷新。OCR 已建立入口和服务抽象，但真实图像文字识别仍需后续接入视觉模型或本地 OCR 引擎。

## 技术栈

- .NET 10
- .NET MAUI
- OpenAI-compatible Chat Completions
- Anthropic Messages API
- Gemini generateContent REST API
- DocumentFormat.OpenXml
- PdfPig
