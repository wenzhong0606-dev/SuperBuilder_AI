using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Migrations
{
    /// <inheritdoc />
    public partial class P7_1_Theme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Themes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=内置/全局模板）"),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "主题键（同租户内唯一）"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "主题名称"),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false, comment: "是否内置主题"),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "DSL版本"),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "主题DSL文档（结构化令牌，非CSS/HTML）"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Themes", x => x.Id);
                },
                comment: "主题");

            migrationBuilder.CreateIndex(
                name: "IX_Themes_TenantId",
                table: "Themes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Themes_TenantId_Key",
                table: "Themes",
                columns: new[] { "TenantId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Themes");
        }
    }
}
