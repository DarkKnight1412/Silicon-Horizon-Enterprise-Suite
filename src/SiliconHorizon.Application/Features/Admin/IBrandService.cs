using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Admin.Dto;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Use case thương hiệu — CRUD bằng EF Core trên bảng cấu hình <c>dbo.Brands</c>
/// (Database/README.md mục 10: <c>/api/admin/brands</c>). <c>GET /api/v1/brands</c> (công khai) đã có ở module Catalog.
/// </summary>
public interface IBrandService
{
    /// <summary>GET /api/v1/admin/brands — danh sách thương hiệu, có phân trang và lọc.</summary>
    Task<PagedResult<BrandDto>> GetPagedAsync(BrandListQuery query, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/admin/brands/{id} — chi tiết thương hiệu; không tồn tại → 404.</summary>
    Task<BrandDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/admin/brands — tạo thương hiệu.
    /// Tên duy nhất (<c>UQ_Brands_Name</c>, không phân biệt hoa thường) → trùng trả 409.
    /// </summary>
    Task<BrandDto> CreateAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);

    /// <summary>PUT /api/v1/admin/brands/{id} — đổi tên thương hiệu.</summary>
    Task<BrandDto> UpdateAsync(int id, UpdateBrandRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// PATCH /api/v1/admin/brands/{id}/status — ngừng dùng/cho dùng lại thương hiệu.
    /// Thay cho xóa cứng vì thương hiệu đã gắn với sản phẩm (<c>Products.BrandId</c>).
    /// </summary>
    Task SetStatusAsync(int id, SetBrandStatusRequest request, CancellationToken cancellationToken = default);
}
