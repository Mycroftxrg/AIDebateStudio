# AI 辩论工作室 1.1.0

这是面向 Windows 和 Android 分发的 1.1 维护版本。核心辩论功能延续 1.0.0，本版本重点补齐 Release 构建、Windows 安装包和启动诊断。

## 新增

- Windows 安装脚本：新增 `AIDebateStudioInstaller.iss`，可生成 `AIDebateStudio-1.1-windows-x64-setup.exe`。
- 启动诊断日志：MAUI/WinUI 初始化异常会写入 `%LOCALAPPDATA%\AIDebateStudio\startup-error.log`，便于排查桌面端白屏、闪退或初始化失败。
- 发布产物目录忽略：`artifacts/` 已加入 `.gitignore`，本地安装包和 APK 不进入源码仓库。

## 调整

- 应用版本更新到 1.1.0，应用内部显示为 1.1。
- Windows Release 固定使用 `win-x64` publish 输出。
- Android Release 默认生成 `android-arm64` APK，并关闭 AOT、link、trim 等容易影响个人分发稳定性的优化。
- README 和版本说明已同步到 1.1。

## 修复

- 修复动态 UI 创建时读取 `Caption` 样式的资源作用域问题，避免运行时找不到页面资源。

## 下载

- Windows：`AIDebateStudio-1.1-windows-x64-setup.exe`
- Android：`AIDebateStudio-1.1-android-arm64-signed.apk`

## 已验证

- `dotnet publish .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0 -c Release`
- `dotnet publish .\AIDebateStudio.csproj -f net10.0-android -c Release`
- `ISCC .\AIDebateStudioInstaller.iss`

## 已知限制

- OCR 仍是入口和服务抽象，真实图片文字识别需要后续接入视觉模型或本地 OCR 引擎。
- API Key 当前保存在本机 MAUI Preferences 中，适合个人本机使用；团队或公开分发前建议迁移到系统凭据库或加密存储。
- 构建会出现若干 .NET 10/MAUI 过时 API 警告，主要是旧版弹窗和动画扩展方法，未阻塞 1.1.0 发布。
