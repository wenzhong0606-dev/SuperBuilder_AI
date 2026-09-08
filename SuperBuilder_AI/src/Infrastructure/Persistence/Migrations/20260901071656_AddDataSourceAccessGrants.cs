using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSourceAccessGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataSourceAccessGrants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectType = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceAccessGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataSourceAccessGrants_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceAccessGrants_DataSourceId",
                table: "DataSourceAccessGrants",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceAccessGrants_TenantId_DataSourceId_SubjectType_SubjectId",
                table: "DataSourceAccessGrants",
                columns: new[] { "TenantId", "DataSourceId", "SubjectType", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceAccessGrants_TenantId_SubjectType_SubjectId",
                table: "DataSourceAccessGrants",
                columns: new[] { "TenantId", "SubjectType", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataSourceAccessGrants");
        }
    }
}
