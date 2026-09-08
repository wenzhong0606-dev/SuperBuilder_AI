using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M1_02_TenantAndSettingIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DataType",
                table: "TenantSettings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                comment: "值类型(string|int|bool|json)",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldComment: "值类型");

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "TenantSettings",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否锁定(租户不可覆盖)");

            migrationBuilder.AlterColumn<string>(
                name: "TenantName",
                table: "Tenants",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "租户名称",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "租户名称");

            migrationBuilder.AlterColumn<string>(
                name: "TenantCode",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "租户编码（规范化小写存储）",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true,
                oldComment: "租户编码");

            migrationBuilder.AddColumn<DateTime>(
                name: "DisabledAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true,
                comment: "停用时间(UTC)");

            migrationBuilder.AddColumn<long>(
                name: "DisabledByUserId",
                table: "Tenants",
                type: "bigint",
                nullable: true,
                comment: "停用操作者用户Id");

            migrationBuilder.AddColumn<string>(
                name: "DisabledReason",
                table: "Tenants",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                comment: "停用原因");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DisabledByUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DisabledReason",
                table: "Tenants");

            migrationBuilder.AlterColumn<string>(
                name: "DataType",
                table: "TenantSettings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                comment: "值类型",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldComment: "值类型(string|int|bool|json)");

            migrationBuilder.AlterColumn<string>(
                name: "TenantName",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true,
                comment: "租户名称",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "租户名称");

            migrationBuilder.AlterColumn<string>(
                name: "TenantCode",
                table: "Tenants",
                type: "nvarchar(450)",
                nullable: true,
                comment: "租户编码",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "租户编码（规范化小写存储）");
        }
    }
}
