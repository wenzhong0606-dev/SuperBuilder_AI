using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperBuilder_AI.Models.Identity;
using SuperBuilder_AI.Models.Metadata;
using SuperBuilder_AI.Models.Organization;

namespace SuperBuilder_AI.Data.Configurations;

/// <summary>
/// M1 closure hardening. These mappings intentionally tighten the remaining schema compromises
/// that were kept nullable / application-only during the earlier migration batches to preserve
/// incomplete legacy test fixtures. New fixtures must satisfy the production invariants instead.
/// </summary>
public sealed class M1ClosureTenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(x => x.TenantCode).IsRequired().HasMaxLength(Tenant.MaxCodeLength);
        builder.Property(x => x.TenantName).IsRequired().HasMaxLength(Tenant.MaxNameLength);
    }
}

public sealed class M1ClosureUserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.SecurityStamp).IsRequired().HasMaxLength(64);
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class M1ClosureDataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.DbType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ConnectionString).IsRequired().HasMaxLength(2048);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.DataSources)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class M1ClosureMetadataTableConfiguration : IEntityTypeConfiguration<MetadataTable>
{
    public void Configure(EntityTypeBuilder<MetadataTable> builder)
    {
        builder.Property(x => x.TenantId).IsRequired();
        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_MetadataTables_TenantId");
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class M1ClosureMetadataColumnConfiguration : IEntityTypeConfiguration<MetadataColumn>
{
    public void Configure(EntityTypeBuilder<MetadataColumn> builder)
    {
        builder.Property(x => x.MetadataTableId).IsRequired();
        builder.HasOne(x => x.MetadataTable)
            .WithMany(x => x.Columns)
            .HasForeignKey(x => x.MetadataTableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
