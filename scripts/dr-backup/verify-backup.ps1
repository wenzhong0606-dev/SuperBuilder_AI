<#
.SYNOPSIS
    SuperBuilder_AI 备份完整性校验。
.DESCRIPTION
    对 <BackupDir> 下每个 *.manifest.json 执行：
      - 对应数据文件存在性
      - 当前 SHA256 与 manifest 记录一致（防静默损坏）
      - 文件大小与 manifest 一致
    可选 -CheckConnectivity：若提供了连接串，则测试数据库可达。
.NOTES
    用法示例：
      .\verify-backup.ps1 -BackupDir ./backups
      .\verify-backup.ps1 -BackupDir ./backups -ConnectionString $env:SB_DB_CS
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupDir,
    [string]$ConnectionString,
    [switch]$CheckConnectivity
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $BackupDir)) { throw ('备份目录不存在: ' + $BackupDir) }

$manifests = Get-ChildItem -Path $BackupDir -Filter '*.manifest.json'
if ($manifests.Count -eq 0) { Write-Warning '未发现任何 manifest.json，跳过。'; exit 0 }

$allOk = $true
foreach ($m in $manifests) {
    $obj = Get-Content -Path $m.FullName -Encoding UTF8 | ConvertFrom-Json
    $dataFile = Join-Path $BackupDir $obj.file
    $tag = ($obj.artifact + ' / ' + $obj.file)
    if (-not (Test-Path $dataFile)) {
        Write-Host ('[verify] FAIL 缺失数据文件: ' + $tag) -ForegroundColor Red
        $allOk = $false
        continue
    }
    $size = (Get-Item $dataFile).Length
    $hash = (Get-FileHash -Algorithm SHA256 -Path $dataFile).Hash
    $sizeOk = ($size -eq $obj.sizeBytes)
    $hashOk = ($hash -eq $obj.sha256)
    if ($sizeOk -and $hashOk) {
        $shortHash = $hash.Substring(0, 12)
        Write-Host ('[verify] OK  ' + $tag + ' (size=' + $size + ' sha256=' + $shortHash + '...)') -ForegroundColor Green
    }
    else {
        Write-Host ('[verify] FAIL ' + $tag + ' sizeOk=' + $sizeOk + ' hashOk=' + $hashOk) -ForegroundColor Red
        $allOk = $false
    }
}

if ($CheckConnectivity -and $ConnectionString) {
    Write-Host '[verify] 连接性检查 (sqlcmd -Q SELECT 1)' -ForegroundColor Cyan
    try {
        & sqlcmd -C -b -S $ConnectionString -Q "SELECT 'reachable' AS state" | Out-Null
        if ($LASTEXITCODE -eq 0) { Write-Host '[verify] OK 数据库可达' -ForegroundColor Green }
        else { Write-Host '[verify] WARN 数据库连接失败（非备份文件问题）' -ForegroundColor Yellow }
    }
    catch {
        Write-Host ('[verify] WARN 连接检查异常: ' + $_.Exception.Message) -ForegroundColor Yellow
    }
}

if (-not $allOk) { throw '备份校验发现不一致，请重新生成备份。' }
Write-Host '[verify] 全部 manifest 校验通过。' -ForegroundColor Green
