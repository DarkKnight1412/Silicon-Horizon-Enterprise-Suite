using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Catalog.Dto;
using SiliconHorizon.Domain.Entities.Catalog;

namespace SiliconHorizon.Application.Features.Catalog;

/// <summary>Hiện thực use case danh mục — CRUD bằng EF Core (bảng cấu hình, không cần stored procedure).</summary>
public sealed class CategoryService : ICategoryService
{
    private readonly IReadRepository<Category> _readRepository;
    private readonly IWriteRepository<Category> _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;

    public CategoryService(
        IReadRepository<Category> readRepository,
        IWriteRepository<Category> writeRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<CategoryDto>> GetPagedAsync(
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var result = await _readRepository.ListPagedAsync(
            query => query.OrderBy(category => category.Code),
            page.PageNumber,
            page.PageSize,
            cancellationToken);

        return PagedResult<CategoryDto>.Create(
            result.Items.Select(CategoryDto.FromEntity).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount);
    }

    public async Task<CategoryDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await GetCategoryOrThrowAsync(id, cancellationToken);

        return CategoryDto.FromEntity(category);
    }

    public async Task<CategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = new Category
        {
            Code = request.Code,
            Name = request.Name,
            SpecType = request.SpecType,
            ParentId = request.ParentId,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CategoryDto.FromEntity(category);
    }

    public async Task SetStatusAsync(
        int id,
        SetCategoryStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await GetCategoryOrThrowAsync(id, cancellationToken);

        category.IsActive = request.IsActive;
        category.UpdatedAt = _clock.UtcNow;
        category.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Category> GetCategoryOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy danh mục Id = {id}.");
}
