using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.src.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M7_09_CustomComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomComponents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ComponentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "结构化组件草稿DSL（禁止HTML/脚本）"),
                    PublishedDslJson = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "已发布组件DSL快照"),
                    PublishedVersion = table.Column<int>(type: "int", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomComponents", x => x.Id);
                },
                comment: "租户自定义组件定义");

            migrationBuilder.CreateTable(
                name: "CustomComponentVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComponentId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ComponentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RolledBackFromVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomComponentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomComponentVersions_CustomComponents_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "CustomComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "自定义组件发布版本快照");

            migrationBuilder.CreateIndex(
                name: "IX_CustomComponents_TenantId_ComponentType",
                table: "CustomComponents",
                columns: new[] { "TenantId", "ComponentType" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomComponents_TenantId_Key",
                table: "CustomComponents",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomComponentVersions_ComponentId_Version",
                table: "CustomComponentVersions",
                columns: new[] { "ComponentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomComponentVersions_TenantId_ComponentId",
                table: "CustomComponentVersions",
                columns: new[] { "TenantId", "ComponentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomComponentVersions");

            migrationBuilder.DropTable(
                name: "CustomComponents");
        }
    }
}
