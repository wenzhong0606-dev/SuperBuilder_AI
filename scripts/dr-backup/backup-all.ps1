<#
.SYNOPSIS
    SuperBuilder_AI 全量备份编排（关系库 + Qdrant + 配置/种子文件）。
.DESCRIPTION
    一次性产出一次"恢复点"所需全部组件：
      1) 关系库备份（backup-db.ps1）
      2) Qdrant 向量库快照（backup-qdrant.ps1）
      3) 配置与种子文件归档（appsettings.Local.json / 环境变量清单 / Semantic.csv）
    每个组件带 manifest.json，便于 verify-backup.ps1 整体校验。
.NOTES
    凭据请通过环境变量注入（避免明文）。示例：
      $env:SB_DB_CS = 'Server=...;...'
      .\backup-all.ps1 -DbConnectionString $env:SB_DB_CS -OutputDir ./backups/$($date)
#>
[CmdletBinding()]
param(
    [string]$DbConnectionString,
    [string]$QdrantHost = 'localhost',
    [int]$QdrantPort = 6333,
    [string]$QdrantCollection = 'superbi_metadata',
    [string]$QdrantApiKey,
    [string]$ConfigDir = './SuperBuilder_AI',
    [string]$OutputDir = './backups'
)

$ErrorActionPreference = 'Stop'
$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss')
$runDir = Join-Path $OutputDir "sb_rp_$stamp"
New-Item -ItemType Directory -Path $runDir | Out-Null
Write-Host "[backup-all] 恢复点目录: $runDir" -ForegroundColor Cyan

$dbScript = Join-Path $PSScriptRoot 'backup-db.ps1'
$qdScript = Join-Path $PSScriptRoot 'backup-qdrant.ps1'

# 1) 关系库
if ($DbConnectionString) {
    & $dbScript -ConnectionString $DbConnectionString -OutputDir $runDir
    if ($LASTEXITCODE -ne 0) { throw "关系库备份失败" }
}
else { Write-Host '[backup-all] 跳过关系库（未提供 -DbConnectionString）' -ForegroundColor Yellow }

# 2) Qdrant
& $qdScript -Host $QdrantHost -Port $QdrantPort -Collection $QdrantCollection -ApiKey $QdrantApiKey -OutputDir $runDir
if ($LASTEXITCODE -ne 0) { throw "Qdrant 备份失败" }

# 3) 配置/种子归档（不含明文密钥，仅结构 + 占位符文件）
$cfgOut = Join-Path $runDir 'config'
New-Item -ItemType Directory -Path $cfgOut | Out-Null
@('appsettings.json', 'appsettings.Development.json') | ForEach-Object {
    $src = Join-Path $ConfigDir $_
    if (Test-Path $src) { Copy-Item $src $cfgOut -Force }
}
# 环境变量清单（仅键名，不包含值）
Get-ChildItem env: | Where-Object { $_.Name -match '^(SB_|Qdrant|Qwen|Embedding|Auth|ConnectionStrings)' } |
    Select-Object -ExpandProperty Name | Sort-Object | Set-Content -Path (Join-Path $cfgOut 'env-keys.txt') -Encoding UTF8
# 种子/语义 CSV
$semantic = Join-Path $ConfigDir 'Document/Semantic.csv'
if (Test-Path $semantic) { Copy-Item $semantic $cfgOut -Force }

"恢复点 $stamp 生成完成。组件：关系库(Qdrant 可选) + 配置归档。" | Set-Content -Path (Join-Path $runDir 'RECOVERY_POINT.txt') -Encoding UTF8
Write-Host "[backup-all] 完成。运行 verify-backup.ps1 -BackupDir $runDir 校验。" -ForegroundColor Green
