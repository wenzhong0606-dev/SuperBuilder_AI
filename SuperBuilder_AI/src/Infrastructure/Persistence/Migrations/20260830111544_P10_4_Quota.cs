using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Migrations
{
    /// <inheritdoc />
    public partial class P10_4_Quota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuotaPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=平台默认）"),
                    ResourceType = table.Column<int>(type: "int", nullable: false, comment: "资源类型"),
                    Limit = table.Column<long>(type: "bigint", nullable: false, comment: "上限"),
                    Window = table.Column<int>(type: "int", nullable: false, comment: "周期窗口"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotaPolicies", x => x.Id);
                },
                comment: "配额策略");

            migrationBuilder.CreateTable(
                name: "QuotaUsages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户"),
                    ResourceType = table.Column<int>(type: "int", nullable: false, comment: "资源类型"),
                    Used = table.Column<long>(type: "bigint", nullable: false, comment: "已用"),
                    PeriodKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "周期键"),
                    LastReset = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotaUsages", x => x.Id);
                },
                comment: "配额使用量");

            migrationBuilder.CreateIndex(
                name: "IX_QuotaPolicies_TenantId_ResourceType",
                table: "QuotaPolicies",
                columns: new[] { "TenantId", "ResourceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotaUsages_TenantId_ResourceType",
                table: "QuotaUsages",
                columns: new[] { "TenantId", "ResourceType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotaPolicies");

            migrationBuilder.DropTable(
                name: "QuotaUsages");
        }
    }
}
