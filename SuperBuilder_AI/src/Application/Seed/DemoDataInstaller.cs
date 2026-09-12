using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Audit;
using SuperBuilder_AI.Interfaces.BI;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Interfaces.Seed;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Infrastructure.Security;
using SuperBuilder_AI.Models.Theme;

namespace SuperBuilder_AI.Services.Seed;

/// <summary>
/// 演示数据安装器实现（M2-07 / DEC-05）。
/// <list type="bullet">
///   <item>独立安装器：<strong>不</strong>接入启动序列，生产默认不安装演示数据。</item>
///   <item>事务原子：租户 + 本地化设置 + 管理员 + 数据源 + 元数据/字段/语义 + 业务实体 + 仪表盘 同事务提交，失败整体回滚。</item>
///   <item>重复执行保护：已存在演示租户（code="demo"）时直接返回 <c>already_installed</c>，不再写入。</item>
///   <item>审计：安装完成后写 <c>demo.seed</c> 审计事件。</item>
/// </list>
/// </summary>
public sealed class DemoDataInstaller : IDemoDataInstaller
{
    private const string DemoTenantCode = "demo";
    private const string DemoTenantName = "演示租户";
    private const string DemoAdminUsername = "demo_admin";
    private const string DemoAdminEmail = "demo@demo.local";
    private const string DemoAdminPassword = "Demo@123456";

	private readonly SuperBIContext _db;
	private readonly IIdentityService _identity;
	private readonly IAuditLogService _audit;
	private readonly IDashboardDslSerializer _dashboardSerializer;
	private readonly ITenantLanguageService _tenantLanguage;
	private readonly ISecretStore _secrets;

	public DemoDataInstaller(
		SuperBIContext db,
		IIdentityService identity,
		IAuditLogService audit,
		IDashboardDslSerializer dashboardSerializer,
		ITenantLanguageService tenantLanguage,
		ISecretStore secrets)
	{
		_db = db;
		_identity = identity;
		_audit = audit;
		_dashboardSerializer = dashboardSerializer;
		_tenantLanguage = tenantLanguage;
		_secrets = secrets;
	}

    public async Task<DemoInstallPlan> PreviewAsync(CancellationToken ct = default)
    {
        var alreadyInstalled = await _db.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.TenantCode == DemoTenantCode, ct);

        var items = new List<DemoPlanItem>
        {
            new("Tenant", 1, "演示租户（含本地化设置）"),
            new("User", 1, "租户管理员 demo_admin"),
            new("DataSource", 1, "演示销售数据库（MySQL）"),
            new("MetadataTable", 1, "销售订单表"),
            new("MetadataColumn", 5, "订单字段（金额/客户/时间/状态等）"),
            new("MetadataSemantic", 5, "字段业务语义"),
            new("BusinessEntity", 1, "销售订单业务实体（语义层）"),
            new("Dashboard", 1, "演示销售概览仪表盘"),
        };

