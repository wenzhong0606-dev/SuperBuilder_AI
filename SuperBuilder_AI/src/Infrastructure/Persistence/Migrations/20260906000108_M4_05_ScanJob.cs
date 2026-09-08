using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4_05_ScanJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetadataScanJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "触发用户标识"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Queued", comment: "扫描状态"),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "进度百分比"),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "开始时间(UTC)"),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "结束时间(UTC)"),
                    TablesScanned = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "已扫描表数"),
                    ColumnsScanned = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "已扫描字段数"),
                    OrphansDetected = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "孤儿对象数"),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "错误码(仅异常类型名,脱敏)"),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "错误摘要(脱敏,不含连接串)"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataScanJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataScanJobs_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "元数据扫描任务");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataScanJobs_DataSourceId",
                table: "MetadataScanJobs",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataScanJobs_Status",
                table: "MetadataScanJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataScanJobs_TenantId",
                table: "MetadataScanJobs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetadataScanJobs");
        }
    }
}
