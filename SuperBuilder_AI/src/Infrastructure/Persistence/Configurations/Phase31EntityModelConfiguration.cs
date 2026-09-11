using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperBuilder_AI.Models.BI.Entity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Data.Configurations;

/// <summary>
/// Phase 3.1 Business Entity 持久化映射。
/// 仅管理 SuperBuilder Metadata DB，不创建或修改动态业务数据库 Schema。
/// </summary>
public sealed class BusinessEntityConfiguration : IEntityTypeConfiguration<BusinessEntity>
{
    public void Configure(EntityTypeBuilder<BusinessEntity> builder)
    {
        builder.ToTable("BusinessEntities", tb => tb.HasComment("业务实体"));
        builder.HasIndex(x => new { x.TenantId, x.BusinessKey }).IsUnique();
        builder.Property(x => x.BusinessKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Domain).WithMany(x => x.Entities).HasForeignKey(x => x.BusinessDomainId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Keys).WithOne(x => x.BusinessEntity).HasForeignKey(x => x.BusinessEntityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Attributes).WithOne(x => x.BusinessEntity).HasForeignKey(x => x.BusinessEntityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Metrics).WithOne(x => x.BusinessEntity).HasForeignKey(x => x.BusinessEntityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.SourceRelationships).WithOne(x => x.SourceEntity).HasForeignKey(x => x.SourceEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.TargetRelationships).WithOne(x => x.TargetEntity).HasForeignKey(x => x.TargetEntityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BusinessEntityKeyConfiguration : IEntityTypeConfiguration<BusinessEntityKey>
{
    public void Configure(EntityTypeBuilder<BusinessEntityKey> builder)
    {
        builder.ToTable("BusinessEntityKeys", tb => tb.HasComment("业务实体键"));
        builder.HasIndex(x => new { x.BusinessEntityId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.BusinessEntityId, x.IsPrimary });
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.BusinessEntity).WithMany(x => x.Keys).HasForeignKey(x => x.BusinessEntityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BusinessEntityAttributeConfiguration : IEntityTypeConfiguration<BusinessEntityAttribute>
{
    public void Configure(EntityTypeBuilder<BusinessEntityAttribute> builder)
    {
        builder.ToTable("BusinessEntityAttributes", tb => tb.HasComment("业务实体属性"));
        builder.HasIndex(x => new { x.BusinessEntityId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.BusinessEntityId, x.IsIdentifier });
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.BusinessEntity).WithMany(x => x.Attributes).HasForeignKey(x => x.BusinessEntityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BusinessEntityMetricConfiguration : IEntityTypeConfiguration<BusinessEntityMetric>
{
    public void Configure(EntityTypeBuilder<BusinessEntityMetric> builder)
    {
        builder.ToTable("BusinessEntityMetrics", tb => tb.HasComment("业务实体指标"));
        builder.HasIndex(x => new { x.BusinessEntityId, x.Name }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Aggregation).HasMaxLength(50);
        // M12 增量：计算口径与结果类型
        builder.Property(x => x.Expression).HasMaxLength(1000);
        builder.Property(x => x.DataType).HasMaxLength(50);
        builder.HasOne(x => x.BusinessEntity).WithMany(x => x.Metrics).HasForeignKey(x => x.BusinessEntityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BusinessEntityRelationshipConfiguration : IEntityTypeConfiguration<BusinessEntityRelationship>
{
    public void Configure(EntityTypeBuilder<BusinessEntityRelationship> builder)
    {
        builder.ToTable("BusinessEntityRelationships", tb => tb.HasComment("业务实体关系"));
        builder.HasIndex(x => new { x.SourceEntityId, x.TargetEntityId, x.Name }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.SourceEntity).WithMany(x => x.SourceRelationships).HasForeignKey(x => x.SourceEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetEntity).WithMany(x => x.TargetRelationships).HasForeignKey(x => x.TargetEntityId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PhysicalBindingConfiguration : IEntityTypeConfiguration<PhysicalBinding>
{
    public void Configure(EntityTypeBuilder<PhysicalBinding> builder)
    {
        builder.ToTable("PhysicalBindings", tb =>
        {
            tb.HasComment("业务语义到物理元数据的映射");
        });

        builder.HasIndex(x => new { x.DataSourceId, x.MetadataTableId, x.MetadataColumnId, x.Priority });
        builder.HasIndex(x => new { x.BusinessEntityKeyId, x.IsActive });
        builder.HasIndex(x => new { x.BusinessEntityAttributeId, x.IsActive });
        builder.HasIndex(x => new { x.BusinessEntityMetricId, x.IsActive });
        builder.HasIndex(x => new { x.BusinessEntityRelationshipId, x.PhysicalRole, x.IsActive });

        builder.HasOne(x => x.BusinessEntityKey).WithMany(x => x.PhysicalBindings).HasForeignKey(x => x.BusinessEntityKeyId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BusinessEntityAttribute).WithMany(x => x.PhysicalBindings).HasForeignKey(x => x.BusinessEntityAttributeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BusinessEntityMetric).WithMany(x => x.PhysicalBindings).HasForeignKey(x => x.BusinessEntityMetricId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BusinessEntityRelationship).WithMany(x => x.PhysicalBindings).HasForeignKey(x => x.BusinessEntityRelationshipId).OnDelete(DeleteBehavior.NoAction);

        // 显式绑定 dependent navigation + FK，禁止 EF 因重复关系生成 DataSourceId1/MetadataTableId1/MetadataColumnId1 shadow FK。
        builder.HasOne(x => x.DataSource).WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MetadataTable).WithMany().HasForeignKey(x => x.MetadataTableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MetadataColumn).WithMany().HasForeignKey(x => x.MetadataColumnId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PhysicalRole).HasMaxLength(50);
        builder.Property(x => x.BindingType).HasMaxLength(50);
    }
}

/// <summary>
/// P3 批次2：业务域映射。
/// </summary>
public sealed class BusinessDomainConfiguration : IEntityTypeConfiguration<BusinessDomain>
{
    public void Configure(EntityTypeBuilder<BusinessDomain> builder)
    {
        builder.ToTable("BusinessDomains", tb => tb.HasComment("业务域"));
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Dimensions).WithOne(x => x.Domain).HasForeignKey(x => x.BusinessDomainId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// P3 批次2：业务维度映射。
/// </summary>
public sealed class BusinessEntityDimensionConfiguration : IEntityTypeConfiguration<BusinessEntityDimension>
{
    public void Configure(EntityTypeBuilder<BusinessEntityDimension> builder)
    {
        builder.ToTable("BusinessEntityDimensions", tb => tb.HasComment("业务实体维度"));
        builder.HasIndex(x => new { x.TenantId, x.BusinessDomainId, x.Name }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        // M12 增量：维度表达式与数据类型 / 时间粒度
        builder.Property(x => x.Expression).HasMaxLength(1000);
        builder.Property(x => x.DataType).HasMaxLength(50);
        builder.HasOne(x => x.Domain).WithMany(x => x.Dimensions).HasForeignKey(x => x.BusinessDomainId).OnDelete(DeleteBehavior.Cascade);
    }
}
