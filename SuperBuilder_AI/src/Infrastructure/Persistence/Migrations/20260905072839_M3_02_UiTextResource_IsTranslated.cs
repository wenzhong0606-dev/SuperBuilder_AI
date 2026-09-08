using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M3_02_UiTextResource_IsTranslated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTranslated",
                table: "UiTextResources",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "是否已翻译（新建语言从其它语言复制键集合时标记待翻译）");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTranslated",
                table: "UiTextResources");
        }
    }
}
