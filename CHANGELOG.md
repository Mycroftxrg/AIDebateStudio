# 更新日志

## 1.2.1 - 2026-06-02

1.2.1 是 1.2 的稳定性和硅基流动专用体验修订版，重点解决 API 配置不明确、输入 API Key 后新增 AI 卡死、黑色模式消息不可读和长时间自动辩论卡顿。

### 新增

- AI 池改为卡片展示，每张卡展示模型、站位、API Key 状态和 Base URL，并提供编辑、删除按钮。
- 聊天窗口新增常见 Markdown 渲染支持，包括标题、列表、引用、代码块、加粗、斜体和行内代码。

### 调整

- 服务商入口收敛为硅基流动，新建 AI 默认使用硅基流动预设。
- 启动时将旧保存的非硅基流动配置迁移为硅基流动 OpenAI-compatible 配置，保留名称、模型、API Key、站位和个性设定。
- 最大轮数上限从 12 轮扩大到 60 轮。
- 硅基流动模型选择继续支持内置常用模型和 `/v1/models?type=text&sub_type=chat` 在线刷新。

### 修复

- 修复填写 API Key 后点击“新增 AI”可能卡死的问题：API Key 输入不再触发整组卡片/站位重绘，配置保存改为防抖写入。
- 修复自动辩论长时间运行后递归续跑导致 UI 卡顿风险，改为显式循环续跑。
- 黑色模式下聊天消息卡片和提示卡片改为使用主题色，避免白底浅字不可读。

### 已验证

- `dotnet build .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0`
- `dotnet build .\AIDebateStudio.csproj -f net10.0-android`

## 1.2.0 - 2026-06-01

1.2.0 重构了 API 配置和正反方站位流程。配置顺序从“编辑单个辩手”调整为“先添加多个 API/模型进入 AI 池，再统一勾选正方或反方站位”，并加入战队协作提示词。

### 硅基流动专用修订

- 服务商入口收敛为硅基流动，新建 AI 默认使用硅基流动预设。
- 启动时将旧保存的非硅基流动配置迁移为硅基流动 OpenAI-compatible 配置，保留名称、模型、API Key、站位和个性设定。
- AI 池改为卡片展示，每张卡展示模型、站位、API Key 状态和 Base URL，并提供编辑、删除按钮。
- 修复填写 API Key 后点击“新增 AI”容易卡死的问题：API Key 输入不再触发整组卡片/站位重绘，配置保存改为防抖写入。
- 修复自动辩论长时间运行后递归续跑导致 UI 卡顿风险，改为显式循环续跑。
- 黑色模式下聊天消息卡片和提示卡片改为使用主题色，避免白底浅字不可读。
- 聊天窗口新增常见 Markdown 渲染支持，包括标题、列表、引用、代码块、加粗、斜体和行内代码。
- 最大轮数上限从 12 轮扩大到 60 轮。

### 硅基流动接入补充

- 硅基流动预设默认模型更新为 `Pro/deepseek-ai/DeepSeek-V3.2`。
- 为硅基流动补充常用 chat 模型快捷选择，包括 DeepSeek、GLM、Qwen 系列。
- 新增模型选择下拉框，预设服务商可以直接选择常用模型，同时保留手动模型名输入。
- 新增“刷新硅基流动模型”按钮，填写 API Key 后会调用 `/v1/models?type=text&sub_type=chat` 获取账号可用模型。

### 新增

- 新增 AI 池配置入口：可连续添加多个 AI，分别维护服务商、Base URL、模型名、API Key、温度和输出长度。
- 新增正方/反方站位列表：每个 AI 通过复选框加入正方或反方，取消勾选即不参赛，同一个 AI 只允许属于一个阵营。
- 新增 `DebateSide` 阵营模型，AI 显示名会标出站位，例如 `deepseek-chat（正方）`、`gpt-4.1-mini（未站位）`。
- 新增正方、反方独立前置提示词字段，并持久化保存。
- 新增 DeepSeek 生成按钮：使用 AI 池中的 DeepSeek 配置，根据当前辩题生成简短明确的正反方战队前置提示词。

