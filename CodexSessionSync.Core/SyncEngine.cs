using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace CodexSessionSync.Core;

public class SyncEngine
{
    private static readonly Regex StateDbPattern = new(@"^state_(\d+)\.sqlite$", RegexOptions.Compiled);
    private static readonly Regex SanitizePattern = new(@"[^A-Za-z0-9._-]+", RegexOptions.Compiled);
    private static readonly Regex InputImagePattern = new("\"type\"\\s*:\\s*\"input_image\"", RegexOptions.Compiled);
    private const int TimiCcImageRiskBytes = 700 * 1024;

    public static string DefaultCodexHome()
    {
        var env = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    }

    public static ConfigStatus InspectConfig(string configPath, string targetProvider)
    {
        var activeProvider = (string?)null;
        var providerKeys = new List<string>();
        var targetDefined = false;
        var sqliteHome = (string?)null;

        if (File.Exists(configPath))
        {
            var text = File.ReadAllText(configPath);
            if (text.StartsWith("\ufeff")) text = text[1..];
            var data = TomlParser.Parse(text);

            if (data.TryGetValue("model_provider", out var mp) && mp is string s1)
                activeProvider = s1.Trim();

            if (data.TryGetValue("model_providers", out var mps) && mps is Dictionary<string, object?> dict)
            {
                foreach (var key in dict.Keys.Where(k => !string.IsNullOrWhiteSpace(k)).OrderBy(k => k))
                {
                    providerKeys.Add(key.Trim());
                }
                targetDefined = dict.ContainsKey(targetProvider);
            }

            if (data.TryGetValue("sqlite_home", out var sh) && sh is string s2 && !string.IsNullOrWhiteSpace(s2))
                sqliteHome = s2;
        }

        return new ConfigStatus(configPath, activeProvider, providerKeys, targetDefined, sqliteHome);
    }

