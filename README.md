# Codex Session Sync

Codex 会话同步工具——在多个 LLM provider 之间同步 OpenAI Codex（现 OpenAI CLI）的会话数据。

## 功能

- **全供应商互同步**：将配置中的每个 provider 设置为其他所有 provider 的镜像，使所有 provider 都能看到同一批会话。
- **OpenAI 同步到全部**：以单个 provider 为源，为其它所有 provider 创建镜像会话。
- **单目标迁移**：将不属于保留 provider 的会话批量迁移到目标 provider（直接改写 JSONL 和 SQLite 中的 `model_provider` 字段）。
- 预览模式：默认只读，确认后才实际写入。报告会区分「已存在」「需更新」「已过期(skipped)」「冲突」等状态。
- SQLite 索引同步：除了 JSONL 文件，还会在 `state.db` 中插入或更新对应的 `threads` 行和关联子表。
- 自动备份：写入前可指定备份目录，自动备份 SQLite 和被修改的 JSONL 文件。

## 项目结构

```
CodexSessionSync.Core/        核心同步引擎（纯类库，无 GUI 依赖）
CodexSessionSync.Wpf/         WPF 桌面界面
CodexSessionSync.WinForms/    WinForms 桌面界面
CodexSessionSync.Avalonia/    Avalonia 跨平台桌面界面
CodexSessionSync.WinUI/       WinUI 3 桌面界面
CodexSessionSync.Tui/         终端交互界面（Spectre.Console）
dist/                         发布输出目录
release/                      GitHub Release 资产输出目录（由脚本生成）
publish-all.ps1               一键构建发布脚本
```

## 构建与发布

```powershell
# 构建并发布所有 release 产物
.\publish-all.ps1
```

脚本会先把各项目发布到 `dist\`，再把可分发资产写入 `release\`。`release\` 是 GitHub Release 上传目录，不提交到仓库。

`dist\` 目录中的主要输出：

| 项目 | 输出 |
|------|------|
| `dist\CodexSessionSync.Wpf\CodexSessionSync.Wpf.exe` | WPF 界面 |
| `dist\CodexSessionSync.WinForms-aot\CodexSessionSync.WinForms.exe` | WinForms 界面 |
| `dist\CodexSessionSync.Avalonia-aot\CodexSessionSync.Avalonia.exe` | Avalonia 界面 |
| `dist\CodexSessionSync.Tui-aot\CodexSessionSync.Tui.exe` | 终端界面 |
| `dist\CodexSessionSync.WinUI-aot\CodexSessionSync.WinUI.exe` | WinUI 3 界面 |

GitHub Release 资产：

| 资产 | 说明 |
|------|------|
| `release\CodexSessionSync.Avalonia-aot.zip` | Avalonia self-contained AOT 单文件 |
| `release\CodexSessionSync.Tui-aot.zip` | TUI self-contained AOT 单文件 |
| `release\CodexSessionSync.WinForms-aot.zip` | WinForms self-contained AOT 单文件 |
| `release\CodexSessionSync.WinUI-aot.zip` | WinUI 3 self-contained AOT 文件夹包 |
| `release\CodexSessionSync.Wpf.exe` | WPF self-contained 单文件 |

WinForms、Avalonia、TUI 版本为 self-contained AOT 单文件；WinUI 3 版本由于 Windows App SDK 运行时限制，采用 self-contained AOT 文件夹发布；WPF 版本为 self-contained 单文件但不支持 Native AOT。

## 使用方法

### GUI 版本（WPF / WinForms / Avalonia / WinUI）

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

自动发现所有 provider（从 `config.toml` 和已有 session 文件中），为每个源会话在各 provider 目录下创建或更新镜像 JSONL 文件和 SQLite 索引行。

**配对逻辑：**
- **正向**（无 `forked_from_id` 的源 → 其他 provider）：通过 UUIDv5 确定性哈希 `UUIDv5(源ID:目标provider)` 计算目标 ID，与现有文件路径比对。
- **反向**（有 `forked_from_id` 的源 → 原始 provider）：通过 `forked_from_id` 链反查原始 session 的 provider 和路径，直接回写原文件而非创建新文件。

**方向判断：**
- 比对时先做 JSON 格式化归一化 + 跳过 `session_meta` 行，消除格式差异和元数据干扰
- 内容不同时比归一化行数，消息多的一方覆盖消息少的一方
- 同一对的两个方向只会有一个执行更新，另一个跳过（stale）

**容错：** 读取文件时使用 `FileShare.ReadWrite`，即使 Codex Desktop 正在使用也能正常读取。

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
| `SyncEngine.FindMutualSourceSessions(codexHome, providers, report, out idMap, out allProviders)` | 遍历 JSONL 文件，筛选源会话，同时返回全局 ID→provider+路径 映射和自动发现的所有 provider |
| `SyncEngine.BuildMutualMirrorPlans(sessions, providers, idMap)` | 为每个源会话生成镜像计划，支持 forked_from_id 反向映射 |
| `SyncEngine.SyncRolloutMirrors(plans, apply, report)` | 执行 JSONL 镜像文件的创建/更新，基于归一化内容比对 + 行数方向判断 |
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
