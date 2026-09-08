using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M2_02_PlatformAdminScopeAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ManagementTargetTenantId",
                table: "AuditLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlatformAdminTenantScopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "授权时间(UTC)"),
                    GrantedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "授权操作者")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAdminTenantScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformAdminTenantScopes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlatformAdminTenantScopes_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "平台管理员租户范围绑定");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdminTenantScopes_AdminUserId",
                table: "PlatformAdminTenantScopes",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdminTenantScopes_AdminUserId_TenantId",
                table: "PlatformAdminTenantScopes",
                columns: new[] { "AdminUserId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdminTenantScopes_TenantId",
                table: "PlatformAdminTenantScopes",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAdminTenantScopes");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ManagementTargetTenantId",
                table: "AuditLogs");
        }
    }
}
