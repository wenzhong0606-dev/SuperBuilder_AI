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

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

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
    }
}

public sealed class BusinessEntityRelationshipConfiguration : IEntityTypeConfiguration<BusinessEntityRelationship>
{
    public void Configure(EntityTypeBuilder<BusinessEntityRelationship> builder)
    {
        builder.ToTable("BusinessEntityRelationships", tb => tb.HasComment("业务实体关系"));
        builder.HasIndex(x => new { x.SourceEntityId, x.TargetEntityId, x.Name }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}

public sealed class PhysicalBindingConfiguration : IEntityTypeConfiguration<PhysicalBinding>
{
    public void Configure(EntityTypeBuilder<PhysicalBinding> builder)
    {
        builder.ToTable("PhysicalBindings", tb =>
        {
            tb.HasComment("业务语义到物理元数据的映射");
            tb.HasCheckConstraint(
                "CK_PhysicalBindings_ExactlyOneOwner",
                "((CASE WHEN BusinessEntityKeyId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityAttributeId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityMetricId IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN BusinessEntityRelationshipId IS NOT NULL THEN 1 ELSE 0 END)) = 1");
        });

        builder.HasIndex(x => new { x.DataSourceId, x.MetadataTableId, x.MetadataColumnId, x.Priority });
        builder.HasIndex(x => new { x.BusinessEntityKeyId, x.IsActive });
        builder.HasIndex(x => new { x.BusinessEntityAttributeId, x.IsActive });
        builder.HasIndex(x => new { x.BusinessEntityMetricId, x.IsActive });
        builder.HasIndex(x => new { x.BusinessEntityRelationshipId, x.PhysicalRole, x.IsActive });

        builder.HasOne(x => x.BusinessEntityKey).WithMany(x => x.PhysicalBindings).HasForeignKey(x => x.BusinessEntityKeyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.BusinessEntityAttribute).WithMany(x => x.PhysicalBindings).HasForeignKey(x => x.BusinessEntityAttributeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.BusinessEntityMetric).WithMany(x => x.PhysicalBindings).HasForeignKey(x => x.BusinessEntityMetricId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.BusinessEntityRelationship).WithMany(x => x.PhysicalBindings).HasForeignKey(x => x.BusinessEntityRelationshipId).OnDelete(DeleteBehavior.Cascade);

        // 这些 FK 只引用 SuperBuilder 自己的 Metadata DB 记录，不跨库指向动态业务数据库。
        builder.HasOne<DataSource>().WithMany().HasForeignKey(x => x.DataSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MetadataTable>().WithMany().HasForeignKey(x => x.MetadataTableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MetadataColumn>().WithMany().HasForeignKey(x => x.MetadataColumnId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PhysicalRole).HasMaxLength(50);
        builder.Property(x => x.BindingType).HasMaxLength(50);
    }
}
