using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new System.Drawing.Size(1120, 780);
        MinimumSize = new System.Drawing.Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        InitializeComponents();
        ResetDefaults();
    }

    private void InitializeComponents()
    {
        var font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        Font = font;
        BackColor = System.Drawing.SystemColors.Control;
        Padding = new Padding(12);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            AutoSize = true
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titlePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, AutoSize = true };
        titlePanel.Controls.Add(new Label
        {
            Text = "Codex 会话同步工具",
            Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.SystemColors.ControlText,
            AutoSize = true
        }, 0, 0);
        titlePanel.Controls.Add(new Label
        {
            Text = "先预览同步计划，再确认写入 JSONL 和 SQLite 索引。",
            ForeColor = System.Drawing.SystemColors.GrayText,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        }, 0, 1);
        headerPanel.Controls.Add(titlePanel, 0, 0);

        _statusLabel = new Label
        {
            Text = "准备就绪",
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            ForeColor = System.Drawing.SystemColors.GrayText,
            Font = new System.Drawing.Font(font, System.Drawing.FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(12, 8, 0, 0)
        };
        headerPanel.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(headerPanel, 0, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270F));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.Controls.Add(body, 0, 1);

        var modeGroup = new GroupBox
        {
            Text = "同步模式",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        var modePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 7,
            AutoSize = true
        };
        _modeMutual = CreateModeButton("全供应商互同步", true);
        _modeOpenAi = CreateModeButton("OpenAI 同步到全部", false);
        _modeMigrate = CreateModeButton("单目标迁移", false);
        _modeMutual.CheckedChanged += (_, __) => UpdateModeButtonVisuals();
        _modeOpenAi.CheckedChanged += (_, __) => UpdateModeButtonVisuals();
        _modeMigrate.CheckedChanged += (_, __) => UpdateModeButtonVisuals();
        modePanel.Controls.Add(_modeMutual, 0, 0);
        modePanel.Controls.Add(CreateHelpLabel("所有 provider 互相补齐镜像会话。"), 0, 1);
        modePanel.Controls.Add(_modeOpenAi, 0, 2);
        modePanel.Controls.Add(CreateHelpLabel("以源 provider 为起点创建镜像。"), 0, 3);
        modePanel.Controls.Add(_modeMigrate, 0, 4);
        modePanel.Controls.Add(CreateHelpLabel("把历史会话归并到目标 provider。"), 0, 5);
        modePanel.Controls.Add(CreateHelpLabel("预览不会写入；执行写入前需要勾选确认。"), 0, 6);
        modeGroup.Controls.Add(modePanel);
        body.Controls.Add(modeGroup, 0, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(12, 0, 0, 0) };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        body.Controls.Add(right, 1, 0);

        var configGroup = new GroupBox
        {
            Text = "路径与 provider",
            Dock = DockStyle.Top,
            Padding = new Padding(10),
            AutoSize = true
        };
        var configPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true
        };
        configPanel.ColumnCount = 2;
        configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        configPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        configPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        configPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _codexHomeBox = CreateTextBox();
        _backupDirBox = CreateTextBox();
        _sourceProviderBox = CreateTextBox("openai");
        _targetProviderBox = CreateTextBox("openai");

        configPanel.Controls.Add(CreateField("Codex Home", _codexHomeBox, "默认读取 CODEX_HOME 或当前用户的 .codex。"), 0, 0);
        configPanel.Controls.Add(CreateField("备份目录", _backupDirBox, "写入时建议填写，例如桌面上的 codex-backup。"), 1, 0);
        configPanel.Controls.Add(CreateField("源 provider", _sourceProviderBox, null), 0, 1);
        configPanel.Controls.Add(CreateField("目标 provider", _targetProviderBox, null), 1, 1);
        configGroup.Controls.Add(configPanel);
        right.Controls.Add(configGroup, 0, 0);

        var outputGroup = new GroupBox
        {
            Text = "运行输出",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            Margin = new Padding(0, 10, 0, 0)
        };
        _outputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            BackColor = System.Drawing.SystemColors.Window,
            ForeColor = System.Drawing.SystemColors.WindowText,
            Font = new System.Drawing.Font("Consolas", 10F),
            Text = "等待运行。"
        };
        outputGroup.Controls.Add(_outputBox);
        right.Controls.Add(outputGroup, 0, 1);

        var actionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 0),
            AutoSize = true
        };
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _previewBtn = CreateActionButton("预览", System.Drawing.Color.FromArgb(31, 95, 168));
        _applyBtn = CreateActionButton("执行写入", System.Drawing.Color.FromArgb(180, 35, 24));
        _defaultsBtn = CreateActionButton("恢复默认路径", System.Drawing.Color.FromArgb(71, 84, 103));
        _confirmCheck = new CheckBox { Text = "我已备份或确认可以写入", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(12, 6, 0, 0) };
        _previewBtn.Click += async (_, __) => await RunSyncAsync(false);
        _applyBtn.Click += async (_, __) => await RunSyncAsync(true);
        _defaultsBtn.Click += (_, __) => ResetDefaults();
        actionPanel.Controls.Add(_previewBtn, 0, 0);
        actionPanel.Controls.Add(_applyBtn, 1, 0);
        actionPanel.Controls.Add(_defaultsBtn, 2, 0);
        actionPanel.Controls.Add(_confirmCheck, 4, 0);
        root.Controls.Add(actionPanel, 0, 2);
        UpdateModeButtonVisuals();
    }

    private static TableLayoutPanel CreateSectionPanel(string title)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(14),
            BackColor = System.Drawing.Color.FromArgb(252, 253, 255),
            AutoSize = true
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label
        {
            Text = title,
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(52, 64, 84),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);
        return panel;
    }

    private static RadioButton CreateModeButton(string text, bool isChecked)
    {
        return new RadioButton
        {
            Text = text,
            Checked = isChecked,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0),
            ForeColor = System.Drawing.SystemColors.ControlText
        };
    }

    private static TextBox CreateTextBox(string text = "")
    {
        return new TextBox
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 26,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private static Control CreateField(string label, TextBox textBox, string? help)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = help == null ? 2 : 3, ColumnCount = 1, Padding = new Padding(0, 0, 12, 12), AutoSize = true };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (help != null) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold), ForeColor = System.Drawing.SystemColors.ControlText, AutoSize = true }, 0, 0);
        panel.Controls.Add(textBox, 0, 1);
        if (help != null)
            panel.Controls.Add(CreateHelpLabel(help), 0, 2);
        return panel;
    }

    private static Label CreateHelpLabel(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = System.Drawing.SystemColors.GrayText,
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(240, 0),
            Margin = new Padding(22, 2, 0, 10)
        };
    }

    private static Button CreateActionButton(string text, System.Drawing.Color backColor)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new System.Drawing.Size(92, 36),
            Margin = new Padding(0, 0, 10, 0),
            BackColor = backColor,
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat
        };
    }

    private void UpdateModeButtonVisuals()
    {
        StyleModeButton(_modeMutual);
        StyleModeButton(_modeOpenAi);
        StyleModeButton(_modeMigrate);
    }

    private static void StyleModeButton(RadioButton button) => button.Font = new System.Drawing.Font(button.Font, button.Checked ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular);

    private void ResetDefaults()
    {
        _codexHomeBox.Text = SyncEngine.DefaultCodexHome();
        _backupDirBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "codex-session-sync-backup");
        _sourceProviderBox.Text = "openai";
        _targetProviderBox.Text = "openai";
    }

    private async Task RunSyncAsync(bool apply)
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
            await Task.Run(() =>
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
            });

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
        if (busy)
            _statusLabel.Text = "运行中";
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
