using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodexSessionSync.Core;
using Microsoft.Data.Sqlite;

namespace CodexSessionSync.Desktop.Shared;

public static class SyncUiRunner
{
    public static Task<string> RunAsync(SyncRunOptions options, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Run(options, cancellationToken), cancellationToken);
    }

    private static string Run(SyncRunOptions options, CancellationToken cancellationToken)
    {
        if (options.Apply && options.BackupDir != null)
            Directory.CreateDirectory(options.BackupDir);

        var sb = new StringBuilder();
        var config = SyncEngine.InspectConfig(Path.Combine(options.CodexHome, "config.toml"), options.TargetProvider);
        var stateDb = SyncEngine.ResolveStateDb(options.CodexHome, config, null);
        var providers = SyncEngine.ResolveConfiguredProviders(config);

        switch (options.Mode)
        {
            case SyncMode.Mutual:
                AppendMutualReport(sb, options, config, stateDb, providers, cancellationToken);
                break;
            case SyncMode.OpenAiToAll:
                AppendOpenAiReport(sb, options, stateDb, providers, cancellationToken);
                break;
            case SyncMode.MigrateToTarget:
                AppendMigrateReport(sb, options, config, stateDb, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unknown sync mode: {options.Mode}");
        }

        return sb.ToString();
    }

    private static void AppendMutualReport(
        StringBuilder sb,
        SyncRunOptions options,
        ConfigStatus config,
        string? stateDb,
        List<string> providers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var report = new SyncReport { SourceProvider = "*", TargetProviders = providers };
        var sessions = SyncEngine.FindMutualSourceSessions(options.CodexHome, providers, report, out var idMap, out var allProviders);
        report.TargetProviders = allProviders;
        var plans = SyncEngine.BuildMutualMirrorPlans(sessions, allProviders, idMap);

        if (options.Apply && options.BackupDir != null && stateDb != null)
            SyncEngine.BackupSqlite(stateDb, options.BackupDir);

        SyncEngine.SyncRolloutMirrors(plans, options.Apply, report);
        var sqliteReport = SyncEngine.SyncSqliteMirrors(stateDb, plans, options.Apply, report);

        sb.AppendLine($"Mode: mutual provider session sync / {(options.Apply ? "apply" : "preview")}");
        sb.AppendLine($"Codex Home: {options.CodexHome}");
        sb.AppendLine($"Providers: {string.Join(", ", allProviders)}");
        if (options.BackupDir != null) sb.AppendLine($"Backup dir: {options.BackupDir}");
        sb.AppendLine();
        sb.AppendLine("Config");
        sb.AppendLine($"- config.toml: {config.Path}");
        sb.AppendLine($"- active model_provider: {config.ActiveProvider ?? "<missing>"}");
        sb.AppendLine($"- configured providers: {(config.ProviderKeys.Count > 0 ? string.Join(", ", config.ProviderKeys) : "<none>")}");
        sb.AppendLine();
        AppendMirrorReport(sb, report);
        AppendSqliteMirrorReport(sb, sqliteReport, report);

        if (report.TargetProviders.Count == 0)
            sb.AppendLine("No configured providers found. Define model_providers in config.toml first.");
        if (report.MirrorFileConflicts > 0 || report.SqliteRowsConflicting > 0)
            sb.AppendLine("Conflicts were skipped. Existing files/rows were not overwritten.");
        if (!options.Apply)
            sb.AppendLine("Preview only. Check confirm box and click apply to write changes.");
    }

    private static void AppendOpenAiReport(
        StringBuilder sb,
        SyncRunOptions options,
        string? stateDb,
        List<string> providers,
        CancellationToken cancellationToken)
    {
        var targetProviders = providers.Where(p => p != options.SourceProvider).ToList();
        var report = new SyncReport { SourceProvider = options.SourceProvider, TargetProviders = targetProviders };

        var sessions = new List<SourceSession>();
        foreach (var path in SyncEngine.IterRolloutFiles(options.CodexHome))
        {
            cancellationToken.ThrowIfCancellationRequested();

            report.FilesScanned++;
            var (meta, _) = SyncEngine.GetSessionMeta(path);
            if (meta == null) continue;
            var provider = meta.Provider;
            var providerKey = provider ?? "<missing>";
            report.ProviderCountsBefore[providerKey] = report.ProviderCountsBefore.GetValueOrDefault(providerKey) + 1;
            report.ProviderCountsAfter[providerKey] = report.ProviderCountsAfter.GetValueOrDefault(providerKey) + 1;
            var id = meta.Id;
            if (provider != options.SourceProvider || string.IsNullOrWhiteSpace(id) || SyncEngine.IsGeneratedMirrorSession(meta))
                continue;
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
                if (!seen.Add(mirrorId)) continue;
                var mirrorPath = SyncEngine.ComputeMirrorPath(session.Path, session.ThreadId, mirrorId, provider);
                plans.Add(new MirrorPlan(session.Path, session.ThreadId, provider, mirrorId, mirrorPath));
            }
        }

        if (options.Apply && options.BackupDir != null && stateDb != null)
            SyncEngine.BackupSqlite(stateDb, options.BackupDir);

        SyncEngine.SyncRolloutMirrors(plans, options.Apply, report);
        var sqliteReport = SyncEngine.SyncSqliteMirrors(stateDb, plans, options.Apply, report);

        sb.AppendLine($"Mode: openai sync to all providers / {(options.Apply ? "apply" : "preview")}");
        sb.AppendLine($"Codex Home: {options.CodexHome}");
        sb.AppendLine($"Source provider: {options.SourceProvider}");
        sb.AppendLine($"Target providers: {(targetProviders.Count > 0 ? string.Join(", ", targetProviders) : "<none>")}");
        if (options.BackupDir != null) sb.AppendLine($"Backup dir: {options.BackupDir}");
        sb.AppendLine();
        AppendMirrorReport(sb, report);
        AppendSqliteMirrorReport(sb, sqliteReport, report);

        if (targetProviders.Count == 0)
            sb.AppendLine("No target providers found.");
        if (report.MirrorFileConflicts > 0 || report.SqliteRowsConflicting > 0)
            sb.AppendLine("Conflicts were skipped. Existing files/rows were not overwritten.");
        if (!options.Apply)
            sb.AppendLine("Preview only. Check confirm box and click apply to write changes.");
    }

    private static void AppendMigrateReport(
        StringBuilder sb,
        SyncRunOptions options,
        ConfigStatus config,
        string? stateDb,
        CancellationToken cancellationToken)
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
            cancellationToken.ThrowIfCancellationRequested();

            filesScanned++;
            var lines = File.ReadAllLines(path);
            var rewritten = new List<string>();
            var changed = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine;
                if (string.IsNullOrWhiteSpace(line))
                {
                    rewritten.Add(line);
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var typeElement)
                        && typeElement.GetString() == "session_meta"
                        && root.TryGetProperty("payload", out var payload))
                    {
                        var provider = payload.TryGetProperty("model_provider", out var providerElement)
                            ? providerElement.GetString()
                            : null;
                        var providerKey = provider ?? "<missing>";
                        beforeCounts[providerKey] = beforeCounts.GetValueOrDefault(providerKey) + 1;

                        if (!string.IsNullOrWhiteSpace(provider) && !keepProviders.Contains(provider))
                        {
                            var node = JsonNode.Parse(line);
                            if (node is JsonObject obj
                                && obj.TryGetPropertyValue("payload", out var payloadNode)
                                && payloadNode is JsonObject payloadObj)
                            {
                                payloadObj["model_provider"] = options.TargetProvider;
                                rewritten.Add(obj.ToJsonString());
                                sessionMetaRewritten++;
                                changed = true;
                                afterCounts[options.TargetProvider] = afterCounts.GetValueOrDefault(options.TargetProvider) + 1;
                                continue;
                            }
                        }

                        afterCounts[providerKey] = afterCounts.GetValueOrDefault(providerKey) + 1;
                    }
                }
                catch
                {
                    // Ignore non-JSON lines and keep them unchanged.
                }

                rewritten.Add(line);
            }

            if (!changed) continue;
            filesNeedingUpdate++;
            if (options.Apply)
            {
                if (options.BackupDir != null)
                    SyncEngine.BackupFile(path, options.CodexHome, options.BackupDir);
                File.WriteAllLines(path, rewritten, new UTF8Encoding(false));
                filesUpdated++;
            }
        }

        var sqliteReport = BuildMigrationSqliteReport(options, stateDb, keepProviders);

        sb.AppendLine($"Mode: single-target migration / {(options.Apply ? "apply" : "preview")}");
        sb.AppendLine($"Codex Home: {options.CodexHome}");
        sb.AppendLine($"Target provider: {options.TargetProvider}");
        sb.AppendLine($"Keep providers: {string.Join(", ", keepProviders)}");
        if (options.BackupDir != null) sb.AppendLine($"Backup dir: {options.BackupDir}");
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
        if (!options.Apply)
            sb.AppendLine("Preview only. Check confirm box and click apply to write changes.");
    }

    private static SqliteReport BuildMigrationSqliteReport(SyncRunOptions options, string? stateDb, HashSet<string> keepProviders)
    {
        var sqliteReport = new SqliteReport { Path = stateDb };
        if (stateDb == null || !File.Exists(stateDb))
            return sqliteReport;

        using var conn = new SqliteConnection($"Data Source={stateDb}");
        conn.Open();
        sqliteReport.ProviderCountsBefore = SyncEngine.FetchProviderCounts(conn);
        var placeholders = string.Join(", ", keepProviders.Select(_ => "?"));
        var filterSql = $"model_provider IS NOT NULL AND model_provider NOT IN ({placeholders})";
        var filterParams = keepProviders.OrderBy(p => p).ToList();

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM threads WHERE {filterSql}";
        foreach (var provider in filterParams)
            countCmd.Parameters.AddWithValue(null, provider);
        sqliteReport.RowsNeedingUpdate = Convert.ToInt32(countCmd.ExecuteScalar());

        if (options.Apply && sqliteReport.RowsNeedingUpdate > 0)
        {
            if (options.BackupDir != null)
                SyncEngine.BackupSqlite(stateDb, options.BackupDir);

            using var tx = conn.BeginTransaction();
            using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = $"UPDATE threads SET model_provider = ? WHERE {filterSql}";
            updateCmd.Parameters.AddWithValue(null, options.TargetProvider);
            foreach (var provider in filterParams)
                updateCmd.Parameters.AddWithValue(null, provider);
            sqliteReport.RowsUpdated = updateCmd.ExecuteNonQuery();
            tx.Commit();
            conn.ExecuteNonQuery("PRAGMA wal_checkpoint(TRUNCATE)");
        }

        sqliteReport.ProviderCountsAfter = options.Apply
            ? SyncEngine.FetchProviderCounts(conn)
            : SimulateMigrateCounts(sqliteReport.ProviderCountsBefore, options.TargetProvider, keepProviders);

        return sqliteReport;
    }

    private static void AppendMirrorReport(StringBuilder sb, SyncReport report)
    {
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
    }

    private static void AppendSqliteMirrorReport(StringBuilder sb, SqliteReport sqliteReport, SyncReport report)
    {
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
    }

    private static List<string> FormatCounts(Dictionary<string, int> counts)
    {
        var items = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
        if (items.Count == 0) return new List<string> { "- none" };
        return items.Select(kv => $"- {kv.Key}: {kv.Value}").ToList();
    }

    private static List<string> FormatCounts(List<(string? Provider, int Count)> counts)
    {
        var merged = new Dictionary<string, int>();
        foreach (var (provider, count) in counts)
        {
            var label = provider ?? "<missing>";
            merged[label] = merged.GetValueOrDefault(label) + count;
        }

        var items = merged.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
        if (items.Count == 0) return new List<string> { "- none" };
        return items.Select(kv => $"- {kv.Key}: {kv.Value}").ToList();
    }

    private static List<(string?, int)> SimulateMigrateCounts(List<(string?, int)> before, string target, HashSet<string> keep)
    {
        var dict = new Dictionary<string, int>();
        foreach (var (provider, count) in before)
        {
            var final = provider == null || keep.Contains(provider) ? provider : target;
            var key = final ?? "";
            dict[key] = dict.GetValueOrDefault(key) + count;
        }

        return dict
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => ((string?)kv.Key, kv.Value))
            .ToList();
    }
}
