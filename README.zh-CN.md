# Codex 会话同步工具
中文说明: [README.zh-CN.md](README.zh-CN.md)
这是一个本地工具，用来同步 Codex 在不同模型供应商 provider 下的历史会话可见性。

工具默认只做预览，不会写入数据。只有在 Web UI 中勾选确认并点击“执行写入”，或在命令行中显式追加 `--apply`，才会修改 Codex 会话文件和 SQLite 索引。

## 目录结构

- `src/m.py`：核心命令行脚本。
- `src/m_webui.py`：本地 Web UI 包装入口。
- `dist/CodexSessionSync.exe`：已经打包好的 Windows 可执行文件。
- `CodexSessionSync.spec`：PyInstaller 打包配置。
- `build/`：PyInstaller 构建缓存，可以删除后重新生成。
- `README.md`：英文说明。
- `README.zh-CN.md`：中文说明。

## 推荐用法：Web UI

在当前目录运行：

```powershell
.\dist\CodexSessionSync.exe
```

程序会启动本地服务，并自动打开浏览器页面。服务只监听本机 `127.0.0.1`。

建议流程：

1. 确认 `Codex Home` 路径是否正确，默认通常是 `C:\Users\<用户名>\.codex`。
2. 选择“全供应商互同步”。
3. 先点击“预览”，查看需要创建的镜像文件、SQLite 行数和冲突数。
4. 如果要正式写入，填写备份目录。
5. 勾选“我已备份或确认可以写入”。
6. 点击“执行写入”。

关闭启动窗口后，本地 Web 服务会停止。

## 支持的同步模式

### 全供应商互同步

这是推荐模式。它会读取 `config.toml` 中已配置的 providers，让每个 provider 最终都能看到同一批原始会话。

规则：

- provider 范围来自 `config.toml` 的 `model_providers`，并包含当前 `model_provider`。
- 所有 provider 的原始会话会取并集。
- 某个 provider 缺少某条会话时，会创建镜像会话。
- 已经由本工具生成的镜像不会再次作为源会话扩散。
- 文件或 SQLite 行发生冲突时跳过并报告，不覆盖。

命令行预览：

```powershell
python .\src\m.py --sync-all-providers-mutually
```

命令行写入：

```powershell
python .\src\m.py --sync-all-providers-mutually --backup-dir .\backup --apply
```

### OpenAI 同步到全部 provider

以指定源 provider 为源，给其它 provider 创建镜像。默认源 provider 是 `openai`。

```powershell
python .\src\m.py --sync-openai-to-all-providers
```

指定源 provider：

```powershell
python .\src\m.py --sync-openai-to-all-providers --source-provider openai
```

### 单目标迁移

把非保留 provider 的历史会话迁移到目标 provider。默认目标 provider 是 `openai`。

```powershell
python .\src\m.py --target-provider openai
```

正式写入时追加：

```powershell
python .\src\m.py --target-provider openai --backup-dir .\backup --apply
```

## 重新打包 EXE

如果修改了 `src/m.py` 或 `src/m_webui.py`，需要重新生成 exe。

在 `tools/codex-session-sync` 目录运行：

```powershell
python -m PyInstaller --clean --noconfirm .\CodexSessionSync.spec
```

生成结果：

```text
dist/CodexSessionSync.exe
```

如果提示 exe 被占用，先关闭正在运行的 `CodexSessionSync.exe` 窗口或进程，再重新打包。

## 安全注意事项

- 第一次使用一定先预览。
- 正式写入前建议填写备份目录。
- `--apply` 会修改 Codex 会话文件和 SQLite 索引。
- 工具不会覆盖冲突文件或冲突 SQLite 行，只会跳过并报告。
- 如果要分发给别人，建议同时分发整个 `tools/codex-session-sync` 文件夹，或至少分发 `dist/CodexSessionSync.exe` 和这份说明。

## 常见问题

### 运行后没有打开浏览器怎么办？

查看启动窗口中的地址，通常类似：

```text
http://127.0.0.1:8765/
```

复制到浏览器打开即可。

### 找不到会话或 provider 怎么办？

先确认 `Codex Home` 是否指向正确的 `.codex` 目录，并检查该目录下是否存在 `config.toml`、`sessions` 或 `archived_sessions`。

### 可以直接操作真实 Codex 目录吗？

可以，但建议先复制一份 `.codex` 到临时目录做预览测试。确认输出符合预期后，再对真实目录执行写入。
