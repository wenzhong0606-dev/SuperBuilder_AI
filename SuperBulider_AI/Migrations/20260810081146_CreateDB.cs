using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBulider_AI.Migrations
{
    /// <inheritdoc />
    public partial class CreateDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: true, comment: "租户标识"),
                    Question = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "问题内容"),
                    MetadataColumnId = table.Column<long>(type: "bigint", nullable: true, comment: "元数据列标识"),
                    Correct = table.Column<bool>(type: "bit", nullable: true, comment: "回答是否正确"),
                    Feedback = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "反馈内容"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningRecords", x => x.Id);
                },
                comment: "学习记录");

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantCode = table.Column<string>(type: "nvarchar(450)", nullable: true, comment: "租户编码"),
                    TenantName = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "租户名称"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, comment: "是否启用"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                },
                comment: "租户");

            migrationBuilder.CreateTable(
                name: "DataSources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: true, comment: "租户标识"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "名称"),
                    DbType = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "数据库类型"),
                    ConnectionString = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "连接字符串"),
                    Enabled = table.Column<bool>(type: "bit", nullable: true, comment: "是否启用"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataSources_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                },
                comment: "数据源");

            migrationBuilder.CreateTable(
                name: "MetadataTables",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "租户标识"),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false, comment: "数据源标识"),
                    TableName = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "表名"),
                    TableComment = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "表注释"),
                    BusinessDomain = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "业务域"),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "搜索文本(Embedding)"),
                    VectorId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "向量存储ID"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataTables_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "元数据表");

            migrationBuilder.CreateTable(
                name: "MetadataColumns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetadataTableId = table.Column<long>(type: "bigint", nullable: true, comment: "元数据表标识"),
                    ColumnName = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "列名"),
                    ColumnComment = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "列注释"),
                    DataType = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "数据类型"),
                    Length = table.Column<long>(type: "bigint", nullable: true, comment: "长度/精度"),
                    IsNullable = table.Column<bool>(type: "bit", nullable: true, comment: "是否允许空值"),
                    IsPrimaryKey = table.Column<bool>(type: "bit", nullable: true, comment: "是否主键"),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "搜索文本(Embedding)"),
                    VectorId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "向量存储ID"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataColumns_MetadataTables_MetadataTableId",
                        column: x => x.MetadataTableId,
                        principalTable: "MetadataTables",
                        principalColumn: "Id");
                },
                comment: "元数据列");

            migrationBuilder.CreateTable(
                name: "MetadataSemantics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetadataColumnId = table.Column<long>(type: "bigint", nullable: true, comment: "元数据列标识"),
                    BusinessMeaning = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "业务含义"),
                    Keywords = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "关键词(JSON数组)"),
                    Synonyms = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "同义词"),
                    ExampleQuestions = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "示例问题"),
                    BusinessDomain = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "业务域"),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: true, comment: "置信度"),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "来源"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataSemantics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataSemantics_MetadataColumns_MetadataColumnId",
                        column: x => x.MetadataColumnId,
                        principalTable: "MetadataColumns",
                        principalColumn: "Id");
                },
                comment: "元数据语义");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_TenantId",
                table: "DataSources",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataColumns_MetadataTableId",
                table: "MetadataColumns",
                column: "MetadataTableId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataSemantics_MetadataColumnId",
                table: "MetadataSemantics",
                column: "MetadataColumnId",
                unique: true,
                filter: "[MetadataColumnId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId",
                table: "MetadataTables",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantCode",
                table: "Tenants",
                column: "TenantCode",
                unique: true,
                filter: "[TenantCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningRecords");

            migrationBuilder.DropTable(
                name: "MetadataSemantics");

            migrationBuilder.DropTable(
                name: "MetadataColumns");

            migrationBuilder.DropTable(
                name: "MetadataTables");

            migrationBuilder.DropTable(
                name: "DataSources");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
