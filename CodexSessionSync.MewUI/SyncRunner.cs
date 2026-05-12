using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodexSessionSync.Core;
using Microsoft.Data.Sqlite;

namespace CodexSessionSync.MewUI;

internal static class SyncRunner
{
    public static string Run(SyncOptions options)
    {
        if (options.Apply && options.BackupDir != null)
        {
            Directory.CreateDirectory(options.BackupDir);
        }

        var config = SyncEngine.InspectConfig(Path.Combine(options.CodexHome, "config.toml"), options.TargetProvider);
        var stateDb = SyncEngine.ResolveStateDb(options.CodexHome, config, null);
        var providers = SyncEngine.ResolveConfiguredProviders(config);
        var sb = new StringBuilder();

        switch (options.Mode)
        {
            case SyncMode.Mutual:
                RunMutual(options, config, stateDb, providers, sb);
                break;
            case SyncMode.OpenAi:
                RunOpenAi(options, stateDb, providers, sb);
                break;
            case SyncMode.Migrate:
                RunMigrate(options, stateDb, sb);
                break;
        }

        return sb.ToString();
    }

    private static void RunMutual(SyncOptions options, ConfigStatus config, string? stateDb, List<string> providers, StringBuilder sb)
    {
        var report = new SyncReport { SourceProvider = "*", TargetProviders = providers };
        var sessions = SyncEngine.FindMutualSourceSessions(options.CodexHome, providers, report, out var idMap, out var allProviders);
        report.TargetProviders = allProviders;
        var plans = SyncEngine.BuildMutualMirrorPlans(sessions, allProviders, idMap);

        if (options.Apply && options.BackupDir != null && stateDb != null)
        {
            SyncEngine.BackupSqlite(stateDb, options.BackupDir);
        }

        SyncEngine.SyncRolloutMirrors(plans, options.Apply, report);
        var sqliteReport = SyncEngine.SyncSqliteMirrors(stateDb, plans, options.Apply, report);

        AppendMutualReport(sb, options.CodexHome, options.BackupDir, config, allProviders, report, sqliteReport, options.Apply);
    }

    private static void RunOpenAi(SyncOptions options, string? stateDb, List<string> providers, StringBuilder sb)
    {
        var targetProviders = providers.Where(p => p != options.SourceProvider).ToList();
        var report = new SyncReport { SourceProvider = options.SourceProvider, TargetProviders = targetProviders };
        var sessions = new List<SourceSession>();

        foreach (var path in SyncEngine.IterRolloutFiles(options.CodexHome))
        {
            report.FilesScanned++;
            var (meta, _) = SyncEngine.GetSessionMeta(path);
            if (meta == null)
            {
                continue;
            }

            var provider = meta.Provider;
            var providerKey = provider ?? "<missing>";
            report.ProviderCountsBefore[providerKey] = report.ProviderCountsBefore.GetValueOrDefault(providerKey) + 1;
            report.ProviderCountsAfter[providerKey] = report.ProviderCountsAfter.GetValueOrDefault(providerKey) + 1;

            var id = meta.Id;
            if (provider != options.SourceProvider || string.IsNullOrWhiteSpace(id) || SyncEngine.IsGeneratedMirrorSession(meta))
            {
                continue;
            }

            sessions.Add(new SourceSession(path, id, options.SourceProvider));
        }

        report.SourceSessionsFound = sessions.Count;

        var plans = new List<MirrorPlan>();
        var seen = new HashSet<string>();
        foreach (var session in sessions)
        {
            foreach (var provider in targetProviders)
            {
                var mirrorId = UuidV5.Create(UuidV5.SyncNamespace, $"{session.ThreadId}:{provider}").ToString();
                if (!seen.Add(mirrorId))
                {
                    continue;
                }

                var mirrorPath = SyncEngine.ComputeMirrorPath(session.Path, session.ThreadId, mirrorId, provider);
                plans.Add(new MirrorPlan(session.Path, session.ThreadId, provider, mirrorId, mirrorPath));
            }
        }

        if (options.Apply && options.BackupDir != null && stateDb != null)
        {
            SyncEngine.BackupSqlite(stateDb, options.BackupDir);
        }

        SyncEngine.SyncRolloutMirrors(plans, options.Apply, report);
        var sqliteReport = SyncEngine.SyncSqliteMirrors(stateDb, plans, options.Apply, report);

        AppendOpenAiReport(sb, options.CodexHome, options.BackupDir, options.SourceProvider, targetProviders, report, sqliteReport, options.Apply);
    }

