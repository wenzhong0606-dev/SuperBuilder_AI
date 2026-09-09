using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M7_11_AskQuerySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AskQuerySnapshots",
                columns: table => new
                {
                    TurnId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "查询引用标识(GUID)"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户"),
                    UserId = table.Column<long>(type: "bigint", nullable: false, comment: "快照创建者"),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false, comment: "解析数据源Id（运行时硬约束）"),
                    EntityCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "主表业务实体语义名"),
                    QueryPlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "允许查询的QueryPlan语义(JSON,RLS注入前截取)"),
                    RequestHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "请求摘要哈希(创建幂等冲突检测)"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "过期时间(UTC)"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AskQuerySnapshots", x => x.TurnId);
                },
                comment: "Ask 查询快照（应用运行时引用）");

            migrationBuilder.CreateIndex(
                name: "IX_AskQuerySnapshots_ExpiresAt",
                table: "AskQuerySnapshots",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AskQuerySnapshots_TenantId_UserId",
                table: "AskQuerySnapshots",
                columns: new[] { "TenantId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AskQuerySnapshots");
        }
    }
}
