using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M13_DictionaryActiveFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveFilterColumn",
                table: "MetadataDictionaryConfigs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "角色:有效行过滤列(可选)");

            migrationBuilder.AddColumn<string>(
                name: "ActiveFilterValue",
                table: "MetadataDictionaryConfigs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "有效行过滤值(默认0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveFilterColumn",
                table: "MetadataDictionaryConfigs");

            migrationBuilder.DropColumn(
                name: "ActiveFilterValue",
                table: "MetadataDictionaryConfigs");
        }
    }
}
