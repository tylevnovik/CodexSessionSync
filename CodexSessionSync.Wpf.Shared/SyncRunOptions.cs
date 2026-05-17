using System.IO;

namespace CodexSessionSync.Desktop.Shared;

public sealed record SyncRunOptions(
    string CodexHome,
    string? BackupDir,
    string SourceProvider,
    string TargetProvider,
    SyncMode Mode,
    bool Apply)
{
    public static SyncRunOptions FromUi(
        string? codexHome,
        string? backupDir,
        string? sourceProvider,
        string? targetProvider,
        SyncMode mode,
        bool apply)
    {
        var normalizedCodexHome = string.IsNullOrWhiteSpace(codexHome)
            ? SyncEngineDefaults.DefaultCodexHome()
            : Path.GetFullPath(codexHome.Trim());

        var normalizedBackupDir = string.IsNullOrWhiteSpace(backupDir)
            ? null
            : Path.GetFullPath(backupDir.Trim());

        return new SyncRunOptions(
            normalizedCodexHome,
            normalizedBackupDir,
            string.IsNullOrWhiteSpace(sourceProvider) ? "openai" : sourceProvider.Trim(),
            string.IsNullOrWhiteSpace(targetProvider) ? "openai" : targetProvider.Trim(),
            mode,
            apply);
    }
}
