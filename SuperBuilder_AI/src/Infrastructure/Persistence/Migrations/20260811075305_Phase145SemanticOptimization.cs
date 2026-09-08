using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase145SemanticOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataSemantics_MetadataColumns_MetadataColumnId",
                table: "MetadataSemantics");

            migrationBuilder.DropIndex(
                name: "IX_MetadataTables_DataSourceId",
                table: "MetadataTables");

            migrationBuilder.DropIndex(
                name: "IX_MetadataColumns_MetadataTableId",
                table: "MetadataColumns");

            migrationBuilder.AlterTable(
                name: "MetadataSemantics",
                comment: "字段AI语义",
                oldComment: "元数据语义");

            migrationBuilder.AlterTable(
                name: "MetadataColumns",
                comment: "元数据字段",
                oldComment: "元数据列");

            migrationBuilder.AlterColumn<string>(
                name: "VectorId",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                comment: "Qdrant向量ID",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "向量存储ID");

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "MetadataTables",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                table: "MetadataTables",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "表名");

            migrationBuilder.AlterColumn<string>(
                name: "TableComment",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "表注释");

            migrationBuilder.AlterColumn<string>(
                name: "SearchText",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                comment: "Embedding文本",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "搜索文本(Embedding)");

            migrationBuilder.AlterColumn<long>(
                name: "DataSourceId",
                table: "MetadataTables",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "数据源标识");

            migrationBuilder.AlterColumn<string>(
                name: "BusinessDomain",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "业务域");

            migrationBuilder.AlterColumn<string>(
                name: "VectorId",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "Qdrant语义向量ID",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "Qdrant向量ID");

            migrationBuilder.AlterColumn<long>(
                name: "MetadataColumnId",
                table: "MetadataSemantics",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "元数据列标识");

            migrationBuilder.AlterColumn<string>(
                name: "Keywords",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "关键词",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "关键词(JSON数组)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Confidence",
                table: "MetadataSemantics",
                type: "decimal(5,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)",
                oldNullable: true,
                oldComment: "置信度");

            migrationBuilder.AddColumn<string>(
                name: "BusinessKey",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VectorId",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                comment: "Qdrant字段向量ID",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "向量存储ID");

            migrationBuilder.AlterColumn<string>(
                name: "SearchText",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                comment: "字段Embedding文本",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "搜索文本(Embedding)");

            migrationBuilder.AlterColumn<long>(
                name: "MetadataTableId",
                table: "MetadataColumns",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "元数据表标识");

            migrationBuilder.AlterColumn<long>(
                name: "Length",
                table: "MetadataColumns",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "长度/精度");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPrimaryKey",
                table: "MetadataColumns",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true,
                oldComment: "是否主键");

            migrationBuilder.AlterColumn<bool>(
                name: "IsNullable",
                table: "MetadataColumns",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true,
                oldComment: "是否允许空值");

            migrationBuilder.AlterColumn<string>(
                name: "DataType",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "数据类型");

            migrationBuilder.AlterColumn<string>(
                name: "ColumnName",
                table: "MetadataColumns",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "列名");

            migrationBuilder.AlterColumn<string>(
                name: "ColumnComment",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "列注释");

            migrationBuilder.AddColumn<string>(
                name: "BusinessKey",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                comment: "字段业务唯一标识");

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "LearningRecords",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "Question",
                table: "LearningRecords",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "问题内容");

            migrationBuilder.AlterColumn<long>(
                name: "MetadataColumnId",
                table: "LearningRecords",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "元数据列标识");

            migrationBuilder.AlterColumn<string>(
                name: "Feedback",
                table: "LearningRecords",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "反馈内容");

            migrationBuilder.AlterColumn<bool>(
                name: "Correct",
                table: "LearningRecords",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true,
                oldComment: "回答是否正确");

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "DataSources",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "租户标识");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DataSources",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "名称");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "DataSources",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true,
                oldComment: "是否启用");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId_TableName",
                table: "MetadataTables",
                columns: new[] { "DataSourceId", "TableName" },
                unique: true,
                filter: "[TableName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataColumns_MetadataTableId_ColumnName",
                table: "MetadataColumns",
                columns: new[] { "MetadataTableId", "ColumnName" },
                unique: true,
                filter: "[MetadataTableId] IS NOT NULL AND [ColumnName] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataSemantics_MetadataColumns_MetadataColumnId",
                table: "MetadataSemantics",
                column: "MetadataColumnId",
                principalTable: "MetadataColumns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataSemantics_MetadataColumns_MetadataColumnId",
                table: "MetadataSemantics");

            migrationBuilder.DropIndex(
                name: "IX_MetadataTables_DataSourceId_TableName",
                table: "MetadataTables");

            migrationBuilder.DropIndex(
                name: "IX_MetadataColumns_MetadataTableId_ColumnName",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "BusinessKey",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "BusinessKey",
                table: "MetadataColumns");

            migrationBuilder.AlterTable(
                name: "MetadataSemantics",
                comment: "元数据语义",
                oldComment: "字段AI语义");

            migrationBuilder.AlterTable(
                name: "MetadataColumns",
                comment: "元数据列",
                oldComment: "元数据字段");

            migrationBuilder.AlterColumn<string>(
                name: "VectorId",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                comment: "向量存储ID",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "Qdrant向量ID");

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "MetadataTables",
                type: "bigint",
                nullable: false,
                comment: "租户标识",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                comment: "表名",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TableComment",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                comment: "表注释",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SearchText",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                comment: "搜索文本(Embedding)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "Embedding文本");

            migrationBuilder.AlterColumn<long>(
                name: "DataSourceId",
                table: "MetadataTables",
                type: "bigint",
                nullable: false,
                comment: "数据源标识",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "BusinessDomain",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true,
                comment: "业务域",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VectorId",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "Qdrant向量ID",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "Qdrant语义向量ID");

            migrationBuilder.AlterColumn<long>(
                name: "MetadataColumnId",
                table: "MetadataSemantics",
                type: "bigint",
                nullable: true,
                comment: "元数据列标识",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Keywords",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true,
                comment: "关键词(JSON数组)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "关键词");

            migrationBuilder.AlterColumn<decimal>(
                name: "Confidence",
                table: "MetadataSemantics",
                type: "decimal(5,4)",
                nullable: true,
                comment: "置信度",
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VectorId",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                comment: "向量存储ID",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "Qdrant字段向量ID");

            migrationBuilder.AlterColumn<string>(
                name: "SearchText",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                comment: "搜索文本(Embedding)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "字段Embedding文本");

            migrationBuilder.AlterColumn<long>(
                name: "MetadataTableId",
                table: "MetadataColumns",
                type: "bigint",
                nullable: true,
                comment: "元数据表标识",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "Length",
                table: "MetadataColumns",
                type: "bigint",
                nullable: true,
                comment: "长度/精度",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPrimaryKey",
                table: "MetadataColumns",
                type: "bit",
                nullable: true,
                comment: "是否主键",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsNullable",
                table: "MetadataColumns",
                type: "bit",
                nullable: true,
                comment: "是否允许空值",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DataType",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                comment: "数据类型",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ColumnName",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                comment: "列名",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ColumnComment",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                comment: "列注释",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "LearningRecords",
                type: "bigint",
                nullable: true,
                comment: "租户标识",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Question",
                table: "LearningRecords",
                type: "nvarchar(max)",
                nullable: true,
                comment: "问题内容",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "MetadataColumnId",
                table: "LearningRecords",
                type: "bigint",
                nullable: true,
                comment: "元数据列标识",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Feedback",
                table: "LearningRecords",
                type: "nvarchar(max)",
                nullable: true,
                comment: "反馈内容",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Correct",
                table: "LearningRecords",
                type: "bit",
                nullable: true,
                comment: "回答是否正确",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "DataSources",
                type: "bigint",
                nullable: true,
                comment: "租户标识",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DataSources",
                type: "nvarchar(max)",
                nullable: true,
                comment: "名称",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                table: "DataSources",
                type: "bit",
                nullable: true,
                comment: "是否启用",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId",
                table: "MetadataTables",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataColumns_MetadataTableId",
                table: "MetadataColumns",
                column: "MetadataTableId");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataSemantics_MetadataColumns_MetadataColumnId",
                table: "MetadataSemantics",
                column: "MetadataColumnId",
                principalTable: "MetadataColumns",
                principalColumn: "Id");
        }
    }
}
