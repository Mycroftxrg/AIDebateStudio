# Next Session

## 当前状态

- 仓库：`C:\Users\ASUS\Desktop\AIDebateStudio`
- 远端：`https://github.com/Mycroftxrg/AIDebateStudio.git`
- 当前发布 tag：`v1.2.0`
- 当前主分支：`main`
- 目标平台：Windows、Android
- 技术栈：.NET 10、.NET MAUI、OpenAI-compatible/Anthropic/Gemini REST API、OpenXML、PdfPig

## 1.2.0 已完成

- 将 API 配置重构为 AI 池：先添加多个 API/模型，再统一勾选正反方站位。
- 新增 `DebateSide` 阵营模型，AI 显示名会带 `（正方）`、`（反方）`、`（未站位）`。
- 正方、反方分别拥有独立前置提示词，并持久化保存。
- 新增 DeepSeek 生成按钮，根据当前辩题生成简短明确的正反方战队提示词。
- 辩论调度只使用已站位 AI，按正方/反方交错发言。
- system prompt 增加同队协作要求：承接队友论点、补强薄弱处、避免重复，并可分工推进。
- 旧版保存的“正方/反方”立场文本会自动迁移到新阵营字段。
- 硅基流动预设支持常用模型快捷选择，并可用 API Key 调 `/v1/models?type=text&sub_type=chat` 刷新账号可用模型。

## 1.1.0 已完成

- 版本从 1.0.0 升到 1.1.0，应用内显示为 1.1。
- Windows Release 固定 `win-x64`，并新增 Inno Setup 安装脚本。
- Android Release 固定 `android-arm64` APK，关闭 AOT/link/trim/assembly store，优先保证个人分发稳定性。
- 新增 `StartupDiagnostics`，启动初始化异常会写到 `%LOCALAPPDATA%\AIDebateStudio\startup-error.log`。
- 修复动态创建控件读取 `Caption` 样式时的资源作用域问题。
- `artifacts/` 已加入 `.gitignore`，发布资产只用于本地和 GitHub Release，不提交源码。

## 1.1.0 发布资产

- Windows 安装包：`artifacts\windows\AIDebateStudio-1.1-windows-x64-setup.exe`
- Android APK：`artifacts\android\AIDebateStudio-1.1-android-arm64-signed.apk`
- GitHub Release 正文来源：`RELEASE_NOTES_v1.1.0.md`

## 1.2.0 发布资产

- Windows 安装包：`artifacts\windows\AIDebateStudio-1.2-windows-x64-setup.exe`
- Android APK：`artifacts\android\AIDebateStudio-1.2-android-arm64-signed.apk`
- GitHub Release 正文来源：`RELEASE_NOTES_v1.2.0.md`

## 重要代码入口

- UI 和主要流程：[MainPage.xaml.cs](C:\Users\ASUS\Desktop\AIDebateStudio\MainPage.xaml.cs)
- 页面结构：[MainPage.xaml](C:\Users\ASUS\Desktop\AIDebateStudio\MainPage.xaml)
- API 调用：[Services\AiChatClient.cs](C:\Users\ASUS\Desktop\AIDebateStudio\Services\AiChatClient.cs)
- 上下文拼装和压缩提示：[Services\ContextComposer.cs](C:\Users\ASUS\Desktop\AIDebateStudio\Services\ContextComposer.cs)
- 厂商预设：[Services\ApiPresetCatalog.cs](C:\Users\ASUS\Desktop\AIDebateStudio\Services\ApiPresetCatalog.cs)
- 硅基流动模型列表：[Services\SiliconFlowModelService.cs](C:\Users\ASUS\Desktop\AIDebateStudio\Services\SiliconFlowModelService.cs)
- 文件解析：[Services\FileParsingService.cs](C:\Users\ASUS\Desktop\AIDebateStudio\Services\FileParsingService.cs)
- OCR 入口：[Services\OcrService.cs](C:\Users\ASUS\Desktop\AIDebateStudio\Services\OcrService.cs)
- 启动诊断：[Services\StartupDiagnostics.cs](C:\Users\ASUS\Desktop\AIDebateStudio\Services\StartupDiagnostics.cs)
- 项目与发布配置：[AIDebateStudio.csproj](C:\Users\ASUS\Desktop\AIDebateStudio\AIDebateStudio.csproj)
- Windows 安装脚本：[AIDebateStudioInstaller.iss](C:\Users\ASUS\Desktop\AIDebateStudio\AIDebateStudioInstaller.iss)

## 常用命令

```powershell
cd "$HOME\Desktop\AIDebateStudio"

dotnet build .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0
dotnet run --project .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0

dotnet publish .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0 -c Release
dotnet publish .\AIDebateStudio.csproj -f net10.0-android -c Release

& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" .\AIDebateStudioInstaller.iss
```

## 架构速记

- `MainPage` 维护本地状态：辩手配置、消息、资料、插话队列和压缩记忆，使用 MAUI `Preferences` 保存。
- `RunNextTurnAsync` 是核心调度：从已站位 AI 中按正方/反方交错选择下一位，调用 `ContextComposer` 生成上下文，再用 `AiChatClient` 请求模型。
- 人工插话默认排队，当前 AI 发言结束后由 `FlushQueuedInterjectionsAsync` 写入正式消息历史。
- 上下文压缩由 `MaybeCompressContextAsync` 触发，保留近期消息，把更早消息压成中文记忆。
- `AiChatClient` 根据 `AiProviderKind` 分流到 OpenAI-compatible、Anthropic Messages、Gemini generateContent。
- OCR 当前只完成入口和服务抽象，后续应在 `OcrService` 接入真实视觉模型或本地 OCR。
- DeepSeek 生成正反方战队提示词复用 `AiChatClient`，入口在 `OnGenerateSidePromptsClicked`。

## 后续建议

- 迁移 .NET 10/MAUI 过时 API：`DisplayAlertAsync`、`FadeToAsync`、`TranslateToAsync`、`ScaleToAsync`。
- 给 API Key 存储换成系统凭据库或加密存储，当前 Preferences 更适合个人本机使用。
- 做真实 OCR：优先明确走视觉模型 API 还是本地 OCR，再扩展 `OcrService`。
- 增加 Android 多 ABI 或通用分发策略，如果需要覆盖更多设备。
- 给 `AiChatClient` 和 `ContextComposer` 加小型单元测试，特别是三类 API payload 和上下文压缩边界。
