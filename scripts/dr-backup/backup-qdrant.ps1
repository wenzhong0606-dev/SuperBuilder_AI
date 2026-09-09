<#
.SYNOPSIS
    SuperBuilder_AI Qdrant 向量库快照备份。
.DESCRIPTION
    调用 Qdrant REST API 触发集合快照并下载：
      POST /collections/{Collection}/snapshots -> 取 snapshot 名
      GET  /collections/{Collection}/snapshots/{name} -> 下载 .snapshot 文件
    产出：<OutputDir>/<BackupName>.snapshot + manifest.json（含 SHA256）。
.NOTES
    需 PowerShell 7+（Invoke-RestMethod）。Qdrant 默认 REST 端口 6333。
    用法示例：
      .\backup-qdrant.ps1 -Host localhost -Port 6333 -Collection superbi_metadata -OutputDir ./backups
#>
[CmdletBinding()]
param(
    [string]$Host = 'localhost',
    [int]$Port = 6333,
    [string]$Collection = 'superbi_metadata',
    [string]$ApiKey,
    [string]$OutputDir = './backups',
    [string]$BackupName
)

$ErrorActionPreference = 'Stop'
$base = "http://$Host`:$Port"
$headers = @{}
if ($ApiKey) { $headers['api-key'] = $ApiKey }

$timestamp = (Get-Date -Format 'yyyyMMdd-HHmmss')
if (-not $BackupName) { $BackupName = "sb_qdrant_$Collection`_$timestamp" }
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }
$outFile = Join-Path $OutputDir "$BackupName.snapshot"

Write-Host "[backup-qdrant] 触发快照 collection=$Collection" -ForegroundColor Cyan
$snap = Invoke-RestMethod -Method Post -Uri "$base/collections/$Collection/snapshots" -Headers $headers -ContentType 'application/json' -Body '{}'
$snapName = $snap.result.name
if (-not $snapName) { throw "快照创建未返回名称: $($snap | ConvertTo-Json)" }
Write-Host "[backup-qdrant] snapshot=$snapName 下载中..." -ForegroundColor Gray

Invoke-RestMethod -Method Get -Uri "$base/collections/$Collection/snapshots/$snapName" -Headers $headers -OutFile $outFile

$size = (Get-Item $outFile).Length
$hash = (Get-FileHash -Algorithm SHA256 -Path $outFile).Hash
$obj = [ordered]@{
    artifact     = 'SuperBuilder_AI.Qdrant.Backup'
    collection   = $Collection
    snapshot     = $snapName
    createdUtc   = (Get-Date -AsUTC -Format 'o')
    file         = (Split-Path $outFile -Leaf)
    sizeBytes    = $size
    sha256       = $hash
}
$obj | ConvertTo-Json -Compress | Set-Content -Path (Join-Path $OutputDir "$BackupName.manifest.json") -Encoding UTF8
Write-Host "[backup-qdrant] OK file=$outFile size=$size sha256=$hash" -ForegroundColor Green
