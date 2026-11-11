**简体中文** · [English](README.en.md)

# QuickText

> Windows 托盘速贴：常用文本 / 图片，几键贴到光标处

常驻托盘。邮箱、签名、模板、回复、提示词、Skills、图片等存一次，在**任意输入框**里搜拼音或打缩写，贴到光标处。

- 单文件绿色版，**无账号、默认不联网**（检查更新、AI 生成为可选）
- 数据是本地 **JSON**，可放同步盘；明文存储，机密请用密码管理器
- 深色 / 浅色 / 跟随系统；**18 种界面语言**；设置即时生效

## 下载

**[⬇ 下载最新版](https://github.com/rockyflux/QuickText/releases)** — Windows x64，免安装。

| 文件 | 前提 | 说明 |
| --- | --- | --- |
| `QuickText-<版本>-win-x64.zip` | 无 | **拿不准就下这个** |
| `QuickText-<版本>-win-x64-needs-dotnet10.zip` | 已装 [.NET 10 桌面运行时](https://dotnet.microsoft.com/download/dotnet/10.0) | 体积更小 |
| `QuickText.exe` | 同上 | 无 zip，点开即用或覆盖更新 |

未签名：SmartScreen 点**更多信息 → 仍要运行**。

## 上手

1. 双击 `QuickText.exe`（托盘常驻，首次带示例库）
2. `Ctrl+Shift+8` 呼出面板 → 搜拼音 / 名称 → 回车，贴到光标处
3. 托盘右键打开**管理**维护条目；**设置**改热键、主题、语言等

同一库还可：打缩写触发**文本指令**、用 `{变量}` 做模板、开启**剪贴板历史**（默认 `Ctrl+Shift+V`）、在管理器里用 **AI 生成**起草条目。

## 能力一览

- **搜索**：名称 / 拼音 / 缩写 / 正文；分类浏览；收藏与最近常用
- **内容**：文本与图片；按条目启用变量占位符；可选代码语法高亮
- **扩展**：文本指令、剪贴板历史（文本、3 天）、Skills 导入、AI 生成（自备 Key）
- **数据**：本地 JSON、导出/导入备份、每日自动备份、便携模式（数据跟 exe 走）

## 构建

根目录运行 `build.bat`（菜单：编译 / 测试 / 冒烟 / 打包；也可 `build.bat 1`…`8` 直接选）。或：

```bash
dotnet build QuickText.sln -c Debug
dotnet test  tests/QuickText.Core.Tests/QuickText.Core.Tests.csproj
dotnet run  --project src/QuickText.App
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64                 # 自带运行时
dotnet publish src/QuickText.App -c Release -p:PublishProfile=win-x64-needs-dotnet10  # 需已装 .NET 10 桌面运行时
```

需要 .NET 10 SDK。仅 Windows。
