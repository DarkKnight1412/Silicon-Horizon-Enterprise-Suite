using SiliconHorizon.Domain.Common;
using SiliconHorizon.Domain.Entities.Pricing;

namespace SiliconHorizon.Domain.Entities.Customers;

/// <summary>dbo.Customers — khách lẻ hoặc khách sỉ; UserId NULL nghĩa là khách vãng lai.</summary>
public class Customer : IAuditableEntity
{
    public long Id { get; set; }

    public long? UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public int CustomerGroupId { get; set; }

    public string? TaxCode { get; set; }

    /// <summary>Hạn mức công nợ cho khách sỉ (PAY-04).</summary>
    public decimal CreditLimit { get; set; }

    public int PaymentTermDays { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public CustomerGroup CustomerGroup { get; set; } = null!;

    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
}

/// <summary>dbo.CustomerAddresses — sổ địa chỉ giao hàng của khách.</summary>
public class CustomerAddress
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public string RecipientName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string Province { get; set; } = null!;

    public string District { get; set; } = null!;

    public string Ward { get; set; } = null!;

    public string Street { get; set; } = null!;

    public bool IsDefault { get; set; }

    public Customer Customer { get; set; } = null!;
}
