using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodexSessionSync.Core;
using Spectre.Console;

namespace CodexSessionSync.Tui;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            InnerMain(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            if (args.Length == 0 && !Console.IsInputRedirected)
            {
                Console.WriteLine("Press any key to exit...");
                try { Console.ReadKey(); } catch { }
            }
        }
    }

    static void InnerMain(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            ShowHelp();
            return;
        }

        if (args.Contains("--preview") || args.Contains("--apply"))
        {
            var mode = ParseMode(GetArg(args, "--mode") ?? "mutual");
            var codexHome = Path.GetFullPath(GetArg(args, "--codex-home") ?? SyncEngine.DefaultCodexHome());
            var backupDirRaw = GetArg(args, "--backup-dir");
            var backupDir = string.IsNullOrWhiteSpace(backupDirRaw) ? null : Path.GetFullPath(backupDirRaw);
            var sourceProvider = GetArg(args, "--source") ?? "openai";
            var targetProvider = GetArg(args, "--target") ?? "openai";
            var apply = args.Contains("--apply");
            RenderRunSummary(mode, codexHome, backupDir, sourceProvider, targetProvider, apply);
            RunSyncAndShow(mode, codexHome, backupDir, sourceProvider, targetProvider, apply);
            return;
        }

        RenderIntro();

        while (true)
        {
            var mode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold #1f5fa8]选择同步模式[/]")
                    .PageSize(4)
                    .HighlightStyle(new Style(Color.White, Color.Blue))
                    .AddChoices(new[] {
                        "全供应商互同步",
                        "OpenAI 同步到全部",
                        "单目标迁移",
                        "退出"
                    }));

            if (mode == "退出") break;
            RenderModeHint(mode);

            var codexHome = AnsiConsole.Prompt(
                new TextPrompt<string>("[#1f5fa8]Codex Home[/] (默认读取 CODEX_HOME 或 ~/.codex):")
                    .DefaultValue(SyncEngine.DefaultCodexHome())
                    .AllowEmpty());
            if (string.IsNullOrWhiteSpace(codexHome)) codexHome = SyncEngine.DefaultCodexHome();
            codexHome = Path.GetFullPath(codexHome);

            var backupDir = AnsiConsole.Prompt(
                new TextPrompt<string>("[#1f5fa8]备份目录[/] (可选，写入前备份 SQLite):")
                    .DefaultValue(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "codex-session-sync-backup"))
                    .AllowEmpty());
            if (string.IsNullOrWhiteSpace(backupDir)) backupDir = null;
            else backupDir = Path.GetFullPath(backupDir);

            var sourceProvider = "openai";
            var targetProvider = "openai";

            if (mode == "OpenAI 同步到全部")
            {
                sourceProvider = AnsiConsole.Prompt(
                    new TextPrompt<string>("[#1f5fa8]源 provider[/]:")
                        .DefaultValue("openai"));
            }

            if (mode == "单目标迁移")
            {
                targetProvider = AnsiConsole.Prompt(
                    new TextPrompt<string>("[#1f5fa8]目标 provider[/]:")
                        .DefaultValue("openai"));
            }

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold #1f5fa8]操作[/]")
                    .HighlightStyle(new Style(Color.White, Color.Blue))
                    .AddChoices(new[] { "预览", "执行写入" }));

            var apply = action == "执行写入";
            if (apply)
            {
                var confirmed = AnsiConsole.Confirm("我已备份或确认可以写入?", false);
                if (!confirmed)
                {
                    AnsiConsole.MarkupLine("[yellow]未确认，返回主菜单。[/]");
                    continue;
                }
            }

            RenderRunSummary(mode, codexHome, backupDir, sourceProvider, targetProvider, apply);
            RunSyncAndShow(mode, codexHome, backupDir, sourceProvider, targetProvider, apply);

            if (!apply)
            {
                AnsiConsole.WriteLine();
                var previewNextAction = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold #1f5fa8]预览完成，接下来[/]")
                        .HighlightStyle(new Style(Color.White, Color.Blue))
                        .AddChoices(new[] { "使用相同参数执行写入", "修改参数", "退出" }));

                if (previewNextAction == "使用相同参数执行写入")
                {
                    var confirmed = AnsiConsole.Confirm("我已备份或确认可以写入?", false);
                    if (confirmed)
                    {
                        RenderRunSummary(mode, codexHome, backupDir, sourceProvider, targetProvider, apply: true);
                        RunSyncAndShow(mode, codexHome, backupDir, sourceProvider, targetProvider, apply: true);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]未确认，返回主菜单。[/]");
                        continue;
                    }
                }
                else if (previewNextAction == "修改参数")
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            AnsiConsole.WriteLine();
            var nextAction = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold #1f5fa8]接下来[/]")
                    .HighlightStyle(new Style(Color.White, Color.Blue))
                    .AddChoices(new[] { "继续操作", "退出" }));
            if (nextAction == "退出") break;
        }
    }

    static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    static string ParseMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "mutual" => "全供应商互同步",
            "openai" => "OpenAI 同步到全部",
            "migrate" => "单目标迁移",
            _ => "全供应商互同步"
        };
    }

    static void RenderIntro()
    {
        var grid = new Grid()
            .AddColumn()
            .AddColumn();
        grid.AddRow(
            new Markup("[bold #101828]Codex Session Sync[/]\n[grey]同步 JSONL 会话文件和 state_*.sqlite 索引[/]"),
            new Panel("[#1f5fa8]PREVIEW FIRST[/]\n[grey]默认预览，不写入[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(Color.Blue)));

        AnsiConsole.Write(new Panel(grid)
            .Header("会话同步控制台")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.SteelBlue)));
        AnsiConsole.WriteLine();
    }

    static void RenderModeHint(string mode)
    {
        var text = mode switch
        {
            "全供应商互同步" => "自动发现 provider，并为每个 provider 补齐镜像会话。",
            "OpenAI 同步到全部" => "只从源 provider 读取会话，并为其它 provider 创建镜像。",
            "单目标迁移" => "把非保留 provider 的历史会话迁移到指定目标 provider。",
            _ => ""
        };
        AnsiConsole.Write(new Panel(text)
            .Header(mode)
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Grey)));
    }

    static void RenderRunSummary(string mode, string codexHome, string? backupDir, string sourceProvider, string targetProvider, bool apply)
    {
        var table = new Table()
            .NoBorder()
            .AddColumn("项")
            .AddColumn("值");
        table.AddRow("模式", mode);
        table.AddRow("操作", apply ? "[red]执行写入[/]" : "[blue]预览[/]");
        table.AddRow("Codex Home", Markup.Escape(codexHome));
        table.AddRow("备份目录", backupDir == null ? "[grey]<未设置>[/]" : Markup.Escape(backupDir));
        table.AddRow("源 provider", Markup.Escape(sourceProvider));
        table.AddRow("目标 provider", Markup.Escape(targetProvider));

        AnsiConsole.Write(new Panel(table)
            .Header("本次运行")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(apply ? Color.Red : Color.Blue)));
    }

    static void ShowHelp()
    {
        AnsiConsole.Write(new Panel(new Text("""
Usage:
  CodexSessionSync.Tui.exe
  CodexSessionSync.Tui.exe --preview [--mode mutual|openai|migrate]
  CodexSessionSync.Tui.exe --apply --backup-dir <path> [--mode mutual|openai|migrate]

Options:
  --codex-home <path>   指定 Codex Home，默认读取 CODEX_HOME 或 ~/.codex
  --backup-dir <path>   写入前的备份目录
  --source <provider>   OpenAI 同步到全部模式的源 provider，默认 openai
  --target <provider>   单目标迁移模式的目标 provider，默认 openai
"""))
            .Header("Codex Session Sync")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.SteelBlue)));
    }

    static void RunSyncAndShow(string mode, string codexHome, string? backupDir, string sourceProvider, string targetProvider, bool apply)
    {
        if (apply && backupDir != null)
            Directory.CreateDirectory(backupDir);

        AnsiConsole.Status()
            .Start(apply ? "正在执行写入..." : "正在预览...", ctx =>
            {
                try
                {
                    RunSync(mode, codexHome, backupDir, sourceProvider, targetProvider, apply);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                    AnsiConsole.MarkupLine($"[dim]{ex.StackTrace}[/]");
                }
            });
    }

    static void RunSync(string mode, string codexHome, string? backupDir, string sourceProvider, string targetProvider, bool apply)
    {
        var config = SyncEngine.InspectConfig(Path.Combine(codexHome, "config.toml"), targetProvider);
        var stateDb = SyncEngine.ResolveStateDb(codexHome, config, null);
        var providers = SyncEngine.ResolveConfiguredProviders(config);
        var sb = new StringBuilder();

        if (mode == "全供应商互同步")
        {
            var report = new SyncReport { SourceProvider = "*", TargetProviders = providers };
            var sessions = SyncEngine.FindMutualSourceSessions(codexHome, providers, report, out var idMap, out var allProviders);
            report.TargetProviders = allProviders;
            var plans = SyncEngine.BuildMutualMirrorPlans(sessions, allProviders, idMap);

            if (apply && backupDir != null && stateDb != null)
                SyncEngine.BackupSqlite(stateDb, backupDir);

            SyncEngine.SyncRolloutMirrors(plans, apply, report);
            var sqliteReport = SyncEngine.SyncSqliteMirrors(stateDb, plans, apply, report);

            RenderMutualReport(sb, codexHome, backupDir, config, allProviders, report, sqliteReport, apply);
        }
        else if (mode == "OpenAI 同步到全部")
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

            RenderOpenAiReport(sb, codexHome, backupDir, sourceProvider, targetProviders, report, sqliteReport, apply);
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

            RenderMigrateReport(sb, codexHome, backupDir, targetProvider, keepProviders, filesScanned, filesNeedingUpdate, filesUpdated, sessionMetaRewritten, beforeCounts, afterCounts, sqliteReport, apply);
        }

        AnsiConsole.Write(new Panel(new Text(sb.ToString()))
            .Header($"{(apply ? "执行结果" : "预览结果")}")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(apply ? Color.Green : Color.Blue)));
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

    static void RenderMutualReport(StringBuilder sb, string codexHome, string? backupDir, ConfigStatus config, List<string> providers, SyncReport report, SqliteReport sqliteReport, bool apply)
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

    static void RenderOpenAiReport(StringBuilder sb, string codexHome, string? backupDir, string sourceProvider, List<string> targetProviders, SyncReport report, SqliteReport sqliteReport, bool apply)
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

    static void RenderMigrateReport(StringBuilder sb, string codexHome, string? backupDir, string targetProvider, HashSet<string> keepProviders, int filesScanned, int filesNeedingUpdate, int filesUpdated, int sessionMetaRewritten, Dictionary<string, int> beforeCounts, Dictionary<string, int> afterCounts, SqliteReport sqliteReport, bool apply)
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
