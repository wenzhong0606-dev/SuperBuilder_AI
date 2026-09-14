-- Hotfix: 2026-09-14
-- Symptom: 点击"重新扫描"提示"服务暂时不可用"，API 500。
-- Root cause: 代码已升级 MetadataScanJob 实体（ProgressDetailsJson / Stage），
--             但真实元库 __EFMigrationsHistory 缺少 20260914062000_M13_MetadataScanRichProgress，
--             导致 MetadataScanJobs 表缺列，SaveChangesAsync 抛 SqlException。
-- Scope:      SQL Server 元库（如 SuperBuilder_Platform）
-- Idempotent: 可重复执行。

USE [SuperBuilder_Platform];
GO

-- 1. 补齐 MetadataScanJobs 表的两列（与迁移 20260914062000_M13_MetadataScanRichProgress 等价）
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[MetadataScanJobs]')
      AND name = N'ProgressDetailsJson'
)
BEGIN
    ALTER TABLE [dbo].[MetadataScanJobs]
        ADD [ProgressDetailsJson] nvarchar(max) NULL;

    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'扫描富进度快照(JSON)',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'MetadataScanJobs',
        @level2type = N'COLUMN', @level2name = N'ProgressDetailsJson';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[MetadataScanJobs]')
      AND name = N'Stage'
)
BEGIN
    ALTER TABLE [dbo].[MetadataScanJobs]
        ADD [Stage] nvarchar(64) NOT NULL DEFAULT N'Queued';

    EXEC sys.sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'当前扫描阶段',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE',  @level1name = N'MetadataScanJobs',
        @level2type = N'COLUMN', @level2name = N'Stage';
END
GO

-- 2. 让 EF 认为该迁移已应用，避免后续 dotnet ef migrations script 重复生成 ALTER
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260914062000_M13_MetadataScanRichProgress'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260914062000_M13_MetadataScanRichProgress', N'10.0.10');
END
GO

PRINT N'Hotfix applied: MetadataScanJobs.ProgressDetailsJson + Stage columns ready.';
GO
