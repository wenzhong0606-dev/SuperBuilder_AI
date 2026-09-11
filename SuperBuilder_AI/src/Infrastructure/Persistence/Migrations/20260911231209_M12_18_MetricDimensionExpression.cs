using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M12_18_MetricDimensionExpression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataType",
                table: "BusinessEntityMetrics",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Expression",
                table: "BusinessEntityMetrics",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataType",
                table: "BusinessEntityDimensions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Expression",
                table: "BusinessEntityDimensions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataType",
                table: "BusinessEntityMetrics");

            migrationBuilder.DropColumn(
                name: "Expression",
                table: "BusinessEntityMetrics");

            migrationBuilder.DropColumn(
                name: "DataType",
                table: "BusinessEntityDimensions");

            migrationBuilder.DropColumn(
                name: "Expression",
                table: "BusinessEntityDimensions");
        }
    }
}
