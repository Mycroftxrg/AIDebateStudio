# AI 辩论工作室 1.2.0

这是一次 API 配置和站位流程重构版本。1.2.0 把旧的“单个辩手配置”改为“AI 池 + 正反方站位”，更适合先接入多个模型，再统一组织辩论双方。

## 新增

- AI 池：先添加多个 API/模型，再分别维护服务商、Base URL、模型名、API Key、温度和输出长度。
- 正反方站位列表：每个 AI 通过复选框加入正方或反方，取消勾选即不参赛。
- 阵营显示名：聊天、下位发言和 AI 池列表会显示 `模型名（正方）`、`模型名（反方）` 或 `模型名（未站位）`。
- 双方前置提示词：正方和反方分别有独立提示词。
- DeepSeek 生成：使用 AI 池里的 DeepSeek 配置，根据当前辩题生成简短明确的正反方战队提示词。

## 调整

- 辩论调度只使用已站位 AI，未站位 AI 保留在 AI 池但不参赛。
- 发言顺序按正方、反方交错组织。
- 同队 AI 不再只是各说各的：system prompt 要求承接队友论点、补强薄弱环节、避免重复，并可分工补案例、逻辑链、边界条件或回应对方攻击。
- 旧版保存的“正方/反方”立场文本会自动迁移到新的阵营字段。

## 下载

- Windows：`AIDebateStudio-1.2-windows-x64-setup.exe`
- Android：`AIDebateStudio-1.2-android-arm64-signed.apk`

## 已验证

- `dotnet build .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0`
- `dotnet build .\AIDebateStudio.csproj -f net10.0-android`
- `dotnet publish .\AIDebateStudio.csproj -f net10.0-windows10.0.19041.0 -c Release`
- `dotnet publish .\AIDebateStudio.csproj -f net10.0-android -c Release`
- `ISCC .\AIDebateStudioInstaller.iss`

## 已知限制

- DeepSeek 生成前置提示词需要 AI 池里已有 DeepSeek 配置并填写 API Key。
- OCR 仍是入口和服务抽象，真实图片文字识别需要后续接入视觉模型或本地 OCR 引擎。
- 构建仍存在若干 .NET 10/MAUI 过时 API 警告，主要是旧版弹窗和动画扩展方法，未阻塞 1.2.0 发布。
