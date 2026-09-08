using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRowLevelSecurityPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RowLevelSecurityPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    MetadataTableId = table.Column<long>(type: "bigint", nullable: false),
                    MetadataColumnId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectType = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: true),
                    SubjectKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SubjectValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Effect = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RowLevelSecurityPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RowLevelSecurityPolicies_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RowLevelSecurityPolicies_MetadataColumns_MetadataColumnId",
                        column: x => x.MetadataColumnId,
                        principalTable: "MetadataColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RowLevelSecurityPolicies_MetadataTables_MetadataTableId",
                        column: x => x.MetadataTableId,
                        principalTable: "MetadataTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_DataSourceId",
                table: "RowLevelSecurityPolicies",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_MetadataColumnId",
                table: "RowLevelSecurityPolicies",
                column: "MetadataColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_MetadataTableId",
                table: "RowLevelSecurityPolicies",
                column: "MetadataTableId");

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_TenantId_DataSourceId_MetadataTableId_Enabled",
                table: "RowLevelSecurityPolicies",
                columns: new[] { "TenantId", "DataSourceId", "MetadataTableId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_TenantId_SubjectType_SubjectId",
                table: "RowLevelSecurityPolicies",
                columns: new[] { "TenantId", "SubjectType", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RowLevelSecurityPolicies");
        }
    }
}
