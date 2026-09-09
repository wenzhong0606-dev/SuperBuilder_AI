using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Quota;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Models.Theme;
using SuperBuilder_AI.Models.ModelAccount;
using SuperBuilder_AI.Models.Components;
using SuperBuilder_AI.Models;
using SuperBuilder_AI.Data.Configurations;

namespace SuperBuilder_AI.Data;

/// <summary>SuperBI数据库上下文。</summary>
public class SuperBIContext : DbContext
{
    public SuperBIContext(DbContextOptions<SuperBIContext> options) : base(options) { }

    #region P4.3 Global Tenant Query Filter
    // 默认关闭（no-op）。由 Runtime 入口（BIConversationService.ExecuteAsync）在已知 tenantId 后显式调用 ApplyTenantScope 开启；
    // Golden 无租户路径从不调用，故查询恒不过滤。
    // 采用"显式开启"而非"读取 IPlatformContextAccessor"，避免 DbContext 构造期/请求期取值错位
    // （此前 P4.3 曾因读取 System 上下文的 TenantId=0 而过度过滤，触发 Golden 回归）。
    private bool _tenantFilterEnabled;
    private long _scopedTenantId;

    /// <summary>M1-01：审计时间 UTC 转换器。写入时转为 UTC，读回时强制 Kind=Utc（EF 提供程序默认读回 Unspecified）。</summary>
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter =
        new(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    /// <summary>M1-01：可空审计时间 UTC 转换器（UpdatedTime）。</summary>
    private static readonly ValueConverter<DateTime?, DateTime?> UtcNullableDateTimeConverter =
        new(v => v == null ? null : v.Value.ToUniversalTime(),
            v => v == null ? null : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));

    /// <summary>
    /// 在请求作用域内开启全局租户过滤（P4.3 防御性隔离）。
    /// 仅当 <paramref name="tenantId"/> &gt; 0 时启用；否则保持关闭（等价于 no-op），不影响系统/全局/ Golden 路径。
    /// </summary>
    public void ApplyTenantScope(long tenantId)
    {
        _tenantFilterEnabled = tenantId > 0;
        _scopedTenantId = tenantId;
    }
    #endregion

	/// <summary>审计日志为追加写存储：已持久化记录禁止通过 EF 更新或删除。</summary>
	private void EnforceAuditAppendOnly()
	{
		if (ChangeTracker.Entries<AuditLog>().Any(x =>
			x.State is EntityState.Modified or EntityState.Deleted))
			throw new InvalidOperationException("AuditLog is append-only and cannot be updated or deleted.");
	}

