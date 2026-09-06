using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperBuilder_AI.src.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M7_02_AppVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentPlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=全局模板）"),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "业务编码"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "名称"),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true, comment: "描述"),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "状态"),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "DSL版本"),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "DSL文档（结构化，非裸HTML）"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPlans", x => x.Id);
                },
                comment: "Agent计划");

            migrationBuilder.CreateTable(
                name: "AppPlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=全局模板）"),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "业务编码"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "名称"),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true, comment: "描述"),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "状态"),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "DSL版本"),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "DSL文档（结构化，非裸HTML）"),
                    ThemeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "主题键"),
                    PublishedDslJson = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "已发布DSL快照（null=未发布）"),
                    PublishedVersion = table.Column<int>(type: "int", nullable: false, comment: "当前发布版本号（0=未发布）"),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "最近发布时间(UTC)"),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "最近发布者"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPlans", x => x.Id);
                },
                comment: "应用");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=平台级）"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "操作者标识"),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "动作类型"),
                    EntityType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "实体类型"),
                    EntityId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "实体Id"),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "结果"),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManagementTargetTenantId = table.Column<long>(type: "bigint", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                },
                comment: "审计日志");

            migrationBuilder.CreateTable(
                name: "Dashboards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=全局模板）"),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "业务编码"),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "标题"),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true, comment: "描述"),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "状态"),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "DSL版本"),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "DSL文档（结构化，非裸HTML）"),
                    ThemeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "主题键"),
                    PublishedDslJson = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "已发布DSL快照（null=未发布）"),
                    PublishedVersion = table.Column<int>(type: "int", nullable: false, comment: "当前发布版本号（0=未发布）"),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "最近发布时间(UTC)"),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "最近发布者"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dashboards", x => x.Id);
                },
                comment: "仪表盘");

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=全局权限）"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "权限码（同租户唯一）"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "权限名"),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "权限分类"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                },
                comment: "权限");

            migrationBuilder.CreateTable(
                name: "QuotaPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=平台默认）"),
                    ResourceType = table.Column<int>(type: "int", nullable: false, comment: "资源类型"),
                    Limit = table.Column<long>(type: "bigint", nullable: false, comment: "上限"),
                    Window = table.Column<int>(type: "int", nullable: false, comment: "周期窗口"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotaPolicies", x => x.Id);
                },
                comment: "配额策略");

            migrationBuilder.CreateTable(
                name: "QuotaUsages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户"),
                    ResourceType = table.Column<int>(type: "int", nullable: false, comment: "资源类型"),
                    Used = table.Column<long>(type: "bigint", nullable: false, comment: "已用"),
                    PeriodKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "周期键"),
                    LastReset = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotaUsages", x => x.Id);
                },
                comment: "配额使用量");

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=全局角色）"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "角色码（同租户唯一）"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "角色名"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                },
                comment: "角色");

            migrationBuilder.CreateTable(
                name: "SemanticLabels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=全局共享）"),
                    ConceptType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "概念类型"),
                    ConceptId = table.Column<long>(type: "bigint", nullable: false, comment: "概念实体Id"),
                    Culture = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "语言标签"),
                    LabelKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "标签种类"),
                    Value = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false, comment: "标签文本"),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "来源"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, comment: "排序"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemanticLabels", x => x.Id);
                },
                comment: "语义多语言标签");

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "租户编码（规范化小写存储）"),
                    TenantName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "租户名称"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, comment: "是否启用"),
                    DisabledReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, comment: "停用原因"),
                    DisabledAt = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "停用时间(UTC)"),
                    DisabledByUserId = table.Column<long>(type: "bigint", nullable: true, comment: "停用操作者用户Id"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                },
                comment: "租户");

            migrationBuilder.CreateTable(
                name: "Themes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户（0=内置/全局模板）"),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "主题键（同租户内唯一）"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "主题名称"),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false, comment: "是否内置主题"),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "DSL版本"),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "主题DSL文档（结构化令牌，非CSS/HTML）"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Themes", x => x.Id);
                },
                comment: "主题");

            migrationBuilder.CreateTable(
                name: "UiLanguages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Culture = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NativeName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiLanguages", x => x.Id);
                },
                comment: "平台界面语言目录");

            migrationBuilder.CreateTable(
                name: "UiTextResources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Culture = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ResourceKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsTranslated = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否已翻译（新建语言从其它语言复制键集合时标记待翻译）"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiTextResources", x => x.Id);
                },
                comment: "平台及租户界面文本");

            migrationBuilder.CreateTable(
                name: "UserLanguagePreferences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Culture = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLanguagePreferences", x => x.Id);
                },
                comment: "用户界面语言偏好");

            migrationBuilder.CreateTable(
                name: "AppVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "作用域租户（0=全局模板）"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "业务编码快照"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "名称快照"),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true, comment: "描述快照"),
                    ThemeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "主题键快照"),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "DSL版本快照"),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "发布时刻固化的DSL文档（只读快照）"),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "发布者"),
                    RolledBackFromVersion = table.Column<int>(type: "int", nullable: true, comment: "回滚来源版本号（非回滚为null）"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppVersions_AppPlans_AppId",
                        column: x => x.AppId,
                        principalTable: "AppPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "应用发布版本快照");

            migrationBuilder.CreateTable(
                name: "DashboardVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DashboardId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "作用域租户（0=全局模板）"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "业务编码快照"),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "标题快照"),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true, comment: "描述快照"),
                    ThemeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "主题键快照"),
                    DslVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "DSL版本快照"),
                    DslJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "发布时刻固化的DSL文档（只读快照）"),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "发布者"),
                    RolledBackFromVersion = table.Column<int>(type: "int", nullable: true, comment: "回滚来源版本号（非回滚为null）"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardVersions_Dashboards_DashboardId",
                        column: x => x.DashboardId,
                        principalTable: "Dashboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "仪表盘发布版本快照");

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "角色-权限关联");

            migrationBuilder.CreateTable(
                name: "BusinessDomains",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "业务域名称"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessDomains_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "业务域");

            migrationBuilder.CreateTable(
                name: "DataSources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "名称（展示用，保留原始大小写）"),
                    NormalizedName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "规范化名称（小写去空白），租户内唯一键"),
                    DbType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "数据库类型(MYSQL/SQLSERVER/POSTGRESQL)"),
                    ConnectionString = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, comment: "连接字符串（敏感，禁止日志记录）"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否启用"),
                    LastTestStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "最近连接测试状态(Ok/Failed/Unknown)"),
                    LastTestTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "最近连接测试时间(UTC)"),
                    LastErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "最近连接测试错误码(仅异常类型名,脱敏)"),
                    LastScanAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSources", x => x.Id);
                    table.UniqueConstraint("AK_DataSources_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_DataSources_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "数据源");

            migrationBuilder.CreateTable(
                name: "TenantSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "配置键"),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "配置值"),
                    DataType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "值类型(string|int|bool|json)"),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "是否锁定(租户不可覆盖)"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "租户键值配置");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户"),
                    Username = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "登录名（展示用，大小写原始）"),
                    NormalizedUsername = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "规范化登录名（小写，租户内唯一）"),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "显示名"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "邮箱"),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "规范化邮箱（小写）"),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "邮箱是否已验证"),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true, comment: "口令哈希（PBKDF2，可选）"),
                    SecurityStamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "安全戳（令牌吊销用）"),
                    Status = table.Column<int>(type: "int", nullable: false, comment: "状态"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "用户");

            migrationBuilder.CreateTable(
                name: "TenantUiLanguages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false, comment: "所属租户"),
                    UiLanguageId = table.Column<long>(type: "bigint", nullable: false, comment: "平台语言目录 Id"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否启用（租户范围内）"),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false, comment: "是否为租户默认语言（每租户恰一个）"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, comment: "排序"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUiLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUiLanguages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantUiLanguages_UiLanguages_UiLanguageId",
                        column: x => x.UiLanguageId,
                        principalTable: "UiLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "租户界面语言关系（替代 localization:availableCultures/defaultCulture JSON）");

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
                    BusinessDomainId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessEntities_BusinessDomains_BusinessDomainId",
                        column: x => x.BusinessDomainId,
                        principalTable: "BusinessDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessEntities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "业务实体");

            migrationBuilder.CreateTable(
                name: "BusinessEntityDimensions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessDomainId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessEntityDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessEntityDimensions_BusinessDomains_BusinessDomainId",
                        column: x => x.BusinessDomainId,
                        principalTable: "BusinessDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "业务实体维度");

            migrationBuilder.CreateTable(
                name: "DataSourceAccessGrants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectType = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceAccessGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataSourceAccessGrants_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DataSourceAccessGrants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetadataScanJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "触发用户标识"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Queued", comment: "扫描状态"),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "进度百分比"),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "开始时间(UTC)"),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "结束时间(UTC)"),
                    TablesScanned = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "已扫描表数"),
                    ColumnsScanned = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "已扫描字段数"),
                    OrphansDetected = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "孤儿对象数"),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "错误码(仅异常类型名,脱敏)"),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, comment: "错误摘要(脱敏,不含连接串)"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataScanJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataScanJobs_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "元数据扫描任务");

            migrationBuilder.CreateTable(
                name: "MetadataTables",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "表名"),
                    CatalogName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "目录名"),
                    SchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "模式名"),
                    TableComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessDomain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "Embedding文本"),
                    VectorId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "Qdrant向量ID"),
                    EmbeddingModel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "Embedding模型"),
                    VectorDimension = table.Column<int>(type: "int", nullable: true, comment: "向量维度"),
                    VectorSyncTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "向量同步时间(UTC)"),
                    VectorStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true, comment: "向量状态"),
                    VectorErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "向量错误码"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataTables_DataSources_DataSourceId_TenantId",
                        columns: x => new { x.DataSourceId, x.TenantId },
                        principalTable: "DataSources",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataTables_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "元数据表");

            migrationBuilder.CreateTable(
                name: "PlatformAdminTenantScopes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "授权时间(UTC)"),
                    GrantedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "授权操作者")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAdminTenantScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformAdminTenantScopes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlatformAdminTenantScopes_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "平台管理员租户范围绑定");

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "用户-角色关联");

            migrationBuilder.CreateTable(
                name: "UserTenants",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true, comment: "操作者用户 Id（管理员代加成员）")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTenants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "用户—租户成员关系（多租户切换）");

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
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
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
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
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
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
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
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
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
                name: "MetadataColumns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetadataTableId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessKey = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "字段业务唯一标识"),
                    ColumnName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, comment: "列名"),
                    Ordinal = table.Column<int>(type: "int", nullable: false, defaultValue: 0, comment: "列序号"),
                    NativeType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "原生类型"),
                    Precision = table.Column<int>(type: "int", nullable: true, comment: "精度"),
                    Scale = table.Column<int>(type: "int", nullable: true, comment: "小数位"),
                    ColumnComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Length = table.Column<long>(type: "bigint", nullable: true),
                    IsNullable = table.Column<bool>(type: "bit", nullable: true),
                    IsPrimaryKey = table.Column<bool>(type: "bit", nullable: true),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "字段Embedding文本"),
                    VectorId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "Qdrant字段向量ID"),
                    EmbeddingModel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "Embedding模型"),
                    VectorDimension = table.Column<int>(type: "int", nullable: true, comment: "向量维度"),
                    VectorSyncTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "向量同步时间(UTC)"),
                    VectorStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true, comment: "向量状态"),
                    VectorErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "向量错误码"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataColumns_MetadataTables_MetadataTableId",
                        column: x => x.MetadataTableId,
                        principalTable: "MetadataTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "元数据字段");

            migrationBuilder.CreateTable(
                name: "LearningRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataColumnId = table.Column<long>(type: "bigint", nullable: true),
                    Correct = table.Column<bool>(type: "bit", nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningRecords_MetadataColumns_MetadataColumnId",
                        column: x => x.MetadataColumnId,
                        principalTable: "MetadataColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LearningRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "学习记录");

            migrationBuilder.CreateTable(
                name: "MetadataSemantics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetadataColumnId = table.Column<long>(type: "bigint", nullable: true),
                    BusinessMeaning = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "业务含义"),
                    Keywords = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true, comment: "关键词"),
                    Synonyms = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true, comment: "同义词"),
                    ExampleQuestions = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true, comment: "示例问题"),
                    BusinessDomain = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "业务域"),
                    BusinessDomainId = table.Column<long>(type: "bigint", nullable: true, comment: "业务域Id（FK 权威，逐步替代字符串 BusinessDomain）"),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: true, comment: "AI生成置信度(0-1)"),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Manual", comment: "来源"),
                    SearchText = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "语义Embedding文本"),
                    VectorId = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "Qdrant语义向量ID"),
                    EmbeddingModel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true, comment: "Embedding模型"),
                    VectorDimension = table.Column<int>(type: "int", nullable: true, comment: "向量维度"),
                    VectorSyncTime = table.Column<DateTime>(type: "datetime2", nullable: true, comment: "向量同步时间(UTC)"),
                    VectorStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true, comment: "向量状态"),
                    VectorErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "向量错误码"),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataSemantics", x => x.Id);
                    table.CheckConstraint("CK_MetadataSemantics_Confidence", "[Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 1)");
                    table.ForeignKey(
                        name: "FK_MetadataSemantics_BusinessDomains_BusinessDomainId",
                        column: x => x.BusinessDomainId,
                        principalTable: "BusinessDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MetadataSemantics_MetadataColumns_MetadataColumnId",
                        column: x => x.MetadataColumnId,
                        principalTable: "MetadataColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "字段AI语义");

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
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false, comment: "创建时间"),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L, comment: "乐观并发版本(ETag)，每次更新自增")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalBindings", x => x.Id);
                    table.CheckConstraint("CK_PhysicalBindings_PriorityNonNeg", "[Priority] >= 0");
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

            migrationBuilder.CreateTable(
                name: "RowLevelSecurityPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "主键")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    DataSourceId = table.Column<long>(type: "bigint", nullable: false),
                    MetadataTableId = table.Column<long>(type: "bigint", nullable: false),
                    MetadataColumnId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectType = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: true),
                    SubjectKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SubjectValue = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Effect = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RowLevelSecurityPolicies", x => x.Id);
                    table.CheckConstraint("CK_RlsPolicies_Operator", "[Operator] IN ('=', '!=', '>', '>=', '<', '<=', 'LIKE', 'IN', 'IS NULL', 'IS NOT NULL')");
                    table.CheckConstraint("CK_RlsPolicies_SubjectConsistency", "([SubjectType] = 0 AND [SubjectId] IS NULL AND [SubjectKey] IS NULL) OR ([SubjectType] = 1 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 2 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 3 AND [SubjectKey] IS NOT NULL) OR ([SubjectType] NOT IN (0,1,2,3))");
                    table.ForeignKey(
                        name: "FK_RowLevelSecurityPolicies_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RowLevelSecurityPolicies_MetadataColumns_MetadataColumnId",
                        column: x => x.MetadataColumnId,
                        principalTable: "MetadataColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RowLevelSecurityPolicies_MetadataTables_MetadataTableId",
                        column: x => x.MetadataTableId,
                        principalTable: "MetadataTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlans_TenantId_Code",
                table: "AgentPlans",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlans_TenantId_Status",
                table: "AgentPlans",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPlans_TenantId_Code",
                table: "AppPlans",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPlans_TenantId_Status",
                table: "AppPlans",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppVersions_AppId_Version",
                table: "AppVersions",
                columns: new[] { "AppId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppVersions_TenantId_AppId",
                table: "AppVersions",
                columns: new[] { "TenantId", "AppId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Action",
                table: "AuditLogs",
                columns: new[] { "TenantId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_EntityType",
                table: "AuditLogs",
                columns: new[] { "TenantId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessDomains_TenantId",
                table: "BusinessDomains",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessDomains_TenantId_Name",
                table: "BusinessDomains",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntities_BusinessDomainId",
                table: "BusinessEntities",
                column: "BusinessDomainId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntities_TenantId_BusinessKey",
                table: "BusinessEntities",
                columns: new[] { "TenantId", "BusinessKey" },
                unique: true);

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
                name: "IX_BusinessEntityDimensions_BusinessDomainId",
                table: "BusinessEntityDimensions",
                column: "BusinessDomainId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessEntityDimensions_TenantId_BusinessDomainId_Name",
                table: "BusinessEntityDimensions",
                columns: new[] { "TenantId", "BusinessDomainId", "Name" },
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
                name: "IX_Dashboards_TenantId_Code",
                table: "Dashboards",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dashboards_TenantId_Status",
                table: "Dashboards",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardVersions_DashboardId_Version",
                table: "DashboardVersions",
                columns: new[] { "DashboardId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardVersions_TenantId_DashboardId",
                table: "DashboardVersions",
                columns: new[] { "TenantId", "DashboardId" });

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceAccessGrants_DataSourceId",
                table: "DataSourceAccessGrants",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceAccessGrants_TenantId_DataSourceId_SubjectType_SubjectId",
                table: "DataSourceAccessGrants",
                columns: new[] { "TenantId", "DataSourceId", "SubjectType", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceAccessGrants_TenantId_SubjectType_SubjectId",
                table: "DataSourceAccessGrants",
                columns: new[] { "TenantId", "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_TenantId",
                table: "DataSources",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSources_TenantId_NormalizedName",
                table: "DataSources",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningRecords_MetadataColumnId",
                table: "LearningRecords",
                column: "MetadataColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningRecords_TenantId",
                table: "LearningRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataColumns_MetadataTableId_ColumnName",
                table: "MetadataColumns",
                columns: new[] { "MetadataTableId", "ColumnName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataScanJobs_DataSourceId",
                table: "MetadataScanJobs",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataScanJobs_Status",
                table: "MetadataScanJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataScanJobs_TenantId",
                table: "MetadataScanJobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataSemantics_BusinessDomainId",
                table: "MetadataSemantics",
                column: "BusinessDomainId");

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
                name: "IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName",
                table: "MetadataTables",
                columns: new[] { "DataSourceId", "CatalogName", "SchemaName", "TableName" },
                unique: true,
                filter: "[CatalogName] IS NOT NULL AND [SchemaName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_DataSourceId_TenantId",
                table: "MetadataTables",
                columns: new[] { "DataSourceId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTables_TenantId",
                table: "MetadataTables",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId_Code",
                table: "Permissions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_BusinessEntityAttributeId_IsActive",
                table: "PhysicalBindings",
                columns: new[] { "BusinessEntityAttributeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalBindings_BusinessEntityKeyId_BusinessEntityAttributeId_BusinessEntityMetricId_BusinessEntityRelationshipId",
                table: "PhysicalBindings",
                columns: new[] { "BusinessEntityKeyId", "BusinessEntityAttributeId", "BusinessEntityMetricId", "BusinessEntityRelationshipId" });

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
                name: "IX_PhysicalBindings_DataSourceId_MetadataTableId_MetadataColumnId",
                table: "PhysicalBindings",
                columns: new[] { "DataSourceId", "MetadataTableId", "MetadataColumnId" });

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

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdminTenantScopes_AdminUserId",
                table: "PlatformAdminTenantScopes",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdminTenantScopes_AdminUserId_TenantId",
                table: "PlatformAdminTenantScopes",
                columns: new[] { "AdminUserId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdminTenantScopes_TenantId",
                table: "PlatformAdminTenantScopes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotaPolicies_TenantId_ResourceType",
                table: "QuotaPolicies",
                columns: new[] { "TenantId", "ResourceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotaUsages_TenantId_ResourceType",
                table: "QuotaUsages",
                columns: new[] { "TenantId", "ResourceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "TenantId", "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Code",
                table: "Roles",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_DataSourceId",
                table: "RowLevelSecurityPolicies",
                column: "DataSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_MetadataColumnId",
                table: "RowLevelSecurityPolicies",
                column: "MetadataColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_MetadataTableId",
                table: "RowLevelSecurityPolicies",
                column: "MetadataTableId");

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_TenantId_DataSourceId_MetadataTableId_Enabled",
                table: "RowLevelSecurityPolicies",
                columns: new[] { "TenantId", "DataSourceId", "MetadataTableId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_RowLevelSecurityPolicies_TenantId_SubjectType_SubjectId",
                table: "RowLevelSecurityPolicies",
                columns: new[] { "TenantId", "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_SemanticLabels_ConceptType_ConceptId_Culture",
                table: "SemanticLabels",
                columns: new[] { "ConceptType", "ConceptId", "Culture" });

            migrationBuilder.CreateIndex(
                name: "IX_SemanticLabels_TenantId_ConceptType_ConceptId_Culture_LabelKind_SortOrder",
                table: "SemanticLabels",
                columns: new[] { "TenantId", "ConceptType", "ConceptId", "Culture", "LabelKind", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantCode",
                table: "Tenants",
                column: "TenantCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantSettings_TenantId_Key",
                table: "TenantSettings",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUiLanguages_TenantId_IsDefault",
                table: "TenantUiLanguages",
                columns: new[] { "TenantId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUiLanguages_TenantId_UiLanguageId",
                table: "TenantUiLanguages",
                columns: new[] { "TenantId", "UiLanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUiLanguages_UiLanguageId",
                table: "TenantUiLanguages",
                column: "UiLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_Themes_TenantId",
                table: "Themes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Themes_TenantId_Key",
                table: "Themes",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UiLanguages_Culture",
                table: "UiLanguages",
                column: "Culture",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UiTextResources_TenantId_Culture_ResourceKey",
                table: "UiTextResources",
                columns: new[] { "TenantId", "Culture", "ResourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguagePreferences_TenantId_UserId",
                table: "UserLanguagePreferences",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "TenantId", "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_NormalizedUsername",
                table: "Users",
                columns: new[] { "TenantId", "NormalizedUsername" },
                unique: true,
                filter: "[NormalizedUsername] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_TenantId",
                table: "UserTenants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_UserId",
                table: "UserTenants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_UserId_TenantId",
                table: "UserTenants",
                columns: new[] { "UserId", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPlans");

            migrationBuilder.DropTable(
                name: "AppVersions");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BusinessEntityDimensions");

            migrationBuilder.DropTable(
                name: "DashboardVersions");

            migrationBuilder.DropTable(
                name: "DataSourceAccessGrants");

            migrationBuilder.DropTable(
                name: "LearningRecords");

            migrationBuilder.DropTable(
                name: "MetadataScanJobs");

            migrationBuilder.DropTable(
                name: "MetadataSemantics");

            migrationBuilder.DropTable(
                name: "PhysicalBindings");

            migrationBuilder.DropTable(
                name: "PlatformAdminTenantScopes");

            migrationBuilder.DropTable(
                name: "QuotaPolicies");

            migrationBuilder.DropTable(
                name: "QuotaUsages");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "RowLevelSecurityPolicies");

            migrationBuilder.DropTable(
                name: "SemanticLabels");

            migrationBuilder.DropTable(
                name: "TenantSettings");

            migrationBuilder.DropTable(
                name: "TenantUiLanguages");

            migrationBuilder.DropTable(
                name: "Themes");

            migrationBuilder.DropTable(
                name: "UiTextResources");

            migrationBuilder.DropTable(
                name: "UserLanguagePreferences");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTenants");

            migrationBuilder.DropTable(
                name: "AppPlans");

            migrationBuilder.DropTable(
                name: "Dashboards");

            migrationBuilder.DropTable(
                name: "BusinessEntityAttributes");

            migrationBuilder.DropTable(
                name: "BusinessEntityKeys");

            migrationBuilder.DropTable(
                name: "BusinessEntityMetrics");

            migrationBuilder.DropTable(
                name: "BusinessEntityRelationships");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "MetadataColumns");

            migrationBuilder.DropTable(
                name: "UiLanguages");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "BusinessEntities");

            migrationBuilder.DropTable(
                name: "MetadataTables");

            migrationBuilder.DropTable(
                name: "BusinessDomains");

            migrationBuilder.DropTable(
                name: "DataSources");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
