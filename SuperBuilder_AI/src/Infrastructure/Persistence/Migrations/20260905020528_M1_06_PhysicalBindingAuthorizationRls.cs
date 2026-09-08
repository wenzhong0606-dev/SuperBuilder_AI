using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M1_06_PhysicalBindingAuthorizationRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalBindings_ExactlyOneOwner",
                table: "PhysicalBindings");

            migrationBuilder.AddColumn<long>(
                name: "BusinessDomainId",
                table: "MetadataSemantics",
                type: "bigint",
                nullable: true,
                comment: "业务域Id（FK 权威，逐步替代字符串 BusinessDomain）");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "BusinessDomains",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                comment: "业务域名称",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RlsPolicies_Operator",
                table: "RowLevelSecurityPolicies",
                sql: "[Operator] IN ('=', '!=', '>', '>=', '<', '<=', 'LIKE', 'IN', 'IS NULL', 'IS NOT NULL')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RlsPolicies_SubjectConsistency",
                table: "RowLevelSecurityPolicies",
                sql: "([SubjectType] = 0 AND [SubjectId] IS NULL AND [SubjectKey] IS NULL) OR ([SubjectType] = 1 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 2 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 3 AND [SubjectKey] IS NOT NULL) OR ([SubjectType] NOT IN (0,1,2,3))");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_BusinessEntityKeyId_BusinessEntityAttributeId_BusinessEntityMetricId_BusinessEntityRelationshipId",
                table: "PhysicalBindings",
                columns: new[] { "BusinessEntityKeyId", "BusinessEntityAttributeId", "BusinessEntityMetricId", "BusinessEntityRelationshipId" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_DataSourceId_MetadataTableId_MetadataColumnId",
                table: "PhysicalBindings",
                columns: new[] { "DataSourceId", "MetadataTableId", "MetadataColumnId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalBindings_PriorityNonNeg",
                table: "PhysicalBindings",
                sql: "[Priority] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataSemantics_BusinessDomainId",
                table: "MetadataSemantics",
                column: "BusinessDomainId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessDomains_TenantId",
                table: "BusinessDomains",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_DataSourceAccessGrants_Tenants_TenantId",
                table: "DataSourceAccessGrants",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataSemantics_BusinessDomains_BusinessDomainId",
                table: "MetadataSemantics",
                column: "BusinessDomainId",
                principalTable: "BusinessDomains",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DataSourceAccessGrants_Tenants_TenantId",
                table: "DataSourceAccessGrants");

            migrationBuilder.DropForeignKey(
                name: "FK_MetadataSemantics_BusinessDomains_BusinessDomainId",
                table: "MetadataSemantics");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RlsPolicies_Operator",
                table: "RowLevelSecurityPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RlsPolicies_SubjectConsistency",
                table: "RowLevelSecurityPolicies");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalBindings_BusinessEntityKeyId_BusinessEntityAttributeId_BusinessEntityMetricId_BusinessEntityRelationshipId",
                table: "PhysicalBindings");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalBindings_DataSourceId_MetadataTableId_MetadataColumnId",
                table: "PhysicalBindings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhysicalBindings_PriorityNonNeg",
                table: "PhysicalBindings");

            migrationBuilder.DropIndex(
                name: "IX_MetadataSemantics_BusinessDomainId",
                table: "MetadataSemantics");

            migrationBuilder.DropIndex(
                name: "IX_BusinessDomains_TenantId",
                table: "BusinessDomains");

            migrationBuilder.DropColumn(
                name: "BusinessDomainId",
                table: "MetadataSemantics");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "BusinessDomains",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldComment: "业务域名称");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhysicalBindings_ExactlyOneOwner",
                table: "PhysicalBindings",
                sql: "((CASE WHEN BusinessEntityKeyId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityAttributeId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityMetricId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityRelationshipId IS NOT NULL THEN 1 ELSE 0 END)) = 1");
        }
    }
}
