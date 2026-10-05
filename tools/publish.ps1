# 产出可分发的便携版 ZIP。
#
# 为什么需要它：WindowsInstallerPackaging.vdproj 仍写死 .NET Framework 4.8 和
# bin\Release\ShutdownTimerClassic.exe，迁移到 .NET 8 后路径与框架都不对，
# 需要在 Visual Studio 的 Installer Projects 扩展里重建才能出 MSI。
# 在此之前，这个脚本给出一个可用的替代：自包含单文件夹，用户解压即用，
# 不需要预装 .NET 运行时。
#
# 用法（在 Windows 上）:
#   .\tools\publish.ps1                      # win-x64 自包含
#   .\tools\publish.ps1 -Rid win-arm64
#   .\tools\publish.ps1 -FrameworkDependent  # 体积最小，但要求装了 .NET 8 桌面运行时

param(
    [ValidateSet('win-x64', 'win-arm64', 'win-x86')]
    [string]$Rid = 'win-x64',

    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\ShutdownTimer\ShutdownTimer.csproj'
$outDir  = Join-Path $root 'artifacts\publish'
$zipDir  = Join-Path $root 'artifacts\zip'

$version = (dotnet msbuild $project -getProperty:Version -nologo | ConvertFrom-Json).Properties.Version
if (-not $version) { $version = '0.0.0' }

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outDir, $zipDir | Out-Null

$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
$kind = if ($FrameworkDependent) { 'framework-dependent' } else { 'self-contained' }

Write-Host "publishing $kind for $Rid (v$version)" -ForegroundColor Cyan

dotnet publish $project `
    -c Release `
    -r $Rid `
    --self-contained $selfContained `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败（退出码 $LASTEXITCODE）" }

$zip = Join-Path $zipDir "ShutdownTimerClassic-$version-$Rid-$kind.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip -CompressionLevel Optimal

Write-Host ""
Write-Host "完成：$zip" -ForegroundColor Green
Write-Host "注意：解压后必须保持所有文件在同一目录，不要重命名。" -ForegroundColor Yellow
