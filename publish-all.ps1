$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$rootFull = [IO.Path]::GetFullPath($root)

function Assert-UnderRoot {
    param([string]$Path)
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes repository root: $fullPath"
    }
    return $fullPath
}

function Reset-Directory {
    param([string]$Path)
    $fullPath = Assert-UnderRoot $Path
    Remove-Item -Recurse -Force $fullPath -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

# ── variant definitions ──────────────────────────────────
# fdd kept as reference but not in default matrix (trim not allowed)
$variants = @{
    "aot" = @{
        SelfContained    = $true
        PublishAot       = $true
        PublishTrimmed   = $true
        PublishSingleFile = $true
    }
    "fdd" = @{
        SelfContained    = $false
        PublishAot       = $false
        PublishTrimmed   = $false
        PublishSingleFile = $false
    }
}

# ── project × variant matrix ────────────────────────────
# Rule: if AOT available → only AOT. Otherwise → self-contained.
$matrix = @(
    @{ Project = "CodexSessionSync.Avalonia"; Csproj = "CodexSessionSync.Avalonia\CodexSessionSync.Avalonia.csproj"; Variants = @("aot") }
    @{ Project = "CodexSessionSync.Tui";      Csproj = "CodexSessionSync.Tui\CodexSessionSync.Tui.csproj";           Variants = @("aot") }
    @{ Project = "CodexSessionSync.WinForms"; Csproj = "CodexSessionSync.WinForms\CodexSessionSync.WinForms.csproj"; Variants = @("aot") }
)

# WPF cannot do AOT — self-contained only
$scOnly = @(
    @{ Project = "CodexSessionSync.Wpf"; Csproj = "CodexSessionSync.Wpf\CodexSessionSync.Wpf.csproj"; OutDir = "dist\CodexSessionSync.Wpf" }
)

# WinUI supports NativeAOT, but Windows App SDK self-contained deployment is a folder.
$winuiAot = @{ Project = "CodexSessionSync.WinUI"; Csproj = "CodexSessionSync.WinUI\CodexSessionSync.WinUI.csproj"; OutDir = "dist\CodexSessionSync.WinUI-aot" }

# ═══════════════════════════════════════════════════════════
function Publish-Variant {
    param($Project, $Csproj, $VariantName, $VariantProps)
    $outDir = Join-Path $root "dist\$Project-$VariantName"
    Write-Host "`n=== $Project - $VariantName  ->  $outDir ===" -ForegroundColor Cyan
    Reset-Directory $outDir

    $props = @(
        "-c", "Release",
        "-r", "win-x64",
        "-p:SelfContained=$($VariantProps.SelfContained)",
        "-p:PublishAot=$($VariantProps.PublishAot)",
        "-p:PublishTrimmed=$($VariantProps.PublishTrimmed)",
        "-p:PublishSingleFile=$($VariantProps.PublishSingleFile)",
        "-o", $outDir
    )

    dotnet publish (Join-Path $root $Csproj) @props
    if ($LASTEXITCODE -ne 0) { throw "FAILED: $Project - $VariantName" }
}

function Publish-WinUIAot {
    param($Project, $Csproj, $OutDir)
    $outDir = Join-Path $root $OutDir
    Write-Host "`n=== $Project - aot  ->  $outDir ===" -ForegroundColor Cyan
    Reset-Directory $outDir

    $props = @(
        "-c", "Release",
        "-r", "win-x64",
        "-p:Platform=x64",
        "-p:SelfContained=true",
        "-p:PublishAot=true",
        "-p:PublishTrimmed=true",
        "-p:PublishSingleFile=false",
        "-p:TrimMode=partial",
        "-p:WindowsPackageType=None",
        "-p:WindowsAppSDKSelfContained=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:CopyOutputSymbolsToPublishDirectory=false",
        "-o", $outDir
    )

    dotnet publish (Join-Path $root $Csproj) @props
    if ($LASTEXITCODE -ne 0) { throw "FAILED: $Project - aot" }
}

function Compress-ReleaseDirectory {
    param($SourceDir, $ZipPath)
    if (-not (Test-Path -LiteralPath $SourceDir)) { throw "Missing release source: $SourceDir" }

    $stageDir = Join-Path ([IO.Path]::GetTempPath()) "codex-session-sync-release-$([guid]::NewGuid())"
    New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
    try {
        Get-ChildItem -LiteralPath $SourceDir -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stageDir $_.Name) -Recurse -Force
        }
        Get-ChildItem -LiteralPath $stageDir -Recurse -Force -File -Filter "*.pdb" | Remove-Item -Force

        $entries = Get-ChildItem -LiteralPath $stageDir -Force
        if (-not $entries) { throw "No release files found in $SourceDir" }

        Compress-Archive -Path $entries.FullName -DestinationPath $ZipPath -Force
        return [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
    }
    finally {
        Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ═══════════════════════════════════════════════════════════
# Build AOT variants
foreach ($entry in $matrix) {
    foreach ($vName in $entry.Variants) {
        Publish-Variant -Project $entry.Project -Csproj $entry.Csproj -VariantName $vName -VariantProps $variants[$vName]
    }
}

Publish-WinUIAot -Project $winuiAot.Project -Csproj $winuiAot.Csproj -OutDir $winuiAot.OutDir

# Build self-contained variants (no AOT support)
foreach ($sc in $scOnly) {
    $outDir = Join-Path $root $sc.OutDir
    Write-Host "`n=== $($sc.Project) - sc  ->  $outDir ===" -ForegroundColor Cyan
    Reset-Directory $outDir
    dotnet publish (Join-Path $root $sc.Csproj) -c Release -r win-x64 --self-contained true -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "FAILED: $($sc.Project) self-contained" }
}

# ═══════════════════════════════════════════════════════════
# Zip AOT assets (exclude .pdb)
$releaseDir = Join-Path $root "release"
Reset-Directory $releaseDir

foreach ($entry in $matrix) {
    foreach ($vName in $entry.Variants) {
        $srcDir  = Join-Path $root "dist\$($entry.Project)-$vName"
        $zipPath = Join-Path $releaseDir "$($entry.Project)-$vName.zip"
        $sizeMB = Compress-ReleaseDirectory -SourceDir $srcDir -ZipPath $zipPath
        Write-Host "  $($entry.Project)-$vName.zip  $sizeMB MB"
    }
}

$winuiSrcDir = Join-Path $root $winuiAot.OutDir
$winuiZipPath = Join-Path $releaseDir "$($winuiAot.Project)-aot.zip"
$winuiSizeMB = Compress-ReleaseDirectory -SourceDir $winuiSrcDir -ZipPath $winuiZipPath
Write-Host "  $($winuiAot.Project)-aot.zip  $winuiSizeMB MB"

# Self-contained exe (no zip needed — single file)
foreach ($sc in $scOnly) {
    $srcExe = Join-Path $root "$($sc.OutDir)\$($sc.Project).exe"
    Copy-Item $srcExe $releaseDir -Force
    $sizeMB = [math]::Round((Get-Item (Join-Path $releaseDir "$($sc.Project).exe")).Length / 1MB, 1)
    Write-Host "  $($sc.Project).exe  $sizeMB MB"
}

Write-Host "`n=== All release assets in release\ ===" -ForegroundColor Green