### 调整

- 辩论调度只从已勾选正方/反方的 AI 中轮流选择发言，未站位 AI 保留在池中但不参赛。
- 发言顺序按正方、反方交错组织，便于双方攻防推进。
- 辩论 system prompt 增加同队协作要求：承接队友论点、补强薄弱处、避免机械重复，并可分工补案例、逻辑链、边界条件或回应对方攻击。
- 旧版保存的“正方/反方”立场文本会在启动时自动迁移为新的阵营字段。
- README 和接力文档同步更新到 1.2。

### 已验证

- `dotnet build .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0`
- `dotnet build .\AIDebateStudio.csproj -f net10.0-android`

## 1.1.0 - 2026-06-01

1.1.0 是面向发布分发和启动稳定性的维护版本。1.0.0 已完成的多 AI 辩论、人工插话、上下文压缩、资料入口和多厂商 API 接入能力保持不变，本版本重点把 Windows/Android 产物、启动诊断和小型运行时问题补齐。

### 新增

- 增加 Windows 安装脚本 `AIDebateStudioInstaller.iss`，可基于 `win-x64` publish 输出生成 `AIDebateStudio-1.1-windows-x64-setup.exe`。
- 增加启动阶段诊断服务 `StartupDiagnostics`，桌面端初始化失败时会写入 `%LOCALAPPDATA%\AIDebateStudio\startup-error.log`。
- `.gitignore` 新增 `artifacts/`，避免本地安装包、APK 等构建产物进入源码提交。

### 调整

- 将应用版本从 1.0.0 更新为 1.1：`ApplicationDisplayVersion=1.1`、`ApplicationVersion=2`、程序集/文件版本更新到 1.1.0。
- Windows Release 构建固定使用 `win-x64` RuntimeIdentifier，便于和安装脚本的 publish 路径对齐。
- Android Release 构建默认输出 APK，并固定 `android-arm64`；同时关闭 AOT、assembly store、link 和 trimming，降低 Release 包因裁剪导致运行异常的风险。
- README 的版本说明更新为 1.1，并明确 OCR 当前仍是入口和服务抽象，真实识别需要后续接入视觉模型或本地 OCR。

### 修复

- 修复动态创建的消息卡片、提示卡片、资料列表读取 `Caption` 样式时使用页面资源作用域导致找不到资源的问题，改为从应用级资源读取。

### 已验证

- `dotnet publish .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0 -c Release`
- `dotnet publish .\AIDebateStudio.csproj -f net10.0-android -c Release`
- `ISCC .\AIDebateStudioInstaller.iss`

### 已知问题

- 构建存在 .NET 10/MAUI 过时 API 警告，主要集中在 `DisplayAlert`、`FadeTo`、`TranslateTo`、`ScaleTo`，功能未受阻，后续建议迁移到 `DisplayAlertAsync` 和对应动画 Async API。
- Android 1.1.0 当前发布的是 arm64 APK；如需覆盖 x86_64 或多 ABI，需要调整 Release RuntimeIdentifier/打包策略。

## 1.0.0 - 2026-06-01

- 建立 AI 辩论工作室首个版本，支持 Windows 和 Android。
- 完成中文主页、中文运行日志、中文导出记录。
- 实现多 AI 辩手轮流发言，每位辩手拥有独立身份、立场、模型名和 API 配置。
- 内置国内外主流模型 API 快捷预设，并支持手动填写 OpenAI-compatible 接口。
- 接入 OpenAI-compatible、Anthropic Messages、Gemini generateContent 三类调用协议。
- 实现人工插话等待队列：当前 AI 发言结束后再插入正式上下文。
- 实现长对话压缩记忆：保留近期原文，将更早内容压缩为中文摘要。
- 增加文件解析入口，支持文本、PDF、Word、PowerPoint、Excel 等资料加入辩论上下文。
- 增加 OCR 图片入口和可扩展服务层。
- 增加页面进入动画、消息出现动画、按钮按压过渡。
- 使用与 image2studio 接近的简洁视觉风格：浅背景、低饱和面板、8px 圆角、小面积强调色。
