using SiliconHorizon.Application.Common.Models;

namespace SiliconHorizon.Application.Abstractions.Persistence;

/// <summary>
/// Truy vấn chỉ đọc. Trả về <see cref="IQueryable{T}"/> nên lớp này không phụ thuộc EF Core;
/// Infrastructure hiện thực bằng EF Core (hoặc Dapper) tùy module.
/// </summary>
public interface IReadRepository<TEntity>
    where TEntity : class
{
    IQueryable<TEntity> Query();

    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    Task<List<TEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Lấy một trang dữ liệu; việc COUNT + OFFSET/FETCH do tầng hạ tầng thực thi.</summary>
    Task<PagedResult<TEntity>> ListPagedAsync(
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default);
}

/// <summary>Thao tác ghi trên một aggregate.</summary>
public interface IWriteRepository<TEntity>
    where TEntity : class
{
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);
}

/// <summary>Gom nhiều thay đổi của một use case vào một giao dịch.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>Phạm vi giao dịch dùng trong using để tự rollback khi lỗi.</summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
