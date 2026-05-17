using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodexSessionSync.Core;

public record ConfigStatus(
    string Path,
    string? ActiveProvider,
    List<string> ProviderKeys,
    bool TargetDefined,
    string? SqliteHome
);

public record SourceSession(string Path, string ThreadId, string Provider, string? ForkedFromId = null);

public record MirrorPlan(
    string SourcePath,
    string SourceId,
    string TargetProvider,
    string MirrorId,
    string MirrorPath
);

public class SyncReport
{
    public string SourceProvider { get; set; } = "";
    public List<string> TargetProviders { get; set; } = new();
    public int FilesScanned { get; set; }
    public int SourceSessionsFound { get; set; }
    public int MirrorFilesNeeded { get; set; }
    public int MirrorFilesCreated { get; set; }
    public int MirrorFilesExisting { get; set; }
    public int MirrorFilesUpdated { get; set; }
    public int MirrorFilesStale { get; set; }
    public int MirrorFileConflicts { get; set; }
    public int SqliteRowsNeeded { get; set; }
    public int SqliteRowsCreated { get; set; }
    public int SqliteRowsExisting { get; set; }
    public int SqliteRowsConflicting { get; set; }
    public int SqliteSourceRowsMissing { get; set; }
    public Dictionary<string, int> ProviderCountsBefore { get; set; } = new();
    public Dictionary<string, int> ProviderCountsAfter { get; set; } = new();
    public List<string> RiskWarnings { get; set; } = new();
}

public class SqliteReport
{
    public string? Path { get; set; }
    public int RowsNeedingUpdate { get; set; }
    public int RowsUpdated { get; set; }
    public List<(string? Provider, int Count)> ProviderCountsBefore { get; set; } = new();
    public List<(string? Provider, int Count)> ProviderCountsAfter { get; set; } = new();
}

public record SessionMetaInfo(
    string? Id,
    string? Provider,
    string? ForkedFromId
);
