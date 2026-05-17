using System.IO;
using CodexSessionSync.Core;

namespace CodexSessionSync.Desktop.Shared;

public static class SyncEngineDefaults
{
    public static string DefaultCodexHome() => SyncEngine.DefaultCodexHome();

    public static string DefaultBackupDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "codex-session-sync-backup");
    }
}