        return new DemoInstallPlan(alreadyInstalled, DemoTenantCode, DemoTenantName, DemoAdminUsername, DemoAdminEmail, items);
    }

    public async Task<DemoInstallResult> InstallAsync(CancellationToken ct = default)
    {
        // 重复执行保护：已安装则直接返回，不重复创建。
        var existing = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantCode == DemoTenantCode, ct);
        if (existing is not null)
        {
            var admin = await _db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.TenantId == existing.Id && u.Username == DemoAdminUsername, ct);
            return new DemoInstallResult(true, "already_installed", existing.Id, admin?.Id ?? 0, DemoAdminUsername);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1) 租户 + 本地化设置
            var tenant = new Tenant { TenantCode = DemoTenantCode, TenantName = DemoTenantName, Enabled = true };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(ct);

            // M3-01：语言授权写入关系模型 TenantUiLanguage（替代 localization:* JSON）。
            // 演示租户默认启用平台 zh-CN（若无显式授权则回退平台默认）。
            await _tenantLanguage.EnsureTenantLanguagesAsync(tenant.Id, ct);

            // 2) 租户管理员
            var created = await _identity.CreateUserAsync(
                tenant.Id, DemoAdminUsername, "演示管理员", DemoAdminEmail, new[] { IdentityRoles.TenantAdmin }, ct);
            if (!created.Success)
            {
                await tx.RollbackAsync(ct);
                return new DemoInstallResult(false, "failed", Error: string.Join("；", created.Errors));
            }

            var pwd = await _identity.SetPasswordAsync(tenant.Id, created.Id!.Value, DemoAdminPassword, ct);
            if (!pwd.Success)
            {
                await tx.RollbackAsync(ct);
                return new DemoInstallResult(false, "failed", Error: string.Join("；", pwd.Errors));
            }

            // 3) 数据源
            var dataSource = new DataSource
            {
                TenantId = tenant.Id,
                Name = "演示销售数据库",
                NormalizedName = DataSource.NormalizeName("演示销售数据库"),
                DbType = "MYSQL",
                ConnectionString = _secrets.Protect("Server=localhost;Port=3306;Database=demo_sales;Uid=demo_ro;Pwd=demo_ro;"),
                Enabled = true,
            };
            _db.DataSources.Add(dataSource);
            await _db.SaveChangesAsync(ct);

            // 4) 元数据表
            var table = new MetadataTable
            {
                TenantId = tenant.Id,
                DataSourceId = dataSource.Id,
                TableName = "sales_order",
                TableComment = "销售订单",
                BusinessDomain = "销售",
                SearchText = "销售订单 sales_order 订单金额 客户 下单时间 订单状态",
            };
            _db.MetadataTables.Add(table);
            await _db.SaveChangesAsync(ct);

            // 5) 字段 + 业务语义
            var columns = new List<MetadataColumn>
            {
                new()
                {
                    MetadataTableId = table.Id, ColumnName = "order_id", Ordinal = 0,
                    NativeType = "bigint", DataType = "bigint", IsPrimaryKey = true, IsNullable = false,
                    ColumnComment = "订单唯一标识",
                    Semantic = new MetadataSemantic
                    {
                        BusinessMeaning = "销售订单的唯一标识", Keywords = "订单号,OrderId",
                        Synonyms = "Order No", ExampleQuestions = "按订单号查询这笔订单",
                        BusinessDomain = "销售", Confidence = 0.95m, Source = SemanticSource.Manual,
                    },
                },
                new()
                {
                    MetadataTableId = table.Id, ColumnName = "customer_name", Ordinal = 1,
                    NativeType = "varchar", DataType = "varchar", Length = 128, IsNullable = false,
                    ColumnComment = "客户姓名",
                    Semantic = new MetadataSemantic
                    {
                        BusinessMeaning = "下单客户的姓名", Keywords = "客户,姓名",
                        Synonyms = "Customer", ExampleQuestions = "有哪些客户下过单",
                        BusinessDomain = "销售", Confidence = 0.92m, Source = SemanticSource.Manual,
                    },
                },
                new()
                {
                    MetadataTableId = table.Id, ColumnName = "amount", Ordinal = 2,
                    NativeType = "decimal", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = false,
                    ColumnComment = "订单金额",
                    Semantic = new MetadataSemantic
                    {
                        BusinessMeaning = "订单交易产生的销售金额", Keywords = "销售金额,收入,营业额",
                        Synonyms = "Revenue,Sales Amount", ExampleQuestions = "本月销售额是多少",
                        BusinessDomain = "销售", Confidence = 0.96m, Source = SemanticSource.Manual,
                    },
                },
                new()
                {
                    MetadataTableId = table.Id, ColumnName = "order_date", Ordinal = 3,
                    NativeType = "datetime", DataType = "datetime", IsNullable = false,
                    ColumnComment = "下单时间",
                    Semantic = new MetadataSemantic
                    {
                        BusinessMeaning = "订单创建的时间", Keywords = "下单时间,订单日期",
                        Synonyms = "Order Date", ExampleQuestions = "最近的订单有哪些",
                        BusinessDomain = "销售", Confidence = 0.90m, Source = SemanticSource.Manual,
                    },
                },
                new()
                {
                    MetadataTableId = table.Id, ColumnName = "status", Ordinal = 4,
                    NativeType = "varchar", DataType = "varchar", Length = 32, IsNullable = false,
                    ColumnComment = "订单状态",
                    Semantic = new MetadataSemantic
                    {
                        BusinessMeaning = "订单当前所处的业务状态", Keywords = "状态,订单状态",
                        Synonyms = "Order Status", ExampleQuestions = "有多少已完成订单",
                        BusinessDomain = "销售", Confidence = 0.90m, Source = SemanticSource.Manual,
                    },
                },
            };
            _db.MetadataColumns.AddRange(columns);
            await _db.SaveChangesAsync(ct);

            // 6) 业务实体（语义层）
            var entity = new BusinessEntity
            {
                TenantId = tenant.Id,
                BusinessKey = "sales_order",
                Name = "销售订单",
                DisplayName = "销售订单",
                Description = "演示用销售订单业务实体，由演示数据安装器生成。",
                BusinessDomain = "销售",
                SemanticText = "销售订单代表一次客户购买行为，包含订单金额、客户、下单时间与订单状态。",
                Status = "Active",
            };
            _db.BusinessEntities.Add(entity);
            await _db.SaveChangesAsync(ct);

            // 7) 仪表盘
            var dashboard = new Dashboard
            {
                TenantId = tenant.Id,
                Code = "demo-sales-overview",
                Title = "演示销售概览",
                Description = "由演示数据安装器生成的示例仪表盘。",
                Status = DashboardStatuses.Published,
                DslVersion = DslVersions.Current,
                ThemeKey = BuiltInThemeKeys.Default,
                DslJson = _dashboardSerializer.Serialize(BuildDemoDashboardDsl()),
            };
            _db.Dashboards.Add(dashboard);
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            await _audit.LogAsync(new AuditLogEntry(
                tenant.Id,
                "demo.seed",
                "Tenant",
                UserId: created.Id,
                Actor: "system",
                EntityId: tenant.Id.ToString(),
                Result: "success",
                Message: $"演示数据已安装：租户 {DemoTenantCode}、管理员 {DemoAdminUsername}。"), ct);

            return new DemoInstallResult(
                true, "installed", tenant.Id, created.Id!.Value, DemoAdminUsername, DemoAdminPassword);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new DemoInstallResult(false, "failed", Error: ex.Message);
        }
    }

    private static DashboardDsl BuildDemoDashboardDsl() => new()
    {
        Title = "演示销售概览",
        Description = "由演示数据安装器生成的示例仪表盘。",
        ThemeKey = BuiltInThemeKeys.Default,
        Pages = new List<PageDsl>
        {
            new()
            {
                Id = "overview",
                Name = "概览",
                Order = 0,
                Widgets = new List<WidgetDsl>
                {
                    new TextWidgetDsl
                    {
                        Id = "intro",
                        Title = "欢迎",
                        Content = "这是一个由演示数据安装器创建的示例仪表盘，展示销售订单相关的元数据与业务语义。",
                        Level = "subtitle",
                    },
                    new FilterWidgetDsl
                    {
                        Id = "status-filter",
                        Title = "订单状态",
                        Field = "status",
                        Control = "select",
                        Options = new List<string> { "已完成", "处理中", "已取消" },
                    },
                },
            },
        },
    };
}
