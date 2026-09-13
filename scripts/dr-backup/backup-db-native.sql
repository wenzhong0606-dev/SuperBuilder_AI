-- =============================================================================
-- SuperBuilder_AI 关系库原生备份（SQL Server，经 sqlcmd 执行）
-- 目的：在没有 sqlpackage 的环境下提供可执行、自校验的备份路径（DR-01 已验证主路径）。
-- 用法（编辑本文件下方 :setvar 后执行；注意 sqlcmd 的 -v 不接受含冒号的 Windows 路径）：
--   sqlcmd -S localhost -U <user> -P <pwd> -I -W < scripts\dr-backup\backup-db-native.sql
--   （Windows 下也可：Get-Content scripts\dr-backup\backup-db-native.sql | sqlcmd -S localhost -U <user> -P <pwd> -I）
-- 说明：
--   * 路径类变量请直接改下方 :setvar；不要用 -v 传 C:\... （本仓库 sqlcmd 以 ':' 作分隔符，会报「参数无效」）。
--   * 目标目录须预先存在（T-SQL 不能建 OS 目录）。
--   * BACKUP ... WITH CHECKSUM + RESTORE VERIFYONLY WITH CHECKSUM = 备份后立即校验可还原性（失败即报错退出非 0）。
--   * COMPRESSION 实测把 72MB 数据文件压到约 6MB（本仓库元库实测）。
--   * 本文件仅做结构/数据备份，不含密钥（密钥走密钥管理器，见 docs/ops/backup-restore-dr.md §9）。
-- =============================================================================
:setvar Database   "SuperBuilder_Platform"
:setvar BackupFile "C:\backups\SuperBuilder_Platform.bak"

SET NOCOUNT ON;
PRINT '--- BACKUP START: $(Database) -> $(BackupFile) ---';
BACKUP DATABASE [$(Database)]
  TO DISK = N'$(BackupFile)'
  WITH INIT, COMPRESSION, CHECKSUM, STATS = 25, NAME = N'$(Database)-Full';
GO
PRINT '--- VERIFY (RESTORE VERIFYONLY) ---';
RESTORE VERIFYONLY FROM DISK = N'$(BackupFile)' WITH CHECKSUM;
GO
PRINT '--- BACKUP END (verified) ---';