	/// <summary>
	/// M1-01：写入期统一回填审计字段与乐观并发版本。
	/// - 新增：RowVersion 初始为 1。
	/// - 修改：UpdatedTime 置为当前 UTC；RowVersion 自增 1。
	/// 由 SaveChanges/SaveChangesAsync 在落库前调用，确保 CreatedTime 恒为 UTC 且并发令牌一致推进。
	/// </summary>
	private void ApplyAuditAndConcurrency()
	{
		foreach (var entry in ChangeTracker.Entries<IAuditable>())
		{
			switch (entry.State)
			{
				case EntityState.Added:
					if (entry.Entity.RowVersion == 0) entry.Entity.RowVersion = 1;
					break;
				case EntityState.Modified:
					entry.Entity.UpdatedTime = DateTime.UtcNow;
					entry.Entity.RowVersion += 1;
					break;
			}
		}
	}

	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		EnforceAuditAppendOnly();
		ApplyAuditAndConcurrency();
		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		EnforceAuditAppendOnly();
		ApplyAuditAndConcurrency();
		return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
	}

    #region Organization
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<DataSource> DataSources { get; set; }
    public DbSet<TenantSetting> TenantSettings { get; set; }
    #endregion

    #region Metadata
    public DbSet<MetadataTable> MetadataTables { get; set; }
    public DbSet<MetadataColumn> MetadataColumns { get; set; }
    public DbSet<MetadataSemantic> MetadataSemantics { get; set; }
    public DbSet<MetadataLearningRecord> LearningRecords { get; set; }
    public DbSet<MetadataScanJob> MetadataScanJobs { get; set; }
    #endregion

    #region P5 Localization
    public DbSet<SemanticLabel> SemanticLabels { get; set; }
        public DbSet<UiLanguage> UiLanguages { get; set; }
        public DbSet<UiTextResource> UiTextResources { get; set; }
        public DbSet<UserLanguagePreference> UserLanguagePreferences { get; set; }
        public DbSet<TenantUiLanguage> TenantUiLanguages { get; set; }
    #endregion

    #region P6 Low-code BI
    public DbSet<Dashboard> Dashboards { get; set; }
    public DbSet<DashboardVersion> DashboardVersions { get; set; }
    #endregion

        #region P7 Multi-Theme / Style Engine
        public DbSet<Theme> Themes { get; set; }
        #endregion

        #region P7.3 Model Accounts (BYO)
        public DbSet<ModelAccount> ModelAccounts { get; set; }
        #endregion

        #region M7-09 Custom Component Library
        public DbSet<CustomComponentDefinition> CustomComponents { get; set; }
        public DbSet<CustomComponentVersion> CustomComponentVersions { get; set; }
        #endregion

    #region P8 AI App Builder
    public DbSet<AppPlan> AppPlans { get; set; }
    public DbSet<AppVersion> AppVersions { get; set; }
    public DbSet<AskQuerySnapshot> AskQuerySnapshots { get; set; }
    public DbSet<AppPublishIdempotency> AppPublishIdempotencies { get; set; }
    #endregion

        #region P9 AI Agent / Copilot
        public DbSet<AgentPlan> AgentPlans { get; set; }
        public DbSet<AgentRun> AgentRuns { get; set; }
        #endregion

        #region P10.1 Identity
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<DataSourceAccessGrant> DataSourceAccessGrants { get; set; }
        public DbSet<RowLevelSecurityPolicy> RowLevelSecurityPolicies { get; set; }
        public DbSet<PlatformAdminTenantScope> PlatformAdminTenantScopes { get; set; }
        public DbSet<UserTenant> UserTenants { get; set; }
        #endregion

        #region P10.3 Audit
        public DbSet<AuditLog> AuditLogs { get; set; }
        #endregion

        #region P10.4 Quota
        public DbSet<QuotaPolicy> QuotaPolicies { get; set; }
        public DbSet<QuotaUsage> QuotaUsages { get; set; }
        #endregion

    #region Phase 3.1 Business Entity
    public DbSet<Models.BI.Entity.BusinessEntity> BusinessEntities { get; set; }
    public DbSet<Models.BI.Entity.BusinessEntityKey> BusinessEntityKeys { get; set; }
    public DbSet<Models.BI.Entity.BusinessEntityAttribute> BusinessEntityAttributes { get; set; }
    public DbSet<Models.BI.Entity.BusinessEntityMetric> BusinessEntityMetrics { get; set; }
    public DbSet<Models.BI.Entity.BusinessEntityRelationship> BusinessEntityRelationships { get; set; }
    public DbSet<Models.BI.Entity.PhysicalBinding> PhysicalBindings { get; set; }
    public DbSet<Models.BI.Entity.BusinessDomain> BusinessDomains { get; set; }
    public DbSet<Models.BI.Entity.BusinessEntityDimension> BusinessEntityDimensions { get; set; }
    #endregion

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            entityType.FindProperty("Id")?.SetComment("主键");
            entityType.FindProperty("CreatedTime")?.SetComment("创建时间");
        }

        #region M1-01 审计与乐观并发（RowVersion / ETag）
        // 对所有实现 IAuditable 的实体（BaseEntity 派生类与 Role）配置应用层托管的并发令牌与 UTC 时间转换。
        // 采用 long + IsConcurrencyToken（而非数据库 rowversion），以兼容 SQL Server 与 SQLite（测试）。
        // UTC 转换器确保审计时间在数据库往返后 Kind 恒为 Utc（EF 提供程序默认会将其读回为 Unspecified）。
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(IAuditable).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .Property("CreatedTime")
                    .HasConversion(UtcDateTimeConverter);
                builder.Entity(entityType.ClrType)
                    .Property("UpdatedTime")
                    .HasConversion(UtcNullableDateTimeConverter);
                builder.Entity(entityType.ClrType)
                    .Property("RowVersion")
                    .IsRequired()
                    .IsConcurrencyToken()
                    .HasDefaultValue(1)
                    .HasComment("乐观并发版本(ETag)，每次更新自增");
            }
        }
        #endregion

        #region Tenant
        builder.Entity<Tenant>().HasIndex(x => x.TenantCode).IsUnique();
        builder.Entity<Tenant>().ToTable(tb => tb.HasComment("租户"));
        builder.Entity<Tenant>().Property(x => x.TenantCode).IsRequired().HasMaxLength(Tenant.MaxCodeLength).HasComment("租户编码（规范化小写存储）");
        builder.Entity<Tenant>().Property(x => x.TenantName).IsRequired().HasMaxLength(Tenant.MaxNameLength).HasComment("租户名称");
        builder.Entity<Tenant>().Property(x => x.Enabled).HasComment("是否启用");
        // M1-02：停用治理字段。
        builder.Entity<Tenant>().Property(x => x.DisabledReason).HasMaxLength(Tenant.MaxDisabledReasonLength).HasComment("停用原因");
        builder.Entity<Tenant>().Property(x => x.DisabledAt).HasConversion(UtcNullableDateTimeConverter).HasComment("停用时间(UTC)");
        builder.Entity<Tenant>().Property(x => x.DisabledByUserId).HasComment("停用操作者用户Id");
        #endregion

        #region TenantSetting
        builder.Entity<TenantSetting>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TenantSetting>().ToTable(tb => tb.HasComment("租户键值配置"));
        builder.Entity<TenantSetting>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        builder.Entity<TenantSetting>().Property(x => x.Key).IsRequired().HasMaxLength(128).HasComment("配置键");
        builder.Entity<TenantSetting>().Property(x => x.Value).HasComment("配置值");
        builder.Entity<TenantSetting>().Property(x => x.DataType).HasMaxLength(32).HasComment("值类型(string|int|bool|json)");
        // M1-02：锁定标记——平台/安全配置租户不可覆盖。
        builder.Entity<TenantSetting>().Property(x => x.IsLocked).IsRequired().HasDefaultValue(false).HasComment("是否锁定(租户不可覆盖)");
        #endregion

        #region DataSource
        // M9-11：移除 Tenant.DataSources 集合导航以打破 Models.Organization → Models.Metadata 循环依赖；
        // 关系改由从属侧 DataSource.Tenant + TenantId 定义，数据库结构（FK/索引）不变。
        builder.Entity<DataSource>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).IsRequired().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DataSource>().ToTable(tb => tb.HasComment("数据源"));
        // M1-04：保留按 TenantId 的常规查询索引（List/Manage 按租户过滤），与下方过滤唯一索引共存。
        builder.Entity<DataSource>().HasIndex(x => x.TenantId).HasDatabaseName("IX_DataSources_TenantId");
        // M1 退出门禁：作为 MetadataTable 组合外键的主键，强制表与数据源属于同一租户。
        builder.Entity<DataSource>().HasAlternateKey(x => new { x.Id, x.TenantId })
            .HasName("AK_DataSources_Id_TenantId");
        // M1-04：名称规范化 + 租户内唯一，写入路径与数据库双重强制必填与去重。
        builder.Entity<DataSource>().Property(x => x.Name).IsRequired().HasMaxLength(128).HasComment("名称（展示用，保留原始大小写）");
        builder.Entity<DataSource>().Property(x => x.NormalizedName).IsRequired().HasMaxLength(128).HasComment("规范化名称（小写去空白），租户内唯一键");
        builder.Entity<DataSource>().HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique()
            .HasDatabaseName("IX_DataSources_TenantId_NormalizedName");
        builder.Entity<DataSource>().Property(x => x.DbType).IsRequired().HasMaxLength(32).HasComment("数据库类型(MYSQL/SQLSERVER/POSTGRESQL)");
        builder.Entity<DataSource>().Property(x => x.ConnectionString).IsRequired().HasMaxLength(2048).HasComment("连接字符串（敏感，禁止日志记录）");
        // M1-04：Enabled 非空（默认启用），兼容既有种子与查询计划测试。
        builder.Entity<DataSource>().Property(x => x.Enabled).IsRequired().HasDefaultValue(true).HasComment("是否启用");
        // M1-04：连接测试记录（脱敏）。
        builder.Entity<DataSource>().Property(x => x.LastTestStatus).HasMaxLength(32).HasComment("最近连接测试状态(Ok/Failed/Unknown)");
        builder.Entity<DataSource>().Property(x => x.LastTestTime).HasConversion(UtcNullableDateTimeConverter).HasComment("最近连接测试时间(UTC)");
        builder.Entity<DataSource>().Property(x => x.LastErrorCode).HasMaxLength(64).HasComment("最近连接测试错误码(仅异常类型名,脱敏)");
        #endregion

        #region MetadataTable
        builder.Entity<MetadataTable>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).IsRequired().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MetadataTable>().HasOne(x => x.DataSource).WithMany(x => x.Tables)
            .HasForeignKey(x => new { x.DataSourceId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId })
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MetadataTable>().ToTable(tb => tb.HasComment("元数据表"));
        // 租户内唯一键：DataSource + Catalog + Schema + Table。
        // 使用过滤唯一索引：仅当 Catalog/Schema 均非空时才强制唯一，
        // 兼容存量/测试中以 (DataSourceId, TableName) 唯一的历史数据。
        builder.Entity<MetadataTable>()
            .HasIndex(x => new { x.DataSourceId, x.CatalogName, x.SchemaName, x.TableName })
            .IsUnique()
            .HasDatabaseName("IX_MetadataTables_DataSourceId_CatalogName_SchemaName_TableName")
            .HasFilter("[CatalogName] IS NOT NULL AND [SchemaName] IS NOT NULL");
        builder.Entity<MetadataTable>().HasIndex(x => x.DataSourceId)
            .HasDatabaseName("IX_MetadataTables_DataSourceId");
        builder.Entity<MetadataTable>().Property(x => x.TableName).IsRequired().HasMaxLength(128).HasComment("表名");
        builder.Entity<MetadataTable>().Property(x => x.CatalogName).HasMaxLength(128).HasComment("目录名");
        builder.Entity<MetadataTable>().Property(x => x.SchemaName).HasMaxLength(128).HasComment("模式名");
        builder.Entity<MetadataTable>().Property(x => x.SearchText).HasComment("Embedding文本");
        builder.Entity<MetadataTable>().Property(x => x.VectorId).HasComment("Qdrant向量ID");
        builder.Entity<MetadataTable>().Property(x => x.EmbeddingModel).HasMaxLength(128).HasComment("Embedding模型");
        builder.Entity<MetadataTable>().Property(x => x.VectorDimension).HasComment("向量维度");
        builder.Entity<MetadataTable>().Property(x => x.VectorSyncTime).HasComment("向量同步时间(UTC)");
        builder.Entity<MetadataTable>().Property(x => x.VectorStatus).HasMaxLength(16).HasComment("向量状态");
        builder.Entity<MetadataTable>().Property(x => x.VectorErrorCode).HasMaxLength(64).HasComment("向量错误码");
        #endregion

        #region MetadataColumn
        builder.Entity<MetadataColumn>().HasOne(x => x.MetadataTable).WithMany(x => x.Columns).HasForeignKey(x => x.MetadataTableId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MetadataColumn>().ToTable(tb => tb.HasComment("元数据字段"));
        builder.Entity<MetadataColumn>().HasIndex(x => new { x.MetadataTableId, x.ColumnName }).IsUnique();
        builder.Entity<MetadataColumn>().Property(x => x.ColumnName).IsRequired().HasMaxLength(128).HasComment("列名");
        builder.Entity<MetadataColumn>().Property(x => x.Ordinal).HasDefaultValue(0).HasComment("列序号");
        builder.Entity<MetadataColumn>().Property(x => x.NativeType).HasMaxLength(64).HasComment("原生类型");
        builder.Entity<MetadataColumn>().Property(x => x.Precision).HasComment("精度");
        builder.Entity<MetadataColumn>().Property(x => x.Scale).HasComment("小数位");
        builder.Entity<MetadataColumn>().Property(x => x.SearchText).HasComment("字段Embedding文本");
        builder.Entity<MetadataColumn>().Property(x => x.VectorId).HasComment("Qdrant字段向量ID");
        builder.Entity<MetadataColumn>().Property(x => x.BusinessKey).HasComment("字段业务唯一标识");
        builder.Entity<MetadataColumn>().Property(x => x.EmbeddingModel).HasMaxLength(128).HasComment("Embedding模型");
        builder.Entity<MetadataColumn>().Property(x => x.VectorDimension).HasComment("向量维度");
        builder.Entity<MetadataColumn>().Property(x => x.VectorSyncTime).HasComment("向量同步时间(UTC)");
        builder.Entity<MetadataColumn>().Property(x => x.VectorStatus).HasMaxLength(16).HasComment("向量状态");
        builder.Entity<MetadataColumn>().Property(x => x.VectorErrorCode).HasMaxLength(64).HasComment("向量错误码");
        #endregion

        #region MetadataSemantic
        builder.Entity<MetadataSemantic>().HasOne(x => x.MetadataColumn).WithOne(x => x.Semantic).HasForeignKey<MetadataSemantic>(x => x.MetadataColumnId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MetadataSemantic>().HasIndex(x => x.MetadataColumnId).IsUnique();
        builder.Entity<MetadataSemantic>().Property(x => x.Confidence).HasColumnType("decimal(5,4)")
            .HasComment("AI生成置信度(0-1)");
        builder.Entity<MetadataSemantic>().ToTable(tb => tb.HasComment("字段AI语义"));
        // 置信度检查约束：NULL 或落在 [0,1]。
        builder.Entity<MetadataSemantic>().ToTable(tb => tb.HasCheckConstraint(
            "CK_MetadataSemantics_Confidence",
            "[Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 1)"));
        builder.Entity<MetadataSemantic>().Property(x => x.BusinessMeaning).HasComment("业务含义");
        builder.Entity<MetadataSemantic>().Property(x => x.Keywords).HasMaxLength(2048).HasComment("关键词");
        builder.Entity<MetadataSemantic>().Property(x => x.Synonyms).HasMaxLength(2048).HasComment("同义词");
        builder.Entity<MetadataSemantic>().Property(x => x.ExampleQuestions).HasMaxLength(2048).HasComment("示例问题");
        builder.Entity<MetadataSemantic>().Property(x => x.BusinessDomain).HasComment("业务域");
        builder.Entity<MetadataSemantic>().Property(x => x.Source)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(SemanticSource.Manual)
            .HasComment("来源");
        builder.Entity<MetadataSemantic>().Property(x => x.SearchText).HasComment("语义Embedding文本");
        builder.Entity<MetadataSemantic>().Property(x => x.VectorId).HasComment("Qdrant语义向量ID");
        builder.Entity<MetadataSemantic>().Property(x => x.EmbeddingModel).HasMaxLength(128).HasComment("Embedding模型");
        builder.Entity<MetadataSemantic>().Property(x => x.VectorDimension).HasComment("向量维度");
        builder.Entity<MetadataSemantic>().Property(x => x.VectorSyncTime).HasComment("向量同步时间(UTC)");
        builder.Entity<MetadataSemantic>().Property(x => x.VectorStatus).HasMaxLength(16).HasComment("向量状态");
        builder.Entity<MetadataSemantic>().Property(x => x.VectorErrorCode).HasMaxLength(64).HasComment("向量错误码");
        #endregion

        #region Learning
        builder.Entity<MetadataLearningRecord>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        builder.Entity<MetadataLearningRecord>().HasOne(x => x.MetadataColumn).WithMany().HasForeignKey(x => x.MetadataColumnId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<MetadataLearningRecord>().ToTable(tb => tb.HasComment("学习记录"));
        #endregion

        #region MetadataScanJob
        builder.Entity<MetadataScanJob>().HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MetadataScanJob>().ToTable(tb => tb.HasComment("元数据扫描任务"));
        builder.Entity<MetadataScanJob>().Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(MetadataScanJobStatus.Queued).HasComment("扫描状态");
        builder.Entity<MetadataScanJob>().Property(x => x.ProgressPercent).HasDefaultValue(0).HasComment("进度百分比");
        builder.Entity<MetadataScanJob>().Property(x => x.StartedAt).HasConversion(UtcNullableDateTimeConverter).HasComment("开始时间(UTC)");
        builder.Entity<MetadataScanJob>().Property(x => x.FinishedAt).HasConversion(UtcNullableDateTimeConverter).HasComment("结束时间(UTC)");
        builder.Entity<MetadataScanJob>().Property(x => x.TablesScanned).HasDefaultValue(0).HasComment("已扫描表数");
        builder.Entity<MetadataScanJob>().Property(x => x.ColumnsScanned).HasDefaultValue(0).HasComment("已扫描字段数");
        builder.Entity<MetadataScanJob>().Property(x => x.OrphansDetected).HasDefaultValue(0).HasComment("孤儿对象数");
        builder.Entity<MetadataScanJob>().Property(x => x.TriggeredBy).HasMaxLength(64).HasComment("触发用户标识");
        builder.Entity<MetadataScanJob>().Property(x => x.ErrorCode).HasMaxLength(64).HasComment("错误码(仅异常类型名,脱敏)");
        builder.Entity<MetadataScanJob>().Property(x => x.ErrorMessage).HasMaxLength(2000).HasComment("错误摘要(脱敏,不含连接串)");
        builder.Entity<MetadataScanJob>().HasIndex(x => x.DataSourceId).HasDatabaseName("IX_MetadataScanJobs_DataSourceId");
        builder.Entity<MetadataScanJob>().HasIndex(x => x.TenantId).HasDatabaseName("IX_MetadataScanJobs_TenantId");
        builder.Entity<MetadataScanJob>().HasIndex(x => x.Status).HasDatabaseName("IX_MetadataScanJobs_Status");
        #endregion

        #region P5.2 SemanticLabel
        builder.Entity<SemanticLabel>().ToTable(tb => tb.HasComment("语义多语言标签"));
        // 唯一键含 SortOrder：同义词/示例问句是多值标签（一个概念一种语言可有多条），
        // 若只按 LabelKind 唯一，第二条同义词会撞键被覆盖。以 SortOrder 作为槽位区分多值。
        // 读取侧按 Value 去重，避免同一译文占多个槽位时重复展示。
        builder.Entity<SemanticLabel>()
            .HasIndex(x => new { x.TenantId, x.ConceptType, x.ConceptId, x.Culture, x.LabelKind, x.SortOrder })
            .IsUnique();
        // 按概念 + 语言 + 排序 取标签的高频查询路径
        builder.Entity<SemanticLabel>()
            .HasIndex(x => new { x.ConceptType, x.ConceptId, x.Culture });
        builder.Entity<SemanticLabel>().Property(x => x.TenantId).HasComment("所属租户（0=全局共享）");
        builder.Entity<SemanticLabel>().Property(x => x.ConceptType).IsRequired().HasMaxLength(64).HasComment("概念类型");
        builder.Entity<SemanticLabel>().Property(x => x.ConceptId).HasComment("概念实体Id");
        builder.Entity<SemanticLabel>().Property(x => x.Culture).IsRequired().HasMaxLength(16).HasComment("语言标签");
        builder.Entity<SemanticLabel>().Property(x => x.LabelKind).IsRequired().HasMaxLength(32).HasComment("标签种类");
        builder.Entity<SemanticLabel>().Property(x => x.Value).IsRequired().HasMaxLength(512).HasComment("标签文本");
        builder.Entity<SemanticLabel>().Property(x => x.Source).HasMaxLength(32).HasComment("来源");
        builder.Entity<SemanticLabel>().Property(x => x.SortOrder).HasComment("排序");
        #endregion

        #region UI Localization
        builder.Entity<UiLanguage>().ToTable(tb => tb.HasComment("平台界面语言目录"));
        builder.Entity<UiLanguage>().HasIndex(x => x.Culture).IsUnique();
        builder.Entity<UiLanguage>().Property(x => x.Culture).IsRequired().HasMaxLength(16);
        builder.Entity<UiLanguage>().Property(x => x.DisplayName).IsRequired().HasMaxLength(64);
        builder.Entity<UiLanguage>().Property(x => x.NativeName).IsRequired().HasMaxLength(64);
        builder.Entity<UiTextResource>().ToTable(tb => tb.HasComment("平台及租户界面文本"));
        builder.Entity<UiTextResource>().HasIndex(x => new { x.TenantId, x.Culture, x.ResourceKey }).IsUnique();
        builder.Entity<UiTextResource>().Property(x => x.Culture).IsRequired().HasMaxLength(16);
        builder.Entity<UiTextResource>().Property(x => x.ResourceKey).IsRequired().HasMaxLength(160);
        builder.Entity<UiTextResource>().Property(x => x.Value).IsRequired().HasMaxLength(2048);
        builder.Entity<UiTextResource>().Property(x => x.Description).HasMaxLength(256);
        builder.Entity<UiTextResource>().Property(x => x.IsTranslated).IsRequired().HasDefaultValue(true).HasComment("是否已翻译（新建语言从其它语言复制键集合时标记待翻译）");
        builder.Entity<UserLanguagePreference>().ToTable(tb => tb.HasComment("用户界面语言偏好"));
        builder.Entity<UserLanguagePreference>().HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        builder.Entity<UserLanguagePreference>().Property(x => x.Culture).IsRequired().HasMaxLength(16);

        // M3-01 租户界面语言关系（替代 localization:availableCultures/defaultCulture JSON）
        builder.Entity<TenantUiLanguage>().ToTable(tb => tb.HasComment("租户界面语言关系（替代 localization:availableCultures/defaultCulture JSON）"));
        builder.Entity<TenantUiLanguage>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TenantUiLanguage>().HasOne<UiLanguage>().WithMany().HasForeignKey(x => x.UiLanguageId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TenantUiLanguage>().HasIndex(x => new { x.TenantId, x.UiLanguageId }).IsUnique();
        builder.Entity<TenantUiLanguage>().HasIndex(x => new { x.TenantId, x.IsDefault });
        builder.Entity<TenantUiLanguage>().Property(x => x.TenantId).HasComment("所属租户");
        builder.Entity<TenantUiLanguage>().Property(x => x.UiLanguageId).HasComment("平台语言目录 Id");
        builder.Entity<TenantUiLanguage>().Property(x => x.Enabled).IsRequired().HasDefaultValue(true).HasComment("是否启用（租户范围内）");
        builder.Entity<TenantUiLanguage>().Property(x => x.IsDefault).IsRequired().HasDefaultValue(false).HasComment("是否为租户默认语言（每租户恰一个）");
        builder.Entity<TenantUiLanguage>().Property(x => x.SortOrder).HasComment("排序");


        #endregion

        #region P6.1 Dashboard
        // 刻意不建指向 Tenant 的外键：TenantId=0 表示全局模板，而 Tenant 表无 Id=0 的行，
        // 加外键会在写入全局模板时违反外键约束（与 P5.2 SemanticLabel 保持一致）。
        builder.Entity<Dashboard>().ToTable(tb => tb.HasComment("仪表盘"));
        builder.Entity<Dashboard>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.Entity<Dashboard>().HasIndex(x => new { x.TenantId, x.Status });
        builder.Entity<Dashboard>().Property(x => x.TenantId).HasComment("所属租户（0=全局模板）");
        builder.Entity<Dashboard>().Property(x => x.Code).IsRequired().HasMaxLength(128).HasComment("业务编码");
        builder.Entity<Dashboard>().Property(x => x.Title).IsRequired().HasMaxLength(256).HasComment("标题");
        builder.Entity<Dashboard>().Property(x => x.Description).HasMaxLength(1024).HasComment("描述");
        builder.Entity<Dashboard>().Property(x => x.Status).IsRequired().HasMaxLength(32).HasComment("状态");
        builder.Entity<Dashboard>().Property(x => x.DslVersion).IsRequired().HasMaxLength(16).HasComment("DSL版本");
        builder.Entity<Dashboard>().Property(x => x.DslJson).IsRequired().HasComment("DSL文档（结构化，非裸HTML）");
        builder.Entity<Dashboard>().Property(x => x.ThemeKey).HasMaxLength(64).HasComment("主题键");
        builder.Entity<Dashboard>().Property(x => x.PublishedDslJson).HasComment("已发布DSL快照（null=未发布）");
        builder.Entity<Dashboard>().Property(x => x.PublishedVersion).HasComment("当前发布版本号（0=未发布）");
        builder.Entity<Dashboard>().Property(x => x.PublishedAt).HasComment("最近发布时间(UTC)");
        builder.Entity<Dashboard>().Property(x => x.PublishedBy).HasMaxLength(128).HasComment("最近发布者");
        #endregion

        #region P6.1 DashboardVersion（M7-01 版本快照）
        // 与 Dashboard 一致：TenantId=0 表示全局模板，不建指向 Tenant 的外键。
        builder.Entity<DashboardVersion>().ToTable(tb => tb.HasComment("仪表盘发布版本快照"));
        builder.Entity<DashboardVersion>().HasIndex(x => new { x.DashboardId, x.Version }).IsUnique()
            .HasDatabaseName("IX_DashboardVersions_DashboardId_Version");
        builder.Entity<DashboardVersion>().HasIndex(x => new { x.TenantId, x.DashboardId })
            .HasDatabaseName("IX_DashboardVersions_TenantId_DashboardId");
        builder.Entity<DashboardVersion>().Property(x => x.TenantId).HasComment("作用域租户（0=全局模板）");
        builder.Entity<DashboardVersion>().Property(x => x.Code).IsRequired().HasMaxLength(128).HasComment("业务编码快照");
        builder.Entity<DashboardVersion>().Property(x => x.Title).IsRequired().HasMaxLength(256).HasComment("标题快照");
        builder.Entity<DashboardVersion>().Property(x => x.Description).HasMaxLength(1024).HasComment("描述快照");
        builder.Entity<DashboardVersion>().Property(x => x.ThemeKey).HasMaxLength(64).HasComment("主题键快照");
        builder.Entity<DashboardVersion>().Property(x => x.DslVersion).IsRequired().HasMaxLength(16).HasComment("DSL版本快照");
        builder.Entity<DashboardVersion>().Property(x => x.DslJson).IsRequired().HasComment("发布时刻固化的DSL文档（只读快照）");
        builder.Entity<DashboardVersion>().Property(x => x.PublishedBy).HasMaxLength(128).HasComment("发布者");
        builder.Entity<DashboardVersion>().Property(x => x.RolledBackFromVersion).HasComment("回滚来源版本号（非回滚为null）");
        builder.Entity<DashboardVersion>()
            .HasOne<Dashboard>().WithMany().HasForeignKey(x => x.DashboardId)
            .OnDelete(DeleteBehavior.Cascade);
        #endregion

        #region P7.1 Theme
        // 与 Dashboard 一致：TenantId=0 表示内置/全局模板，不建指向 Tenant 的外键（Tenant 表无 Id=0 行）。
        builder.Entity<Theme>().ToTable(tb => tb.HasComment("主题"));
        builder.Entity<Theme>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        builder.Entity<Theme>().HasIndex(x => x.TenantId);
        builder.Entity<Theme>().Property(x => x.TenantId).HasComment("所属租户（0=内置/全局模板）");
        builder.Entity<Theme>().Property(x => x.Key).IsRequired().HasMaxLength(64).HasComment("主题键（同租户内唯一）");
        builder.Entity<Theme>().Property(x => x.Name).IsRequired().HasMaxLength(128).HasComment("主题名称");
        builder.Entity<Theme>().Property(x => x.IsBuiltIn).HasComment("是否内置主题");
        builder.Entity<Theme>().Property(x => x.DslVersion).IsRequired().HasMaxLength(16).HasComment("DSL版本");
        builder.Entity<Theme>().Property(x => x.DslJson).IsRequired().HasComment("主题DSL文档（结构化令牌，非CSS/HTML）");
        #endregion

        #region P7.3 ModelAccount (BYO, M7-07)
        // 租户级模型账号绑定；密钥仅密文存储。TenantId 恒 > 0，无全局行，故不建指向 Tenant 的外键（同 AgentRun）。
        builder.Entity<ModelAccount>().ToTable(tb => tb.HasComment("模型账号绑定（BYO 加密存储）"));
        builder.Entity<ModelAccount>().HasIndex(x => x.TenantId).HasDatabaseName("IX_ModelAccounts_TenantId");
        // 同一租户内 (Provider, ModelId) 唯一：一个模型仅一个绑定；写入路径与数据库双重强制。
        builder.Entity<ModelAccount>().HasIndex(x => new { x.TenantId, x.Provider, x.ModelId }).IsUnique()
            .HasDatabaseName("IX_ModelAccounts_TenantId_Provider_ModelId");
        builder.Entity<ModelAccount>().Property(x => x.TenantId).HasComment("所属租户（>0）");
        builder.Entity<ModelAccount>().Property(x => x.Provider).IsRequired().HasMaxLength(64).HasComment("供应商标识(Qwen/OpenAI/...)");
        builder.Entity<ModelAccount>().Property(x => x.ModelId).IsRequired().HasMaxLength(128).HasComment("模型标识(qwen-plus/gpt-4o/...)");
        builder.Entity<ModelAccount>().Property(x => x.DisplayName).IsRequired().HasMaxLength(128).HasComment("展示名");
        builder.Entity<ModelAccount>().Property(x => x.EncryptedKey).IsRequired().HasMaxLength(2048).HasComment("AES-GCM 密文(禁止日志记录/明文下发)");
        builder.Entity<ModelAccount>().Property(x => x.MaskedKey).IsRequired().HasMaxLength(64).HasComment("展示掩码(如 sk-***1234)");
        builder.Entity<ModelAccount>().Property(x => x.Note).HasMaxLength(512).HasComment("备注");
        builder.Entity<ModelAccount>().Property(x => x.IsDefault).IsRequired().HasDefaultValue(false).HasComment("是否为租户默认模型(每租户至多一个)");
        #endregion

        #region M7-09 Custom Component Library
        builder.Entity<CustomComponentDefinition>().ToTable(tb => tb.HasComment("租户自定义组件定义"));
        builder.Entity<CustomComponentDefinition>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        builder.Entity<CustomComponentDefinition>().HasIndex(x => new { x.TenantId, x.ComponentType });
        builder.Entity<CustomComponentDefinition>().Property(x => x.Key).IsRequired().HasMaxLength(64);
        builder.Entity<CustomComponentDefinition>().Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Entity<CustomComponentDefinition>().Property(x => x.Description).HasMaxLength(1024);
        builder.Entity<CustomComponentDefinition>().Property(x => x.ComponentType).IsRequired().HasMaxLength(32);
        builder.Entity<CustomComponentDefinition>().Property(x => x.DslVersion).IsRequired().HasMaxLength(16);
        builder.Entity<CustomComponentDefinition>().Property(x => x.DslJson).IsRequired().HasComment("结构化组件草稿DSL（禁止HTML/脚本）");
        builder.Entity<CustomComponentDefinition>().Property(x => x.PublishedDslJson).HasComment("已发布组件DSL快照");
        builder.Entity<CustomComponentDefinition>().Property(x => x.PublishedBy).HasMaxLength(128);

        builder.Entity<CustomComponentVersion>().ToTable(tb => tb.HasComment("自定义组件发布版本快照"));
        builder.Entity<CustomComponentVersion>().HasIndex(x => new { x.ComponentId, x.Version }).IsUnique();
        builder.Entity<CustomComponentVersion>().HasIndex(x => new { x.TenantId, x.ComponentId });
        builder.Entity<CustomComponentVersion>().Property(x => x.Key).IsRequired().HasMaxLength(64);
        builder.Entity<CustomComponentVersion>().Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Entity<CustomComponentVersion>().Property(x => x.Description).HasMaxLength(1024);
        builder.Entity<CustomComponentVersion>().Property(x => x.ComponentType).IsRequired().HasMaxLength(32);
        builder.Entity<CustomComponentVersion>().Property(x => x.DslVersion).IsRequired().HasMaxLength(16);
        builder.Entity<CustomComponentVersion>().Property(x => x.DslJson).IsRequired();
        builder.Entity<CustomComponentVersion>().Property(x => x.PublishedBy).HasMaxLength(128);
        builder.Entity<CustomComponentVersion>()
            .HasOne(x => x.Component)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.ComponentId)
            .OnDelete(DeleteBehavior.Cascade);
        #endregion

        #region P8.1 AppPlan
        // 与 Dashboard/Theme 一致：TenantId=0 表示全局模板，不建指向 Tenant 的外键（Tenant 表无 Id=0 行）。
        builder.Entity<AppPlan>().ToTable(tb => tb.HasComment("应用"));
        builder.Entity<AppPlan>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.Entity<AppPlan>().HasIndex(x => new { x.TenantId, x.Status });
        builder.Entity<AppPlan>().Property(x => x.TenantId).HasComment("所属租户（0=全局模板）");
        builder.Entity<AppPlan>().Property(x => x.Code).IsRequired().HasMaxLength(128).HasComment("业务编码");
        builder.Entity<AppPlan>().Property(x => x.Name).IsRequired().HasMaxLength(256).HasComment("名称");
        builder.Entity<AppPlan>().Property(x => x.Description).HasMaxLength(1024).HasComment("描述");
        builder.Entity<AppPlan>().Property(x => x.Status).IsRequired().HasMaxLength(32).HasComment("状态");
        builder.Entity<AppPlan>().Property(x => x.DslVersion).IsRequired().HasMaxLength(16).HasComment("DSL版本");
        builder.Entity<AppPlan>().Property(x => x.DslJson).IsRequired().HasComment("DSL文档（结构化，非裸HTML）");
        builder.Entity<AppPlan>().Property(x => x.ThemeKey).HasMaxLength(64).HasComment("主题键");
        builder.Entity<AppPlan>().Property(x => x.PublishedDslJson).HasComment("已发布DSL快照（null=未发布）");
        builder.Entity<AppPlan>().Property(x => x.PublishedVersion).HasComment("当前发布版本号（0=未发布）");
        builder.Entity<AppPlan>().Property(x => x.PublishedAt).HasComment("最近发布时间(UTC)");
        builder.Entity<AppPlan>().Property(x => x.PublishedBy).HasMaxLength(128).HasComment("最近发布者");
        builder.Entity<AppPlan>().Property(x => x.DraftRevision).IsRequired().HasDefaultValue(1).HasComment("草稿修订乐观并发令牌(每次编辑+1,发布校验用)");
        #endregion

        #region P8.2 AppVersion（M7-02 版本快照）
        // 与 AppPlan 一致：TenantId=0 表示全局模板，不建指向 Tenant 的外键。
        builder.Entity<AppVersion>().ToTable(tb => tb.HasComment("应用发布版本快照"));
        builder.Entity<AppVersion>().HasIndex(x => new { x.AppId, x.Version }).IsUnique()
            .HasDatabaseName("IX_AppVersions_AppId_Version");
        builder.Entity<AppVersion>().HasIndex(x => new { x.TenantId, x.AppId })
            .HasDatabaseName("IX_AppVersions_TenantId_AppId");
        builder.Entity<AppVersion>().Property(x => x.TenantId).HasComment("作用域租户（0=全局模板）");
        builder.Entity<AppVersion>().Property(x => x.Code).IsRequired().HasMaxLength(128).HasComment("业务编码快照");
        builder.Entity<AppVersion>().Property(x => x.Name).IsRequired().HasMaxLength(256).HasComment("名称快照");
        builder.Entity<AppVersion>().Property(x => x.Description).HasMaxLength(1024).HasComment("描述快照");
        builder.Entity<AppVersion>().Property(x => x.ThemeKey).HasMaxLength(64).HasComment("主题键快照");
        builder.Entity<AppVersion>().Property(x => x.DslVersion).IsRequired().HasMaxLength(16).HasComment("DSL版本快照");
        builder.Entity<AppVersion>().Property(x => x.DslJson).IsRequired().HasComment("发布时刻固化的DSL文档（只读快照）");
        builder.Entity<AppVersion>().Property(x => x.PublishedBy).HasMaxLength(128).HasComment("发布者");
        builder.Entity<AppVersion>().Property(x => x.RolledBackFromVersion).HasComment("回滚来源版本号（非回滚为null）");
        builder.Entity<AppVersion>()
            .HasOne<AppPlan>().WithMany().HasForeignKey(x => x.AppId)
            .OnDelete(DeleteBehavior.Cascade);
        #endregion

        #region M7-11 AskQuerySnapshot（应用运行时查询上下文引用）
        builder.Entity<AskQuerySnapshot>().ToTable(tb => tb.HasComment("Ask 查询快照（应用运行时引用）"));
        builder.Entity<AskQuerySnapshot>().HasKey(x => x.TurnId);
        builder.Entity<AskQuerySnapshot>().Property(x => x.TurnId).IsRequired().HasMaxLength(64).HasComment("查询引用标识(GUID)");
        builder.Entity<AskQuerySnapshot>().Property(x => x.TenantId).IsRequired().HasComment("所属租户");
        builder.Entity<AskQuerySnapshot>().Property(x => x.UserId).IsRequired().HasComment("快照创建者");
        builder.Entity<AskQuerySnapshot>().Property(x => x.DataSourceId).IsRequired().HasComment("解析数据源Id（运行时硬约束）");
        builder.Entity<AskQuerySnapshot>().Property(x => x.EntityCode).HasMaxLength(128).HasComment("主表业务实体语义名");
        builder.Entity<AskQuerySnapshot>().Property(x => x.QueryPlanJson).IsRequired().HasComment("允许查询的QueryPlan语义(JSON,RLS注入前截取)");
        builder.Entity<AskQuerySnapshot>().Property(x => x.RequestHash).IsRequired().HasMaxLength(128).HasComment("请求摘要哈希(创建幂等冲突检测)");
        builder.Entity<AskQuerySnapshot>().Property(x => x.ExpiresAt).IsRequired().HasComment("过期时间(UTC)");
        builder.Entity<AskQuerySnapshot>().HasIndex(x => new { x.TenantId, x.UserId }).HasDatabaseName("IX_AskQuerySnapshots_TenantId_UserId");
        builder.Entity<AskQuerySnapshot>().HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_AskQuerySnapshots_ExpiresAt");
        #endregion

        #region M7-11 AppPublishIdempotency（发布/回滚幂等）
        builder.Entity<AppPublishIdempotency>().ToTable(tb => tb.HasComment("应用发布/回滚幂等记录"));
        builder.Entity<AppPublishIdempotency>().HasKey(x => x.Id);
        builder.Entity<AppPublishIdempotency>().Property(x => x.TenantId).IsRequired().HasComment("作用域租户");
        builder.Entity<AppPublishIdempotency>().Property(x => x.AppCode).IsRequired().HasMaxLength(128).HasComment("应用业务编码");
        builder.Entity<AppPublishIdempotency>().Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128).HasComment("客户端幂等键(UUID)");
        builder.Entity<AppPublishIdempotency>().Property(x => x.ExpectedDraftRevision).HasComment("发布时的期望草稿修订号");
        builder.Entity<AppPublishIdempotency>().Property(x => x.PublishedVersion).HasComment("成功发布固化的版本号(同键重入返回此值)");
        builder.Entity<AppPublishIdempotency>().Property(x => x.CreatedAt).HasComment("记录创建时间(UTC)");
        builder.Entity<AppPublishIdempotency>()
            .HasIndex(x => new { x.TenantId, x.AppCode, x.IdempotencyKey }).IsUnique()
            .HasDatabaseName("IX_AppPublishIdempotencies_Tenant_AppCode_Key");
        #endregion

        #region P9.1 AgentPlan
        // 与 Dashboard/Theme/AppPlan 一致：TenantId=0 表示全局模板，不建指向 Tenant 的外键（Tenant 表无 Id=0 行）。
        builder.Entity<AgentPlan>().ToTable(tb => tb.HasComment("Agent计划"));
        builder.Entity<AgentPlan>().HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.Entity<AgentPlan>().HasIndex(x => new { x.TenantId, x.Status });
        builder.Entity<AgentPlan>().Property(x => x.TenantId).HasComment("所属租户（0=全局模板）");
        builder.Entity<AgentPlan>().Property(x => x.Code).IsRequired().HasMaxLength(128).HasComment("业务编码");
        builder.Entity<AgentPlan>().Property(x => x.Name).IsRequired().HasMaxLength(256).HasComment("名称");
        builder.Entity<AgentPlan>().Property(x => x.Description).HasMaxLength(1024).HasComment("描述");
        builder.Entity<AgentPlan>().Property(x => x.Status).IsRequired().HasMaxLength(32).HasComment("状态");
        builder.Entity<AgentPlan>().Property(x => x.DslVersion).IsRequired().HasMaxLength(16).HasComment("DSL版本");
        builder.Entity<AgentPlan>().Property(x => x.DslJson).IsRequired().HasComment("DSL文档（结构化，非裸HTML）");

        #region M7.3 AgentRun
        builder.Entity<AgentRun>().ToTable(tb => tb.HasComment("Agent运行记录（M7-03 Agent Runtime）"));
        builder.Entity<AgentRun>().HasIndex(x => new { x.TenantId, x.PlanCode });
        builder.Entity<AgentRun>().HasIndex(x => x.Status);
        builder.Entity<AgentRun>().Property(x => x.TenantId).HasComment("所属租户（运行恒归属某一租户）");
        builder.Entity<AgentRun>().Property(x => x.PlanCode).IsRequired().HasMaxLength(128).HasComment("Agent编码");
        builder.Entity<AgentRun>().Property(x => x.Status).IsRequired().HasMaxLength(32).HasComment("运行状态");
        builder.Entity<AgentRun>().Property(x => x.Actor).HasMaxLength(256).HasComment("触发者");
        builder.Entity<AgentRun>().Property(x => x.ResultSummary).HasMaxLength(1024).HasComment("结果摘要");
        builder.Entity<AgentRun>().Property(x => x.StepLogJson).HasComment("每步执行结果（JSON 信封）");
        builder.Entity<AgentRun>().Property(x => x.GrantedToolsJson).HasComment("授权工具集合（JSON）");
        #endregion
        #endregion

        #region P10.1 Identity
        // User：租户作用域。用户名唯一范围收窄为租户内 (TenantId, NormalizedUsername)（DEC-02）。
        builder.Entity<User>().ToTable(tb => tb.HasComment("用户"));
        builder.Entity<User>().HasOne(u => u.Tenant).WithMany().HasForeignKey(u => u.TenantId).IsRequired().OnDelete(DeleteBehavior.Restrict);
        builder.Entity<User>().HasIndex(u => new { u.TenantId, u.NormalizedUsername }).IsUnique()
            .HasDatabaseName("IX_Users_TenantId_NormalizedUsername");
        builder.Entity<User>().HasIndex(u => u.TenantId);
        builder.Entity<User>().Property(u => u.TenantId).HasComment("所属租户");
        builder.Entity<User>().Property(u => u.Username).IsRequired().HasMaxLength(128).HasComment("登录名（展示用，大小写原始）");
        builder.Entity<User>().Property(u => u.NormalizedUsername).HasMaxLength(128).HasComment("规范化登录名（小写，租户内唯一）");
        builder.Entity<User>().Property(u => u.DisplayName).IsRequired().HasMaxLength(128).HasComment("显示名");
        builder.Entity<User>().Property(u => u.Email).HasMaxLength(256).HasComment("邮箱");
        builder.Entity<User>().Property(u => u.NormalizedEmail).HasMaxLength(256).HasComment("规范化邮箱（小写）");
        builder.Entity<User>().Property(u => u.EmailConfirmed).IsRequired().HasDefaultValue(false).HasComment("邮箱是否已验证");
        builder.Entity<User>().Property(u => u.Status).HasComment("状态");
        builder.Entity<User>().Property(u => u.PasswordHash).HasMaxLength(256).HasComment("口令哈希（PBKDF2，可选）");
        builder.Entity<User>().Property(u => u.SecurityStamp).IsRequired().HasMaxLength(64).HasComment("安全戳（令牌吊销用）");

        // Role：TenantId=0 为平台全局角色；同租户内 Code 唯一。
        builder.Entity<Role>().ToTable(tb => tb.HasComment("角色"));
        builder.Entity<Role>().HasIndex(r => new { r.TenantId, r.Code }).IsUnique();
        builder.Entity<Role>().Property(r => r.TenantId).HasComment("所属租户（0=全局角色）");
        builder.Entity<Role>().Property(r => r.Code).IsRequired().HasMaxLength(64).HasComment("角色码（同租户唯一）");
        builder.Entity<Role>().Property(r => r.Name).IsRequired().HasMaxLength(128).HasComment("角色名");

        // Permission：TenantId=0 为平台全局权限；同租户内 Code 唯一。
        builder.Entity<Permission>().ToTable(tb => tb.HasComment("权限"));
        builder.Entity<Permission>().HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
        builder.Entity<Permission>().Property(p => p.TenantId).HasComment("所属租户（0=全局权限）");
        builder.Entity<Permission>().Property(p => p.Code).IsRequired().HasMaxLength(64).HasComment("权限码（同租户唯一）");
        builder.Entity<Permission>().Property(p => p.Name).IsRequired().HasMaxLength(128).HasComment("权限名");
        builder.Entity<Permission>().Property(p => p.Category).IsRequired().HasMaxLength(32).HasComment("权限分类");

        // UserRole：同租户内 (User, Role) 唯一；FK 到 User/Role，级联删除保证引用完整性。
        builder.Entity<UserRole>().ToTable(tb => tb.HasComment("用户-角色关联"));
        builder.Entity<UserRole>().HasIndex(ur => new { ur.TenantId, ur.UserId, ur.RoleId }).IsUnique();
        builder.Entity<UserRole>()
            .HasOne<User>().WithMany().HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<UserRole>()
            .HasOne<Role>().WithMany().HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);

        // RolePermission：同租户内 (Role, Permission) 唯一；FK 到 Role/Permission，级联删除保证引用完整性。
        builder.Entity<RolePermission>().ToTable(tb => tb.HasComment("角色-权限关联"));
        builder.Entity<RolePermission>().HasIndex(rp => new { rp.TenantId, rp.RoleId, rp.PermissionId }).IsUnique();
        builder.Entity<RolePermission>()
            .HasOne<Role>().WithMany().HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<RolePermission>()
            .HasOne<Permission>().WithMany().HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);
		builder.Entity<DataSourceAccessGrant>().ToTable("DataSourceAccessGrants");
		builder.Entity<DataSourceAccessGrant>().HasIndex(x => new { x.TenantId, x.DataSourceId, x.SubjectType, x.SubjectId }).IsUnique();
		builder.Entity<DataSourceAccessGrant>().HasIndex(x => new { x.TenantId, x.SubjectType, x.SubjectId });
		builder.Entity<DataSourceAccessGrant>().HasOne<DataSource>().WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Cascade);
		builder.Entity<DataSourceAccessGrant>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
		builder.Entity<RowLevelSecurityPolicy>().ToTable("RowLevelSecurityPolicies");
		builder.Entity<RowLevelSecurityPolicy>().HasIndex(x => new { x.TenantId, x.DataSourceId, x.MetadataTableId, x.Enabled });
		builder.Entity<RowLevelSecurityPolicy>().HasIndex(x => new { x.TenantId, x.SubjectType, x.SubjectId });
		builder.Entity<RowLevelSecurityPolicy>().Property(x => x.Operator).HasMaxLength(16).IsRequired();
		builder.Entity<RowLevelSecurityPolicy>().Property(x => x.Value).HasMaxLength(2048).IsRequired();
		builder.Entity<RowLevelSecurityPolicy>().Property(x => x.SubjectKey).HasMaxLength(128);
		builder.Entity<RowLevelSecurityPolicy>().Property(x => x.SubjectValue).HasMaxLength(512);
		builder.Entity<RowLevelSecurityPolicy>().HasOne<DataSource>().WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<RowLevelSecurityPolicy>().HasOne<MetadataTable>().WithMany().HasForeignKey(x => x.MetadataTableId).OnDelete(DeleteBehavior.Cascade);
		builder.Entity<RowLevelSecurityPolicy>().HasOne<MetadataColumn>().WithMany().HasForeignKey(x => x.MetadataColumnId).OnDelete(DeleteBehavior.Restrict);
		// M1-06 防御性 CHECK：明确 Everyone 类型（SubjectId/SubjectKey 均空），User/Role 必须指定 SubjectId，Attribute 必须指定 SubjectKey。
		builder.Entity<RowLevelSecurityPolicy>().HasCheckConstraint("CK_RlsPolicies_SubjectConsistency",
			"([SubjectType] = 0 AND [SubjectId] IS NULL AND [SubjectKey] IS NULL) OR ([SubjectType] = 1 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 2 AND [SubjectId] IS NOT NULL) OR ([SubjectType] = 3 AND [SubjectKey] IS NOT NULL) OR ([SubjectType] NOT IN (0,1,2,3))");
		// M1-06 防御性 CHECK：Operator 仅允许受控词表（与 RlsVocabularyValidator.AllowedOperators 对齐）。
		builder.Entity<RowLevelSecurityPolicy>().HasCheckConstraint("CK_RlsPolicies_Operator",
			"[Operator] IN ('=', '!=', '>', '>=', '<', '<=', 'LIKE', 'IN', 'IS NULL', 'IS NOT NULL')");

		#endregion

		#region M2-02 PlatformAdminTenantScope
		// 平台管理员租户范围白名单：无记录 = 默认管理全部租户；有记录 = 仅所列租户。
		// 与 DataSourceAccessGrant / RowLevelSecurityPolicy 一致，级联删除保证引用完整性（管理员/租户删除时联动清理）。
		builder.Entity<PlatformAdminTenantScope>().ToTable(tb => tb.HasComment("平台管理员租户范围绑定"));
		builder.Entity<PlatformAdminTenantScope>().HasIndex(x => x.AdminUserId);
		builder.Entity<PlatformAdminTenantScope>().HasIndex(x => x.TenantId);
		builder.Entity<PlatformAdminTenantScope>().HasIndex(x => new { x.AdminUserId, x.TenantId }).IsUnique();
		builder.Entity<PlatformAdminTenantScope>().Property(x => x.GrantedBy).HasMaxLength(128).HasComment("授权操作者");
		builder.Entity<PlatformAdminTenantScope>().Property(x => x.GrantedAt).HasComment("授权时间(UTC)");
		builder.Entity<PlatformAdminTenantScope>().HasOne<User>().WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
		builder.Entity<PlatformAdminTenantScope>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
		#endregion

		#region M2-05 UserTenant（多租户成员关系 / SB-P1-16）
		// 用户在主租户之外的可切换成员租户；唯一(UserId, TenantId)；级联删除保证引用完整性。
		builder.Entity<UserTenant>().ToTable(tb => tb.HasComment("用户—租户成员关系（多租户切换）"));
		builder.Entity<UserTenant>().HasIndex(x => x.UserId);
		builder.Entity<UserTenant>().HasIndex(x => x.TenantId);
		builder.Entity<UserTenant>().HasIndex(x => new { x.UserId, x.TenantId }).IsUnique();
		builder.Entity<UserTenant>().Property(x => x.CreatedByUserId).HasComment("操作者用户 Id（管理员代加成员）");
		builder.Entity<UserTenant>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
		builder.Entity<UserTenant>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
		#endregion

		#region PhysicalBinding (M1-06)
		builder.Entity<PhysicalBinding>().ToTable("PhysicalBindings");
		builder.Entity<PhysicalBinding>().HasIndex(x => new { x.DataSourceId, x.MetadataTableId, x.MetadataColumnId });
		builder.Entity<PhysicalBinding>().HasIndex(x => new { x.BusinessEntityKeyId, x.BusinessEntityAttributeId, x.BusinessEntityMetricId, x.BusinessEntityRelationshipId });
		builder.Entity<PhysicalBinding>().HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<PhysicalBinding>().HasOne(x => x.MetadataTable).WithMany().HasForeignKey(x => x.MetadataTableId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<PhysicalBinding>().HasOne(x => x.MetadataColumn).WithMany().HasForeignKey(x => x.MetadataColumnId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<PhysicalBinding>().HasOne(x => x.BusinessEntityKey).WithMany().HasForeignKey(x => x.BusinessEntityKeyId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<PhysicalBinding>().HasOne(x => x.BusinessEntityAttribute).WithMany().HasForeignKey(x => x.BusinessEntityAttributeId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<PhysicalBinding>().HasOne(x => x.BusinessEntityMetric).WithMany().HasForeignKey(x => x.BusinessEntityMetricId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<PhysicalBinding>().HasOne(x => x.BusinessEntityRelationship).WithMany().HasForeignKey(x => x.BusinessEntityRelationshipId).OnDelete(DeleteBehavior.Restrict);
		// Priority 非负由 CHECK 与写入路径(BusinessEntityService.ValidateBindingsAsync)双重保证。
		// 「恰好一个 Owner」为跨列业务规则，因既有种子(0 owner)存在，按 M1-02/03/04 约定仅在写入路径强制，不加硬 CHECK。
		builder.Entity<PhysicalBinding>().HasCheckConstraint("CK_PhysicalBindings_PriorityNonNeg", "[Priority] >= 0");
        #endregion

		#region M1-06 BusinessDomain / MetadataSemantic 收敛
		builder.Entity<BusinessDomain>().ToTable("BusinessDomains");
		builder.Entity<BusinessDomain>().HasIndex(x => x.TenantId);
		builder.Entity<BusinessDomain>().Property(x => x.Name).IsRequired().HasMaxLength(128).HasComment("业务域名称");
		builder.Entity<BusinessDomain>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
		// MetadataSemantic.BusinessDomain 字符串保留作展示兼容；BusinessDomainId 为权威外键。
		builder.Entity<MetadataSemantic>().Property(x => x.BusinessDomainId).HasComment("业务域Id（FK 权威，逐步替代字符串 BusinessDomain）");
		builder.Entity<MetadataSemantic>().HasOne(x => x.BusinessDomainRef).WithMany().HasForeignKey(x => x.BusinessDomainId).OnDelete(DeleteBehavior.SetNull);
        #endregion

        #region P10.3 Audit
        builder.Entity<AuditLog>().ToTable(tb => tb.HasComment("审计日志"));
        builder.Entity<AuditLog>().HasIndex(a => a.TenantId);
        builder.Entity<AuditLog>().HasIndex(a => new { a.TenantId, a.Action });
        builder.Entity<AuditLog>().HasIndex(a => new { a.TenantId, a.EntityType });
        builder.Entity<AuditLog>().HasIndex(a => a.Timestamp);
        builder.Entity<AuditLog>().Property(a => a.TenantId).HasComment("所属租户（0=平台级）");
        builder.Entity<AuditLog>().Property(a => a.Actor).IsRequired().HasMaxLength(128).HasComment("操作者标识");
        builder.Entity<AuditLog>().Property(a => a.Action).IsRequired().HasMaxLength(128).HasComment("动作类型");
        builder.Entity<AuditLog>().Property(a => a.EntityType).IsRequired().HasMaxLength(128).HasComment("实体类型");
        builder.Entity<AuditLog>().Property(a => a.EntityId).HasMaxLength(256).HasComment("实体Id");
        builder.Entity<AuditLog>().Property(a => a.Result).IsRequired().HasMaxLength(32).HasComment("结果");
        #endregion

        #region P10.4 Quota
        builder.Entity<QuotaPolicy>().ToTable(tb => tb.HasComment("配额策略"));
        builder.Entity<QuotaPolicy>().HasIndex(q => new { q.TenantId, q.ResourceType }).IsUnique();
        builder.Entity<QuotaPolicy>().Property(q => q.TenantId).HasComment("所属租户（0=平台默认）");
        builder.Entity<QuotaPolicy>().Property(q => q.ResourceType).HasComment("资源类型");
        builder.Entity<QuotaPolicy>().Property(q => q.Limit).HasComment("上限");
        builder.Entity<QuotaPolicy>().Property(q => q.Window).HasComment("周期窗口");

        builder.Entity<QuotaUsage>().ToTable(tb => tb.HasComment("配额使用量"));
        builder.Entity<QuotaUsage>().HasIndex(q => new { q.TenantId, q.ResourceType }).IsUnique();
        builder.Entity<QuotaUsage>().Property(q => q.TenantId).HasComment("所属租户");
        builder.Entity<QuotaUsage>().Property(q => q.ResourceType).HasComment("资源类型");
        builder.Entity<QuotaUsage>().Property(q => q.Used).HasComment("已用");
        builder.Entity<QuotaUsage>().Property(q => q.PeriodKey).IsRequired().HasMaxLength(32).HasComment("周期键");
        #endregion

        #region P4.3 Global Tenant Query Filter
        // 直接持有 TenantId 的根实体施加全局过滤；子实体经父实体 FK 间接隔离。
        // 表达式 !_tenantFilterEnabled || e.TenantId == _scopedTenantId：
        //   - 未开启（Golden/系统路径，ApplyTenantScope 未被调用）：!false => 恒真，SQL 不产生 WHERE，等价于 no-op；
        //   - 已开启：e.TenantId == 当前租户，强制跨租户不可见（与现有应用层手动 tenantId 过滤一致）。
        // DataSource.TenantId 为数据库必填字段，可直接与当前租户比较。
        // 与 !_tenantFilterEnabled 做 || 时破坏 EF 表达式处理（"Nullable object must have a value"）。
        builder.Entity<DataSource>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);
        builder.Entity<MetadataTable>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);
        builder.Entity<Models.BI.Entity.BusinessEntity>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);
        builder.Entity<TenantSetting>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);

        // SemanticLabel：租户私有标签 + 全局共享标签（TenantId=0）均对本租户可见。
        // 与其余实体不同，此处显式放行 TenantId == 0，否则全局译文在租户作用域内会被误过滤。
        builder.Entity<SemanticLabel>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

        // Dashboard：同 SemanticLabel，放行 TenantId == 0 的全局模板（否则租户作用域内模板会消失）。
        builder.Entity<Dashboard>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

        // Theme：同 Dashboard，放行 TenantId == 0 的内置/全局主题（租户可继承平台默认主题）。
        builder.Entity<Theme>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

        // ModelAccount：租户专属绑定，不放行 TenantId == 0（绑定恒归属某一租户，无全局绑定）。
        builder.Entity<ModelAccount>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);

        builder.Entity<CustomComponentDefinition>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);
        builder.Entity<CustomComponentVersion>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);

        // AppPlan：同 Dashboard/Theme，放行 TenantId == 0 的全局模板（租户可继承平台默认应用）。
        builder.Entity<AppPlan>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

        // AgentPlan：同 Dashboard/Theme/AppPlan，放行 TenantId == 0 的全局模板。
        builder.Entity<AgentPlan>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

        // AgentRun：租户专属运行记录，不放行 TenantId == 0（运行恒归属某一租户，不存在全局运行）。
        builder.Entity<AgentRun>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);

        // User：租户作用域，不放行 TenantId == 0（用户恒归属某一租户）。
        builder.Entity<User>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);

        // Role/Permission：与 AgentPlan 一致，放行 TenantId == 0 的全局角色/权限（租户可继承平台默认）。
        builder.Entity<Role>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);
        builder.Entity<Permission>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

        // AuditLog：与 AgentPlan 等一致，放行 TenantId == 0 的全局审计（租户可查看平台级审计）。
        builder.Entity<AuditLog>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

        // QuotaPolicy：与 Role/Permission 一致，放行 TenantId == 0 的平台默认配额（租户可继承平台默认）。
        builder.Entity<QuotaPolicy>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

        // QuotaUsage：租户级使用量，不放行 TenantId == 0（用量恒归属某一租户）。
        builder.Entity<QuotaUsage>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId);

        // UiTextResource：同 SemanticLabel/Dashboard，放行 TenantId == 0 的平台基线（租户作用域内自动继承基线译文），
        // 同时仅可见本租户覆盖；租户作用域未开启时为 no-op，不影响平台治理/种子/Golden 路径。
        builder.Entity<UiTextResource>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);
        #endregion

        // Phase 3.1：Business Entity 只持久化到 SuperBuilder Metadata DB。
        // ApplyConfigurationsFromAssembly 不会连接或修改任何动态业务数据库。
        builder.ApplyConfigurationsFromAssembly(typeof(BusinessEntityConfiguration).Assembly);
    }
}
