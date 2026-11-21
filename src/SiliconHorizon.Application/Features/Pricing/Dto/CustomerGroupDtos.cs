using SiliconHorizon.Domain.Entities.Pricing;

namespace SiliconHorizon.Application.Features.Pricing.Dto;

/// <summary>
/// Nhóm khách hàng trả ra API — CRUD bằng EF Core trên bảng cấu hình <c>dbo.CustomerGroups</c>
/// (Database/README.md mục 10: <c>/api/admin/customer-groups</c>).
/// </summary>
public class CustomerGroupDto
{
    public int Id { get; init; }

    /// <summary>RETAIL, WHOLESALE... (varchar(30), duy nhất).</summary>
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Nhóm mặc định — dùng khi khách vãng lai kiểm tra voucher (<c>usp_Voucher_Validate</c> chọn
    /// <c>Id FROM CustomerGroups WHERE IsDefault = 1</c>). Chỉ một nhóm được là mặc định.
    /// </summary>
    public bool IsDefault { get; init; }

    public static CustomerGroupDto FromEntity(CustomerGroup group) => new()
    {
        Id = group.Id,
        Code = group.Code,
        Name = group.Name,
        IsDefault = group.IsDefault
    };
}

/// <summary>Tham số lọc danh sách nhóm khách — GET /api/v1/admin/customer-groups.</summary>
public class CustomerGroupListQuery
{
    /// <summary>Tìm theo mã hoặc tên nhóm.</summary>
    public string? Keyword { get; set; }

    /// <summary><c>null</c> = lấy cả nhóm mặc định và không mặc định.</summary>
    public bool? IsDefault { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Yêu cầu tạo nhóm khách — POST /api/v1/admin/customer-groups.</summary>
public class CreateCustomerGroupRequest
{
    /// <summary>Mã nhóm (varchar(30), duy nhất, không phân biệt hoa thường).</summary>
    public string? Code { get; set; }

    public string? Name { get; set; }

    /// <summary><c>true</c> sẽ bỏ cờ mặc định của nhóm đang là mặc định (mỗi lúc chỉ một nhóm mặc định).</summary>
    public bool IsDefault { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật nhóm khách — PUT /api/v1/admin/customer-groups/{id}.
/// Không đổi <c>code</c> để giữ toàn vẹn tham chiếu (khách hàng, bảng giá, voucher, giá bậc thang đang trỏ tới nhóm này).
/// </summary>
public class UpdateCustomerGroupRequest
{
    public string? Name { get; set; }

    /// <summary><c>true</c> sẽ bỏ cờ mặc định của nhóm khác.</summary>
    public bool IsDefault { get; set; }
}
