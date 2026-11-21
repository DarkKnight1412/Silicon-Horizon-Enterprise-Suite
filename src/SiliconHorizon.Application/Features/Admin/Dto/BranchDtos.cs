using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Application.Features.Admin.Dto;

/// <summary>
/// Chi nhánh trả ra API — CRUD bằng EF Core trên <c>dbo.Branches</c>
/// (Database/README.md mục 10: <c>/api/admin/branches</c>).
/// </summary>
public class BranchDto
{
    public int Id { get; init; }

    /// <summary>Mã chi nhánh (varchar(20), duy nhất — <c>UQ_Branches_Code</c>).</summary>
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string? Phone { get; init; }

    /// <summary>
    /// Ngừng hoạt động = <c>false</c>. Các proc chỉ xét tồn của chi nhánh đang hoạt động
    /// (<c>JOIN Branches br ON br.IsActive = 1</c>) nên tắt chi nhánh sẽ loại chi nhánh khỏi bán hàng.
    /// </summary>
    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static BranchDto FromEntity(Branch branch) => new()
    {
        Id = branch.Id,
        Code = branch.Code,
        Name = branch.Name,
        Address = branch.Address,
        Phone = branch.Phone,
        IsActive = branch.IsActive,
        CreatedAt = branch.CreatedAt,
        UpdatedAt = branch.UpdatedAt
    };
}

/// <summary>Tham số lọc danh sách chi nhánh — GET /api/v1/admin/branches.</summary>
public class BranchListQuery
{
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Yêu cầu tạo chi nhánh — POST /api/v1/admin/branches.</summary>
public class CreateBranchRequest
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }
}

/// <summary>Yêu cầu cập nhật chi nhánh — PUT /api/v1/admin/branches/{id} (không đổi mã).</summary>
public class UpdateBranchRequest
{
    public string? Name { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }
}

/// <summary>
/// Đổi trạng thái chi nhánh — PATCH /api/v1/admin/branches/{id}/status.
/// Không xóa cứng vì chi nhánh đã gắn với nhân viên, tồn kho, đơn hàng.
/// </summary>
public record SetBranchStatusRequest(bool IsActive);
