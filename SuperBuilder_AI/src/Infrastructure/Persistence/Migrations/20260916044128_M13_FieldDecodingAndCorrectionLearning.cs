using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M13_FieldDecodingAndCorrectionLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DictCategoryValue",
                table: "MetadataColumns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "字典分类值");

            migrationBuilder.AddColumn<long>(
                name: "DictConfigId",
                table: "MetadataColumns",
                type: "bigint",
                nullable: true,
                comment: "关联字典配置Id");

            migrationBuilder.AddColumn<bool>(
                name: "IsDictBacked",
                table: "MetadataColumns",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否跨源字典译码");

            migrationBuilder.AddColumn<string>(
                name: "ReferencedColumn",
                table: "MetadataColumns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "外键目标列");

            migrationBuilder.AddColumn<string>(
                name: "ReferencedDisplayColumn",
                table: "MetadataColumns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "外键展示列");

            migrationBuilder.AddColumn<string>(
                name: "ReferencedTable",
                table: "MetadataColumns",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "外键目标表");

            migrationBuilder.AddColumn<string>(
                name: "ValueMapJson",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true,
                comment: "码值映射JSON");

            migrationBuilder.CreateTable(
                name: "MetadataDictionaryConfigs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "字典表名"),
                    CodeColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "角色:码值列"),
                    NameColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "角色:名称列"),
                    TypeColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "角色:分类列(可选)"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否启用"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataDictionaryConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataDictionaryConfigs_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataDictionaryConfigs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "跨源字典表配置");

            migrationBuilder.CreateTable(
                name: "QueryCorrectionRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: true, comment: "作用数据源(可选)"),
                    TriggerPattern = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "触发模式(归一化问句子串)"),
                    Kind = table.Column<int>(type: "int", nullable: false, comment: "规则类型(CorrectionKind)"),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "规则载荷JSON"),
                    HitCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "命中次数"),
                    LastMatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueryCorrectionRules", x => x.Id);
                },
                comment: "自主学习纠错规则");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataDictionaryConfigs_DataSourceId",
                table: "MetadataDictionaryConfigs",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataDictionaryConfigs_Tenant_DataSource_Table",
                table: "MetadataDictionaryConfigs",
                columns: new[] { "TenantId", "DataSourceId", "TableName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueryCorrectionRules_Tenant_User_Trigger",
                table: "QueryCorrectionRules",
                columns: new[] { "TenantId", "UserId", "TriggerPattern" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueryCorrectionRules_TenantId",
                table: "QueryCorrectionRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QueryCorrectionRules_UserId",
                table: "QueryCorrectionRules",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetadataDictionaryConfigs");

            migrationBuilder.DropTable(
                name: "QueryCorrectionRules");

            migrationBuilder.DropColumn(
                name: "DictCategoryValue",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "DictConfigId",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "IsDictBacked",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "ReferencedColumn",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "ReferencedDisplayColumn",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "ReferencedTable",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "ValueMapJson",
                table: "MetadataColumns");
        }
    }
}
