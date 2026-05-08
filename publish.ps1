$ErrorActionPreference = "Stop"

$projects = @(
    @{ Name = "CodexSessionSync.Wpf";      SubDir = "CodexSessionSync.Wpf" },
    @{ Name = "CodexSessionSync.WinForms";  SubDir = "CodexSessionSync.WinForms" },
    @{ Name = "CodexSessionSync.Avalonia";  SubDir = "CodexSessionSync.Avalonia" },
    @{ Name = "CodexSessionSync.Tui";       SubDir = "CodexSessionSync.Tui" }
)

$root = $PSScriptRoot

foreach ($proj in $projects) {
    $csproj = Join-Path $root "$($proj.SubDir)\$($proj.Name).csproj"
    if (-not (Test-Path $csproj)) {
        Write-Host "SKIP: $csproj not found" -ForegroundColor Yellow
        continue
    }
    Write-Host "`n=== Publishing $($proj.Name) ===" -ForegroundColor Cyan
    dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $root "dist\$($proj.Name)")
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $($proj.Name)" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n=== All published to dist\ ===" -ForegroundColor Green