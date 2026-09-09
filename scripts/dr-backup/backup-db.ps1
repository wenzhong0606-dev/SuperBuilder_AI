<#
.SYNOPSIS
    SuperBuilder_AI 关系库备份（多引擎：SQL Server / MySQL / PostgreSQL）。
.DESCRIPTION
    依据连接字符串自动识别引擎，调用对应原生工具导出：
      - SQL Server : sqlpackage Export（BACPAC，自包含架构+数据）
      - MySQL      : mysqldump
      - PostgreSQL : pg_dump（自定义格式 -Fc）
    产出：<OutputDir>/<BackupName>.<ext> + manifest.json（引擎/库名/时间/大小/SHA256）。
.NOTES
    需对应原生客户端：
      SqlServer -> dotnet tool install --global dotnet-sqlpackage
      MySql     -> mysqldump（MySQL 客户端）
      PostgreSql-> pg_dump（PostgreSQL 客户端）
    凭据建议通过环境变量/secret manager 注入，避免在命令行明文出现。
    用法示例：
      .\backup-db.ps1 -ConnectionString $env:SB_DB_CS -OutputDir ./backups
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,
    [string]$OutputDir = './backups',
    [string]$BackupName,
    [ValidateSet('Auto', 'SqlServer', 'MySql', 'PostgreSql')]
    [string]$Engine = 'Auto'
)

$ErrorActionPreference = 'Stop'

function Detect-Engine {
    param([string]$Cs)
    if ($Cs -match 'Uid=' -or $Cs -match 'uid=') { return 'MySql' }
    if ($Cs -match 'PORT=5432' -or $Cs -match 'Host=' -or $Cs -match 'Pooling=') { return 'PostgreSql' }
    if ($Cs -match 'Server=' -or $Cs -match 'Data Source=') { return 'SqlServer' }
    return 'SqlServer'
}

function Get-DbName {
    param([string]$Cs)
    if ($Cs -match 'Database=([^;]+)') { return $Matches[1] }
    if ($Cs -match 'Initial Catalog=([^;]+)') { return $Matches[1] }
    return 'unknown'
}

function Sha256 {
    param([string]$Path)
    $hash = (Get-FileHash -Algorithm SHA256 -Path $Path).Hash
    return $hash
}

$resolvedEngine = if ($Engine -eq 'Auto') { Detect-Engine $ConnectionString } else { $Engine }
$dbName = Get-DbName $ConnectionString
$timestamp = (Get-Date -Format 'yyyyMMdd-HHmmss')
if (-not $BackupName) { $BackupName = "sb_$dbName`_$timestamp" }

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }
$outFile = Join-Path $OutputDir $BackupName
$manifest = Join-Path $OutputDir "$BackupName.manifest.json"

Write-Host "[backup-db] engine=$resolvedEngine db=$dbName out=$outFile" -ForegroundColor Cyan

switch ($resolvedEngine) {
    'SqlServer' {
        $outFile = "$outFile.bacpac"
        $sqlpkg = (Get-Command sqlpackage -ErrorAction SilentlyContinue)
        if (-not $sqlpkg) { throw 'sqlpackage 未找到，请执行: dotnet tool install --global dotnet-sqlpackage' }
        & sqlpackage /Action:Export /SourceConnectionString:"$ConnectionString" /TargetFile:"$outFile" /OverwriteFiles:True
        if ($LASTEXITCODE -ne 0) { throw "sqlpackage Export 失败 (exit $LASTEXITCODE)" }
    }
    'MySql' {
        $outFile = "$outFile.sql"
        & mysqldump --single-transaction --routines --events --triggers -C "$ConnectionString" > "$outFile"
        if ($LASTEXITCODE -ne 0) { throw "mysqldump 失败 (exit $LASTEXITCODE)" }
    }
    'PostgreSql' {
        $outFile = "$outFile.dump"
        $env:PGCONNECTIONSTRING = $ConnectionString
        & pg_dump -Fc -f "$outFile"
        if ($LASTEXITCODE -ne 0) { throw "pg_dump 失败 (exit $LASTEXITCODE)" }
    }
}

$size = (Get-Item $outFile).Length
$hash = Sha256 $outFile
if ($resolvedEngine -eq 'SqlServer') { $method = 'sqlpackage-BACPAC' }
elseif ($resolvedEngine -eq 'MySql') { $method = 'mysqldump' }
else { $method = 'pg_dump' }
$obj = [ordered]@{
    artifact   = 'SuperBuilder_AI.DB.Backup'
    engine     = $resolvedEngine
    database   = $dbName
    createdUtc = (Get-Date -AsUTC -Format 'o')
    file       = (Split-Path $outFile -Leaf)
    sizeBytes  = $size
    sha256     = $hash
    method     = $method
}
$obj | ConvertTo-Json -Compress | Set-Content -Path $manifest -Encoding UTF8
Write-Host "[backup-db] OK file=$outFile size=$size sha256=$hash" -ForegroundColor Green
Write-Host "[backup-db] manifest=$manifest" -ForegroundColor Gray
