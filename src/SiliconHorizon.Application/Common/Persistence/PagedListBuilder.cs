using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Common.Models;

namespace SiliconHorizon.Application.Common.Persistence;

/// <summary>
/// Tiện ích trả danh sách phân trang cho các bảng cấu hình (module Pricing/Admin/Customers).
/// <list type="bullet">
/// <item><b>Không có điều kiện lọc</b> → phân trang ngay ở tầng dữ liệu bằng
/// <see cref="IReadRepository{TEntity}.ListPagedAsync"/> (COUNT + OFFSET/FETCH do EF Core thực thi).</item>
/// <item><b>Có điều kiện lọc</b> → đọc bảng (bảng cấu hình nhỏ, ít thay đổi — theo ghi chú trong
/// <c>Features/README.md</c> và cách làm sẵn có của <c>SupplierService</c>) rồi lọc/sắp xếp/phân trang
/// trong bộ nhớ, vì <c>ListPagedAsync</c> theo quy ước không nhận điều kiện lọc.</item>
/// </list>
/// </summary>
public static class PagedListBuilder
{
    /// <summary>Trần <c>pageSize</c> — khớp <see cref="PageRequest"/>.</summary>
    public const int MaxPageSize = 200;

    /// <summary>
    /// Lấy một trang dữ liệu đã ánh xạ sang DTO. <paramref name="predicate"/> <c>null</c> nghĩa là lấy tất cả.
    /// </summary>
    public static async Task<PagedResult<TDto>> BuildAsync<TEntity, TDto>(
        IReadRepository<TEntity> repository,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy,
        int pageNumber,
        int pageSize,
        Func<TEntity, bool>? predicate,
        Func<TEntity, TDto> map,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var safePageNumber = pageNumber < 1 ? 1 : pageNumber;
        var safePageSize = pageSize switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => pageSize
        };

        if (predicate is null)
        {
            var page = await repository.ListPagedAsync(orderBy, safePageNumber, safePageSize, cancellationToken);

            return PagedResult<TDto>.Create(
                page.Items.Select(map).ToList(),
                page.PageNumber,
                page.PageSize,
                page.TotalCount);
        }

        var all = await repository.ListAsync(cancellationToken);
        var ordered = orderBy(all.Where(predicate).AsQueryable()).ToList();

        var items = ordered
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .Select(map)
            .ToList();

        return PagedResult<TDto>.Create(items, safePageNumber, safePageSize, ordered.Count);
    }
}
