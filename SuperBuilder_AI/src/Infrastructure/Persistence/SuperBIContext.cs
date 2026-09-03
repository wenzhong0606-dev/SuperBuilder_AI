using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Models.Agent;
using SuperBuilder_AI.Models.AppBuilder;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Audit;
using SuperBuilder_AI.Models.Quota;
using SuperBuilder_AI.Models.Dashboard;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Models.Theme;
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

	public override int SaveChanges(bool acceptAllChangesOnSuccess)
	{
		EnforceAuditAppendOnly();
		return base.SaveChanges(acceptAllChangesOnSuccess);
	}

	public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
	{
		EnforceAuditAppendOnly();
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
    #endregion

    #region P5 Localization
    public DbSet<SemanticLabel> SemanticLabels { get; set; }
    public DbSet<UiLanguage> UiLanguages { get; set; }
    public DbSet<UiTextResource> UiTextResources { get; set; }
    #endregion

    #region P6 Low-code BI
    public DbSet<Dashboard> Dashboards { get; set; }
    #endregion

    #region P7 Multi-Theme / Style Engine
    public DbSet<Theme> Themes { get; set; }
    #endregion

    #region P8 AI App Builder
    public DbSet<AppPlan> AppPlans { get; set; }
    #endregion

        #region P9 AI Agent / Copilot
        public DbSet<AgentPlan> AgentPlans { get; set; }
        #endregion

        #region P10.1 Identity
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<DataSourceAccessGrant> DataSourceAccessGrants { get; set; }
        public DbSet<RowLevelSecurityPolicy> RowLevelSecurityPolicies { get; set; }
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

        #region Tenant
        builder.Entity<Tenant>().HasIndex(x => x.TenantCode).IsUnique();
        builder.Entity<Tenant>().ToTable(tb => tb.HasComment("租户"));
        builder.Entity<Tenant>().Property(x => x.TenantCode).HasComment("租户编码");
        builder.Entity<Tenant>().Property(x => x.TenantName).HasComment("租户名称");
        builder.Entity<Tenant>().Property(x => x.Enabled).HasComment("是否启用");
        #endregion

        #region TenantSetting
        builder.Entity<TenantSetting>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TenantSetting>().ToTable(tb => tb.HasComment("租户键值配置"));
        builder.Entity<TenantSetting>().HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        builder.Entity<TenantSetting>().Property(x => x.Key).IsRequired().HasMaxLength(128).HasComment("配置键");
        builder.Entity<TenantSetting>().Property(x => x.Value).HasComment("配置值");
        builder.Entity<TenantSetting>().Property(x => x.DataType).HasMaxLength(32).HasComment("值类型");
        #endregion

        #region DataSource
        builder.Entity<DataSource>().HasOne(x => x.Tenant).WithMany(x => x.DataSources).HasForeignKey(x => x.TenantId);
        builder.Entity<DataSource>().ToTable(tb => tb.HasComment("数据源"));
        builder.Entity<DataSource>().Property(x => x.ConnectionString).HasComment("连接字符串");
        builder.Entity<DataSource>().Property(x => x.DbType).HasComment("数据库类型");
        #endregion

        #region MetadataTable
        builder.Entity<MetadataTable>().HasOne(x => x.DataSource).WithMany(x => x.Tables).HasForeignKey(x => x.DataSourceId);
        builder.Entity<MetadataTable>().ToTable(tb => tb.HasComment("元数据表"));
        builder.Entity<MetadataTable>().HasIndex(x => new { x.DataSourceId, x.TableName }).IsUnique();
        builder.Entity<MetadataTable>().Property(x => x.SearchText).HasComment("Embedding文本");
        builder.Entity<MetadataTable>().Property(x => x.VectorId).HasComment("Qdrant向量ID");
        #endregion

        #region MetadataColumn
        builder.Entity<MetadataColumn>().HasOne(x => x.MetadataTable).WithMany(x => x.Columns).HasForeignKey(x => x.MetadataTableId);
        builder.Entity<MetadataColumn>().ToTable(tb => tb.HasComment("元数据字段"));
        builder.Entity<MetadataColumn>().HasIndex(x => new { x.MetadataTableId, x.ColumnName }).IsUnique();
        builder.Entity<MetadataColumn>().Property(x => x.SearchText).HasComment("字段Embedding文本");
        builder.Entity<MetadataColumn>().Property(x => x.VectorId).HasComment("Qdrant字段向量ID");
        builder.Entity<MetadataColumn>().Property(x => x.BusinessKey).HasComment("字段业务唯一标识");
        #endregion

        #region MetadataSemantic
        builder.Entity<MetadataSemantic>().HasOne(x => x.MetadataColumn).WithOne(x => x.Semantic).HasForeignKey<MetadataSemantic>(x => x.MetadataColumnId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<MetadataSemantic>().HasIndex(x => x.MetadataColumnId).IsUnique();
        builder.Entity<MetadataSemantic>().Property(x => x.Confidence).HasColumnType("decimal(5,4)");
        builder.Entity<MetadataSemantic>().ToTable(tb => tb.HasComment("字段AI语义"));
        builder.Entity<MetadataSemantic>().Property(x => x.BusinessMeaning).HasComment("业务含义");
        builder.Entity<MetadataSemantic>().Property(x => x.Keywords).HasComment("关键词");
        builder.Entity<MetadataSemantic>().Property(x => x.Synonyms).HasComment("同义词");
        builder.Entity<MetadataSemantic>().Property(x => x.ExampleQuestions).HasComment("示例问题");
        builder.Entity<MetadataSemantic>().Property(x => x.BusinessDomain).HasComment("业务域");
        builder.Entity<MetadataSemantic>().Property(x => x.Source).HasComment("来源");
        builder.Entity<MetadataSemantic>().Property(x => x.SearchText).HasComment("语义Embedding文本");
        builder.Entity<MetadataSemantic>().Property(x => x.VectorId).HasComment("Qdrant语义向量ID");
        #endregion

        #region Learning
        builder.Entity<MetadataLearningRecord>().ToTable(tb => tb.HasComment("学习记录"));
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
        #endregion

        #region P10.1 Identity
        // User：租户作用域，用户名全局唯一（TenantId=0 不放开，用户始终归属某一租户）。
        builder.Entity<User>().ToTable(tb => tb.HasComment("用户"));
        builder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        builder.Entity<User>().HasIndex(u => u.TenantId);
        builder.Entity<User>().Property(u => u.TenantId).HasComment("所属租户");
        builder.Entity<User>().Property(u => u.Username).IsRequired().HasMaxLength(128).HasComment("登录名（全局唯一）");
        builder.Entity<User>().Property(u => u.DisplayName).IsRequired().HasMaxLength(128).HasComment("显示名");
        builder.Entity<User>().Property(u => u.Email).HasMaxLength(256).HasComment("邮箱");
        builder.Entity<User>().Property(u => u.Status).HasComment("状态");
        builder.Entity<User>().Property(u => u.PasswordHash).HasMaxLength(256).HasComment("口令哈希（PBKDF2，可选）");
        builder.Entity<User>().Property(u => u.SecurityStamp).HasMaxLength(64).HasComment("安全戳（令牌吊销用）");

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

        // UserRole：同租户内 (User, Role) 唯一。
        builder.Entity<UserRole>().ToTable(tb => tb.HasComment("用户-角色关联"));
        builder.Entity<UserRole>().HasIndex(ur => new { ur.TenantId, ur.UserId, ur.RoleId }).IsUnique();

        // RolePermission：同租户内 (Role, Permission) 唯一。
        builder.Entity<RolePermission>().ToTable(tb => tb.HasComment("角色-权限关联"));
        builder.Entity<RolePermission>().HasIndex(rp => new { rp.TenantId, rp.RoleId, rp.PermissionId }).IsUnique();
		builder.Entity<DataSourceAccessGrant>().ToTable("DataSourceAccessGrants");
		builder.Entity<DataSourceAccessGrant>().HasIndex(x => new { x.TenantId, x.DataSourceId, x.SubjectType, x.SubjectId }).IsUnique();
		builder.Entity<DataSourceAccessGrant>().HasIndex(x => new { x.TenantId, x.SubjectType, x.SubjectId });
		builder.Entity<DataSourceAccessGrant>().HasOne<DataSource>().WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Cascade);
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
        // DataSource.TenantId 为 long?：必须用 HasValue 守卫，避免 long? == long 产生被提升的 bool?
        // 与 !_tenantFilterEnabled 做 || 时破坏 EF 表达式处理（"Nullable object must have a value"）。
        builder.Entity<DataSource>().HasQueryFilter(e => !_tenantFilterEnabled || (e.TenantId.HasValue && e.TenantId.Value == _scopedTenantId));
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

        // AppPlan：同 Dashboard/Theme，放行 TenantId == 0 的全局模板（租户可继承平台默认应用）。
        builder.Entity<AppPlan>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

        // AgentPlan：同 Dashboard/Theme/AppPlan，放行 TenantId == 0 的全局模板。
        builder.Entity<AgentPlan>().HasQueryFilter(e => !_tenantFilterEnabled || e.TenantId == _scopedTenantId || e.TenantId == 0);

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
        #endregion

        // Phase 3.1：Business Entity 只持久化到 SuperBuilder Metadata DB。
        // ApplyConfigurationsFromAssembly 不会连接或修改任何动态业务数据库。
        builder.ApplyConfigurationsFromAssembly(typeof(BusinessEntityConfiguration).Assembly);
    }
}
