using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M3_01_TenantUiLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantUiLanguages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户"),
                    UiLanguageId = table.Column<long>(type: "bigint", nullable: false, comment: "平台语言目录 Id"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否启用（租户范围内）"),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "是否为租户默认语言（每租户恰一个）"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, comment: "排序"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUiLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUiLanguages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantUiLanguages_UiLanguages_UiLanguageId",
                        column: x => x.UiLanguageId,
                        principalTable: "UiLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "租户界面语言关系（替代 localization:availableCultures/defaultCulture JSON）");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUiLanguages_TenantId_IsDefault",
                table: "TenantUiLanguages",
                columns: new[] { "TenantId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUiLanguages_TenantId_UiLanguageId",
                table: "TenantUiLanguages",
                columns: new[] { "TenantId", "UiLanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUiLanguages_UiLanguageId",
                table: "TenantUiLanguages",
                column: "UiLanguageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantUiLanguages");
        }
    }
}
