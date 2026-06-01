# 更新日志

## 1.1 - 2026-06-01

- 将应用版本更新为 1.1，并同步 Windows 安装脚本输出名称。
- 调整 Release 构建配置：Windows 使用 win-x64 publish 输出，Android Release 默认生成 arm64 APK。
- 增加启动阶段诊断日志，捕获 MAUI/WinUI 初始化异常并写入本地日志。
- 修复部分动态创建控件读取 Caption 样式时的资源作用域问题。

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
