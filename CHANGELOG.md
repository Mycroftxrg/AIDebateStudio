# 更新日志

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
