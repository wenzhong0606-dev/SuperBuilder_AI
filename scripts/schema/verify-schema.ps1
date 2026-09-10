<#
.SYNOPSIS
    SuperBuilder_AI Schema 版本可重复校验（新旧环境统一口径）。
.DESCRIPTION
    以 scripts/schema/schema-version.json 为权威清单，提供两种校验模式：

      -Offline（默认，无需数据库）：校验「迁移程序集」与清单完全一致，
        防止迁移漏提交/清单过期。可在任何有 .NET SDK 的机器（含 CI、离线沙箱）重复执行。

      -Online（-ConnectionString）：连接目标库读取 __EFMigrationsHistory，
        校验「已应用迁移」与清单一致，输出 schemaVersion / 缺失迁移 / 多余迁移（漂移）。
        当前实现走 sqlcmd（对应默认引擎 SQL Server）；MySQL/PG 请用对应客户端按同样逻辑校验。

    退出码：0=一致；2=发现漂移或校验失败。
.NOTES
    用法：
      # 离线：校验迁移程序集 vs 清单
      .\verify-schema.ps1 -ProjectDir ./SuperBuilder_AI
      # 在线：校验目标库已应用迁移 vs 清单
      .\verify-schema.ps1 -ConnectionString $env:SB_DB_CS
#>
[CmdletBinding()]
param(
    [string]$Manifest = (Join-Path $PSScriptRoot 'schema-version.json'),
    [string]$ProjectDir = '',
    [string]$ConnectionString = ''
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Manifest)) { throw ('清单不存在: ' + $Manifest) }
$mf = Get-Content -Path $Manifest -Encoding UTF8 | ConvertFrom-Json
$expected = @($mf.migrations)
Write-Output ('[schema] 清单 schemaVersion=' + $mf.schemaVersion + ' count=' + $expected.Count)

$actual = $null

if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
    # ---- 在线模式：读取 __EFMigrationsHistory ----
    $server = ''; $database = ''; $user = ''; $password = ''
    if ($ConnectionString -match 'Server=([^;]+)') { $server = $Matches[1] }
    if ($ConnectionString -match 'Database=([^;]+)') { $database = $Matches[1] }
    if ($ConnectionString -match 'User Id=([^;]+)') { $user = $Matches[1] }
    if ($ConnectionString -match 'Password=([^;]+)') { $password = $Matches[1] }
    if ($server.Length -eq 0 -or $database.Length -eq 0) {
        throw ('无法从连接串解析 Server/Database（当前在线模式仅支持 Server=...;Database=... 格式）: 请改用离线模式或提供可解析的连接串。')
    }
    $sql = 'SET NOCOUNT ON; SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;'
    $args = @('-C', '-b', '-h', '-1', '-W', '-S', $server, '-d', $database, '-Q', $sql)
    if ($user.Length -gt 0) { $args += @('-U', $user, '-P', $password) } else { $args += '-E' }
    $prevEap2 = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $raw = & sqlcmd @args 2>&1 }
    finally { $ErrorActionPreference = $prevEap2 }
    $actual = @($raw | Where-Object { $_ -match '^[0-9]{14}_' } | ForEach-Object { $_.ToString().Trim() })
    Write-Output ('[schema] 在线模式：库 ' + $database + ' 已应用 ' + $actual.Count + ' 个迁移')
}
else {
    # ---- 离线模式：读取迁移程序集 ----
    if ([string]::IsNullOrWhiteSpace($ProjectDir)) { throw '离线模式需 -ProjectDir（SuperBuilder_AI 项目目录）。' }
    if (-not (Test-Path $ProjectDir)) { throw ('项目目录不存在: ' + $ProjectDir) }
    Push-Location $ProjectDir
    try {
        # 原生命令 stderr（如 NuGet NU1903 告警）在 $ErrorActionPreference='Stop' 下会被
        # 误判为终止性错误，故本地降级为 Continue，仅按输出内容判定结果。
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try { $raw = & dotnet ef migrations list --no-connect 2>&1 }
        finally { $ErrorActionPreference = $prevEap }
    }
    finally { Pop-Location }
    $actual = @($raw | Where-Object { $_ -match '^[0-9]{14}_[A-Za-z0-9_]+' } | ForEach-Object {
        ($_ -replace '^\s+', '' -replace '\s.*$', '')
    })
    Write-Output ('[schema] 离线模式：迁移程序集 ' + $actual.Count + ' 个迁移')
}

# ---- 比对 ----
$missing = @($expected | Where-Object { $actual -notcontains $_ })
$extra   = @($actual   | Where-Object { $expected -notcontains $_ })

if ($missing.Count -eq 0 -and $extra.Count -eq 0) {
    Write-Output ('[schema] OK 一致：schemaVersion=' + $mf.schemaVersion + '（' + $expected.Count + ' 个迁移全部匹配）')
    exit 0
}

if ($missing.Count -gt 0) {
    Write-Output ('[schema] DRIFT 清单中存在但目标未应用/缺失 (' + $missing.Count + '):')
    foreach ($m in $missing) { Write-Output ('   - ' + $m) }
}
if ($extra.Count -gt 0) {
    Write-Output ('[schema] DRIFT 目标存在但清单未记录 (' + $extra.Count + '):')
    foreach ($m in $extra) { Write-Output ('   + ' + $m) }
}
Write-Output '[schema] FAIL 发现漂移。请确认：1) 迁移是否漏提交；2) 环境是否漏升级；3) 清单是否需重新生成。'
exit 2
