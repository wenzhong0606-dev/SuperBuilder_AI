using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M1_ClosureIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse to invent tenant ownership or physical parentage. Operators must repair
            // orphaned/cross-tenant rows before retrying this transactional migration.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [DataSources] AS ds
                    LEFT JOIN [Tenants] AS t ON t.[Id] = ds.[TenantId]
                    WHERE ds.[TenantId] IS NULL OR t.[Id] IS NULL)
                    THROW 51001, 'M1_ClosureIntegrity: DataSources contain a missing or orphan TenantId.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [Users] AS u
                    LEFT JOIN [Tenants] AS t ON t.[Id] = u.[TenantId]
                    WHERE t.[Id] IS NULL)
                    THROW 51002, 'M1_ClosureIntegrity: Users contain an orphan TenantId.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [MetadataTables] AS mt
                    LEFT JOIN [Tenants] AS t ON t.[Id] = mt.[TenantId]
                    LEFT JOIN [DataSources] AS ds ON ds.[Id] = mt.[DataSourceId]
                    WHERE t.[Id] IS NULL OR ds.[Id] IS NULL)
                    THROW 51003, 'M1_ClosureIntegrity: MetadataTables contain an orphan TenantId or DataSourceId.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [MetadataTables] AS mt
                    INNER JOIN [DataSources] AS ds ON ds.[Id] = mt.[DataSourceId]
                    WHERE mt.[TenantId] <> ds.[TenantId])
                    THROW 51004, 'M1_ClosureIntegrity: MetadataTable and DataSource tenant ownership does not match.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [MetadataColumns] AS mc
                    LEFT JOIN [MetadataTables] AS mt ON mt.[Id] = mc.[MetadataTableId]
                    WHERE mc.[MetadataTableId] IS NULL OR mt.[Id] IS NULL)
                    THROW 51005, 'M1_ClosureIntegrity: MetadataColumns contain a missing or orphan MetadataTableId.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [DataSources]
                    WHERE [DbType] IS NULL
                       OR UPPER(LTRIM(RTRIM([DbType]))) NOT IN ('MYSQL', 'SQLSERVER', 'POSTGRESQL'))
                    THROW 51006, 'M1_ClosureIntegrity: DataSources contain a missing or unsupported DbType.', 1;

                IF EXISTS (SELECT 1 FROM [DataSources] WHERE [ConnectionString] IS NULL)
                    THROW 51007, 'M1_ClosureIntegrity: DataSources contain a NULL ConnectionString; supply or disable/remove the source before retrying.', 1;
                """);

            // Deterministic, collision-resistant labels preserve every historical row while
            // making previously optional identifiers suitable for NOT NULL/unique constraints.
            migrationBuilder.Sql(
                """
                UPDATE [Tenants]
                SET [TenantCode] = CONCAT('__legacy_tenant_', [Id])
                WHERE [TenantCode] IS NULL OR LEN(LTRIM(RTRIM([TenantCode]))) = 0;

                UPDATE [Tenants]
                SET [TenantName] = CONCAT('Legacy Tenant ', [Id])
                WHERE [TenantName] IS NULL OR LEN(LTRIM(RTRIM([TenantName]))) = 0;

                UPDATE [Users]
                SET [SecurityStamp] = LOWER(REPLACE(CONVERT(nvarchar(36), NEWID()), '-', ''))
                WHERE [SecurityStamp] IS NULL OR LEN(LTRIM(RTRIM([SecurityStamp]))) = 0;

                UPDATE [DataSources]
                SET [Name] = CONCAT('Legacy DataSource ', [Id])
                WHERE [Name] IS NULL OR LEN(LTRIM(RTRIM([Name]))) = 0;

                UPDATE [DataSources]
                SET [NormalizedName] = CONCAT('__legacy_datasource_', [Id])
                WHERE [NormalizedName] IS NULL OR LEN(LTRIM(RTRIM([NormalizedName]))) = 0;

                UPDATE [DataSources]
                SET [DbType] = UPPER(LTRIM(RTRIM([DbType])));
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_DataSources_Tenants_TenantId",
                table: "DataSources");

            migrationBuilder.DropForeignKey(
                name: "FK_MetadataColumns_MetadataTables_MetadataTableId",
                table: "MetadataColumns");

            migrationBuilder.DropForeignKey(
                name: "FK_MetadataTables_DataSources_DataSourceId",
                table: "MetadataTables");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_TenantCode",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_MetadataColumns_MetadataTableId_ColumnName",
                table: "MetadataColumns");

            migrationBuilder.DropIndex(
                name: "IX_DataSources_TenantId_NormalizedName",
                table: "DataSources");

            migrationBuilder.AlterColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "安全戳（令牌吊销用）",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "安全戳（令牌吊销用）");

            migrationBuilder.AlterColumn<string>(
                name: "TenantName",
                table: "Tenants",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "租户名称",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "租户名称");

            migrationBuilder.AlterColumn<string>(
                name: "TenantCode",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "租户编码（规范化小写存储）",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "租户编码（规范化小写存储）");

            migrationBuilder.AlterColumn<long>(
                name: "MetadataTableId",
                table: "MetadataColumns",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "DataSources",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "DataSources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "规范化名称（小写去空白），租户内唯一键",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "规范化名称（小写去空白），租户内唯一键");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DataSources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                comment: "名称（展示用，保留原始大小写）",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true,
                oldComment: "名称（展示用，保留原始大小写）");

            migrationBuilder.AlterColumn<string>(
                name: "DbType",
                table: "DataSources",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                comment: "数据库类型(MYSQL/SQLSERVER/POSTGRESQL)",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldComment: "数据库类型(MYSQL/SQLSERVER/POSTGRESQL)");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                table: "DataSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                comment: "连接字符串（敏感，禁止日志记录）",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true,
                oldComment: "连接字符串（敏感，禁止日志记录）");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_DataSources_Id_TenantId",
                table: "DataSources",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantCode",
                table: "Tenants",
                column: "TenantCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId_TenantId",
                table: "MetadataTables",
                columns: new[] { "DataSourceId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_TenantId",
                table: "MetadataTables",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataColumns_MetadataTableId_ColumnName",
                table: "MetadataColumns",
                columns: new[] { "MetadataTableId", "ColumnName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_TenantId_NormalizedName",
                table: "DataSources",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DataSources_Tenants_TenantId",
                table: "DataSources",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataColumns_MetadataTables_MetadataTableId",
                table: "MetadataColumns",
                column: "MetadataTableId",
                principalTable: "MetadataTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataTables_DataSources_DataSourceId_TenantId",
                table: "MetadataTables",
                columns: new[] { "DataSourceId", "TenantId" },
                principalTable: "DataSources",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataTables_Tenants_TenantId",
                table: "MetadataTables",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DataSources_Tenants_TenantId",
                table: "DataSources");

            migrationBuilder.DropForeignKey(
                name: "FK_MetadataColumns_MetadataTables_MetadataTableId",
                table: "MetadataColumns");

            migrationBuilder.DropForeignKey(
                name: "FK_MetadataTables_DataSources_DataSourceId_TenantId",
                table: "MetadataTables");

            migrationBuilder.DropForeignKey(
                name: "FK_MetadataTables_Tenants_TenantId",
                table: "MetadataTables");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_TenantCode",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_MetadataTables_DataSourceId_TenantId",
                table: "MetadataTables");

            migrationBuilder.DropIndex(
                name: "IX_MetadataTables_TenantId",
                table: "MetadataTables");

            migrationBuilder.DropIndex(
                name: "IX_MetadataColumns_MetadataTableId_ColumnName",
                table: "MetadataColumns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_DataSources_Id_TenantId",
                table: "DataSources");

            migrationBuilder.DropIndex(
                name: "IX_DataSources_TenantId_NormalizedName",
                table: "DataSources");

            migrationBuilder.AlterColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "安全戳（令牌吊销用）",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "安全戳（令牌吊销用）");

            migrationBuilder.AlterColumn<string>(
                name: "TenantName",
                table: "Tenants",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "租户名称",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "租户名称");

            migrationBuilder.AlterColumn<string>(
                name: "TenantCode",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "租户编码（规范化小写存储）",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "租户编码（规范化小写存储）");

            migrationBuilder.AlterColumn<long>(
                name: "MetadataTableId",
                table: "MetadataColumns",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "TenantId",
                table: "DataSources",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedName",
                table: "DataSources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "规范化名称（小写去空白），租户内唯一键",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "规范化名称（小写去空白），租户内唯一键");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DataSources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                comment: "名称（展示用，保留原始大小写）",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldComment: "名称（展示用，保留原始大小写）");

            migrationBuilder.AlterColumn<string>(
                name: "DbType",
                table: "DataSources",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                comment: "数据库类型(MYSQL/SQLSERVER/POSTGRESQL)",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldComment: "数据库类型(MYSQL/SQLSERVER/POSTGRESQL)");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionString",
                table: "DataSources",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                comment: "连接字符串（敏感，禁止日志记录）",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldComment: "连接字符串（敏感，禁止日志记录）");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantCode",
                table: "Tenants",
                column: "TenantCode",
                unique: true,
                filter: "[TenantCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataColumns_MetadataTableId_ColumnName",
                table: "MetadataColumns",
                columns: new[] { "MetadataTableId", "ColumnName" },
                unique: true,
                filter: "[MetadataTableId] IS NOT NULL AND [ColumnName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_TenantId_NormalizedName",
                table: "DataSources",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DataSources_Tenants_TenantId",
                table: "DataSources",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataColumns_MetadataTables_MetadataTableId",
                table: "MetadataColumns",
                column: "MetadataTableId",
                principalTable: "MetadataTables",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataTables_DataSources_DataSourceId",
                table: "MetadataTables",
                column: "DataSourceId",
                principalTable: "DataSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
