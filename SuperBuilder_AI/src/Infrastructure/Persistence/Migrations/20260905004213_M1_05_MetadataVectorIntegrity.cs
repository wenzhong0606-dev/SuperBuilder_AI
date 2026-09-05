using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.src.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M1_05_MetadataVectorIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataTables_DataSourceId_TableName",
                table: "MetadataTables");

            // 回填 NULL 行，避免后续 NOT NULL 变更失败。
            migrationBuilder.Sql("UPDATE [MetadataTables] SET [TableName] = N'table_' + CAST([Id] AS NVARCHAR(20)) WHERE [TableName] IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "VectorDimension",
                table: "MetadataTables",
                type: "int",
                nullable: true,
                comment: "向量维度",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                table: "MetadataTables",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "表名",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingModel",
                table: "MetadataTables",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "Embedding模型",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatalogName",
                table: "MetadataTables",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "目录名");

            migrationBuilder.AddColumn<string>(
                name: "SchemaName",
                table: "MetadataTables",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "模式名");

            migrationBuilder.AddColumn<string>(
                name: "VectorErrorCode",
                table: "MetadataTables",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "向量错误码");

            migrationBuilder.AddColumn<string>(
                name: "VectorStatus",
                table: "MetadataTables",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                comment: "向量状态");

            migrationBuilder.AddColumn<DateTime>(
                name: "VectorSyncTime",
                table: "MetadataTables",
                type: "datetime2",
                nullable: true,
                comment: "向量同步时间(UTC)");

            migrationBuilder.AlterColumn<int>(
                name: "VectorDimension",
                table: "MetadataSemantics",
                type: "int",
                nullable: true,
                comment: "向量维度",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Synonyms",
                table: "MetadataSemantics",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                comment: "同义词",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "同义词");

            migrationBuilder.Sql("UPDATE [MetadataSemantics] SET [Source] = N'Manual' WHERE [Source] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "MetadataSemantics",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Manual",
                comment: "来源",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "来源");

            migrationBuilder.AlterColumn<string>(
                name: "Keywords",
                table: "MetadataSemantics",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                comment: "关键词",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "关键词");

            migrationBuilder.AlterColumn<string>(
                name: "ExampleQuestions",
                table: "MetadataSemantics",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                comment: "示例问题",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "示例问题");

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingModel",
                table: "MetadataSemantics",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "Embedding模型",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Confidence",
                table: "MetadataSemantics",
                type: "decimal(5,4)",
                nullable: true,
                comment: "AI生成置信度(0-1)",
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VectorErrorCode",
                table: "MetadataSemantics",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "向量错误码");

            migrationBuilder.AddColumn<string>(
                name: "VectorStatus",
                table: "MetadataSemantics",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                comment: "向量状态");

            migrationBuilder.AddColumn<DateTime>(
                name: "VectorSyncTime",
                table: "MetadataSemantics",
                type: "datetime2",
                nullable: true,
                comment: "向量同步时间(UTC)");

            migrationBuilder.Sql("UPDATE [MetadataColumns] SET [ColumnName] = N'column_' + CAST([Id] AS NVARCHAR(20)) WHERE [ColumnName] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "ColumnName",
                table: "MetadataColumns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "列名",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "MetadataColumns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "Embedding模型");

            migrationBuilder.AddColumn<string>(
                name: "NativeType",
                table: "MetadataColumns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "原生类型");

            migrationBuilder.AddColumn<int>(
                name: "Ordinal",
                table: "MetadataColumns",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "列序号");

            migrationBuilder.AddColumn<int>(
                name: "Precision",
                table: "MetadataColumns",
                type: "int",
                nullable: true,
                comment: "精度");

            migrationBuilder.AddColumn<int>(
                name: "Scale",
                table: "MetadataColumns",
                type: "int",
                nullable: true,
                comment: "小数位");

            migrationBuilder.AddColumn<int>(
                name: "VectorDimension",
                table: "MetadataColumns",
                type: "int",
                nullable: true,
                comment: "向量维度");

            migrationBuilder.AddColumn<string>(
                name: "VectorErrorCode",
                table: "MetadataColumns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "向量错误码");

            migrationBuilder.AddColumn<string>(
                name: "VectorStatus",
                table: "MetadataColumns",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                comment: "向量状态");

            migrationBuilder.AddColumn<DateTime>(
                name: "VectorSyncTime",
                table: "MetadataColumns",
                type: "datetime2",
                nullable: true,
                comment: "向量同步时间(UTC)");

            migrationBuilder.Sql("UPDATE [LearningRecords] SET [TenantId] = 1 WHERE [TenantId] IS NULL;");

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "LearningRecords",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId",
                table: "MetadataTables",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName",
                table: "MetadataTables",
                columns: new[] { "DataSourceId", "CatalogName", "SchemaName", "TableName" },
                unique: true,
                filter: "[CatalogName] IS NOT NULL AND [SchemaName] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MetadataSemantics_Confidence",
                table: "MetadataSemantics",
                sql: "[Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 1)");

            migrationBuilder.CreateIndex(
                name: "IX_LearningRecords_MetadataColumnId",
                table: "LearningRecords",
                column: "MetadataColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningRecords_TenantId",
                table: "LearningRecords",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningRecords_MetadataColumns_MetadataColumnId",
                table: "LearningRecords",
                column: "MetadataColumnId",
                principalTable: "MetadataColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LearningRecords_Tenants_TenantId",
                table: "LearningRecords",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningRecords_MetadataColumns_MetadataColumnId",
                table: "LearningRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LearningRecords_Tenants_TenantId",
                table: "LearningRecords");

            migrationBuilder.DropIndex(
                name: "IX_MetadataTables_DataSourceId",
                table: "MetadataTables");

            migrationBuilder.DropIndex(
                name: "IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName",
                table: "MetadataTables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MetadataSemantics_Confidence",
                table: "MetadataSemantics");

            migrationBuilder.DropIndex(
                name: "IX_LearningRecords_MetadataColumnId",
                table: "LearningRecords");

            migrationBuilder.DropIndex(
                name: "IX_LearningRecords_TenantId",
                table: "LearningRecords");

            migrationBuilder.DropColumn(
                name: "CatalogName",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "SchemaName",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "VectorErrorCode",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "VectorStatus",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "VectorSyncTime",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "VectorErrorCode",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "VectorStatus",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "VectorSyncTime",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "NativeType",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "Ordinal",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "Precision",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "Scale",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "VectorDimension",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "VectorErrorCode",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "VectorStatus",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "VectorSyncTime",
                table: "MetadataColumns");

            migrationBuilder.AlterColumn<int>(
                name: "VectorDimension",
                table: "MetadataTables",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComment: "向量维度");

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                table: "MetadataTables",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "表名");

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingModel",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "Embedding模型");

            migrationBuilder.AlterColumn<int>(
                name: "VectorDimension",
                table: "MetadataSemantics",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComment: "向量维度");

            migrationBuilder.AlterColumn<string>(
                name: "Synonyms",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "同义词",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true,
                oldComment: "同义词");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "来源",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "Manual",
                oldComment: "来源");

            migrationBuilder.AlterColumn<string>(
                name: "Keywords",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "关键词",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true,
                oldComment: "关键词");

            migrationBuilder.AlterColumn<string>(
                name: "ExampleQuestions",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "示例问题",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true,
                oldComment: "示例问题");

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingModel",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "Embedding模型");

            migrationBuilder.AlterColumn<decimal>(
                name: "Confidence",
                table: "MetadataSemantics",
                type: "decimal(5,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)",
                oldNullable: true,
                oldComment: "AI生成置信度(0-1)");

            migrationBuilder.AlterColumn<string>(
                name: "ColumnName",
                table: "MetadataColumns",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "列名");

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "LearningRecords",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId_TableName",
                table: "MetadataTables",
                columns: new[] { "DataSourceId", "TableName" },
                unique: true,
                filter: "[TableName] IS NOT NULL");
        }
    }
}
