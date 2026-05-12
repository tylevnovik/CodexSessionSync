namespace CodexSessionSync.MewUI;

internal enum SyncMode
{
    Mutual,
    OpenAi,
    Migrate
}

internal sealed record SyncOptions(
    SyncMode Mode,
    string CodexHome,
    string? BackupDir,
    string SourceProvider,
    string TargetProvider,
    bool Apply);
