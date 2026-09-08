using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M7_03_AgentRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（运行恒归属某一租户）"),
                    PlanCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "Agent编码"),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "运行状态"),
                    CurrentStepOrder = table.Column<int>(type: "int", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "触发者"),
                    GrantedToolsJson = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "授权工具集合（JSON）"),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultSummary = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true, comment: "结果摘要"),
                    StepLogJson = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "每步执行结果（JSON 信封）"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                },
                comment: "Agent运行记录（M7-03 Agent Runtime）");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_Status",
                table: "AgentRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_TenantId_PlanCode",
                table: "AgentRuns",
                columns: new[] { "TenantId", "PlanCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRuns");
        }
    }
}
