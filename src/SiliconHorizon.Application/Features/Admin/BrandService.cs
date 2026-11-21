using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Domain.Entities.Catalog;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case thương hiệu (CRUD bằng EF Core).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Tên duy nhất</b> (<c>UQ_Brands_Name</c>) — kiểm tra trước khi ghi để trả 409.</item>
/// <item><b>Không xóa cứng</b>: chỉ tắt bằng <c>isActive = false</c>; <c>GET /api/v1/brands</c> công khai
/// lọc theo <c>IsActive</c> nên thương hiệu đã tắt sẽ không còn xuất hiện cho khách.</item>
/// <item><b>Nhật ký</b>: tạo/sửa/đổi trạng thái đều gọi <c>usp_Audit_Write</c>.</item>
/// </list>
/// </remarks>
public sealed class BrandService : IBrandService
{
    /// <summary>Brands.Name nvarchar(100).</summary>
    private const int NameMaxLength = 100;

    private readonly IReadRepository<Brand> _readRepository;
    private readonly IWriteRepository<Brand> _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogWriter _auditLogWriter;

    public BrandService(
        IReadRepository<Brand> readRepository,
        IWriteRepository<Brand> writeRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public Task<PagedResult<BrandDto>> GetPagedAsync(
        BrandListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        var hasFilter = !string.IsNullOrEmpty(keyword) || query.IsActive.HasValue;

        Func<Brand, bool>? predicate = hasFilter
            ? brand => (string.IsNullOrEmpty(keyword)
                        || brand.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                       && (query.IsActive is null || brand.IsActive == query.IsActive)
            : null;

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<Brand> brands) => brands.OrderBy(brand => brand.Name),
            query.PageNumber,
            query.PageSize,
            predicate,
            BrandDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BrandDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => BrandDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<BrandDto> CreateAsync(
        CreateBrandRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên thương hiệu");

        await EnsureNameIsUniqueAsync(name, null, cancellationToken);

        var brand = new Brand
        {
            Name = name,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(brand, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "BrandCreated",
            "Brand",
            brand.Id,
            newValues: new { brand.Name, brand.IsActive },
            cancellationToken: cancellationToken);

        return BrandDto.FromEntity(brand);
    }

    /// <inheritdoc />
    public async Task<BrandDto> UpdateAsync(
        int id,
        UpdateBrandRequest request,
        CancellationToken cancellationToken = default)
    {
        var brand = await GetOrThrowAsync(id, cancellationToken);
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên thương hiệu");

        await EnsureNameIsUniqueAsync(name, id, cancellationToken);

        var oldName = brand.Name;
        brand.Name = name;
        brand.UpdatedAt = _clock.UtcNow;
        brand.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(brand);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "BrandUpdated",
            "Brand",
            brand.Id,
            oldValues: new { Name = oldName },
            newValues: new { brand.Name },
            cancellationToken: cancellationToken);

        return BrandDto.FromEntity(brand);
    }

    /// <inheritdoc />
    public async Task SetStatusAsync(
        int id,
        SetBrandStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var brand = await GetOrThrowAsync(id, cancellationToken);

        if (brand.IsActive == request.IsActive)
        {
            return;
        }

        brand.IsActive = request.IsActive;
        brand.UpdatedAt = _clock.UtcNow;
        brand.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(brand);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            request.IsActive ? "BrandActivated" : "BrandDeactivated",
            "Brand",
            brand.Id,
            oldValues: new { IsActive = !request.IsActive },
            newValues: new { brand.IsActive },
            cancellationToken: cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Brand> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy thương hiệu Id = {id}.");

    /// <summary>UQ_Brands_Name — so khớp tên không phân biệt hoa thường, bỏ qua bản ghi đang sửa.</summary>
    private async Task EnsureNameIsUniqueAsync(string name, int? currentId, CancellationToken cancellationToken)
    {
        var brands = await _readRepository.ListAsync(cancellationToken);

        var duplicated = brands.Any(brand =>
            brand.Id != currentId
            && string.Equals(brand.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicated)
        {
            throw new ConflictException($"Thương hiệu {name} đã tồn tại.", "SQL-50005");
        }
    }
}
