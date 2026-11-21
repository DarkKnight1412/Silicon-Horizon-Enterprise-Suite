using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Common.Models;

namespace SiliconHorizon.Infrastructure.Persistence.Repositories;

/// <summary>Repository chỉ đọc dùng EF Core (áp dụng cho các bảng danh mục/cấu hình).</summary>
public class EfReadRepository<TEntity> : IReadRepository<TEntity>
    where TEntity : class
{
    private readonly AppDbContext _context;

    public EfReadRepository(AppDbContext context) => _context = context;

    public IQueryable<TEntity> Query() => _context.Set<TEntity>().AsNoTracking();

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => await _context.Set<TEntity>().FindAsync(new[] { id }, cancellationToken);

    public async Task<List<TEntity>> ListAsync(CancellationToken cancellationToken = default)
        => await _context.Set<TEntity>().AsNoTracking().ToListAsync(cancellationToken);

    public async Task<PagedResult<TEntity>> ListPagedAsync(
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
        {
            pageNumber = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 20;
        }

        IQueryable<TEntity> query = _context.Set<TEntity>().AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        if (orderBy is not null)
        {
            query = orderBy(query);
        }

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<TEntity>.Create(items, pageNumber, pageSize, totalCount);
    }

    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
        => await GetByIdAsync(id, cancellationToken) is not null;
}

/// <summary>Repository ghi dùng EF Core. Thay đổi chỉ được lưu khi gọi <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
public class EfWriteRepository<TEntity> : IWriteRepository<TEntity>
    where TEntity : class
{
    private readonly AppDbContext _context;

    public EfWriteRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await _context.Set<TEntity>().AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => _context.Set<TEntity>().Update(entity);

    public void Remove(TEntity entity) => _context.Set<TEntity>().Remove(entity);
}

/// <summary>Unit of Work bọc quanh AppDbContext.</summary>
public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public EfUnitOfWork(AppDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        return new EfTransactionScope(transaction);
    }

    private sealed class EfTransactionScope : ITransactionScope
    {
        private readonly IDbContextTransaction _transaction;
        private bool _committed;

        public EfTransactionScope(IDbContextTransaction transaction) => _transaction = transaction;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.RollbackAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                await _transaction.RollbackAsync();
            }

            await _transaction.DisposeAsync();
        }
    }
}
