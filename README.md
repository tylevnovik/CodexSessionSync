# Codex Session Sync

Codex 会话同步工具——在多个 LLM provider 之间同步 OpenAI Codex（现 OpenAI CLI）的会话数据。

## 功能

- **全供应商互同步**：将配置中的每个 provider 设置为其他所有 provider 的镜像，使所有 provider 都能看到同一批会话。
- **OpenAI 同步到全部**：以单个 provider 为源，为其它所有 provider 创建镜像会话。
- **单目标迁移**：将不属于保留 provider 的会话批量迁移到目标 provider（直接改写 JSONL 和 SQLite 中的 `model_provider` 字段）。
- 预览模式：默认只读，确认后才实际写入。
- SQLite 索引同步：除了 JSONL 文件，还会在 `state.db` 中插入或更新对应的 `threads` 行和关联子表。
- 自动备份：写入前可指定备份目录，自动备份 SQLite 和被修改的 JSONL 文件。

## 项目结构

```
CodexSessionSync.Core/        核心同步引擎（纯类库，无 GUI 依赖）
CodexSessionSync.Wpf/         WPF 桌面界面
CodexSessionSync.WinForms/    WinForms 桌面界面
CodexSessionSync.Avalonia/    Avalonia 跨平台桌面界面
CodexSessionSync.Tui/         终端交互界面（Spectre.Console）
dist/                         发布输出目录（各平台单文件 exe）
publish.ps1                   一键构建发布脚本
```

## 构建与发布

```powershell
# 构建所有项目
dotnet build

# 发布为 self-contained 单文件 exe（推荐）
.\publish.ps1
```

发布后的文件在 `dist\` 目录下：

| 项目 | 输出 |
|------|------|
| `dist\CodexSessionSync.Wpf\CodexSessionSync.Wpf.exe` | WPF 界面 |
| `dist\CodexSessionSync.WinForms\CodexSessionSync.WinForms.exe` | WinForms 界面 |
| `dist\CodexSessionSync.Avalonia\CodexSessionSync.Avalonia.exe` | Avalonia 界面 |
| `dist\CodexSessionSync.Tui\CodexSessionSync.Tui.exe` | 终端界面 |

所有 exe 均为 self-contained 单文件，无需安装 .NET 运行时即可运行。

## 使用方法

### GUI 版本（WPF / WinForms / Avalonia）

1. 打开程序，界面自动填入默认 Codex Home 路径。
2. 选择同步模式。
3. 点击「预览」查看将要执行的操作（不会写入）。
4. 确认无误后勾选「我已备份或确认可以写入」，点击「执行写入」。

### TUI 版本

交互模式：直接运行，通过菜单选择模式、填写参数、预览或写入。执行完成后可选择「继续操作」或「退出」。

CLI 模式：

```bash
# 预览（不写入）
CodexSessionSync.Tui.exe --preview

# 执行写入
CodexSessionSync.Tui.exe --apply
```

CLI 模式默认使用全供应商互同步，Codex Home 自动检测。

### Codex Home 路径

按以下顺序检测：
1. 环境变量 `CODEX_HOME`
2. 用户目录下的 `~/.codex`

## 同步模式详解

### 全供应商互同步

读取 `config.toml` 中配置的所有 provider，为每个源会话在各 provider 目录下创建镜像 JSONL 文件和 SQLite 索引行。镜像 ID 通过 UUID v5 确定性生成，同一源会话 + 目标 provider 永远映射到同一个 ID，可安全重复运行。

### OpenAI 同步到全部

与互同步类似，但仅以指定的源 provider 为起点，为其它所有 provider 创建镜像。

### 单目标迁移

直接修改 JSONL 文件中 `session_meta` 的 `model_provider` 字段和 SQLite 中对应的 `threads.model_provider` 列。将非保留 provider 的会话批量迁移到目标 provider。保留 provider 默认包含 `openai` 和目标 provider 本身。

## 核心引擎 API

`CodexSessionSync.Core` 提供的核心功能：

| 方法 | 说明 |
|------|------|
| `SyncEngine.DefaultCodexHome()` | 获取默认 Codex Home 路径 |
| `SyncEngine.InspectConfig(path, targetProvider)` | 解析 config.toml，返回活跃 provider、已配置 provider 列表等 |
| `SyncEngine.ResolveStateDb(codexHome, config, explicitPath)` | 定位 state.sqlite 数据库文件 |
| `SyncEngine.ResolveConfiguredProviders(config)` | 返回所有应参与同步的 provider 列表 |
| `SyncEngine.FindMutualSourceSessions(codexHome, providers, report)` | 遍历 JSONL 文件，筛选出源会话 |
| `SyncEngine.BuildMutualMirrorPlans(sessions, providers)` | 为每个源会话生成镜像计划 |
| `SyncEngine.SyncRolloutMirrors(plans, apply, report)` | 执行 JSONL 镜像文件的创建 |
| `SyncEngine.SyncSqliteMirrors(dbPath, plans, apply, report)` | 执行 SQLite 中 threads 行和子表的同步 |
| `SyncEngine.BackupSqlite(dbPath, backupRoot)` | 备份 SQLite 数据库 |
| `SyncEngine.BackupFile(src, codexHome, backupRoot)` | 备份单个 JSONL 文件 |
| `UuidV5.Create(namespaceId, name)` | UUID v5 确定性生成 |

## 技术细节

- **.NET 10** 框架，C# 13
- **SQLite**：`Microsoft.Data.Sqlite` 操作 state 数据库
- **JSONL**：`System.Text.Json` 解析和序列化会话文件
- **TOML**：自带轻量解析器 `TomlParser`，支持节、点分键、注释
- **UUID v5**：确定性命名空间 UUID，确保镜像 ID 可重复生成
- 镜像会话在 `session_meta.payload` 中记录 `forked_from_id` 字段，标记来源关系

## 各 GUI 版本特点

| 版本 | 框架 | 特点 |
|------|------|------|
| Wpf | WPF (.NET 10) | 深色终端输出区，卡片式模式选择，异步执行 |
| WinForms | Windows Forms | 轻量，自适应布局，同步执行 |
| Avalonia | Avalonia UI 11 | 跨平台潜力，Fluent 主题 |

## 许可

私有项目，未公开许可。