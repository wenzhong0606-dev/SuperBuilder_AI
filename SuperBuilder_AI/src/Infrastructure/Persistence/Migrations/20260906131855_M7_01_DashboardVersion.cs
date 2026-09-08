using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M7_01_DashboardVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Dashboards",
                type: "datetime2",
                nullable: true,
                comment: "最近发布时间(UTC)");

            migrationBuilder.AddColumn<string>(
                name: "PublishedBy",
                table: "Dashboards",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "最近发布者");

            migrationBuilder.AddColumn<string>(
                name: "PublishedDslJson",
                table: "Dashboards",
                type: "nvarchar(max)",
                nullable: true,
                comment: "已发布DSL快照（null=未发布）");

            migrationBuilder.AddColumn<int>(
                name: "PublishedVersion",
                table: "Dashboards",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "当前发布版本号（0=未发布）");

            migrationBuilder.CreateTable(
                name: "DashboardVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DashboardId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "作用域租户（0=全局模板）"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "业务编码快照"),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "标题快照"),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true, comment: "描述快照"),
                    ThemeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "主题键快照"),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "DSL版本快照"),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "发布时刻固化的DSL文档（只读快照）"),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "发布者"),
                    RolledBackFromVersion = table.Column<int>(type: "int", nullable: true, comment: "回滚来源版本号（非回滚为null）"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardVersions_Dashboards_DashboardId",
                        column: x => x.DashboardId,
                        principalTable: "Dashboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "仪表盘发布版本快照");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardVersions_DashboardId_Version",
                table: "DashboardVersions",
                columns: new[] { "DashboardId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardVersions_TenantId_DashboardId",
                table: "DashboardVersions",
                columns: new[] { "TenantId", "DashboardId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DashboardVersions");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "PublishedBy",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "PublishedDslJson",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "PublishedVersion",
                table: "Dashboards");
        }
    }
}
