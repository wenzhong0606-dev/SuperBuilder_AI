using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Models.Localization;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
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
        #endregion

        // Phase 3.1：Business Entity 只持久化到 SuperBuilder Metadata DB。
        // ApplyConfigurationsFromAssembly 不会连接或修改任何动态业务数据库。
        builder.ApplyConfigurationsFromAssembly(typeof(BusinessEntityConfiguration).Assembly);
    }
}
