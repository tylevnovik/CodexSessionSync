# Codex Session Sync

Codex Session Sync 是一个本地会话同步工具，用来把 OpenAI Codex / Codex CLI 的会话数据在多个 LLM provider 之间同步。它会同时处理 JSONL 会话文件和 `state_*.sqlite` 索引，让不同 provider 能看到同一批历史会话。

默认操作是 **预览**，只有勾选确认并执行写入时才会修改文件。写入前可以指定备份目录，工具会先备份 SQLite 和被改动的 JSONL 文件。

## 该下载哪个版本

所有发布包都面向 Windows x64。AOT 版本不需要安装 .NET 运行时。

| 版本 | 推荐场景 | 发布形态 | 说明 |
|------|----------|----------|------|
| WinUI 3 | 想要最接近 Windows 11 的原生体验 | `CodexSessionSync.WinUI-aot.zip` | Windows App SDK + Acrylic + 深浅色切换。必须完整解压后运行，不是单 exe。 |
| Avalonia | 想要现代 Fluent 风格和较小 AOT 包 | `CodexSessionSync.Avalonia-aot.zip` | Acrylic 背景、卡片布局、pill 模式选择器。当前发布为 Windows x64 单文件 AOT。 |
| WinForms | 想要最朴素、直接、兼容性好的 GUI | `CodexSessionSync.WinForms-aot.zip` | 轻量表单界面，self-contained AOT 单文件，适合备用和排错。 |
| WPF | 想要传统 Windows 桌面体验 | `CodexSessionSync.Wpf.exe` | self-contained 单文件，但 WPF 不支持 Native AOT，体积最大。 |
| TUI | 想在终端或脚本里跑 | `CodexSessionSync.Tui-aot.zip` | Spectre.Console 交互界面，也支持 `--preview` / `--apply`。 |

WinUI 3 包里的文件都要保留，尤其是 `.pri`、`.mui`、WinUI/XAML/Windows App SDK 运行时 DLL。它的 “self-contained” 含义是 **不要求目标机器预装 Windows App Runtime**，不是 “只有一个 exe”。

## 功能

- **全供应商互同步**：自动发现配置和会话文件里的 provider，让每个 provider 最终看到同一批会话。
- **OpenAI 同步到全部**：以 `openai` 或指定 source provider 为源，给其它 provider 创建镜像会话。
- **单目标迁移**：把不属于保留 provider 的会话迁移到目标 provider，改写 JSONL 和 SQLite 中的 `model_provider`。
- **预览优先**：默认只报告将要创建、更新、跳过或冲突的项目，不写入。
- **SQLite 索引同步**：除了 JSONL 文件，也会同步 `state_*.sqlite` 中的 `threads` 行和关联索引数据。
- **自动备份**：写入前可备份 SQLite 和被修改的 JSONL 文件。
- **Codex 运行中可读**：读取文件时使用共享读写模式，Codex Desktop 正在运行时也能预览。

## 各界面特点

### WinUI 3

WinUI 3 是最偏“原生 Windows 应用”的版本。它使用 Windows App SDK 2.x，带 `DesktopAcrylicBackdrop`，界面有深色/浅色主题切换，并同步调整窗口标题栏按钮颜色。适合日常使用和追求系统观感的场景。

发布上它是 self-contained Native AOT，但 Windows App SDK 的 XAML、PRI、MUI 和 native runtime 资源不能真正打进单个 exe，所以 release 里提供的是 zip 文件夹包。

### Avalonia

Avalonia 版本是目前最完整的现代 UI 版本之一，使用 Fluent 主题、Acrylic 背景、卡片式信息区和 pill 风格的模式选择器。它有跨平台潜力，但当前 release 只发布 Windows x64 self-contained Native AOT。

如果想要“现代一点、文件又比 WPF 小”的 GUI，优先选 Avalonia。

### WinForms

WinForms 版本是轻量 fallback：控件朴素，布局直接，依赖少，self-contained AOT 单文件。它不追求视觉效果，但启动和排错路径简单，适合在复杂桌面运行时出问题时备用。

### WPF

WPF 版本是传统 Windows 桌面界面，带卡片式模式选择、深色输出区和异步执行。它发布为 self-contained 单文件，不需要安装 .NET 运行时，但 WPF 当前不能 Native AOT，所以体积明显更大。

### TUI

TUI 版本基于 Spectre.Console，适合终端环境、远程桌面、脚本化或快速检查。交互模式会逐步询问同步模式、路径和写入确认；CLI 模式支持：

```bash
CodexSessionSync.Tui.exe --preview
CodexSessionSync.Tui.exe --apply
```

CLI 模式默认执行全供应商互同步。

## 使用方法

### GUI

1. 打开任一 GUI 版本，`Codex Home` 会默认填入 `CODEX_HOME` 或当前用户的 `.codex`。
2. 选择同步模式。
3. 点击 `预览` 查看将要执行的操作。
4. 如果结果没问题，填写或确认备份目录。
5. 勾选 `我已备份或确认可以写入`，点击 `执行写入`。

### Codex Home 路径

工具按以下顺序检测：

