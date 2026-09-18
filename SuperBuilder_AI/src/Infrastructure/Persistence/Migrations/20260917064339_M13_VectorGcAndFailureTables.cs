using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M13_VectorGcAndFailureTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetadataScanJobFailures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalJobId = table.Column<long>(type: "bigint", nullable: true),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    Database = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Schema = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    FirstFailedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastFailedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resolved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataScanJobFailures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetadataVectorGcRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    OldVersion = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataVectorGcRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataScanJobFailures_JobId",
                table: "MetadataScanJobFailures",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataVectorGcRequests_Status",
                table: "MetadataVectorGcRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetadataScanJobFailures");

            migrationBuilder.DropTable(
                name: "MetadataVectorGcRequests");
        }
    }
}
