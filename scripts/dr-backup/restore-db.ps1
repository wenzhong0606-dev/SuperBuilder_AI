<#
.SYNOPSIS
    SuperBuilder_AI 关系库恢复（多引擎：SQL Server / MySQL / PostgreSQL）。
.DESCRIPTION
    与 backup-db.ps1 配对：依据备份文件扩展名/引擎参数，调用对应原生工具还原。
      - .bacpac -> sqlpackage Import
      - .sql    -> mysql client
      - .dump   -> pg_restore
    强烈建议：先在新库或隔离实例执行，校验后再切换流量（见 docs/ops/backup-restore-dr.md）。
.NOTES
    SqlServer -> dotnet tool install --global dotnet-sqlpackage
    MySql     -> mysql 客户端
    PostgreSql-> pg_restore 客户端
    用法示例：
      .\restore-db.ps1 -ConnectionString $env:SB_DB_CS -BackupFile ./backups/sb_db_20260909.bacpac
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [ValidateSet('Auto', 'SqlServer', 'MySql', 'PostgreSql')]
    [string]$Engine = 'Auto'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BackupFile)) { throw "备份文件不存在: $BackupFile" }

function Detect-Engine {
    param([string]$Path)
    $ext = [System.IO.Path]::GetExtension($Path).ToLower()
    if ($ext -eq '.bacpac') { return 'SqlServer' }
    if ($ext -eq '.sql') { return 'MySql' }
    if ($ext -eq '.dump') { return 'PostgreSql' }
    return 'SqlServer'
}

if ($Engine -eq 'Auto') { $resolvedEngine = Detect-Engine $BackupFile } else { $resolvedEngine = $Engine }
Write-Host "[restore-db] engine=$resolvedEngine file=$BackupFile" -ForegroundColor Cyan

switch ($resolvedEngine) {
    'SqlServer' {
        $sqlpkg = (Get-Command sqlpackage -ErrorAction SilentlyContinue)
        if (-not $sqlpkg) { throw 'sqlpackage 未找到，请执行: dotnet tool install --global dotnet-sqlpackage' }
        & sqlpackage /Action:Import /TargetConnectionString:"$ConnectionString" /SourceFile:"$BackupFile" /OverwriteFiles:True
        if ($LASTEXITCODE -ne 0) { throw "sqlpackage Import 失败 (exit $LASTEXITCODE)" }
    }
    'MySql' {
        Get-Content -Path $BackupFile | mysql "$ConnectionString"
        if ($LASTEXITCODE -ne 0) { throw "mysql 还原失败 (exit $LASTEXITCODE)" }
    }
    'PostgreSql' {
        $env:PGCONNECTIONSTRING = $ConnectionString
        & pg_restore -Fc -d "$ConnectionString" "$BackupFile"
        if ($LASTEXITCODE -ne 0) { throw "pg_restore 失败 (exit $LASTEXITCODE)" }
    }
}

Write-Host '[restore-db] OK 还原完成。请按 DR 手册执行一致性校验（行数/迁移历史/种子）。' -ForegroundColor Green
