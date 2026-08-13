using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBulider_AI.Migrations
{
    /// <inheritdoc />
    public partial class Phase145SemanticOptimization2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BusinessKey",
                table: "MetadataSemantics",
                newName: "EmbeddingModel");

            migrationBuilder.AddColumn<int>(
                name: "VectorDimension",
                table: "MetadataSemantics",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VectorDimension",
                table: "MetadataSemantics");

            migrationBuilder.RenameColumn(
                name: "EmbeddingModel",
                table: "MetadataSemantics",
                newName: "BusinessKey");
        }
    }
}