1. 环境变量 `CODEX_HOME`
2. 当前用户目录下的 `.codex`

## 同步模式

### 全供应商互同步

从 `config.toml` 和已有会话文件中自动发现 provider。对每个源会话，为其它 provider 创建或更新镜像 JSONL 文件，并同步 SQLite 索引。

配对逻辑：

- 无 `forked_from_id` 的源会话会通过 UUIDv5 生成目标镜像 ID。
- 有 `forked_from_id` 的镜像会话会反查原始 provider 和路径，避免重复复制。
- 内容比较会先归一化 JSON 行并跳过 `session_meta` 差异，再用消息行数判断更新方向。

### OpenAI 同步到全部

只以指定 source provider 为源，为其它 provider 创建镜像。默认 source provider 是 `openai`。

### 单目标迁移

直接把非保留 provider 的历史会话迁移到目标 provider。保留 provider 默认包含 `openai` 和目标 provider 本身。

## 技术细节

### 核心架构

`CodexSessionSync.Core` 是纯类库，不依赖任何 UI 框架。所有界面版本都调用同一套同步引擎，因此行为保持一致。

核心处理的数据：

- `config.toml`：解析 provider 配置和默认 provider。
- `sessions/**/*.jsonl`：读取和写入 Codex 会话事件流。
- `state_*.sqlite`：同步会话索引，确保 Codex UI 能检索到镜像会话。

关键实现：

- JSONL 使用 `System.Text.Json` 逐行解析，避免把整份会话文件当普通文本乱改。
- TOML 使用项目内轻量解析器，只覆盖 Codex 配置所需的节、点分键和注释。
- 镜像 ID 使用 UUIDv5 确定性生成：同一个源会话同步到同一个 provider 时会得到稳定 ID。
- 生成的镜像会在 `session_meta.payload` 中写入 `forked_from_id`，用于后续反向映射和去重。

### 发布策略

项目目标是尽量提供“不安装 .NET 运行时即可运行”的 Windows x64 包：

| 项目 | 框架 | Native AOT | 单文件 | 发布备注 |
|------|------|------------|--------|----------|
| `CodexSessionSync.Tui` | .NET 10 + Spectre.Console | 是 | 是 | zip 内主要是单个 exe。 |
| `CodexSessionSync.WinForms` | Windows Forms | 是 | 是 | 使用 partial trim，保留 WinForms 运行所需反射路径。 |
| `CodexSessionSync.Avalonia` | Avalonia UI 11 | 是 | 是 | 使用 Fluent 主题和 compiled bindings。 |
| `CodexSessionSync.WinUI` | WinUI 3 / Windows App SDK | 是 | 否 | self-contained 文件夹包，必须完整解压。 |
| `CodexSessionSync.Wpf` | WPF | 否 | 是 | self-contained 单文件，但体积大。 |

WinUI 3 的限制来自 Windows App SDK：XAML 运行时、资源索引、语言资源和部分 native DLL 需要以 side-by-side 文件存在。强行删文件会导致启动或运行时崩溃。

## 构建与发布

```powershell
.\publish-all.ps1
```

脚本会：

1. 发布 Avalonia、TUI、WinForms 的 self-contained AOT 包。
2. 发布 WinUI 3 的 self-contained AOT 文件夹包。
3. 发布 WPF 的 self-contained 单文件包。
4. 把可分发资产写入 `release\`。

`release\` 和 `dist\` 都是生成目录，不提交到仓库。

## 项目结构

```text
CodexSessionSync.Core/        核心同步引擎
CodexSessionSync.Tui/         终端交互界面
CodexSessionSync.WinForms/    WinForms 桌面界面
CodexSessionSync.Wpf/         WPF 桌面界面
CodexSessionSync.Avalonia/    Avalonia 桌面界面
CodexSessionSync.WinUI/       WinUI 3 桌面界面
publish-all.ps1               一键发布脚本
dist/                         发布展开目录，脚本生成
release/                      GitHub Release 资产目录，脚本生成
```

## 核心 API

| 方法 | 说明 |
|------|------|
| `SyncEngine.DefaultCodexHome()` | 获取默认 Codex Home 路径 |
| `SyncEngine.InspectConfig(path, targetProvider)` | 解析 Codex 配置 |
| `SyncEngine.ResolveStateDb(codexHome, config, explicitPath)` | 定位 `state_*.sqlite` |
| `SyncEngine.ResolveConfiguredProviders(config)` | 读取应参与同步的 provider |
| `SyncEngine.FindMutualSourceSessions(...)` | 扫描源会话并建立 ID/provider/path 映射 |
| `SyncEngine.BuildMutualMirrorPlans(...)` | 生成镜像计划 |
| `SyncEngine.SyncRolloutMirrors(...)` | 创建或更新 JSONL 镜像文件 |
| `SyncEngine.SyncSqliteMirrors(...)` | 同步 SQLite 索引 |
| `SyncEngine.BackupSqlite(...)` | 备份 SQLite |
| `SyncEngine.BackupFile(...)` | 备份 JSONL 文件 |
| `UuidV5.Create(...)` | 生成确定性 UUIDv5 |

## 许可

私有项目，未公开许可。
