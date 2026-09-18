using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MetadataVersionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName",
                table: "MetadataTables");

            migrationBuilder.DropIndex(
                name: "IX_MetadataScanJobs_DataSourceId",
                table: "MetadataScanJobs");

            migrationBuilder.AddColumn<int>(
                name: "MetadataVersion",
                table: "MetadataTables",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "元数据版本号(§L 版本生命周期)");

            migrationBuilder.AddColumn<int>(
                name: "ObjectKind",
                table: "MetadataTables",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "对象类型(表/视图,§L/§10.1)");

            migrationBuilder.AddColumn<int>(
                name: "MetadataVersion",
                table: "MetadataSemantics",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "元数据版本号(§L)");

            migrationBuilder.AddColumn<int>(
                name: "ActivatedVersion",
                table: "MetadataScanJobs",
                type: "int",
                nullable: true,
                comment: "激活版本号");

            migrationBuilder.AddColumn<int>(
                name: "BatchVersion",
                table: "MetadataScanJobs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "批次版本号(§L.1)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "MetadataScanJobs",
                type: "datetime2",
                nullable: true,
                comment: "取消时间(UTC)");

            migrationBuilder.AddColumn<string>(
                name: "FailedReason",
                table: "MetadataScanJobs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "结构化失败原因(脱敏)");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatUtc",
                table: "MetadataScanJobs",
                type: "datetime2",
                nullable: true,
                comment: "最近心跳(UTC)");

            migrationBuilder.AddColumn<long>(
                name: "OriginalJobId",
                table: "MetadataScanJobs",
                type: "bigint",
                nullable: true,
                comment: "失败项续扫来源任务Id");

            migrationBuilder.AddColumn<string>(
                name: "ScopeJson",
                table: "MetadataScanJobs",
                type: "nvarchar(max)",
                nullable: true,
                comment: "扫描范围JSON(ScanScope)");

            migrationBuilder.AddColumn<int>(
                name: "SeedVersion",
                table: "MetadataScanJobs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "种子版本号(§L.6)");

            migrationBuilder.AddColumn<int>(
                name: "MetadataVersion",
                table: "MetadataColumns",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "元数据版本号(§L 版本生命周期)");

            migrationBuilder.AddColumn<int>(
                name: "ActiveMetadataVersion",
                table: "DataSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NextMetadataVersion",
                table: "DataSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "VectorsBackfilled",
                table: "DataSources",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName",
                table: "MetadataTables",
                columns: new[] { "DataSourceId", "CatalogName", "SchemaName", "TableName", "MetadataVersion" },
                unique: true,
                filter: "[CatalogName] IS NOT NULL AND [SchemaName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataScanJobs_DataSourceId_Status",
                table: "MetadataScanJobs",
                columns: new[] { "DataSourceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_ds_active_scan",
                table: "MetadataScanJobs",
                column: "DataSourceId",
                unique: true,
                filter: "[Status] IN ('Queued','Running','Cancelling','Retrying')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName",
                table: "MetadataTables");

            migrationBuilder.DropIndex(
                name: "IX_MetadataScanJobs_DataSourceId_Status",
                table: "MetadataScanJobs");

            migrationBuilder.DropIndex(
                name: "ux_ds_active_scan",
                table: "MetadataScanJobs");

            migrationBuilder.DropColumn(
                name: "MetadataVersion",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "ObjectKind",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "MetadataVersion",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "ActivatedVersion",
                table: "MetadataScanJobs");

            migrationBuilder.DropColumn(
                name: "BatchVersion",
                table: "MetadataScanJobs");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "MetadataScanJobs");

            migrationBuilder.DropColumn(
                name: "FailedReason",
                table: "MetadataScanJobs");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatUtc",
                table: "MetadataScanJobs");

            migrationBuilder.DropColumn(
                name: "OriginalJobId",
                table: "MetadataScanJobs");

            migrationBuilder.DropColumn(
                name: "ScopeJson",
                table: "MetadataScanJobs");

            migrationBuilder.DropColumn(
                name: "SeedVersion",
                table: "MetadataScanJobs");

            migrationBuilder.DropColumn(
                name: "MetadataVersion",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "ActiveMetadataVersion",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "NextMetadataVersion",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "VectorsBackfilled",
                table: "DataSources");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName",
                table: "MetadataTables",
                columns: new[] { "DataSourceId", "CatalogName", "SchemaName", "TableName" },
                unique: true,
                filter: "[CatalogName] IS NOT NULL AND [SchemaName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataScanJobs_DataSourceId",
                table: "MetadataScanJobs",
                column: "DataSourceId");
        }
    }
}
