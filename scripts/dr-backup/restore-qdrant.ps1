<#
.SYNOPSIS
    SuperBuilder_AI Qdrant 向量库快照恢复。
.DESCRIPTION
    调用 Qdrant REST API 上传快照并恢复集合：
      POST /collections/{Collection}/snapshots/upload （multipart 文件）
    恢复前建议确认目标集合可被覆盖（API 按快照内容重建分片/向量）。
.NOTES
    需 PowerShell 7+（Invoke-RestMethod -Form）。Qdrant 默认 REST 端口 6333。
    用法示例：
      .\restore-qdrant.ps1 -Host localhost -Port 6333 -Collection superbi_metadata -SnapshotFile ./backups/sb_qdrant_xxx.snapshot
#>
[CmdletBinding()]
param(
    [string]$QdrantHost = 'localhost',
    [int]$Port = 6333,
    [string]$Collection = 'superbi_metadata',
    [string]$ApiKey,
    [Parameter(Mandatory = $true)]
    [string]$SnapshotFile,
    [ValidateSet('snapshot', 'replica', 'none')]
    [string]$Priority = 'snapshot'
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $SnapshotFile)) { throw "快照文件不存在: $SnapshotFile" }

$base = ('http://' + $QdrantHost + ':' + $Port)
$headers = @{}
if ($ApiKey) { $headers['api-key'] = $ApiKey }

Write-Host ('[restore-qdrant] 上传并恢复 collection=' + $Collection + ' snapshot=' + $SnapshotFile) -ForegroundColor Cyan

$body = @{ snapshot = (Get-Item -Path $SnapshotFile) }
$uri = ($base + '/collections/' + $Collection + '/snapshots/upload?priority=' + $Priority)
$resp = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Form $body

if ($resp.status -and $resp.status -ne 'ok' -and $resp.status -ne 'accepted') {
    throw ('快照恢复返回非预期状态: ' + ($resp | ConvertTo-Json))
}

Write-Host '[restore-qdrant] OK 已提交恢复。请通过 /collections/{Collection} 校验状态与向量数。' -ForegroundColor Green
