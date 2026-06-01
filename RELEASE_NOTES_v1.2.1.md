# AI 辩论工作室 1.2.1

这是 1.2 的稳定性和硅基流动专用体验修订版，重点修复 API Key 输入后新增 AI 卡死、黑色模式消息不可读、AI 池不明确和长时间自动辩论卡顿。

## 新增

- AI 池卡片：每个 AI 显示模型、站位、API Key 状态和 Base URL，支持编辑和删除。
- Markdown 回答：聊天窗口支持标题、列表、引用、代码块、加粗、斜体和行内代码。

## 调整

- 服务商入口收敛为硅基流动，新建 AI 默认使用硅基流动预设。
- 旧本地配置启动时迁移为硅基流动 OpenAI-compatible 配置，保留模型、Key、站位和个性设定。
- 最大轮数上限从 12 轮扩大到 60 轮。
- 硅基流动模型选择继续支持内置常用模型和 `/v1/models?type=text&sub_type=chat` 在线刷新。

## 修复

- 修复填写 API Key 后点击“新增 AI”可能卡死的问题：API Key 输入不再触发整组 UI 重绘，配置保存改为防抖写入。
- 修复自动辩论长时间运行后递归续跑造成 UI 卡顿的风险，改为显式循环。
- 修复黑色模式下聊天卡片白底导致文字不可读的问题。

## 下载

- Windows：`AIDebateStudio-1.2.1-windows-x64-setup.exe`
- Android：`AIDebateStudio-1.2.1-android-arm64-signed.apk`

## 已验证

- `dotnet build .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0`
- `dotnet build .\AIDebateStudio.csproj -f net10.0-android`
- `dotnet publish .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0 -c Release`
- `dotnet publish .\AIDebateStudio.csproj -f net10.0-android -c Release`
- `ISCC .\AIDebateStudioInstaller.iss`

## 已知限制

- 构建仍存在若干 .NET 10/MAUI 过时 API 警告，主要是旧版弹窗和动画扩展方法，未阻塞发布。
- OCR 仍是入口和服务抽象，真实图片文字识别需要后续接入视觉模型或本地 OCR 引擎。
