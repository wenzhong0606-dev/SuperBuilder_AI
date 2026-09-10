using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 修复 M7_02 迁移集缺陷：AppPlans 表缺失发布态 4 列。
    ///
    /// <para>背景：<c>20260906141902_M7_02_AppVersion</c> 的 Up() 只保留了
    /// <c>CREATE TABLE AppVersions</c>，而把 <c>AppPlans</c> 的
    /// <c>AddColumn(PublishedDslJson / PublishedVersion / PublishedAt / PublishedBy)</c>
    /// 一并删除（当时的注释称「避免与早期迁移重复建表冲突」），
    /// 导致：</para>
    /// <list type="bullet">
    /// <item>模型快照 <c>SuperBIContextModelSnapshot</c> 记录这 4 列，但没有任何迁移创建它们；</item>
    /// <item>对已有库，<c>AppPlans</c> 永远缺列，发布/取详情时抛
    ///       <c>SqlException 207「列名 'PublishedAt' 无效」</c>
    ///       （HTTP 500 <c>SB_INTERNAL</c>）；</item>
    /// <item>对全新库，幂等脚本同样不含这 4 列，问题必然复现。</item>
    /// </list>
    ///
    /// <para>此迁移与 <c>20260906131855_M7_01_DashboardVersion</c> 对 <c>Dashboards</c>
    /// 的加列完全对称，列定义取自模型快照，确保数据库 schema 与 EF 模型一致。</para>
    ///
    /// <para>幂等性：使用 <c>COL_LENGTH</c> 守卫，重复执行不报错。</para>
    /// </summary>
    public partial class M7_02_Fix_AppPlanPublishColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 该迁移必须对「迁移历史已含 M7_02、但 schema 缺列」的库生效。
            // EF 的 AddColumn 不具备幂等性，这里改用原生 SQL + COL_LENGTH 守卫，
            // 使重复应用安全（历史已标记应用时迁移不会重跑，但手工补跑亦安全）。
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AppPlans', 'PublishedDslJson') IS NULL
    ALTER TABLE [AppPlans] ADD [PublishedDslJson] nvarchar(max) NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AppPlans', 'PublishedVersion') IS NULL
    ALTER TABLE [AppPlans] ADD [PublishedVersion] int NOT NULL DEFAULT 0;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AppPlans', 'PublishedAt') IS NULL
    ALTER TABLE [AppPlans] ADD [PublishedAt] datetime2 NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.AppPlans', 'PublishedBy') IS NULL
    ALTER TABLE [AppPlans] ADD [PublishedBy] nvarchar(128) NULL;
");

            // 列级注释（与 Dashboards 侧保持对称；sp_addextendedproperty 重复调用会报错，故加守卫）。
            migrationBuilder.Sql(@"
IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE major_id = OBJECT_ID('dbo.AppPlans')
          AND minor_id = COLUMNPROPERTY(OBJECT_ID('dbo.AppPlans'), 'PublishedDslJson', 'ColumnId')
          AND name = 'MS_Description')
    EXEC sp_addextendedproperty 'MS_Description', N'发布态DSL快照（null=未发布）',
        'SCHEMA', N'dbo', 'TABLE', N'AppPlans', 'COLUMN', N'PublishedDslJson';
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE major_id = OBJECT_ID('dbo.AppPlans')
          AND minor_id = COLUMNPROPERTY(OBJECT_ID('dbo.AppPlans'), 'PublishedVersion', 'ColumnId')
          AND name = 'MS_Description')
    EXEC sp_addextendedproperty 'MS_Description', N'当前发布版本号（0=未发布）',
        'SCHEMA', N'dbo', 'TABLE', N'AppPlans', 'COLUMN', N'PublishedVersion';
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE major_id = OBJECT_ID('dbo.AppPlans')
          AND minor_id = COLUMNPROPERTY(OBJECT_ID('dbo.AppPlans'), 'PublishedAt', 'ColumnId')
          AND name = 'MS_Description')
    EXEC sp_addextendedproperty 'MS_Description', N'最近发布时间(UTC)',
        'SCHEMA', N'dbo', 'TABLE', N'AppPlans', 'COLUMN', N'PublishedAt';
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE major_id = OBJECT_ID('dbo.AppPlans')
          AND minor_id = COLUMNPROPERTY(OBJECT_ID('dbo.AppPlans'), 'PublishedBy', 'ColumnId')
          AND name = 'MS_Description')
    EXEC sp_addextendedproperty 'MS_Description', N'最近发布者',
        'SCHEMA', N'dbo', 'TABLE', N'AppPlans', 'COLUMN', N'PublishedBy';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "AppPlans");

            migrationBuilder.DropColumn(
                name: "PublishedBy",
                table: "AppPlans");

            migrationBuilder.DropColumn(
                name: "PublishedDslJson",
                table: "AppPlans");

            migrationBuilder.DropColumn(
                name: "PublishedVersion",
                table: "AppPlans");
        }
    }
}
