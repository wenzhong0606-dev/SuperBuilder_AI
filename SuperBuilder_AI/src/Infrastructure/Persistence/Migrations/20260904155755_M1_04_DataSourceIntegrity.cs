using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M1_04_DataSourceIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DataSources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "名称（展示用，保留原始大小写）",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // M1-04：先将可能存在的 NULL Enabled 回填为 1，再改为非空，避免 AlterColumn 因 NULL 行失败。
            migrationBuilder.Sql("UPDATE [DataSources] SET [Enabled] = 1 WHERE [Enabled] IS NULL;");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "DataSources",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "是否启用",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DbType",
                table: "DataSources",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                comment: "数据库类型(MYSQL/SQLSERVER/POSTGRESQL)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "数据库类型");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                table: "DataSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                comment: "连接字符串（敏感，禁止日志记录）",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "连接字符串");

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "DataSources",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "最近连接测试错误码(仅异常类型名,脱敏)");

            migrationBuilder.AddColumn<string>(
                name: "LastTestStatus",
                table: "DataSources",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                comment: "最近连接测试状态(Ok/Failed/Unknown)");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTestTime",
                table: "DataSources",
                type: "datetime2",
                nullable: true,
                comment: "最近连接测试时间(UTC)");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "DataSources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "规范化名称（小写去空白），租户内唯一键");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_TenantId_NormalizedName",
                table: "DataSources",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataSources_TenantId_NormalizedName",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "LastTestStatus",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "LastTestTime",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "DataSources");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DataSources",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "名称（展示用，保留原始大小写）");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "DataSources",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true,
                oldComment: "是否启用");

            migrationBuilder.AlterColumn<string>(
                name: "DbType",
                table: "DataSources",
                type: "nvarchar(max)",
                nullable: true,
                comment: "数据库类型",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldComment: "数据库类型(MYSQL/SQLSERVER/POSTGRESQL)");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                table: "DataSources",
                type: "nvarchar(max)",
                nullable: true,
                comment: "连接字符串",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true,
                oldComment: "连接字符串（敏感，禁止日志记录）");
        }
    }
}
