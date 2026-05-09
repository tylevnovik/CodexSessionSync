using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodexSessionSync.Core;

namespace CodexSessionSync.WinForms;

public class MainForm : Form
{
    private TextBox _codexHomeBox = null!;
    private TextBox _backupDirBox = null!;
    private TextBox _sourceProviderBox = null!;
    private TextBox _targetProviderBox = null!;
    private RadioButton _modeMutual = null!;
    private RadioButton _modeOpenAi = null!;
    private RadioButton _modeMigrate = null!;
    private Button _previewBtn = null!;
    private Button _applyBtn = null!;
    private Button _defaultsBtn = null!;
    private CheckBox _confirmCheck = null!;
    private TextBox _outputBox = null!;
    private Label _statusLabel = null!;

    public MainForm()
    {
        Text = "Codex 会话同步工具";
        Size = new System.Drawing.Size(960, 720);
        MinimumSize = new System.Drawing.Size(640, 480);
        StartPosition = FormStartPosition.CenterScreen;
        InitializeComponents();
        ResetDefaults();
    }

    private void InitializeComponents()
    {
        var font = new System.Drawing.Font("Microsoft YaHei", 9F);
        Font = font;
        Padding = new Padding(16);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Controls.Add(root);

        // === Header ===
        var headerPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));

        var titlePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = true };
        titlePanel.Controls.Add(new Label { Text = "Codex 会话同步工具", Font = new System.Drawing.Font("Microsoft YaHei", 14F, System.Drawing.FontStyle.Bold), AutoSize = true });
        titlePanel.Controls.Add(new Label { Text = "默认预览，不会写入；勾选确认后点击执行写入才会修改文件和 SQLite。", ForeColor = System.Drawing.Color.Gray, AutoSize = true, MaximumSize = new System.Drawing.Size(700, 0) });
        headerPanel.Controls.Add(titlePanel, 0, 0);

        _statusLabel = new Label
        {
            Text = "准备就绪",
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            BackColor = System.Drawing.Color.White,
            Dock = DockStyle.Fill,
            Margin = new Padding(8)
        };
        headerPanel.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(headerPanel, 0, 0);

        // === Config ===
        var configPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(4), AutoSize = true };
        configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var homePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, AutoSize = true };
        homePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        homePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        homePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        homePanel.Controls.Add(new Label { Text = "Codex Home", Font = new System.Drawing.Font(font, System.Drawing.FontStyle.Bold), AutoSize = true }, 0, 0);
        _codexHomeBox = new TextBox { Dock = DockStyle.Top, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        homePanel.Controls.Add(_codexHomeBox, 0, 1);
        homePanel.Controls.Add(new Label { Text = "默认读取 CODEX_HOME 或当前用户的 .codex。", ForeColor = System.Drawing.Color.Gray, AutoSize = true }, 0, 2);
        configPanel.Controls.Add(homePanel, 0, 0);

        var backupPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, AutoSize = true };
        backupPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        backupPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        backupPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        backupPanel.Controls.Add(new Label { Text = "备份目录", Font = new System.Drawing.Font(font, System.Drawing.FontStyle.Bold), AutoSize = true }, 0, 0);
        _backupDirBox = new TextBox { Dock = DockStyle.Top, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        backupPanel.Controls.Add(_backupDirBox, 0, 1);
        backupPanel.Controls.Add(new Label { Text = "执行写入时建议填写，例如桌面上的 codex-backup。", ForeColor = System.Drawing.Color.Gray, AutoSize = true }, 0, 2);
        configPanel.Controls.Add(backupPanel, 1, 0);
        root.Controls.Add(configPanel, 0, 1);

        // === Mode ===
        var modePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, AutoSize = true, Padding = new Padding(4) };
        modePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modePanel.Controls.Add(new Label { Text = "同步模式", Font = new System.Drawing.Font(font, System.Drawing.FontStyle.Bold), AutoSize = true }, 0, 0);

        var modeRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = true };
        _modeMutual = new RadioButton { Text = "全供应商互同步", Checked = true, AutoSize = true };
        _modeOpenAi = new RadioButton { Text = "OpenAI 同步到全部", AutoSize = true };
        _modeMigrate = new RadioButton { Text = "单目标迁移", AutoSize = true };
        modeRow.Controls.Add(_modeMutual);
        modeRow.Controls.Add(_modeOpenAi);
        modeRow.Controls.Add(_modeMigrate);
        modePanel.Controls.Add(modeRow, 0, 1);

        var providerRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = true };
        providerRow.Controls.Add(new Label { Text = "源 provider:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
        _sourceProviderBox = new TextBox { Text = "openai", Width = 120 };
        providerRow.Controls.Add(_sourceProviderBox);
        providerRow.Controls.Add(new Label { Text = "  目标 provider:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
        _targetProviderBox = new TextBox { Text = "openai", Width = 120 };
        providerRow.Controls.Add(_targetProviderBox);
        modePanel.Controls.Add(providerRow, 0, 2);

        var actionRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = true };
        _previewBtn = new Button { Text = "预览", AutoSize = true, MinimumSize = new System.Drawing.Size(80, 32), BackColor = System.Drawing.Color.FromArgb(52, 64, 84), ForeColor = System.Drawing.Color.White, FlatStyle = FlatStyle.Flat };
        _applyBtn = new Button { Text = "执行写入", AutoSize = true, MinimumSize = new System.Drawing.Size(90, 32), BackColor = System.Drawing.Color.FromArgb(180, 35, 24), ForeColor = System.Drawing.Color.White, FlatStyle = FlatStyle.Flat };
        _defaultsBtn = new Button { Text = "恢复默认路径", AutoSize = true, MinimumSize = new System.Drawing.Size(100, 32), BackColor = System.Drawing.Color.FromArgb(71, 84, 103), ForeColor = System.Drawing.Color.White, FlatStyle = FlatStyle.Flat };
        _confirmCheck = new CheckBox { Text = "我已备份或确认可以写入", AutoSize = true };
        actionRow.Controls.Add(_previewBtn);
        actionRow.Controls.Add(_applyBtn);
        actionRow.Controls.Add(_defaultsBtn);
        actionRow.Controls.Add(_confirmCheck);
        modePanel.Controls.Add(actionRow, 0, 3);

        _previewBtn.Click += (_, __) => RunSync(false);
        _applyBtn.Click += (_, __) => RunSync(true);
        _defaultsBtn.Click += (_, __) => ResetDefaults();
        root.Controls.Add(modePanel, 0, 2);

        // === Output ===
        var outputPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(4) };
        outputPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outputPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        outputPanel.Controls.Add(new Label { Text = "运行输出", Font = new System.Drawing.Font(font, System.Drawing.FontStyle.Bold), AutoSize = true }, 0, 0);
        _outputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            BackColor = System.Drawing.Color.FromArgb(16, 24, 40),
            ForeColor = System.Drawing.Color.FromArgb(228, 231, 236),
            Font = new System.Drawing.Font("Consolas", 10F),
            Text = "等待运行。"
        };
        outputPanel.Controls.Add(_outputBox, 0, 1);
        root.Controls.Add(outputPanel, 0, 3);
    }

    private void ResetDefaults()
    {
        _codexHomeBox.Text = SyncEngine.DefaultCodexHome();
        _backupDirBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "codex-session-sync-backup");
        _sourceProviderBox.Text = "openai";
        _targetProviderBox.Text = "openai";
    }

    private void RunSync(bool apply)
    {
        if (apply && !_confirmCheck.Checked)
        {
            _outputBox.Text = "执行写入前，请先勾选确认。";
            return;
        }

        SetBusy(true);
        _outputBox.Text = apply ? "正在执行写入..." : "正在预览...";
        _statusLabel.Text = "运行中";

        var codexHome = _codexHomeBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(codexHome)) codexHome = SyncEngine.DefaultCodexHome();
        codexHome = Path.GetFullPath(codexHome);

        var backupDir = _backupDirBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(backupDir)) backupDir = null;
        else backupDir = Path.GetFullPath(backupDir);

        if (apply && backupDir != null)
            Directory.CreateDirectory(backupDir);

        var sourceProvider = _sourceProviderBox.Text ?? "openai";
        var targetProvider = _targetProviderBox.Text ?? "openai";
        var mode = _modeMutual.Checked ? "mutual" : _modeOpenAi.Checked ? "openai" : "migrate";

        var sb = new StringBuilder();
        try
        {
            var config = SyncEngine.InspectConfig(Path.Combine(codexHome, "config.toml"), targetProvider);
            var stateDb = SyncEngine.ResolveStateDb(codexHome, config, null);
            var providers = SyncEngine.ResolveConfiguredProviders(config);

            if (mode == "mutual")
            {
                var report = new SyncReport { SourceProvider = "*", TargetProviders = providers };
                var sessions = SyncEngine.FindMutualSourceSessions(codexHome, providers, report, out var idMap, out var allProviders);
                report.TargetProviders = allProviders;
                var plans = SyncEngine.BuildMutualMirrorPlans(sessions, allProviders, idMap);

                if (apply && backupDir != null && stateDb != null)
                    SyncEngine.BackupSqlite(stateDb, backupDir);

                SyncEngine.SyncRolloutMirrors(plans, apply, report);
                var sqliteReport = SyncEngine.SyncSqliteMirrors(stateDb, plans, apply, report);
                AppendMutualReport(sb, codexHome, backupDir, config, allProviders, report, sqliteReport, apply);
            }
            else if (mode == "openai")
            {
                var targetProviders = providers.Where(p => p != sourceProvider).ToList();
                var report = new SyncReport { SourceProvider = sourceProvider, TargetProviders = targetProviders };
                var sessions = new List<SourceSession>();
                foreach (var path in SyncEngine.IterRolloutFiles(codexHome))
                {
                    report.FilesScanned++;
                    var (meta, _) = SyncEngine.GetSessionMeta(path);
                    if (meta == null) continue;
                    var provider = meta.Provider;
                    var providerKey = provider ?? "<missing>";
                    report.ProviderCountsBefore[providerKey] = report.ProviderCountsBefore.GetValueOrDefault(providerKey) + 1;
                    report.ProviderCountsAfter[providerKey] = report.ProviderCountsAfter.GetValueOrDefault(providerKey) + 1;
                    var id = meta.Id;
                    if (provider != sourceProvider || string.IsNullOrWhiteSpace(id) || SyncEngine.IsGeneratedMirrorSession(meta))
                        continue;
                    sessions.Add(new SourceSession(path, id, sourceProvider));
                }
                report.SourceSessionsFound = sessions.Count;

                var plans = new List<MirrorPlan>();
                var seen = new HashSet<string>();
                foreach (var session in sessions)
                {
                    foreach (var provider in targetProviders)
                    {
                        var mirrorId = UuidV5.Create(UuidV5.SyncNamespace, $"{session.ThreadId}:{provider}").ToString();
                        if (!seen.Add(mirrorId)) continue;
                        var mirrorPath = SyncEngine.ComputeMirrorPath(session.Path, session.ThreadId, mirrorId, provider);
                        plans.Add(new MirrorPlan(session.Path, session.ThreadId, provider, mirrorId, mirrorPath));
                    }
                }

                if (apply && backupDir != null && stateDb != null)
                    SyncEngine.BackupSqlite(stateDb, backupDir);

                SyncEngine.SyncRolloutMirrors(plans, apply, report);
                var sqliteReport = SyncEngine.SyncSqliteMirrors(stateDb, plans, apply, report);
                AppendOpenAiReport(sb, codexHome, backupDir, sourceProvider, targetProviders, report, sqliteReport, apply);
            }
            else
            {
                var keepProviders = new HashSet<string> { "openai", targetProvider };
                var filesScanned = 0;
                var filesNeedingUpdate = 0;
                var filesUpdated = 0;
                var sessionMetaRewritten = 0;
                var beforeCounts = new Dictionary<string, int>();
                var afterCounts = new Dictionary<string, int>();

                foreach (var path in SyncEngine.IterRolloutFiles(codexHome))
                {
                    filesScanned++;
                    var lines = File.ReadAllLines(path);
                    var rewritten = new List<string>();
                    bool changed = false;

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            rewritten.Add(line);
                            continue;
                        }
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(line);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("type", out var t) && t.GetString() == "session_meta" && root.TryGetProperty("payload", out var payload))
                            {
                                var provider = payload.TryGetProperty("model_provider", out var p) ? p.GetString() : null;
                                var pk = provider ?? "<missing>";
                                beforeCounts[pk] = beforeCounts.GetValueOrDefault(pk) + 1;
                                if (!string.IsNullOrWhiteSpace(provider) && !keepProviders.Contains(provider))
                                {
                                    var node = System.Text.Json.Nodes.JsonNode.Parse(line);
                                    if (node is System.Text.Json.Nodes.JsonObject obj && obj.TryGetPropertyValue("payload", out var pnode) && pnode is System.Text.Json.Nodes.JsonObject po)
                                    {
                                        po["model_provider"] = targetProvider;
                                        rewritten.Add(obj.ToJsonString());
                                        sessionMetaRewritten++;
                                        changed = true;
                                        afterCounts[targetProvider] = afterCounts.GetValueOrDefault(targetProvider) + 1;
                                        continue;
                                    }
                                }
                                afterCounts[pk] = afterCounts.GetValueOrDefault(pk) + 1;
                            }
                        }
                        catch { }
                        rewritten.Add(line);
                    }

                    if (!changed) continue;
                    filesNeedingUpdate++;
                    if (apply)
                    {
                        if (backupDir != null)
                            SyncEngine.BackupFile(path, codexHome, backupDir);
                        File.WriteAllLines(path, rewritten, new System.Text.UTF8Encoding(false));
                        filesUpdated++;
                    }
                }

                var sqliteReport = new SqliteReport { Path = stateDb };
                if (stateDb != null && File.Exists(stateDb))
                {
                    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={stateDb}");
                    conn.Open();
                    sqliteReport.ProviderCountsBefore = SyncEngine.FetchProviderCounts(conn);
                    var placeholders = string.Join(", ", keepProviders.Select(_ => "?"));
                    var filterSql = $"model_provider IS NOT NULL AND model_provider NOT IN ({placeholders})";
                    var filterParams = keepProviders.OrderBy(p => p).ToList();

                    using var countCmd = conn.CreateCommand();
                    countCmd.CommandText = $"SELECT COUNT(*) FROM threads WHERE {filterSql}";
                    for (int i = 0; i < filterParams.Count; i++)
                        countCmd.Parameters.AddWithValue(null, filterParams[i]);
                    sqliteReport.RowsNeedingUpdate = Convert.ToInt32(countCmd.ExecuteScalar());

                    if (apply && sqliteReport.RowsNeedingUpdate > 0)
                    {
                        if (backupDir != null)
                            SyncEngine.BackupSqlite(stateDb, backupDir);
                        using var tx = conn.BeginTransaction();
                        using var updateCmd = conn.CreateCommand();
                        updateCmd.Transaction = tx;
                        updateCmd.CommandText = $"UPDATE threads SET model_provider = ? WHERE {filterSql}";
                        updateCmd.Parameters.AddWithValue(null, targetProvider);
                        for (int i = 0; i < filterParams.Count; i++)
                            updateCmd.Parameters.AddWithValue(null, filterParams[i]);
                        updateCmd.ExecuteNonQuery();
                        tx.Commit();
                        conn.ExecuteNonQuery("PRAGMA wal_checkpoint(TRUNCATE)");
                    }

                    sqliteReport.ProviderCountsAfter = apply
                        ? SyncEngine.FetchProviderCounts(conn)
                        : SimulateMigrateCounts(sqliteReport.ProviderCountsBefore, targetProvider, keepProviders);
                }

                AppendMigrateReport(sb, codexHome, backupDir, targetProvider, keepProviders, filesScanned, filesNeedingUpdate, filesUpdated, sessionMetaRewritten, beforeCounts, afterCounts, sqliteReport, apply);
            }

            _outputBox.Text = sb.ToString();
            _statusLabel.Text = "完成";
        }
        catch (Exception ex)
        {
            _outputBox.Text = $"Error: {ex.Message}\n{ex.StackTrace}";
            _statusLabel.Text = "失败";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _previewBtn.Enabled = !busy;
        _applyBtn.Enabled = !busy;
        _defaultsBtn.Enabled = !busy;
        _statusLabel.Text = busy ? "运行中" : "准备就绪";
    }

    static List<string> FormatCounts(Dictionary<string, int> counts)
    {
        var items = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
        if (items.Count == 0) return new List<string> { "- none" };
        return items.Select(kv => $"- {kv.Key}: {kv.Value}").ToList();
    }

    static List<string> FormatCounts(List<(string?, int)> counts)
    {
        var merged = new Dictionary<string, int>();
        foreach (var (prov, cnt) in counts)
        {
            var label = prov ?? "<missing>";
            merged[label] = merged.GetValueOrDefault(label) + cnt;
        }
        var items = merged.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
        if (items.Count == 0) return new List<string> { "- none" };
        return items.Select(kv => $"- {kv.Key}: {kv.Value}").ToList();
    }

    static List<(string?, int)> SimulateMigrateCounts(List<(string?, int)> before, string target, HashSet<string> keep)
    {
        var dict = new Dictionary<string, int>();
        foreach (var (prov, cnt) in before)
        {
            var final = (prov == null || keep.Contains(prov)) ? prov : target;
            var key = final ?? "";
            dict[key] = dict.GetValueOrDefault(key) + cnt;
        }
        return dict.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => ((string?)kv.Key, kv.Value)).ToList();
    }

    static void AppendMutualReport(StringBuilder sb, string codexHome, string? backupDir, ConfigStatus config, List<string> providers, SyncReport report, SqliteReport sqliteReport, bool apply)
    {
        sb.AppendLine($"Mode: mutual provider session sync / {(apply ? "apply" : "preview")}");
        sb.AppendLine($"Codex Home: {codexHome}");
        sb.AppendLine($"Providers: {string.Join(", ", providers)}");
        if (backupDir != null) sb.AppendLine($"Backup dir: {backupDir}");
        sb.AppendLine();
        sb.AppendLine("Config");
        sb.AppendLine($"- config.toml: {config.Path}");
        sb.AppendLine($"- active model_provider: {config.ActiveProvider ?? "<missing>"}");
        sb.AppendLine($"- configured providers: {(config.ProviderKeys.Count > 0 ? string.Join(", ", config.ProviderKeys) : "<none>")}");
        sb.AppendLine();
        sb.AppendLine("rollout mirrors");
        sb.AppendLine($"- files scanned: {report.FilesScanned}");
        sb.AppendLine($"- source sessions found: {report.SourceSessionsFound}");
        sb.AppendLine($"- mirror files needed: {report.MirrorFilesNeeded}");
        sb.AppendLine($"- mirror files created: {report.MirrorFilesCreated}");
        sb.AppendLine($"- mirror files existing: {report.MirrorFilesExisting}");
        sb.AppendLine($"- mirror files updated: {report.MirrorFilesUpdated}");
        sb.AppendLine($"- mirror files stale (mirror newer, skipped): {report.MirrorFilesStale}");
        sb.AppendLine($"- file conflicts skipped: {report.MirrorFileConflicts}");
        sb.AppendLine("- providers before:");
        foreach (var line in FormatCounts(report.ProviderCountsBefore)) sb.AppendLine($"  {line}");
        sb.AppendLine("- providers after estimated by JSONL:");
        foreach (var line in FormatCounts(report.ProviderCountsAfter)) sb.AppendLine($"  {line}");
        sb.AppendLine();
        sb.AppendLine("SQLite index");
        sb.AppendLine($"- state db: {sqliteReport.Path ?? "<not found>"}");
        sb.AppendLine($"- mirror rows needed: {report.SqliteRowsNeeded}");
        sb.AppendLine($"- mirror rows created: {report.SqliteRowsCreated}");
        sb.AppendLine($"- mirror rows existing: {report.SqliteRowsExisting}");
        sb.AppendLine($"- row conflicts skipped: {report.SqliteRowsConflicting}");
        sb.AppendLine($"- source rows missing: {report.SqliteSourceRowsMissing}");
        sb.AppendLine("- providers before:");
        foreach (var line in FormatCounts(sqliteReport.ProviderCountsBefore)) sb.AppendLine($"  {line}");
        sb.AppendLine("- providers after:");
        foreach (var line in FormatCounts(sqliteReport.ProviderCountsAfter)) sb.AppendLine($"  {line}");
        if (report.TargetProviders.Count == 0)
            sb.AppendLine("\nNo configured providers found. Define model_providers in config.toml first.");
        if (report.MirrorFileConflicts > 0 || report.SqliteRowsConflicting > 0)
            sb.AppendLine("\nConflicts were skipped. Existing files/rows were not overwritten.");
        if (!apply)
            sb.AppendLine("\nPreview only. Check confirm box and click apply to write changes.");
    }

    static void AppendOpenAiReport(StringBuilder sb, string codexHome, string? backupDir, string sourceProvider, List<string> targetProviders, SyncReport report, SqliteReport sqliteReport, bool apply)
    {
        sb.AppendLine($"Mode: openai sync to all providers / {(apply ? "apply" : "preview")}");
        sb.AppendLine($"Codex Home: {codexHome}");
        sb.AppendLine($"Source provider: {sourceProvider}");
        sb.AppendLine($"Target providers: {(targetProviders.Count > 0 ? string.Join(", ", targetProviders) : "<none>")}");
        if (backupDir != null) sb.AppendLine($"Backup dir: {backupDir}");
        sb.AppendLine();
        sb.AppendLine("rollout mirrors");
        sb.AppendLine($"- files scanned: {report.FilesScanned}");
        sb.AppendLine($"- source sessions found: {report.SourceSessionsFound}");
        sb.AppendLine($"- mirror files needed: {report.MirrorFilesNeeded}");
        sb.AppendLine($"- mirror files created: {report.MirrorFilesCreated}");
        sb.AppendLine($"- mirror files existing: {report.MirrorFilesExisting}");
        sb.AppendLine($"- file conflicts skipped: {report.MirrorFileConflicts}");
        sb.AppendLine("- providers before:");
        foreach (var line in FormatCounts(report.ProviderCountsBefore)) sb.AppendLine($"  {line}");
        sb.AppendLine("- providers after estimated by JSONL:");
        foreach (var line in FormatCounts(report.ProviderCountsAfter)) sb.AppendLine($"  {line}");
        sb.AppendLine();
        sb.AppendLine("SQLite index");
        sb.AppendLine($"- state db: {sqliteReport.Path ?? "<not found>"}");
        sb.AppendLine($"- mirror rows needed: {report.SqliteRowsNeeded}");
        sb.AppendLine($"- mirror rows created: {report.SqliteRowsCreated}");
        sb.AppendLine($"- mirror rows existing: {report.SqliteRowsExisting}");
        sb.AppendLine($"- row conflicts skipped: {report.SqliteRowsConflicting}");
        sb.AppendLine($"- source rows missing: {report.SqliteSourceRowsMissing}");
        sb.AppendLine("- providers before:");
        foreach (var line in FormatCounts(sqliteReport.ProviderCountsBefore)) sb.AppendLine($"  {line}");
        sb.AppendLine("- providers after:");
        foreach (var line in FormatCounts(sqliteReport.ProviderCountsAfter)) sb.AppendLine($"  {line}");
        if (targetProviders.Count == 0)
            sb.AppendLine("\nNo target providers found.");
        if (report.MirrorFileConflicts > 0 || report.SqliteRowsConflicting > 0)
            sb.AppendLine("\nConflicts were skipped. Existing files/rows were not overwritten.");
        if (!apply)
            sb.AppendLine("\nPreview only. Check confirm box and click apply to write changes.");
    }

    static void AppendMigrateReport(StringBuilder sb, string codexHome, string? backupDir, string targetProvider, HashSet<string> keepProviders, int filesScanned, int filesNeedingUpdate, int filesUpdated, int sessionMetaRewritten, Dictionary<string, int> beforeCounts, Dictionary<string, int> afterCounts, SqliteReport sqliteReport, bool apply)
    {
        sb.AppendLine($"Mode: single-target migration / {(apply ? "apply" : "preview")}");
        sb.AppendLine($"Codex Home: {codexHome}");
        sb.AppendLine($"Target provider: {targetProvider}");
        sb.AppendLine($"Keep providers: {string.Join(", ", keepProviders)}");
        if (backupDir != null) sb.AppendLine($"Backup dir: {backupDir}");
        sb.AppendLine();
        sb.AppendLine("rollout scan");
        sb.AppendLine($"- files scanned: {filesScanned}");
        sb.AppendLine($"- files needing update: {filesNeedingUpdate}");
        sb.AppendLine($"- files updated: {filesUpdated}");
        sb.AppendLine($"- session_meta rewritten: {sessionMetaRewritten}");
        sb.AppendLine("- providers before:");
        foreach (var line in FormatCounts(beforeCounts)) sb.AppendLine($"  {line}");
        sb.AppendLine("- providers after:");
        foreach (var line in FormatCounts(afterCounts)) sb.AppendLine($"  {line}");
        sb.AppendLine();
        sb.AppendLine("SQLite index");
        sb.AppendLine($"- state db: {sqliteReport.Path ?? "<not found>"}");
        sb.AppendLine($"- rows needing update: {sqliteReport.RowsNeedingUpdate}");
        sb.AppendLine($"- rows updated: {sqliteReport.RowsUpdated}");
        sb.AppendLine("- providers before:");
        foreach (var line in FormatCounts(sqliteReport.ProviderCountsBefore)) sb.AppendLine($"  {line}");
        sb.AppendLine("- providers after:");
        foreach (var line in FormatCounts(sqliteReport.ProviderCountsAfter)) sb.AppendLine($"  {line}");
        if (!apply)
            sb.AppendLine("\nPreview only. Check confirm box and click apply to write changes.");
    }
}
