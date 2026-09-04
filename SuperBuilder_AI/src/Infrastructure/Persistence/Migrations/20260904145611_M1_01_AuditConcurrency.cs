using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.src.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M1_01_AuditConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "UiTextResources",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "UiTextResources",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "UiTextResources",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "UiTextResources",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "UiLanguages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "UiLanguages",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "UiLanguages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "UiLanguages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Themes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "Themes",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Themes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "Themes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TenantSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "TenantSettings",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "TenantSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "TenantSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "Tenants",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "SemanticLabels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "SemanticLabels",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "SemanticLabels",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "SemanticLabels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "Roles",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "Roles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PhysicalBindings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "PhysicalBindings",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "PhysicalBindings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "PhysicalBindings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "MetadataTables",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "MetadataTables",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "MetadataTables",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "MetadataSemantics",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "MetadataSemantics",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "MetadataSemantics",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "MetadataColumns",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "MetadataColumns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "MetadataColumns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "LearningRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "LearningRecords",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "LearningRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "LearningRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "DataSources",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "DataSources",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "DataSources",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "DataSources",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Dashboards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "Dashboards",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Dashboards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "Dashboards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BusinessEntityRelationships",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "BusinessEntityRelationships",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BusinessEntityRelationships",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "BusinessEntityRelationships",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BusinessEntityMetrics",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "BusinessEntityMetrics",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BusinessEntityMetrics",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "BusinessEntityMetrics",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BusinessEntityKeys",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "BusinessEntityKeys",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BusinessEntityKeys",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "BusinessEntityKeys",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BusinessEntityDimensions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "BusinessEntityDimensions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BusinessEntityDimensions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "BusinessEntityDimensions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BusinessEntityAttributes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "BusinessEntityAttributes",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BusinessEntityAttributes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "BusinessEntityAttributes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BusinessEntities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "BusinessEntities",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BusinessEntities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "BusinessEntities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "BusinessDomains",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "BusinessDomains",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "BusinessDomains",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "BusinessDomains",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AppPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "AppPlans",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "AppPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "AppPlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AgentPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RowVersion",
                table: "AgentPlans",
                type: "bigint",
                nullable: false,
                defaultValue: 1L,
                comment: "乐观并发版本(ETag)，每次更新自增");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "AgentPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedTime",
                table: "AgentPlans",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UiTextResources");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UiTextResources");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "UiTextResources");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "UiTextResources");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UiLanguages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UiLanguages");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "UiLanguages");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "UiLanguages");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "Themes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "SemanticLabels");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SemanticLabels");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "SemanticLabels");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "SemanticLabels");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PhysicalBindings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PhysicalBindings");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "PhysicalBindings");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "PhysicalBindings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "MetadataTables");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "MetadataSemantics");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "MetadataColumns");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "LearningRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LearningRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "LearningRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "LearningRecords");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "DataSources");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BusinessEntityRelationships");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BusinessEntityRelationships");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BusinessEntityRelationships");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "BusinessEntityRelationships");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BusinessEntityMetrics");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BusinessEntityMetrics");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BusinessEntityMetrics");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "BusinessEntityMetrics");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BusinessEntityKeys");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BusinessEntityKeys");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BusinessEntityKeys");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "BusinessEntityKeys");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BusinessEntityDimensions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BusinessEntityDimensions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BusinessEntityDimensions");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "BusinessEntityDimensions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BusinessEntityAttributes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BusinessEntityAttributes");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BusinessEntityAttributes");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "BusinessEntityAttributes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BusinessEntities");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BusinessEntities");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BusinessEntities");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "BusinessEntities");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "BusinessDomains");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BusinessDomains");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "BusinessDomains");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "BusinessDomains");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AppPlans");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AppPlans");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "AppPlans");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "AppPlans");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AgentPlans");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AgentPlans");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "AgentPlans");

            migrationBuilder.DropColumn(
                name: "UpdatedTime",
                table: "AgentPlans");
        }
    }
}
