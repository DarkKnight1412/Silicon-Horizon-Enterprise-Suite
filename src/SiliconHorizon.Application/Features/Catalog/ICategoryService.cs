using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Catalog.Dto;

namespace SiliconHorizon.Application.Features.Catalog;

/// <summary>
/// Use case danh mục sản phẩm. Controller chỉ gọi interface này — nhờ vậy có thể thay cách truy cập dữ liệu
/// (EF Core hôm nay, Dapper/cache sau này) mà không sửa tầng API.
/// </summary>
public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetPagedAsync(PageRequest page, CancellationToken cancellationToken = default);

    Task<CategoryDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task SetStatusAsync(int id, SetCategoryStatusRequest request, CancellationToken cancellationToken = default);
}
