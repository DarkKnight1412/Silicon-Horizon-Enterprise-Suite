using SiliconHorizon.Domain.Entities.Catalog;

namespace SiliconHorizon.Application.Features.Admin.Dto;

/// <summary>
/// Thương hiệu trả ra API — CRUD bằng EF Core trên <c>dbo.Brands</c>
/// (Database/README.md mục 10: <c>/api/admin/brands</c>). Danh sách công khai đã có ở <c>GET /api/v1/brands</c>.
/// </summary>
public class BrandDto
{
    public int Id { get; init; }

    /// <summary>Tên thương hiệu (nvarchar(100), duy nhất — <c>UQ_Brands_Name</c>).</summary>
    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public static BrandDto FromEntity(Brand brand) => new()
    {
        Id = brand.Id,
        Name = brand.Name,
        IsActive = brand.IsActive,
        CreatedAt = brand.CreatedAt,
        UpdatedAt = brand.UpdatedAt
    };
}

/// <summary>Tham số lọc danh sách thương hiệu — GET /api/v1/admin/brands.</summary>
public class BrandListQuery
{
    /// <summary>Tìm theo tên thương hiệu.</summary>
    public string? Keyword { get; set; }

    /// <summary><c>null</c> = lấy cả thương hiệu đã ngừng dùng.</summary>
    public bool? IsActive { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>Yêu cầu tạo thương hiệu — POST /api/v1/admin/brands.</summary>
public class CreateBrandRequest
{
    public string? Name { get; set; }
}

/// <summary>Yêu cầu cập nhật thương hiệu — PUT /api/v1/admin/brands/{id}.</summary>
public class UpdateBrandRequest
{
    public string? Name { get; set; }
}

/// <summary>
/// Đổi trạng thái thương hiệu — PATCH /api/v1/admin/brands/{id}/status.
/// Thay cho xóa cứng vì thương hiệu đã gắn với sản phẩm.
/// </summary>
public record SetBrandStatusRequest(bool IsActive);
