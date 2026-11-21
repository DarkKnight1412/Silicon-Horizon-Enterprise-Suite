using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiliconHorizon.Domain.Entities.Commission;

namespace SiliconHorizon.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình nhóm hoa hồng: CommissionRules, CommissionPeriods, CommissionEntries.</summary>
public class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> builder)
    {
        builder.ToTable("CommissionRules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RatePercent).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.TargetAmount).HasPrecision(18, 0);
        builder.Property(x => x.BonusRatePercent).HasPrecision(5, 2);
        builder.Property(x => x.EffectiveFrom).HasColumnType("date");
        builder.Property(x => x.EffectiveTo).HasColumnType("date");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");
    }
}

/// <summary>Cấu hình bảng dbo.CommissionPeriods (YearMonth dạng "2026-11", duy nhất).</summary>
public class CommissionPeriodConfiguration : IEntityTypeConfiguration<CommissionPeriod>
{
    public void Configure(EntityTypeBuilder<CommissionPeriod> builder)
    {
        builder.ToTable("CommissionPeriods");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.YearMonth).HasColumnType("char(7)").IsRequired();
        builder.Property(x => x.ClosedAt).HasColumnType("datetime2(0)");

        builder.HasIndex(x => x.YearMonth).IsUnique();
    }
}

/// <summary>Cấu hình bảng dbo.CommissionEntries (Amount âm khi thu hồi hoa hồng).</summary>
public class CommissionEntryConfiguration : IEntityTypeConfiguration<CommissionEntry>
{
    public void Configure(EntityTypeBuilder<CommissionEntry> builder)
    {
        builder.ToTable("CommissionEntries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BaseAmount).HasPrecision(18, 0).IsRequired();
        builder.Property(x => x.RatePercent).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 0).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
    }
}
