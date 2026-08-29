using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Migrations
{
    /// <inheritdoc />
    public partial class P5_2_SemanticLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SemanticLabels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=全局共享）"),
                    ConceptType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "概念类型"),
                    ConceptId = table.Column<long>(type: "bigint", nullable: false, comment: "概念实体Id"),
                    Culture = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "语言标签"),
                    LabelKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "标签种类"),
                    Value = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false, comment: "标签文本"),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "来源"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, comment: "排序"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemanticLabels", x => x.Id);
                },
                comment: "语义多语言标签");

            migrationBuilder.CreateIndex(
                name: "IX_SemanticLabels_ConceptType_ConceptId_Culture",
                table: "SemanticLabels",
                columns: new[] { "ConceptType", "ConceptId", "Culture" });

            migrationBuilder.CreateIndex(
                name: "IX_SemanticLabels_TenantId_ConceptType_ConceptId_Culture_LabelKind_SortOrder",
                table: "SemanticLabels",
                columns: new[] { "TenantId", "ConceptType", "ConceptId", "Culture", "LabelKind", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SemanticLabels");
        }
    }
}
