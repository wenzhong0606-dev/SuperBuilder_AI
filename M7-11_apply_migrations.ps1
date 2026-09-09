<#
.SYNOPSIS
    M7-11 数据结构同步：应用 EF 迁移并自检。
.DESCRIPTION
    1) 检测并提示停止占用 bin 的旧 SuperBuilder_AI 实例
    2) 运行 dotnet ef database update（应用 20260908051650_M7_11_AskQuerySnapshot 等）
    3) 输出自查 SQL，并提示重启应用以播种 i18n 新键
.NOTES
    需：.NET 10 SDK + dotnet-ef 全局工具（dotnet tool install --global dotnet-ef）
    用法：在仓库根目录右键"使用 PowerShell 运行"，或 powershell -File .\M7-11_apply_migrations.ps1
#>
param(
    [switch]$ForceStopOldInstance
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project  = Join-Path $RepoRoot 'SuperBuilder_AI' 'SuperBuilder_AI.csproj'

Write-Host '== M7-11 迁移应用与验证 ==' -ForegroundColor Cyan

# 0) 旧实例占用检查
$locked = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'SuperBuilder_AI' }
if ($locked) {
    $pids = $locked.Id -join ', '
    Write-Host "检测到正在运行的 SuperBuilder_AI 实例 (PID: $pids)。" -ForegroundColor Yellow
    if ($ForceStopOldInstance) {
        $locked | Stop-Process -Force
        Write-Host '已强制停止旧实例。' -ForegroundColor Green
    } else {
        Write-Host '请先手动停止旧实例（任务管理器结束 SuperBuilder_AI，或关闭运行中的终端），否则迁移/build 可能因文件占用失败。' -ForegroundColor Yellow
        $answer = Read-Host '已停止？(Y 继续 / N 退出)'
        if ($answer -notmatch '^[Yy]') { exit 0 }
    }
}

# 1) 应用迁移
Write-Host '正在应用 EF 迁移...' -ForegroundColor Cyan
dotnet ef database update --project $Project
if ($LASTEXITCODE -ne 0) {
    Write-Error "迁移应用失败（退出码 $LASTEXITCODE）。请检查连接字符串与数据库可达性。"
    exit 1
}
Write-Host '迁移命令已执行完成。' -ForegroundColor Green

# 2) 自查提示
Write-Host ''
Write-Host '请在目标数据库执行以下 SQL 确认新表已创建：' -ForegroundColor Cyan
Write-Host "  SELECT * FROM __EFMigrationsHistory WHERE MigrationId LIKE '%M7_11%'" -ForegroundColor White
Write-Host '  -- 期望返回一行：20260908051650_M7_11_AskQuerySnapshot' -ForegroundColor Gray
Write-Host "  SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='AskQuerySnapshots';" -ForegroundColor White
Write-Host '  -- 期望返回 1' -ForegroundColor Gray

# 3) 重启提示
Write-Host ''
Write-Host '接下来：重新构建并启动应用（dotnet run 或你的启动方式）。' -ForegroundColor Cyan
Write-Host '启动后 Program.cs 会以幂等方式自动播种 i18n 新键（C3 25 + C4 + PlatformLogin 8），无需手动迁移。' -ForegroundColor White
Write-Host '完成。若需自动化 E2E 验收，请按 P0-D1/D2 设置 SB_E2E_* 环境变量并安装 Chromium。' -ForegroundColor Gray
