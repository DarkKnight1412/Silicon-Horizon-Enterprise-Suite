using SiliconHorizon.Domain.Common;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Domain.Entities.Catalog;

/// <summary>dbo.Categories — danh mục sản phẩm, quyết định bảng thông số con qua <see cref="SpecType"/>.</summary>
public class Category : IAuditableEntity
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public SpecType SpecType { get; set; } = SpecType.Generic;

    public int? ParentId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public Category? Parent { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>dbo.Brands — thương hiệu.</summary>
public class Brand : IAuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>dbo.Products — mặt hàng (SKU), không phải từng đơn vị vật lý (đó là <c>SerialItems</c>).</summary>
public class Product : IAuditableEntity
{
    public long Id { get; set; }

    public string Sku { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public int BrandId { get; set; }

    public int CategoryId { get; set; }

    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    /// <summary>Giá nhập gần nhất (tham khảo); giá vốn thực tế tính từ phiếu nhập.</summary>
    public decimal? LastImportPrice { get; set; }

    public decimal ListPrice { get; set; }

    public bool RequiresSerial { get; set; } = true;

    public int MinStockDefault { get; set; }

    public int WarrantyMonths { get; set; }

    public string? WarrantyTerms { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Draft;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public Brand Brand { get; set; } = null!;

    public Category Category { get; set; } = null!;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}

/// <summary>dbo.ProductImages — ảnh sản phẩm.</summary>
public class ProductImage
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string Url { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }

    public Product Product { get; set; } = null!;
}

/// <summary>dbo.CompatibilityRules — quy tắc kiểm tra tương thích linh kiện (PRD-11).</summary>
public class CompatibilityRule
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int LeftCategoryId { get; set; }

    public int RightCategoryId { get; set; }

    public string LeftField { get; set; } = null!;

    public string RightField { get; set; } = null!;

    /// <summary>Toán tử so sánh: EQ, LTE, GTE (ràng buộc CK_CompatibilityRules_Operator).</summary>
    public string Operator { get; set; } = null!;

    public CompatibilitySeverity Severity { get; set; } = CompatibilitySeverity.Warning;

    public bool IsActive { get; set; } = true;

    public Category LeftCategory { get; set; } = null!;

    public Category RightCategory { get; set; } = null!;
}
