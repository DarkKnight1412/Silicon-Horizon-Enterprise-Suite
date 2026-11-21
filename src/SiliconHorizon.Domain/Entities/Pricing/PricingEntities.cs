using SiliconHorizon.Domain.Common;
using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Domain.Entities.Pricing;

/// <summary>dbo.CustomerGroups — nhóm khách hàng (RETAIL, WHOLESALE) quyết định bảng giá.</summary>
public class CustomerGroup
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsDefault { get; set; }

    public ICollection<ProductPrice> ProductPrices { get; set; } = new List<ProductPrice>();
}

/// <summary>dbo.ProductPrices — giá bán theo nhóm khách (PRC-01).</summary>
public class ProductPrice : IAuditableEntity
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public int CustomerGroupId { get; set; }

    public decimal Price { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public Product Product { get; set; } = null!;

    public CustomerGroup CustomerGroup { get; set; } = null!;
}

/// <summary>dbo.PriceTiers — giá bậc thang theo số lượng (PRC-02).</summary>
public class PriceTier : IAuditableEntity
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public int CustomerGroupId { get; set; }

    public int MinQuantity { get; set; }

    public decimal Price { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public Product Product { get; set; } = null!;

    public CustomerGroup CustomerGroup { get; set; } = null!;
}

/// <summary>dbo.Promotions — khuyến mãi theo sản phẩm / thương hiệu / danh mục (PRC-03..06).</summary>
public class Promotion : IAuditableEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public PromotionScopeType ScopeType { get; set; }

    public long ScopeId { get; set; }

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int? MaxQuantity { get; set; }

    public int? MaxPerCustomer { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }
}

/// <summary>dbo.Vouchers — mã giảm giá (PRC-08).</summary>
public class Voucher : IAuditableEntity
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal? MinOrderAmount { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int? MaxUses { get; set; }

    public int UsedCount { get; set; }

    public int? CustomerGroupId { get; set; }

    public bool IsStackable { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public CustomerGroup? CustomerGroup { get; set; }
}
