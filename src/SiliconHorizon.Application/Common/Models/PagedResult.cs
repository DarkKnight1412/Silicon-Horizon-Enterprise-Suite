namespace SiliconHorizon.Application.Common.Models;

/// <summary>
/// Kết quả phân trang theo quy ước <c>pageNumber</c> / <c>pageSize</c>; các proc danh sách trả thêm cột <c>TotalCount</c>
/// (Database/README.md mục 1.1).
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResult<T> Create(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalCount)
    {
        return new PagedResult<T>
        {
            Items = items,
            PageNumber = pageNumber < 1 ? 1 : pageNumber,
            PageSize = pageSize < 1 ? 20 : pageSize,
            TotalCount = totalCount < 0 ? 0 : totalCount
        };
    }
}

/// <summary>Tham số phân trang dùng chung cho mọi API danh sách.</summary>
public class PageRequest
{
    private const int MaxPageSize = 200;

    private int _pageSize = 20;

    public int PageNumber { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > MaxPageSize ? MaxPageSize : value;
    }
}
