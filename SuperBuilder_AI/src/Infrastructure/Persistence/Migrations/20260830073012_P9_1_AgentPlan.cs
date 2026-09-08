using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P9_1_AgentPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentPlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=全局模板）"),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "业务编码"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "名称"),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true, comment: "描述"),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "状态"),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "DSL版本"),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "DSL文档（结构化，非裸HTML）"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPlans", x => x.Id);
                },
                comment: "Agent计划");

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlans_TenantId_Code",
                table: "AgentPlans",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlans_TenantId_Status",
                table: "AgentPlans",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPlans");
        }
    }
}
