using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SuperBuilder_AI.Data;
using SuperBuilder_AI.Interfaces.Identity;
using SuperBuilder_AI.Interfaces.Localization;
using SuperBuilder_AI.Interfaces.Seed;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Services.Seed;

/// <summary>
/// E2E 沙箱种子实现（env-gated，仅 E2E_SEED=true 时由 API 启动序列调用）。
/// <para>
/// 复用 <see cref="IIdentityService.CreateUserAsync"/> / <see cref="IIdentityService.SetPasswordAsync"/>（与 DemoDataInstaller 同模式），
/// 创建 <c>e2eapp</c> 租户（自动 id，CI 下通常为 2；code 恒定故矩阵按 code 解析 id，不依赖具体数字）+ 两个账号。
/// e2eadmin 赋 <c>tenant-admin</c>（含 theme:edit/metadata:edit/dashboard:*/app:*/agent:*，满足管理员正向断言）；
/// e2ereader 赋 <c>viewer</c>（仅 view 权限或无此角色则零权限，创建/编辑按钮必隐藏，满足读者负向断言）。
/// </para>
/// </summary>
public sealed class E2ESandboxSeedService : IE2ESandboxSeedService
{
    private const string E2ETenantCode = "e2eapp";
    private const string E2ETenantName = "E2E App";
    private const string AdminUser = "e2eadmin";
    private const string ReaderUser = "e2ereader";
    private const string E2EPassword = "longping00";

    private readonly SuperBIContext _db;
    private readonly IIdentityService _identity;
    private readonly ITenantLanguageService _tenantLanguage;

    public E2ESandboxSeedService(SuperBIContext db, IIdentityService identity, ITenantLanguageService tenantLanguage)
    {
        _db = db;
        _identity = identity;
        _tenantLanguage = tenantLanguage;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (IsCi())
            await EnsureCiBusinessDatabaseAsync(ct);

        var existing = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TenantCode == E2ETenantCode, ct);
        if (existing is not null) return; // 幂等

        var tenant = new Tenant { TenantCode = E2ETenantCode, TenantName = E2ETenantName, Enabled = true };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        // M3-01：语言授权关系，确保 e2eapp 至少一种启用语言（与 DemoDataInstaller 同模式）
        await _tenantLanguage.EnsureTenantLanguagesAsync(tenant.Id, ct);

        var admin = await _identity.CreateUserAsync(
            tenant.Id, AdminUser, "E2E Admin", $"{AdminUser}@{E2ETenantCode}.local",
            new[] { IdentityRoles.TenantAdmin }, ct);
        if (admin.Success && admin.Id.HasValue)
            await _identity.SetPasswordAsync(tenant.Id, admin.Id.Value, E2EPassword, ct);

        var reader = await _identity.CreateUserAsync(
            tenant.Id, ReaderUser, "E2E Reader", $"{ReaderUser}@{E2ETenantCode}.local",
            new[] { IdentityRoles.Viewer }, ct);
        if (reader.Success && reader.Id.HasValue)
            await _identity.SetPasswordAsync(tenant.Id, reader.Id.Value, E2EPassword, ct);
    }
    private static bool IsCi()
        => string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// CI 下复用元数据库所在 SQL Server，创建独立的“外部业务库”。
    /// 这样 DataSource Scan E2E 连接的是另一数据库，而不是扫描平台自己的元数据库。
    /// </summary>
    private async Task EnsureCiBusinessDatabaseAsync(CancellationToken ct)
    {
        var sourceConnection = _db.Database.GetDbConnection().ConnectionString;
        if (string.IsNullOrWhiteSpace(sourceConnection))
            throw new InvalidOperationException("CI E2E 无法读取元数据库连接字符串。");

        var builder = new SqlConnectionStringBuilder(sourceConnection);
        builder.InitialCatalog = "master";

        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.OpenAsync(ct);
            await using var createDb = master.CreateCommand();
            createDb.CommandText =
                "IF DB_ID(N'SuperBuilder_E2E_Business') IS NULL CREATE DATABASE [SuperBuilder_E2E_Business];";
            await createDb.ExecuteNonQueryAsync(ct);
        }

        builder.InitialCatalog = "SuperBuilder_E2E_Business";
        await using var business = new SqlConnection(builder.ConnectionString);
        await business.OpenAsync(ct);

        await using var schema = business.CreateCommand();
        schema.CommandText =
            """
            IF OBJECT_ID(N'dbo.Inventory', N'U') IS NULL
            CREATE TABLE dbo.Inventory(
                Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                MaterialCode NVARCHAR(64) NOT NULL,
                MaterialName NVARCHAR(128) NOT NULL,
                WarehouseCode NVARCHAR(32) NOT NULL,
                Quantity DECIMAL(18,4) NOT NULL,
                UpdatedAt DATETIME2 NOT NULL
            );

            IF OBJECT_ID(N'dbo.PurchaseOrder', N'U') IS NULL
            CREATE TABLE dbo.PurchaseOrder(
                Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                OrderNo NVARCHAR(64) NOT NULL,
                SupplierCode NVARCHAR(64) NOT NULL,
                MaterialCode NVARCHAR(64) NOT NULL,
                OrderQty DECIMAL(18,4) NOT NULL,
                Amount DECIMAL(18,2) NOT NULL,
                OrderDate DATE NOT NULL
            );

            IF OBJECT_ID(N'dbo.SalesOrder', N'U') IS NULL
            CREATE TABLE dbo.SalesOrder(
                Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                OrderNo NVARCHAR(64) NOT NULL,
                CustomerCode NVARCHAR(64) NOT NULL,
                MaterialCode NVARCHAR(64) NOT NULL,
                SalesQty DECIMAL(18,4) NOT NULL,
                SalesAmount DECIMAL(18,2) NOT NULL,
                OrderDate DATE NOT NULL
            );

            IF NOT EXISTS (SELECT 1 FROM dbo.Inventory)
            INSERT INTO dbo.Inventory(MaterialCode, MaterialName, WarehouseCode, Quantity, UpdatedAt)
            VALUES (N'MAT-001', N'E2E Material A', N'WH-01', 100, SYSUTCDATETIME());
            """;
        await schema.ExecuteNonQueryAsync(ct);
    }

}
