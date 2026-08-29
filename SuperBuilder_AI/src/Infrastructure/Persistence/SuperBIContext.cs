using Microsoft.EntityFrameworkCore;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;
using SuperBuilder_AI.Data.Configurations;

namespace SuperBuilder_AI.Data;

/// <summary>SuperBI数据库上下文。</summary>
public class SuperBIContext : DbContext
{
    public SuperBIContext(DbContextOptions<SuperBIContext> options) : base(options) { }

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

        // Phase 3.1：Business Entity 只持久化到 SuperBuilder Metadata DB。
        // ApplyConfigurationsFromAssembly 不会连接或修改任何动态业务数据库。
        builder.ApplyConfigurationsFromAssembly(typeof(BusinessEntityConfiguration).Assembly);
    }
}
