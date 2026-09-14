using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SuperBuilder_AI.Data;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SuperBIContext))]
[Migration("20260914062000_M13_MetadataScanRichProgress")]
public partial class M13_MetadataScanRichProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ProgressDetailsJson",
            table: "MetadataScanJobs",
            type: "nvarchar(max)",
            nullable: true,
            comment: "扫描富进度快照(JSON)");

        migrationBuilder.AddColumn<string>(
            name: "Stage",
            table: "MetadataScanJobs",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "Queued",
            comment: "当前扫描阶段");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ProgressDetailsJson", table: "MetadataScanJobs");
        migrationBuilder.DropColumn(name: "Stage", table: "MetadataScanJobs");
    }
}
