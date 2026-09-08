using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataSemanticVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "语义Embedding文本");

            migrationBuilder.AddColumn<string>(
                name: "VectorId",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "Qdrant向量ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "VectorId",
                table: "MetadataSemantics");
        }
    }
}
