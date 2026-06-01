# AI 辩论工作室 1.2.0

这是一次 API 配置和站位流程重构版本。1.2.0 把旧的“单个辩手配置”改为“AI 池 + 正反方站位”，更适合先接入多个模型，再统一组织辩论双方。

## 新增

- 硅基流动专用 AI 池：先添加多个硅基流动模型，再分别维护 Base URL、模型名、API Key、温度和输出长度。
- AI 池卡片：每个 AI 以卡片展示模型、站位、API Key 状态和 Base URL，并提供编辑、删除按钮。
- 正反方站位列表：每个 AI 通过复选框加入正方或反方，取消勾选即不参赛。
- 阵营显示名：聊天、下位发言和 AI 池列表会显示 `模型名（正方）`、`模型名（反方）` 或 `模型名（未站位）`。
- 双方前置提示词：正方和反方分别有独立提示词。
- DeepSeek 生成：使用 AI 池里的 DeepSeek 配置，根据当前辩题生成简短明确的正反方战队提示词。
- Markdown 回答：聊天窗口支持标题、列表、引用、代码块、加粗、斜体和行内代码。

## 调整

- 辩论调度只使用已站位 AI，未站位 AI 保留在 AI 池但不参赛。
- 发言顺序按正方、反方交错组织。
- 同队 AI 不再只是各说各的：system prompt 要求承接队友论点、补强薄弱环节、避免重复，并可分工补案例、逻辑链、边界条件或回应对方攻击。
- 服务商预设收敛为硅基流动，新建 AI 默认使用 `Pro/deepseek-ai/DeepSeek-V3.2`。
- 最大轮数上限从 12 轮扩大到 60 轮。
- 旧版保存的“正方/反方”立场文本会自动迁移到新的阵营字段。

## 修复

- 修复填写 API Key 后点击“新增 AI”可能卡死的问题：API Key 输入不再触发整组 UI 重绘，配置保存改为防抖写入。
- 修复自动辩论长时间运行后递归续跑带来的卡顿风险，改为显式自动循环。
- 修复黑色模式下聊天卡片白底导致文字不可读的问题。

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
