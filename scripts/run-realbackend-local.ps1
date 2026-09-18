<#
.SYNOPSIS
  在真机（能访问远程 SQL Server + Qdrant 的网络）运行 v10 真实后端集成测试。
.DESCRIPTION
  默认只跑 MetadataLifecycleRealBackendTests（仅需可建库的 SQL Server + 可写 Qdrant，10 个测试：
  3 原有 + 7 新增四组补强），不需要 MySQL / Postgres。
  -All           跑整个 RealBackend 工程（还需 MySQL + Postgres，用于 QueryScope 跨库/跨 schema 测试）。
  -Filter <expr> 自定义 xunit 过滤表达式（例如 "FullyQualifiedName~CrossSchemaScan"，仅当无 Qdrant 时仍能跑的那一个）。

  连接串从 scripts/realbackend.local.ps1 读取（该文件已 gitignore，勿提交）；
  若不存在则从当前进程环境变量读取 SB_REAL_*。
#>
[CmdletBinding()]
param(
    [switch]$All,
    [string]$Filter
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$local = Join-Path $PSScriptRoot "realbackend.local.ps1"
if (Test-Path $local) {
    Write-Host "已载入本地连接配置: $local"
    . $local
} else {
    Write-Host "未找到 $local；将从当前环境变量读取 SB_REAL_*。"
}

# 必需：SQL Server（测试会建 sb_v10_* 库，账号须有 CREATE DATABASE 权限）
# 必需：Qdrant（测试会建 sb_v10_* 集合并读写）
$missing = @()
foreach ($v in "SB_REAL_SQLSERVER", "SB_REAL_QDRANT_HOST") {
    if (-not (Test-Path -Path "env:$v")) { $missing += $v }
}
if ($missing.Count -gt 0) {
    Write-Error "缺少环境变量: $($missing -join ', ')。请复制 scripts/realbackend.local.ps1.example 为 scripts/realbackend.local.ps1 并填入连接串，或先 export 这两个变量。"
    exit 2
}
if (-not (Test-Path -Path "env:SB_REAL_QDRANT_PORT")) {
    $env:SB_REAL_QDRANT_PORT = "6334"
    Write-Host "SB_REAL_QDRANT_PORT 未设，使用默认 6334。"
}

$project = "tests/SuperBuilder_AI.RealBackend.Tests"
$filter = "FullyQualifiedName~MetadataLifecycleRealBackendTests"
if ($Filter) { $filter = $Filter }
elseif ($All) { $filter = $null }

Write-Host "----------------------------------------"

# 探测 dotnet 是否可用（powershell -File 子进程可能不加载 profile 而丢失 PATH）
try { $dotnetVer = & dotnet --version 2>$null } catch { $dotnetVer = $null }
if (-not $dotnetVer) {
    Write-Error "当前环境中未找到 dotnet（PATH 中缺失）。请在交互式 PowerShell 会话直接运行（不要经 powershell -File 子进程），或把 dotnet 加入系统 PATH；可先 'dotnet --version' 验证。"
    exit 3
}
Write-Host "dotnet 版本: $dotnetVer"

if ($filter) {
    Write-Host "运行范围: $filter"
    & dotnet test $project --filter $filter --logger "console;verbosity=normal"
} else {
    Write-Host "运行范围: 整个 RealBackend 工程（还需 MySQL + Postgres）"
    & dotnet test $project --logger "console;verbosity=normal"
}
exit $LASTEXITCODE
