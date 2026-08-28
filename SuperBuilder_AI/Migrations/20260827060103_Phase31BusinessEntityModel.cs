using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Migrations
{
    /// <inheritdoc />
    public partial class Phase31BusinessEntityModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessEntities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessDomain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SemanticText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantId1 = table.Column<long>(type: "bigint", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessEntities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessEntities_Tenants_TenantId1",
                        column: x => x.TenantId1,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                },
                comment: "业务实体");

            migrationBuilder.CreateTable(
                name: "BusinessEntityAttributes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessEntityId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SemanticType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsNullable = table.Column<bool>(type: "bit", nullable: false),
                    IsIdentifier = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessEntityAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessEntityAttributes_BusinessEntities_BusinessEntityId",
                        column: x => x.BusinessEntityId,
                        principalTable: "BusinessEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "业务实体属性");

            migrationBuilder.CreateTable(
                name: "BusinessEntityKeys",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessEntityId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    KeyType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessEntityKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessEntityKeys_BusinessEntities_BusinessEntityId",
                        column: x => x.BusinessEntityId,
                        principalTable: "BusinessEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "业务实体键");

            migrationBuilder.CreateTable(
                name: "BusinessEntityMetrics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessEntityId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SemanticType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Aggregation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsCalculated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessEntityMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessEntityMetrics_BusinessEntities_BusinessEntityId",
                        column: x => x.BusinessEntityId,
                        principalTable: "BusinessEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "业务实体指标");

            migrationBuilder.CreateTable(
                name: "BusinessEntityRelationships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceEntityId = table.Column<long>(type: "bigint", nullable: false),
                    TargetEntityId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelationshipType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cardinality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessEntityRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessEntityRelationships_BusinessEntities_SourceEntityId",
                        column: x => x.SourceEntityId,
                        principalTable: "BusinessEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessEntityRelationships_BusinessEntities_TargetEntityId",
                        column: x => x.TargetEntityId,
                        principalTable: "BusinessEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "业务实体关系");

            migrationBuilder.CreateTable(
                name: "PhysicalBindings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    MetadataTableId = table.Column<long>(type: "bigint", nullable: false),
                    MetadataColumnId = table.Column<long>(type: "bigint", nullable: false),
                    PhysicalRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BindingType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    BusinessEntityKeyId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessEntityAttributeId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessEntityMetricId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessEntityRelationshipId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalBindings", x => x.Id);
                    table.CheckConstraint("CK_PhysicalBindings_ExactlyOneOwner", "((CASE WHEN BusinessEntityKeyId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityAttributeId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityMetricId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityRelationshipId IS NOT NULL THEN 1 ELSE 0 END)) = 1");
                    table.ForeignKey(
                        name: "FK_PhysicalBindings_BusinessEntityAttributes_BusinessEntityAttributeId",
                        column: x => x.BusinessEntityAttributeId,
                        principalTable: "BusinessEntityAttributes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalBindings_BusinessEntityKeys_BusinessEntityKeyId",
                        column: x => x.BusinessEntityKeyId,
                        principalTable: "BusinessEntityKeys",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalBindings_BusinessEntityMetrics_BusinessEntityMetricId",
                        column: x => x.BusinessEntityMetricId,
                        principalTable: "BusinessEntityMetrics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalBindings_BusinessEntityRelationships_BusinessEntityRelationshipId",
                        column: x => x.BusinessEntityRelationshipId,
                        principalTable: "BusinessEntityRelationships",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysicalBindings_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalBindings_MetadataColumns_MetadataColumnId",
                        column: x => x.MetadataColumnId,
                        principalTable: "MetadataColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalBindings_MetadataTables_MetadataTableId",
                        column: x => x.MetadataTableId,
                        principalTable: "MetadataTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "业务语义到物理元数据的映射");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntities_TenantId_BusinessKey",
                table: "BusinessEntities",
                columns: new[] { "TenantId", "BusinessKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntities_TenantId1",
                table: "BusinessEntities",
                column: "TenantId1");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityAttributes_BusinessEntityId_IsIdentifier",
                table: "BusinessEntityAttributes",
                columns: new[] { "BusinessEntityId", "IsIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityAttributes_BusinessEntityId_Name",
                table: "BusinessEntityAttributes",
                columns: new[] { "BusinessEntityId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityKeys_BusinessEntityId_IsPrimary",
                table: "BusinessEntityKeys",
                columns: new[] { "BusinessEntityId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityKeys_BusinessEntityId_Name",
                table: "BusinessEntityKeys",
                columns: new[] { "BusinessEntityId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityMetrics_BusinessEntityId_Name",
                table: "BusinessEntityMetrics",
                columns: new[] { "BusinessEntityId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityRelationships_SourceEntityId_TargetEntityId_Name",
                table: "BusinessEntityRelationships",
                columns: new[] { "SourceEntityId", "TargetEntityId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityRelationships_TargetEntityId",
                table: "BusinessEntityRelationships",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_BusinessEntityAttributeId_IsActive",
                table: "PhysicalBindings",
                columns: new[] { "BusinessEntityAttributeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_BusinessEntityKeyId_IsActive",
                table: "PhysicalBindings",
                columns: new[] { "BusinessEntityKeyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_BusinessEntityMetricId_IsActive",
                table: "PhysicalBindings",
                columns: new[] { "BusinessEntityMetricId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_BusinessEntityRelationshipId_PhysicalRole_IsActive",
                table: "PhysicalBindings",
                columns: new[] { "BusinessEntityRelationshipId", "PhysicalRole", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_DataSourceId_MetadataTableId_MetadataColumnId_Priority",
                table: "PhysicalBindings",
                columns: new[] { "DataSourceId", "MetadataTableId", "MetadataColumnId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_MetadataColumnId",
                table: "PhysicalBindings",
                column: "MetadataColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_MetadataTableId",
                table: "PhysicalBindings",
                column: "MetadataTableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhysicalBindings");

            migrationBuilder.DropTable(
                name: "BusinessEntityAttributes");

            migrationBuilder.DropTable(
                name: "BusinessEntityKeys");

            migrationBuilder.DropTable(
                name: "BusinessEntityMetrics");

            migrationBuilder.DropTable(
                name: "BusinessEntityRelationships");

            migrationBuilder.DropTable(
                name: "BusinessEntities");
        }
    }
}
