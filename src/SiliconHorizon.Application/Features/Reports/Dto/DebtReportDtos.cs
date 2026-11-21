namespace SiliconHorizon.Application.Features.Reports.Dto;

// ─────────────────────────────────────────────────────────────────────────────
// DTO báo cáo công nợ — khớp CHÍNH XÁC 2 result set của dbo.usp_Report_Debt
// (Database/06_sp_hr_audit_reports.sql; Database/README.md mục 9).
//
// Proc KHÔNG có tham số (không lọc chi nhánh, không lọc ngày) và chỉ trả các đối tượng còn số dư <> 0:
// result set 1 = phải thu của khách, result set 2 = phải trả nhà cung cấp.
// Quy ước quá hạn: khoản ghi tăng (EntryType = 0) đã quá DueDate + mọi khoản giảm (Amount < 0).
// Vì không có tham số chi nhánh nên endpoint này chỉ dành cho Admin.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Công nợ phải thu của một khách — result set 1 của <c>dbo.usp_Report_Debt</c>
/// (CustomerId, FullName, Phone, CreditLimit, Balance, Overdue).
/// </summary>
public sealed class CustomerDebtRow
{
    public long CustomerId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>Hạn mức công nợ (Customers.CreditLimit).</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>Tổng số dư công nợ (dương = khách còn nợ).</summary>
    public decimal Balance { get; set; }

    /// <summary>Phần quá hạn (đã tới DueDate nhưng chưa thanh toán).</summary>
    public decimal Overdue { get; set; }
}

/// <summary>Một dòng công nợ phải thu trả ra API.</summary>
public class CustomerDebtDto
{
    public long CustomerId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public decimal CreditLimit { get; init; }

    public decimal Balance { get; init; }

    public decimal Overdue { get; init; }

    public static CustomerDebtDto FromRow(CustomerDebtRow row) => new()
    {
        CustomerId = row.CustomerId,
        FullName = row.FullName,
        Phone = row.Phone,
        CreditLimit = row.CreditLimit,
        Balance = row.Balance,
        Overdue = row.Overdue
    };
}

/// <summary>
/// Công nợ phải trả một nhà cung cấp — result set 2 của <c>dbo.usp_Report_Debt</c>
/// (SupplierId, Name, Balance, Overdue).
/// </summary>
public sealed class SupplierDebtRow
{
    public long SupplierId { get; set; }

    /// <summary>Tên nhà cung cấp — cột <c>Name</c> trong result set (<c>Suppliers.Name</c>).</summary>
    public string Name { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public decimal Overdue { get; set; }
}

/// <summary>Một dòng công nợ phải trả trả ra API.</summary>
public class SupplierDebtDto
{
    public long SupplierId { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public decimal Balance { get; init; }

    public decimal Overdue { get; init; }

    public static SupplierDebtDto FromRow(SupplierDebtRow row) => new()
    {
        SupplierId = row.SupplierId,
        SupplierName = row.Name,
        Balance = row.Balance,
        Overdue = row.Overdue
    };
}

/// <summary>Kết quả GET /api/v1/reports/debt (usp_Report_Debt, chỉ Admin).</summary>
public class DebtReportDto
{
    /// <summary>Công nợ phải thu của khách (chỉ khách còn số dư).</summary>
    public IReadOnlyList<CustomerDebtDto> Customers { get; init; } = Array.Empty<CustomerDebtDto>();

    /// <summary>Công nợ phải trả nhà cung cấp (chỉ nhà cung cấp còn số dư).</summary>
    public IReadOnlyList<SupplierDebtDto> Suppliers { get; init; } = Array.Empty<SupplierDebtDto>();
}
