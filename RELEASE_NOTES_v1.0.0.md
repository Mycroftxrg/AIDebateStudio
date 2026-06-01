# AI 辩论工作室 1.0.0

这是 AI 辩论工作室的首个可运行版本，面向 Windows 和 Android。

## 新增

- 多模型 AI 辩论：AI 按顺序轮流发言，所有辩手读取同一份聊天历史。
- 独立辩手身份：每个 AI 以模型名命名，并携带自己的立场、风格、API 和模型配置。
- 人工插话队列：运行中输入不会打断当前请求，会等待上一位 AI 说完后插入。
- 多厂商 API 接入：覆盖 OpenAI、Claude、Gemini、DeepSeek、通义千问、豆包、智谱、Kimi、文心、混元、OpenRouter、Groq、Mistral、xAI 等。
- 长上下文压缩：自动生成中文压缩记忆，适合多轮长辩论。
- 文件和 OCR 入口：支持文档解析、图片 OCR 入口和资料上下文注入。
- 中文主页、中文日志和 Markdown 导出。
- 克制简洁的桌面化 UI，以及轻量动画和过渡。

## 已验证

- `dotnet build .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0`
- `dotnet build .\AIDebateStudio.csproj -f net10.0-android`

## 已知限制

- OCR 1.0 版本已完成入口和服务层，真实识别需要后续接入视觉模型或本地 OCR 引擎。
- 当前 API Key 保存在本机 MAUI Preferences 中，适合个人本机使用；团队分发前建议改为系统凭据库或加密存储。
