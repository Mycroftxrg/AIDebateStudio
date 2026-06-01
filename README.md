# AI 辩论工作室

AI 辩论工作室是一款中文优先的跨平台 AI 辩论软件，基于 .NET MAUI 构建，目标平台为 Windows 和 Android。应用采用类似主流 AI 聊天软件的中间聊天流加底栏输入结构，AI 辩手依次轮流发言，用户可以随时插话，插话会在当前 AI 发言结束后进入正式对话，避免扰乱上下文顺序。

## 主要能力

- 多 AI 轮流辩论：每位辩手拥有独立身份、立场、风格、模型名和 API 配置。
- 多厂商 API：内置 OpenAI、Anthropic、Gemini、OpenRouter、Groq、Mistral、xAI、DeepSeek、通义千问、豆包、智谱、Kimi、文心、混元、硅基流动、阶跃星辰、零一万物等快捷预设。
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

4. 在右侧为每位辩手选择服务商、填写 API Key、确认模型名，然后点击“下一位发言”或“自动辩论”。

## 1.1 版本说明

当前版本在完整辩论闭环基础上更新到 1.1，补充 Windows/Android Release 构建配置，并加入启动阶段诊断日志，便于定位桌面端初始化失败。OCR 已建立入口和服务抽象，但真实图像文字识别仍需后续接入视觉模型或本地 OCR 引擎。

## 技术栈

- .NET 10
- .NET MAUI
- OpenAI-compatible Chat Completions
- Anthropic Messages API
- Gemini generateContent REST API
- DocumentFormat.OpenXml
- PdfPig
