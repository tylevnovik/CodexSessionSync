using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using CodexSessionSync.Core;
using WinRT.Interop;
using Microsoft.UI;
using Microsoft.UI.Windowing;

namespace CodexSessionSync.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const string SwitchToLightGlyph = "\xE706";
        private const string SwitchToDarkGlyph = "\xE708";
        private AppWindow? _appWindow;
        private ElementTheme _currentTheme = ElementTheme.Dark;

        public MainWindow()
        {
            InitializeComponent();

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(wndId);

            _appWindow.Title = "Codex 会话同步工具";
            // WinUI 3 AppWindow.Resize does not account for scaling automatically.
            // In WindowsAppSDK standard way is either using OverlappedPresenter or PInvoke.
            // But actually AppWindow.Resize requires size in pixels. Let's just set the size of the AppWindow.
            double scale = GetScaleFactor(hWnd);
            _appWindow.Resize(new Windows.Graphics.SizeInt32((int)(900 * scale), (int)(820 * scale)));

            ApplyTheme(_currentTheme);
            ResetDefaults();
        }

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private double GetScaleFactor(IntPtr hwnd)
        {
            uint dpi = GetDpiForWindow(hwnd);
            return dpi / 96.0;
        }

        private void ResetDefaults()
        {
            CodexHomeBox.Text = SyncEngine.DefaultCodexHome();
            BackupDirBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "codex-session-sync-backup");
            SourceProviderBox.Text = "openai";
            TargetProviderBox.Text = "openai";
        }

        private void OnResetDefaults(object sender, RoutedEventArgs e) => ResetDefaults();

        private void OnThemeToggleClick(object sender, RoutedEventArgs e)
        {
            var nextTheme = _currentTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            ApplyTheme(nextTheme);
        }

        private void ApplyTheme(ElementTheme theme)
        {
            _currentTheme = theme;
            RootGrid.RequestedTheme = theme;
            ThemeIcon.Glyph = theme == ElementTheme.Dark ? SwitchToLightGlyph : SwitchToDarkGlyph;
            ToolTipService.SetToolTip(ThemeToggleBtn, theme == ElementTheme.Dark ? "切换到浅色模式" : "切换到深色模式");
            UpdateTitleBar(theme);
        }

        private void UpdateTitleBar(ElementTheme theme)
        {
            if (_appWindow is null)
                return;

            var titleBar = _appWindow.TitleBar;
            if (theme == ElementTheme.Dark)
            {
                titleBar.BackgroundColor = ColorHelper.FromArgb(255, 15, 17, 23);
                titleBar.ForegroundColor = ColorHelper.FromArgb(255, 249, 250, 251);
                titleBar.ButtonBackgroundColor = ColorHelper.FromArgb(255, 15, 17, 23);
                titleBar.ButtonForegroundColor = ColorHelper.FromArgb(255, 249, 250, 251);
                titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 36, 41, 54);
                titleBar.ButtonHoverForegroundColor = ColorHelper.FromArgb(255, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 46, 52, 66);
                titleBar.ButtonPressedForegroundColor = ColorHelper.FromArgb(255, 255, 255, 255);
                titleBar.InactiveBackgroundColor = ColorHelper.FromArgb(255, 15, 17, 23);
                titleBar.InactiveForegroundColor = ColorHelper.FromArgb(255, 152, 162, 179);
                titleBar.ButtonInactiveBackgroundColor = ColorHelper.FromArgb(255, 15, 17, 23);
                titleBar.ButtonInactiveForegroundColor = ColorHelper.FromArgb(255, 152, 162, 179);
            }
            else
            {
                titleBar.BackgroundColor = ColorHelper.FromArgb(255, 244, 246, 248);
                titleBar.ForegroundColor = ColorHelper.FromArgb(255, 16, 24, 40);
                titleBar.ButtonBackgroundColor = ColorHelper.FromArgb(255, 244, 246, 248);
                titleBar.ButtonForegroundColor = ColorHelper.FromArgb(255, 16, 24, 40);
                titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 233, 237, 243);
                titleBar.ButtonHoverForegroundColor = ColorHelper.FromArgb(255, 16, 24, 40);
                titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 217, 224, 234);
                titleBar.ButtonPressedForegroundColor = ColorHelper.FromArgb(255, 16, 24, 40);
                titleBar.InactiveBackgroundColor = ColorHelper.FromArgb(255, 244, 246, 248);
                titleBar.InactiveForegroundColor = ColorHelper.FromArgb(255, 102, 112, 133);
                titleBar.ButtonInactiveBackgroundColor = ColorHelper.FromArgb(255, 244, 246, 248);
                titleBar.ButtonInactiveForegroundColor = ColorHelper.FromArgb(255, 102, 112, 133);
            }
        }

        private void OnModeCardClick(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Border border && border.Child is RadioButton rb)
            {
                rb.IsChecked = true;
                e.Handled = true;
            }
        }

        private void OnPreviewClick(object sender, RoutedEventArgs e) => RunSync(false);

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            if (ConfirmCheck.IsChecked != true)
            {
                AppendOutput("执行写入前，请先勾选确认。\n");
                return;
            }
            RunSync(true);
        }

        private async void RunSync(bool apply)
        {
            SetBusy(true);
            OutputBox.Text = apply ? "正在执行写入..." : "正在预览...";
            StatusLabel.Text = "运行中";

            var codexHome = CodexHomeBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(codexHome))
                codexHome = SyncEngine.DefaultCodexHome();
            codexHome = Path.GetFullPath(codexHome);

            var backupDir = BackupDirBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(backupDir)) backupDir = null;
            else backupDir = Path.GetFullPath(backupDir);

            var sourceProvider = SourceProviderBox.Text.Trim();
            var targetProvider = TargetProviderBox.Text.Trim();
            var isMutual = ModeMutual.IsChecked == true;
            var isOpenAi = ModeOpenAi.IsChecked == true;

            if (apply && backupDir != null)
                Directory.CreateDirectory(backupDir);

            var sb = new StringBuilder();
            try
            {
                await Task.Run(() =>
                {
                    var config = SyncEngine.InspectConfig(Path.Combine(codexHome, "config.toml"), targetProvider);
                    var stateDb = SyncEngine.ResolveStateDb(codexHome, config, null);
                    var providers = SyncEngine.ResolveConfiguredProviders(config);

                    if (isMutual)
                    {
                        var report = new SyncReport { SourceProvider = "*", TargetProviders = providers };
                        var sessions = SyncEngine.FindMutualSourceSessions(codexHome, providers, report, out var idMap, out var allProviders);
                        report.TargetProviders = allProviders;
                        var plans = SyncEngine.BuildMutualMirrorPlans(sessions, allProviders, idMap);

                        if (apply && backupDir != null && stateDb != null)
                            SyncEngine.BackupSqlite(stateDb, backupDir);

                        SyncEngine.SyncRolloutMirrors(plans, apply, report);
                        var sqliteReport = SyncEngine.SyncSqliteMirrors(stateDb, plans, apply, report);

                        sb.AppendLine($"Mode: mutual provider session sync / {(apply ? "apply" : "preview")}");
                        sb.AppendLine($"Codex Home: {codexHome}");
                        sb.AppendLine($"Providers: {string.Join(", ", allProviders)}");
                        if (backupDir != null) sb.AppendLine($"Backup dir: {backupDir}");
                        sb.AppendLine();
                        sb.AppendLine("Config");
                        sb.AppendLine($"- config.toml: {config.Path}");
                        sb.AppendLine($"- active model_provider: {config.ActiveProvider ?? "<missing>"}");
                        sb.AppendLine($"- configured providers: {(config.ProviderKeys.Count > 0 ? string.Join(", ", config.ProviderKeys) : "<none>")}")
                        ;
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
                        sb.AppendLine();
                        if (report.TargetProviders.Count == 0)
                            sb.AppendLine("No configured providers found. Define model_providers in config.toml first.");
                        if (report.MirrorFileConflicts > 0 || report.SqliteRowsConflicting > 0)
                            sb.AppendLine("Conflicts were skipped. Existing files/rows were not overwritten.");
                        if (!apply)
                            sb.AppendLine("Preview only. Check confirm box and click apply to write changes.");
                    }
                    else if (isOpenAi)
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
                        sb.AppendLine();
                        if (targetProviders.Count == 0)
                            sb.AppendLine("No target providers found.");
                        if (report.MirrorFileConflicts > 0 || report.SqliteRowsConflicting > 0)
                            sb.AppendLine("Conflicts were skipped. Existing files/rows were not overwritten.");
                        if (!apply)
                            sb.AppendLine("Preview only. Check confirm box and click apply to write changes.");
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
                                            var node = System.Text.Json.Nodes.JsonNode.Parse(line, documentOptions: new System.Text.Json.JsonDocumentOptions());
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
                                File.WriteAllLines(path, rewritten, new UTF8Encoding(false));
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

                        sb.AppendLine($"Mode: single-target migration / {(apply ? "apply" : "preview")}");
                        sb.AppendLine($"Codex Home: {codexHome}");
                        sb.AppendLine($"Target provider: {targetProvider}");
                        sb.AppendLine($"Keep providers: {string.Join(", ", keepProviders)}");
                        if (backupDir != null) sb.AppendLine($"Backup dir: {backupDir}");
                        sb.AppendLine();
                        sb.AppendLine("Config");
                        sb.AppendLine($"- config.toml: {config.Path}");
                        sb.AppendLine($"- active model_provider: {config.ActiveProvider ?? "<missing>"}");
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
                        sb.AppendLine();
                        if (!apply)
                            sb.AppendLine("Preview only. Check confirm box and click apply to write changes.");
                    }
                });

                StatusLabel.Text = "完成";
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error: {ex.Message}");
                sb.AppendLine(ex.StackTrace);
                StatusLabel.Text = "失败";
            }
            finally
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    OutputBox.Text = sb.ToString();
                    SetBusy(false);
                });
            }
        }

        private static List<string> FormatCounts(Dictionary<string, int> counts)
        {
            var items = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
            if (items.Count == 0) return new List<string> { "- none" };
            return items.Select(kv => $"- {kv.Key}: {kv.Value}").ToList();
        }

        private static List<string> FormatCounts(List<(string?, int)> counts)
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

        private static List<(string?, int)> SimulateMigrateCounts(List<(string?, int)> before, string target, HashSet<string> keep)
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

        private void SetBusy(bool busy)
        {
            PreviewBtn.IsEnabled = !busy;
            ApplyBtn.IsEnabled = !busy;
            StatusLabel.Text = busy ? "运行中" : "准备就绪";
        }

        private void AppendOutput(string text)
        {
            OutputBox.Text += text;
            var grid = (Grid)OutputBox.Parent;
            var sv = (ScrollViewer)VisualTreeHelper.GetChild(OutputBox, 0);
            if (sv != null)
            {
                sv.ChangeView(null, sv.ScrollableHeight, null);
            }
        }
    }
}
