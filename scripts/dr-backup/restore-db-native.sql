-- =============================================================================
-- SuperBuilder_AI 关系库原生恢复（SQL Server，经 sqlcmd 执行）
-- 目的：把 backup-db-native.sql 产出的 .bak 还原为可服务库（默认还原为独立目标库，不动源库）。
-- 用法（覆盖默认变量）：
--   sqlcmd -S localhost -U <user> -P <pwd> -I ^
--     -v SourceDb="SuperBuilder_Platform" ^
--     -v TargetDb="SuperBuilder_Platform_restored" ^
--     -v DataDir="C:\dr_drill" ^
--     -v BackupFile="C:\backups\SuperBuilder_Platform.bak" ^
--     -i scripts\dr-backup\restore-db-native.sql
-- 说明：
--   * 还原到独立目标库（不覆盖源库），便于演练/校验；如需就地还原，令 TargetDb=SourceDb 并评估停机窗口。
--   * MOVE 假定逻辑文件名 = 源库名（SuperBuilder_Platform / SuperBuilder_Platform_log）；跨库迁移时先
--     RESTORE FILELISTONLY FROM DISK=... 确认逻辑名。
--   * 还原后建议覆核：__EFMigrationsHistory 计数、核心表行数、DBCC CHECKDB。
-- =============================================================================
:setvar SourceDb   "SuperBuilder_Platform"
:setvar TargetDb   "SuperBuilder_Platform_restored"
:setvar DataDir    "C:\backups\restore"
:setvar BackupFile "C:\backups\SuperBuilder_Platform.bak"

SET NOCOUNT ON;
IF DB_ID(N'$(TargetDb)') IS NOT NULL
BEGIN
  PRINT '--- 目标库已存在，先强制下线并删除：$(TargetDb) ---';
  ALTER DATABASE [$(TargetDb)] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [$(TargetDb)];
END
GO
PRINT '--- RESTORE START: $(BackupFile) -> $(TargetDb) ---';
RESTORE DATABASE [$(TargetDb)]
  FROM DISK = N'$(BackupFile)'
  WITH MOVE N'$(SourceDb)'     TO N'$(DataDir)\$(TargetDb).mdf',
       MOVE N'$(SourceDb)_log' TO N'$(DataDir)\$(TargetDb)_log.ldf',
       RECOVERY, STATS = 25;
GO
PRINT '--- RESTORE END ---';