    private static void RunMigrate(SyncOptions options, string? stateDb, StringBuilder sb)
    {
        var keepProviders = new HashSet<string> { "openai", options.TargetProvider };
        var filesScanned = 0;
        var filesNeedingUpdate = 0;
        var filesUpdated = 0;
        var sessionMetaRewritten = 0;
        var beforeCounts = new Dictionary<string, int>();
        var afterCounts = new Dictionary<string, int>();

        foreach (var path in SyncEngine.IterRolloutFiles(options.CodexHome))
        {
            filesScanned++;
            var lines = File.ReadAllLines(path);
            var rewritten = new List<string>();
            var changed = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    rewritten.Add(line);
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var t)
                        && t.GetString() == "session_meta"
                        && root.TryGetProperty("payload", out var payload))
                    {
                        var provider = payload.TryGetProperty("model_provider", out var p) ? p.GetString() : null;
                        var pk = provider ?? "<missing>";
                        beforeCounts[pk] = beforeCounts.GetValueOrDefault(pk) + 1;

                        if (!string.IsNullOrWhiteSpace(provider) && !keepProviders.Contains(provider))
                        {
                            var node = JsonNode.Parse(line);
                            if (node is JsonObject obj
                                && obj.TryGetPropertyValue("payload", out var pnode)
                                && pnode is JsonObject po)
                            {
                                po["model_provider"] = options.TargetProvider;
                                rewritten.Add(obj.ToJsonString());
                                sessionMetaRewritten++;
                                changed = true;
                                afterCounts[options.TargetProvider] = afterCounts.GetValueOrDefault(options.TargetProvider) + 1;
                                continue;
                            }
                        }

                        afterCounts[pk] = afterCounts.GetValueOrDefault(pk) + 1;
                    }
                }
                catch
                {
                    // Ignore malformed JSONL rows and preserve them unchanged.
                }

                rewritten.Add(line);
            }

            if (!changed)
            {
                continue;
            }

            filesNeedingUpdate++;
            if (options.Apply)
            {
                if (options.BackupDir != null)
                {
                    SyncEngine.BackupFile(path, options.CodexHome, options.BackupDir);
                }

                File.WriteAllLines(path, rewritten, new UTF8Encoding(false));
                filesUpdated++;
            }
        }

        var sqliteReport = new SqliteReport { Path = stateDb };
        if (stateDb != null && File.Exists(stateDb))
        {
            using var conn = new SqliteConnection($"Data Source={stateDb}");
            conn.Open();
            sqliteReport.ProviderCountsBefore = SyncEngine.FetchProviderCounts(conn);
            var placeholders = string.Join(", ", keepProviders.Select(_ => "?"));
            var filterSql = $"model_provider IS NOT NULL AND model_provider NOT IN ({placeholders})";
            var filterParams = keepProviders.OrderBy(p => p).ToList();

            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM threads WHERE {filterSql}";
            foreach (var provider in filterParams)
            {
                countCmd.Parameters.AddWithValue(null, provider);
            }

            sqliteReport.RowsNeedingUpdate = Convert.ToInt32(countCmd.ExecuteScalar());

            if (options.Apply && sqliteReport.RowsNeedingUpdate > 0)
            {
                if (options.BackupDir != null)
                {
                    SyncEngine.BackupSqlite(stateDb, options.BackupDir);
                }

                using var tx = conn.BeginTransaction();
                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = $"UPDATE threads SET model_provider = ? WHERE {filterSql}";
                updateCmd.Parameters.AddWithValue(null, options.TargetProvider);
                foreach (var provider in filterParams)
                {
                    updateCmd.Parameters.AddWithValue(null, provider);
                }

                sqliteReport.RowsUpdated = updateCmd.ExecuteNonQuery();
                tx.Commit();
                conn.ExecuteNonQuery("PRAGMA wal_checkpoint(TRUNCATE)");
            }

            sqliteReport.ProviderCountsAfter = options.Apply
                ? SyncEngine.FetchProviderCounts(conn)
                : SimulateMigrateCounts(sqliteReport.ProviderCountsBefore, options.TargetProvider, keepProviders);
        }

        AppendMigrateReport(
            sb,
            options.CodexHome,
            options.BackupDir,
            options.TargetProvider,
            keepProviders,
            filesScanned,
            filesNeedingUpdate,
            filesUpdated,
            sessionMetaRewritten,
            beforeCounts,
            afterCounts,
            sqliteReport,
            options.Apply);
    }

    private static List<string> FormatCounts(Dictionary<string, int> counts)
    {
        var items = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
        return items.Count == 0 ? ["- none"] : items.Select(kv => $"- {kv.Key}: {kv.Value}").ToList();
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
        return items.Count == 0 ? ["- none"] : items.Select(kv => $"- {kv.Key}: {kv.Value}").ToList();
    }

    private static List<(string?, int)> SimulateMigrateCounts(List<(string?, int)> before, string target, HashSet<string> keep)
    {
        var dict = new Dictionary<string, int>();
        foreach (var (prov, cnt) in before)
        {
            var final = prov == null || keep.Contains(prov) ? prov : target;
            var key = final ?? "";
            dict[key] = dict.GetValueOrDefault(key) + cnt;
        }

        return dict
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => ((string?)kv.Key, kv.Value))
            .ToList();
    }

    private static void AppendMutualReport(StringBuilder sb, string codexHome, string? backupDir, ConfigStatus config, List<string> providers, SyncReport report, SqliteReport sqliteReport, bool apply)
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

    private static void AppendOpenAiReport(StringBuilder sb, string codexHome, string? backupDir, string sourceProvider, List<string> targetProviders, SyncReport report, SqliteReport sqliteReport, bool apply)
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

    private static void AppendMigrateReport(StringBuilder sb, string codexHome, string? backupDir, string targetProvider, HashSet<string> keepProviders, int filesScanned, int filesNeedingUpdate, int filesUpdated, int sessionMetaRewritten, Dictionary<string, int> beforeCounts, Dictionary<string, int> afterCounts, SqliteReport sqliteReport, bool apply)
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
