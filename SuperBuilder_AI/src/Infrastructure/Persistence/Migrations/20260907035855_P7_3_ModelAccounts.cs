using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P7_3_ModelAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（>0）"),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "供应商标识(Qwen/OpenAI/...)"),
                    ModelId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "模型标识(qwen-plus/gpt-4o/...)"),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "展示名"),
                    EncryptedKey = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, comment: "AES-GCM 密文(禁止日志记录/明文下发)"),
                    MaskedKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "展示掩码(如 sk-***1234)"),
                    Note = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, comment: "备注"),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "是否为租户默认模型(每租户至多一个)"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelAccounts", x => x.Id);
                },
                comment: "模型账号绑定（BYO 加密存储）");

            migrationBuilder.CreateIndex(
                name: "IX_ModelAccounts_TenantId",
                table: "ModelAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelAccounts_TenantId_Provider_ModelId",
                table: "ModelAccounts",
                columns: new[] { "TenantId", "Provider", "ModelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelAccounts");
        }
    }
}