    public static string? ResolveStateDb(string codexHome, ConfigStatus config, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        var envRaw = Environment.GetEnvironmentVariable("CODEX_SQLITE_HOME");
        var envPath = string.IsNullOrWhiteSpace(envRaw) ? null : Path.GetFullPath(envRaw.Trim());

        var roots = new List<string>();
        void AddRoot(string? r) { if (!string.IsNullOrWhiteSpace(r) && !roots.Contains(r!)) roots.Add(r!); }

        AddRoot(config.SqliteHome);
        AddRoot(envPath);
        AddRoot(codexHome);
        AddRoot(Path.Combine(codexHome, "sqlite"));

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            var candidates = new List<(int version, DateTime mtime, string path)>();
            foreach (var f in Directory.EnumerateFiles(root, "state_*.sqlite"))
            {
                var m = StateDbPattern.Match(Path.GetFileName(f));
                if (m.Success)
                    candidates.Add((int.Parse(m.Groups[1].Value), File.GetLastWriteTime(f), f));
            }
            if (candidates.Count > 0)
            {
                candidates.Sort((a, b) =>
                {
                    var cmp = b.version.CompareTo(a.version);
                    return cmp != 0 ? cmp : b.mtime.CompareTo(a.mtime);
                });
                return candidates[0].path;
            }
        }
        return null;
    }

    public static List<string> ResolveConfiguredProviders(ConfigStatus config)
    {
        var providers = new HashSet<string>(config.ProviderKeys);
        if (!string.IsNullOrWhiteSpace(config.ActiveProvider))
            providers.Add(config.ActiveProvider);
        providers.Add("openai");
        return providers.Where(p => !string.IsNullOrWhiteSpace(p)).OrderBy(p => p).ToList();
    }

    public static List<string> IterRolloutFiles(string codexHome)
    {
        var result = new List<string>();
        foreach (var sub in new[] { "sessions", "archived_sessions" })
        {
            var root = Path.Combine(codexHome, sub);
            if (!Directory.Exists(root)) continue;
            result.AddRange(Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories).OrderBy(p => p));
        }
        return result;
    }

    public static (SessionMetaInfo? meta, int lineNumber) GetSessionMeta(string path)
    {
        string text;
        try
        {
            text = ReadAllTextShared(path);
        }
        catch (IOException) { return (null, 0); }
        catch (UnauthorizedAccessException) { return (null, 0); }
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "session_meta")
                {
                    if (doc.RootElement.TryGetProperty("payload", out var payload))
                    {
                        var id = payload.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        var provider = payload.TryGetProperty("model_provider", out var provEl) ? provEl.GetString() : null;
                        var forked = payload.TryGetProperty("forked_from_id", out var forkEl) ? forkEl.GetString() : null;
                        return (new SessionMetaInfo(id, provider, forked), i + 1);
                    }
                }
            }
            catch { /* ignore */ }
        }
        return (null, 0);
    }

    public static bool IsGeneratedMirrorSession(SessionMetaInfo meta)
    {
        if (string.IsNullOrWhiteSpace(meta.Id) || string.IsNullOrWhiteSpace(meta.Provider) || string.IsNullOrWhiteSpace(meta.ForkedFromId))
            return false;
        return meta.Id == UuidV5.Create(UuidV5.SyncNamespace, $"{meta.ForkedFromId}:{meta.Provider}").ToString();
    }

    public static List<SourceSession> FindMutualSourceSessions(string codexHome, List<string> configuredProviders, SyncReport report, out Dictionary<string, (string Provider, string Path)> idMap, out List<string> allProviders)
    {
        var configuredSet = new HashSet<string>(configuredProviders);
        var discoveredProviders = new HashSet<string>(configuredProviders);
        var allMeta = new List<(string Path, string? Id, string? Provider, string? ForkedFromId)>();
        idMap = new Dictionary<string, (string, string)>();
        foreach (var path in IterRolloutFiles(codexHome))
        {
            report.FilesScanned++;
            var (meta, _) = GetSessionMeta(path);
            if (meta == null) continue;
            var provider = meta.Provider;
            var providerKey = provider ?? "<missing>";
            report.ProviderCountsBefore[providerKey] = report.ProviderCountsBefore.GetValueOrDefault(providerKey) + 1;
            report.ProviderCountsAfter[providerKey] = report.ProviderCountsAfter.GetValueOrDefault(providerKey) + 1;
            var id = meta.Id;
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(provider))
            {
                idMap[id] = (provider, path);
                discoveredProviders.Add(provider);
            }
            allMeta.Add((path, id, provider, meta.ForkedFromId));
        }
        allProviders = discoveredProviders.Where(p => !string.IsNullOrWhiteSpace(p)).OrderBy(p => p).ToList();
        var allSet = new HashSet<string>(allProviders);

        var sessions = new List<SourceSession>();
        foreach (var (path, id, provider, forkedFromId) in allMeta)
        {
            if (string.IsNullOrWhiteSpace(provider) || !allSet.Contains(provider) || string.IsNullOrWhiteSpace(id))
                continue;
            sessions.Add(new SourceSession(path, id, provider, forkedFromId));
        }
        report.SourceSessionsFound = sessions.Count;
        return sessions;
    }

    public static List<MirrorPlan> BuildMutualMirrorPlans(List<SourceSession> sessions, List<string> providers, Dictionary<string, (string Provider, string Path)> idMap)
    {
        var plans = new List<MirrorPlan>();
        var seen = new HashSet<string>();
        foreach (var session in sessions)
        {
            foreach (var provider in providers)
            {
                if (provider == session.Provider) continue;

                string mirrorId;
                string mirrorPath;

                if (session.ForkedFromId != null
                    && idMap.TryGetValue(session.ForkedFromId, out var forkInfo)
                    && forkInfo.Provider == provider)
                {
                    mirrorId = session.ForkedFromId;
                    mirrorPath = forkInfo.Path;
                }
                else
                {
                    mirrorId = UuidV5.Create(UuidV5.SyncNamespace, $"{session.ThreadId}:{provider}").ToString();
                    mirrorPath = ComputeMirrorPath(session.Path, session.ThreadId, mirrorId, provider);
                }

                if (!seen.Add(mirrorId)) continue;
                plans.Add(new MirrorPlan(session.Path, session.ThreadId, provider, mirrorId, mirrorPath));
            }
        }
        return plans;
    }

    public static string ComputeMirrorPath(string sourcePath, string sourceId, string mirrorId, string targetProvider)
    {
        var name = Path.GetFileName(sourcePath);
        if (name.Contains(sourceId))
            return Path.Combine(Path.GetDirectoryName(sourcePath)!, name.Replace(sourceId, mirrorId));
        var part = SanitizePattern.Replace(targetProvider, "-").Trim('.', '-');
        if (string.IsNullOrEmpty(part)) part = "provider";
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        return Path.Combine(Path.GetDirectoryName(sourcePath)!, $"{stem}--{part}-{mirrorId}{ext}");
    }

    public static string RenderMirrorJsonl(MirrorPlan plan)
    {
        var lines = ReadAllLinesShared(plan.SourcePath);
        var rendered = new List<string>();
        bool metaSeen = false;
        var options = new JsonSerializerOptions { WriteIndented = false };

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                rendered.Add(line);
                continue;
            }
            try
            {
                var node = JsonNode.Parse(line);
                if (node == null)
                {
                    rendered.Add(line);
                    continue;
                }

                var replaced = ReplaceIds(node, plan.SourceId, plan.MirrorId);
                if (replaced is JsonObject robj && robj.TryGetPropertyValue("type", out var tnode)
                    && tnode is JsonValue tval && tval.GetValue<string>() == "session_meta")
                {
                    if (robj.TryGetPropertyValue("payload", out var pnode) && pnode is JsonObject payload)
                    {
                        payload["id"] = plan.MirrorId;
                        payload["model_provider"] = plan.TargetProvider;
                        if (!payload.ContainsKey("forked_from_id"))
                            payload["forked_from_id"] = plan.SourceId;
                        metaSeen = true;
                    }
                }
                rendered.Add(replaced.ToJsonString(options));
            }
            catch
            {
                rendered.Add(line);
            }
        }

        if (!metaSeen)
            throw new InvalidOperationException($"No session_meta found in source: {plan.SourcePath}");

        var result = string.Join("\n", rendered);
        if (lines.Count > 0 && (lines[^1].EndsWith('\n') || lines[^1].EndsWith("\r\n"))) result += "\n";
        return result;
    }

    private static JsonNode ReplaceIds(JsonNode node, string sourceId, string mirrorId)
    {
        var idKeys = new HashSet<string> { "id", "thread_id", "session_id", "parent_thread_id", "child_thread_id" };
        return ReplaceIdsRecursive(node.DeepClone(), sourceId, mirrorId, idKeys);
    }

    private static JsonNode ReplaceIdsRecursive(JsonNode node, string sourceId, string mirrorId, HashSet<string> idKeys)
    {
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var prop in obj)
            {
                if (idKeys.Contains(prop.Key) && prop.Value is JsonValue v && v.TryGetValue(out string? s) && s == sourceId)
                    result[prop.Key] = mirrorId;
                else
                    result[prop.Key] = ReplaceIdsRecursive(prop.Value?.DeepClone()!, sourceId, mirrorId, idKeys);
            }
            return result;
        }
        if (node is JsonArray arr)
        {
            var result = new JsonArray();
            foreach (var item in arr)
                result.Add(ReplaceIdsRecursive(item?.DeepClone()!, sourceId, mirrorId, idKeys));
            return result;
        }
        return node;
    }

    public static void SyncRolloutMirrors(List<MirrorPlan> plans, bool apply, SyncReport report)
    {
        var emittedRiskWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in plans)
        {
            if (File.Exists(plan.MirrorPath))
            {
                if (!InspectExistingMirror(plan))
                {
                    report.MirrorFileConflicts++;
                    continue;
                }

                var rendered = RenderMirrorJsonl(plan);
                AppendMirrorRiskWarnings(plan, report, emittedRiskWarnings);
                var existingText = NormalizeLineEndings(ReadAllTextShared(plan.MirrorPath));
                var normRendered = NormalizeJsonlLines(rendered);
                var normExisting = NormalizeJsonlLines(existingText);
                if (normRendered == normExisting)
                {
                    report.MirrorFilesExisting++;
                    continue;
                }

                var srcLines = normRendered.Count(c => c == '\n');
                var mirLines = normExisting.Count(c => c == '\n');
                if (srcLines <= mirLines)
                {
                    report.MirrorFilesStale++;
                    continue;
                }

                report.MirrorFilesUpdated++;
                report.ProviderCountsAfter[plan.TargetProvider] = report.ProviderCountsAfter.GetValueOrDefault(plan.TargetProvider) + 1;
                if (!apply) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(plan.MirrorPath)!);
                File.WriteAllText(plan.MirrorPath, rendered, new UTF8Encoding(false));
                continue;
            }

            AppendMirrorRiskWarnings(plan, report, emittedRiskWarnings);
            report.MirrorFilesNeeded++;
            report.ProviderCountsAfter[plan.TargetProvider] = report.ProviderCountsAfter.GetValueOrDefault(plan.TargetProvider) + 1;
            if (!apply) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(plan.MirrorPath)!);
            File.WriteAllText(plan.MirrorPath, RenderMirrorJsonl(plan), new UTF8Encoding(false));
            report.MirrorFilesCreated++;
        }
    }

    private static void AppendMirrorRiskWarnings(MirrorPlan plan, SyncReport report, HashSet<string> emitted)
    {
        if (!string.Equals(plan.TargetProvider, "timi_cc", StringComparison.OrdinalIgnoreCase))
            return;

        var key = $"{plan.SourceId}:{plan.TargetProvider}";
        if (!emitted.Add(key))
            return;

        string sourceText;
        try
        {
            sourceText = ReadAllTextShared(plan.SourcePath);
        }
        catch
        {
            return;
        }

        var imageInputs = InputImagePattern.Matches(sourceText).Count;
        if (imageInputs == 0)
            return;

        var bytes = Encoding.UTF8.GetByteCount(sourceText);
        if (bytes < TimiCcImageRiskBytes)
            return;

        var sizeKb = (int)Math.Round(bytes / 1024.0);
        report.RiskWarnings.Add(
            $"- {Path.GetFileName(plan.SourcePath)} -> {plan.TargetProvider}: contains {imageInputs} image input(s) and about {sizeKb} KB of source JSONL. Low-cost timi_cc relay pools may reject image-heavy context with 502; official or higher-tier pools can behave differently."
        );
    }

    private static string ReadAllTextShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    private static List<string> ReadAllLinesShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs, Encoding.UTF8);
        var lines = new List<string>();
        string? line;
        while ((line = sr.ReadLine()) != null)
            lines.Add(line);
        return lines;
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static string NormalizeJsonlLines(string text)
    {
        var lines = new List<string>();
        var opts = new JsonSerializerOptions { WriteIndented = false };
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                var node = JsonNode.Parse(line);
                if (node is JsonObject obj && obj.TryGetPropertyValue("type", out var t) && t is JsonValue tv && tv.GetValue<string>() == "session_meta")
                    continue;
                lines.Add(node?.ToJsonString(opts) ?? line);
            }
            catch
            {
                lines.Add(line);
            }
        }
        return string.Join("\n", lines);
    }

    public static bool InspectExistingMirror(MirrorPlan plan)
    {
        var (meta, _) = GetSessionMeta(plan.MirrorPath);
        if (meta == null) return false;
        return meta.Id == plan.MirrorId && meta.Provider == plan.TargetProvider;
    }

    public static SqliteReport SyncSqliteMirrors(string? dbPath, List<MirrorPlan> plans, bool apply, SyncReport report)
    {
        var sqliteReport = new SqliteReport { Path = dbPath };
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
        {
            report.SqliteSourceRowsMissing = plans.Select(p => p.SourceId).Distinct().Count();
            return sqliteReport;
        }

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        sqliteReport.ProviderCountsBefore = FetchProviderCounts(conn);
        var columns = FetchThreadColumns(conn);
        var creatable = new List<MirrorPlan>();

        foreach (var plan in plans)
        {
            if (!ThreadExists(conn, plan.SourceId))
            {
                report.SqliteSourceRowsMissing++;
                continue;
            }
            if (ThreadExists(conn, plan.MirrorId))
            {
                if (MirrorRowMatches(conn, plan))
                    report.SqliteRowsExisting++;
                else
                    report.SqliteRowsConflicting++;
                continue;
            }
            report.SqliteRowsNeeded++;
            creatable.Add(plan);
        }

        sqliteReport.RowsNeedingUpdate = report.SqliteRowsNeeded;

        if (apply && creatable.Count > 0)
        {
            using var tx = conn.BeginTransaction();
            foreach (var plan in creatable)
            {
                report.SqliteRowsCreated += InsertThreadMirror(conn, plan, columns, tx);
                CloneChildTable(conn, "thread_dynamic_tools", "thread_id", plan, tx);
                CloneChildTable(conn, "thread_goals", "thread_id", plan, tx);
                CloneChildTable(conn, "stage1_outputs", "thread_id", plan, tx);
            }
            tx.Commit();
            conn.ExecuteNonQuery("PRAGMA wal_checkpoint(TRUNCATE)");
        }

        sqliteReport.RowsUpdated = report.SqliteRowsCreated;
        sqliteReport.ProviderCountsAfter = apply
            ? FetchProviderCounts(conn)
            : SimulateCounts(sqliteReport.ProviderCountsBefore, creatable);

        return sqliteReport;
    }

    public static List<(string?, int)> FetchProviderCounts(SqliteConnection conn)
    {
        var result = new List<(string?, int)>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT model_provider, COUNT(*) FROM threads GROUP BY model_provider ORDER BY COUNT(*) DESC, model_provider ASC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var prov = reader.IsDBNull(0) ? null : reader.GetString(0);
            result.Add((prov, reader.GetInt32(1)));
        }
        return result;
    }

    public static List<string> FetchThreadColumns(SqliteConnection conn)
    {
        var result = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(threads)";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(1));
        return result;
    }

    public static bool ThreadExists(SqliteConnection conn, string threadId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM threads WHERE id = $id LIMIT 1";
        cmd.Parameters.AddWithValue("$id", threadId);
        return cmd.ExecuteScalar() != null;
    }

    public static bool MirrorRowMatches(SqliteConnection conn, MirrorPlan plan)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT model_provider, rollout_path FROM threads WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", plan.MirrorId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return false;
        return reader.GetString(0) == plan.TargetProvider && reader.GetString(1) == plan.MirrorPath;
    }

    public static int InsertThreadMirror(SqliteConnection conn, MirrorPlan plan, List<string> columns, SqliteTransaction tx)
    {
        var quotedCols = string.Join(", ", columns.Select(c => $"\"{c.Replace("\"", "\"\"")}\""));
        var selectExprs = new List<string>();
        var parameters = new List<SqliteParameter>();
        foreach (var col in columns)
        {
            if (col == "id")
            {
                selectExprs.Add("$id");
                parameters.Add(new SqliteParameter("$id", plan.MirrorId));
            }
            else if (col == "rollout_path")
            {
                selectExprs.Add("$rp");
                parameters.Add(new SqliteParameter("$rp", plan.MirrorPath));
            }
            else if (col == "model_provider")
            {
                selectExprs.Add("$mp");
                parameters.Add(new SqliteParameter("$mp", plan.TargetProvider));
            }
            else
            {
                selectExprs.Add($"\"{col.Replace("\"", "\"\"")}\"");
            }
        }
        parameters.Add(new SqliteParameter("$sid", plan.SourceId));

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"INSERT INTO threads ({quotedCols}) SELECT {string.Join(", ", selectExprs)} FROM threads WHERE id = $sid";
        cmd.Parameters.AddRange(parameters.ToArray());
        return cmd.ExecuteNonQuery();
    }

    public static void CloneChildTable(SqliteConnection conn, string table, string threadColumn, MirrorPlan plan, SqliteTransaction tx)
    {
        var columns = new List<string>();
        using var pragma = conn.CreateCommand();
        pragma.Transaction = tx;
        pragma.CommandText = $"PRAGMA table_info(\"{table.Replace("\"", "\"\"")}\")";
        using var r = pragma.ExecuteReader();
        while (r.Read()) columns.Add(r.GetString(1));
        if (!columns.Contains(threadColumn)) return;

        var quotedCols = string.Join(", ", columns.Select(c => $"\"{c.Replace("\"", "\"\"")}\""));
        var selectExprs = columns.Select(c => c == threadColumn ? "$tid" : $"\"{c.Replace("\"", "\"\"")}\"").ToList();

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"INSERT OR IGNORE INTO \"{table.Replace("\"", "\"\"")}\" ({quotedCols}) SELECT {string.Join(", ", selectExprs)} FROM \"{table.Replace("\"", "\"\"")}\" WHERE \"{threadColumn.Replace("\"", "\"\"")}\" = $sid";
        cmd.Parameters.AddWithValue("$tid", plan.MirrorId);
        cmd.Parameters.AddWithValue("$sid", plan.SourceId);
        cmd.ExecuteNonQuery();
    }

    public static List<(string?, int)> SimulateCounts(List<(string?, int)> before, List<MirrorPlan> creatable)
    {
        var dict = new Dictionary<string, int>();
        foreach (var (prov, cnt) in before)
        {
            var key = prov ?? "";
            dict[key] = dict.GetValueOrDefault(key) + cnt;
        }
        foreach (var plan in creatable)
            dict[plan.TargetProvider] = dict.GetValueOrDefault(plan.TargetProvider) + 1;
        return dict.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => ((string?)kv.Key, kv.Value)).ToList();
    }

    public static void BackupSqlite(string dbPath, string backupRoot)
    {
        var dir = Path.Combine(backupRoot, "sqlite");
        Directory.CreateDirectory(dir);
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var src = dbPath + suffix;
            if (File.Exists(src))
            {
                var dst = Path.Combine(dir, Path.GetFileName(src));
                if (!File.Exists(dst)) File.Copy(src, dst, true);
            }
        }
    }

    public static void BackupFile(string src, string codexHome, string backupRoot)
    {
        var rel = Path.GetRelativePath(codexHome, src);
        if (rel.StartsWith("..") || rel.StartsWith('.')) rel = Path.GetFileName(src);
        var dst = Path.Combine(backupRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        if (!File.Exists(dst)) File.Copy(src, dst, true);
    }
}

public static class SqliteExtensions
{
    public static int ExecuteNonQuery(this SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteNonQuery();
    }
}
