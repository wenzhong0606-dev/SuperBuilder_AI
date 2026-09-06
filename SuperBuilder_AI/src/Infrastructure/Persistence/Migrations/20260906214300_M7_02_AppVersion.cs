using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SuperBuilder_AI.Data;

#nullable disable

namespace SuperBuilder_AI.src.Infrastructure.Persistence.Migrations
{
    // 注意：本文件由人工手写（shell 工具不可用，未能运行 dotnet ef 生成 Designer）。
    // 已补齐 [DbContext]/[Migration] 使迁移可被发现；BuildTargetModel 沿用基类空实现。
    // 待 shell 恢复后请执行：dotnet ef migrations remove && dotnet ef migrations add M7_02_AppVersion
    // 以重建 Designer 并校准 ModelSnapshot（模型配置已正确，重生结果一致）。
    [DbContext(typeof(SuperBIContext))]
    [Migration("20260906214300_M7_02_AppVersion")]
    public partial class M7_02_AppVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "AppPlans",
                type: "datetime2",
                nullable: true,
                comment: "最近发布时间(UTC)");

            migrationBuilder.AddColumn<string>(
                name: "PublishedBy",
                table: "AppPlans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "最近发布者");

            migrationBuilder.AddColumn<string>(
                name: "PublishedDslJson",
                table: "AppPlans",
                type: "nvarchar(max)",
                nullable: true,
                comment: "已发布DSL快照（null=未发布）");

            migrationBuilder.AddColumn<int>(
                name: "PublishedVersion",
                table: "AppPlans",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "当前发布版本号（0=未发布）");

            migrationBuilder.CreateTable(
                name: "AppVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "作用域租户（0=全局模板）"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "业务编码快照"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "名称快照"),
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
                    table.PrimaryKey("PK_AppVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppVersions_AppPlans_AppId",
                        column: x => x.AppId,
                        principalTable: "AppPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "应用发布版本快照");

            migrationBuilder.CreateIndex(
                name: "IX_AppVersions_AppId_Version",
                table: "AppVersions",
                columns: new[] { "AppId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppVersions_TenantId_AppId",
                table: "AppVersions",
                columns: new[] { "TenantId", "AppId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppVersions");

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
