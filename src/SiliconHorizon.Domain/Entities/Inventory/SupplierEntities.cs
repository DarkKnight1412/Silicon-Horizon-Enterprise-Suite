using SiliconHorizon.Domain.Common;
using SiliconHorizon.Domain.Entities.Catalog;

namespace SiliconHorizon.Domain.Entities.Inventory;

/// <summary>dbo.Suppliers — nhà cung cấp (SUP-01..03). CRUD bằng EF Core.</summary>
public class Supplier : IAuditableEntity
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? TaxCode { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public int PaymentTermDays { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public ICollection<SupplierProduct> Products { get; set; } = new List<SupplierProduct>();
}

/// <summary>dbo.SupplierProducts — nhà cung cấp có thể bán những sản phẩm nào (SUP-03).</summary>
public class SupplierProduct
{
    public long SupplierId { get; set; }

    public long ProductId { get; set; }

    public string? SupplierSku { get; set; }

    public Supplier Supplier { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
