using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M7_11_PublishIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DraftRevision",
                table: "AppPlans",
                type: "int",
                nullable: false,
                defaultValue: 1,
                comment: "草稿修订乐观并发令牌(每次编辑+1,发布校验用)");

            migrationBuilder.CreateTable(
                name: "AppPublishIdempotencies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "作用域租户"),
                    AppCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "应用业务编码"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "客户端幂等键(UUID)"),
                    ExpectedDraftRevision = table.Column<int>(type: "int", nullable: false, comment: "发布时的期望草稿修订号"),
                    PublishedVersion = table.Column<int>(type: "int", nullable: false, comment: "成功发布固化的版本号(同键重入返回此值)"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "记录创建时间(UTC)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPublishIdempotencies", x => x.Id);
                },
                comment: "应用发布/回滚幂等记录");

            migrationBuilder.CreateIndex(
                name: "IX_AppPublishIdempotencies_Tenant_AppCode_Key",
                table: "AppPublishIdempotencies",
                columns: new[] { "TenantId", "AppCode", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPublishIdempotencies");

            migrationBuilder.DropColumn(
                name: "DraftRevision",
                table: "AppPlans");
        }
    }
}
