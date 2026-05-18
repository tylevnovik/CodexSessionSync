using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using CodexSessionSync.Core;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;

namespace CodexSessionSync.Avalonia;

public partial class MainWindow : AppWindow
{
    private bool _isLightTheme;

    private TextBox _codexHomeBox = null!;
    private TextBox _backupDirBox = null!;
    private TextBox _sourceProviderBox = null!;
    private TextBox _targetProviderBox = null!;
    private FAComboBox _modeBox = null!;
    private Button _previewBtn = null!;
    private Button _applyBtn = null!;
    private Button _defaultsBtn = null!;
    private Button _themeToggleBtn = null!;
    private SymbolIcon _themeToggleIcon = null!;
    private TextBlock _themeToggleLabel = null!;
    private ToggleSwitch _confirmToggle = null!;
    private TextBox _outputBox = null!;
    private InfoBar _statusInfoBar = null!;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowChrome();
        ResetDefaults();
        ApplyTheme(isLightTheme: false);
        UpdateModeFields();
    }

    private void InitializeComponent()
    {
        global::Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        _codexHomeBox = this.FindControl<TextBox>("CodexHomeBox")!;
        _backupDirBox = this.FindControl<TextBox>("BackupDirBox")!;
        _sourceProviderBox = this.FindControl<TextBox>("SourceProviderBox")!;
        _targetProviderBox = this.FindControl<TextBox>("TargetProviderBox")!;
        _modeBox = this.FindControl<FAComboBox>("ModeBox")!;
        _previewBtn = this.FindControl<Button>("PreviewBtn")!;
        _applyBtn = this.FindControl<Button>("ApplyBtn")!;
        _defaultsBtn = this.FindControl<Button>("DefaultsBtn")!;
        _themeToggleBtn = this.FindControl<Button>("ThemeToggleBtn")!;
        _themeToggleIcon = this.FindControl<SymbolIcon>("ThemeToggleIcon")!;
        _themeToggleLabel = this.FindControl<TextBlock>("ThemeToggleLabel")!;
        _confirmToggle = this.FindControl<ToggleSwitch>("ConfirmToggle")!;
        _outputBox = this.FindControl<TextBox>("OutputBox")!;
        _statusInfoBar = this.FindControl<InfoBar>("StatusInfoBar")!;

        _previewBtn.Click += OnPreviewClick;
        _applyBtn.Click += OnApplyClick;
        _defaultsBtn.Click += OnResetDefaults;
        _themeToggleBtn.Click += OnThemeToggleClick;
        _modeBox.SelectionChanged += OnModeSelectionChanged;
    }

    private string SelectedMode()
    {
        return _modeBox.SelectedIndex switch
        {
            1 => "openai",
            2 => "migrate",
            _ => "mutual"
        };
    }

    private void OnModeSelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateModeFields();

    private void UpdateModeFields()
    {
        var mode = SelectedMode();
        _sourceProviderBox.IsEnabled = mode == "openai";
        _targetProviderBox.IsEnabled = mode == "migrate";
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e) => ApplyTheme(!_isLightTheme);

    private void ConfigureWindowChrome()
    {
        TitleBar.ExtendsContentIntoTitleBar = false;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Simple;
        TitleBar.BackgroundColor = Colors.Transparent;
        TitleBar.InactiveBackgroundColor = Colors.Transparent;
        TitleBar.ButtonBackgroundColor = Colors.Transparent;
        TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        Opened += (_, _) => UpdateTitleBarColors(_isLightTheme);
    }

    private void ApplyTheme(bool isLightTheme)
    {
        _isLightTheme = isLightTheme;
        var variant = isLightTheme ? ThemeVariant.Light : ThemeVariant.Dark;
        RequestedThemeVariant = variant;
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = variant;

        _themeToggleIcon.Symbol = isLightTheme ? Symbol.DarkTheme : Symbol.WeatherSunny;
        _themeToggleLabel.Text = isLightTheme ? "深色模式" : "浅色模式";
        ToolTip.SetTip(_themeToggleBtn, isLightTheme ? "切换到深色模式" : "切换到浅色模式");
        UpdateTitleBarColors(isLightTheme);
    }

    private void UpdateTitleBarColors(bool isLightTheme)
    {
        var foreground = isLightTheme ? Colors.Black : Colors.White;
        var hover = isLightTheme ? Color.FromArgb(28, 0, 0, 0) : Color.FromArgb(36, 255, 255, 255);
        var pressed = isLightTheme ? Color.FromArgb(42, 0, 0, 0) : Color.FromArgb(50, 255, 255, 255);
        var border = isLightTheme ? Color.Parse("#DADDE5") : Color.Parse("#263247");

        TitleBar.ForegroundColor = foreground;
        TitleBar.InactiveForegroundColor = foreground;
        TitleBar.ButtonForegroundColor = foreground;
        TitleBar.ButtonInactiveForegroundColor = foreground;
        TitleBar.ButtonHoverForegroundColor = foreground;
        TitleBar.ButtonPressedForegroundColor = foreground;
        TitleBar.ButtonHoverBackgroundColor = hover;
        TitleBar.ButtonPressedBackgroundColor = pressed;
        PlatformFeatures?.SetWindowBorderColor(border);
    }

    private void SetStatus(string title, string message, InfoBarSeverity severity)
    {
        _statusInfoBar.Title = title;
        _statusInfoBar.Message = message;
        _statusInfoBar.Severity = severity;
        _statusInfoBar.IsOpen = true;
    }

    private void ResetDefaults()
    {
        _codexHomeBox.Text = SyncEngine.DefaultCodexHome();
        _backupDirBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "codex-session-sync-backup");
        _sourceProviderBox.Text = "openai";
        _targetProviderBox.Text = "openai";
    }

    private void OnResetDefaults(object? sender, RoutedEventArgs e) => ResetDefaults();
    private async void OnPreviewClick(object? sender, RoutedEventArgs e) => await RunSyncAsync(false);

    private async void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (_confirmToggle.IsChecked != true)
        {
            _outputBox.Text = "执行写入前，请先勾选确认。";
            SetStatus("需要确认", "开启写入确认后才能执行写入。", InfoBarSeverity.Warning);
            return;
        }
        await RunSyncAsync(true);
    }

    private async Task RunSyncAsync(bool apply)
    {
        SetBusy(true);
        _outputBox.Text = apply ? "正在执行写入..." : "正在预览...";
        SetStatus("运行中", apply ? "正在执行写入..." : "正在预览...", InfoBarSeverity.Informational);

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
        var mode = SelectedMode();

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
            SetStatus("完成", apply ? "写入执行完成。" : "预览完成，尚未写入。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _outputBox.Text = $"Error: {ex.Message}\n{ex.StackTrace}";
            SetStatus("失败", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _previewBtn.IsEnabled = !busy;
        _applyBtn.IsEnabled = !busy;
        _defaultsBtn.IsEnabled = !busy;
        _modeBox.IsEnabled = !busy;
        if (busy)
            SetStatus("运行中", "同步任务正在后台执行。", InfoBarSeverity.Informational);
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
