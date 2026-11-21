using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Pricing.Dto;
using SiliconHorizon.Domain.Entities.Pricing;

namespace SiliconHorizon.Application.Features.Pricing;

/// <summary>
/// Hiện thực use case nhóm khách hàng (CRUD bằng EF Core).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Code bất biến khi sửa</b>: khách hàng, <c>ProductPrices</c>, <c>PriceTiers</c>, <c>Vouchers</c>
/// đều trỏ tới nhóm; đổi mã nhóm sẽ làm sai lệch cấu hình đang dùng nên API chỉ cho đổi tên và cờ mặc định.</item>
/// <item><b>Chỉ một nhóm mặc định</b>: <c>usp_Voucher_Validate</c> lấy
/// <c>Id FROM CustomerGroups WHERE IsDefault = 1</c>, nên khi đặt cờ mặc định phải bỏ cờ của nhóm cũ.</item>
/// <item><b>Không xóa cứng</b>: không cung cấp endpoint DELETE (đúng quy ước mục 10 của Database/README.md).</item>
/// </list>
/// </remarks>
public sealed class CustomerGroupService : ICustomerGroupService
{
    /// <summary>CustomerGroups.Code varchar(30).</summary>
    private const int CodeMaxLength = 30;

    /// <summary>CustomerGroups.Name nvarchar(100).</summary>
    private const int NameMaxLength = 100;

    private readonly IReadRepository<CustomerGroup> _readRepository;
    private readonly IWriteRepository<CustomerGroup> _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogWriter _auditLogWriter;

    public CustomerGroupService(
        IReadRepository<CustomerGroup> readRepository,
        IWriteRepository<CustomerGroup> writeRepository,
        IUnitOfWork unitOfWork,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _unitOfWork = unitOfWork;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public Task<PagedResult<CustomerGroupDto>> GetPagedAsync(
        CustomerGroupListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        var hasFilter = !string.IsNullOrEmpty(keyword) || query.IsDefault.HasValue;

        Func<CustomerGroup, bool>? predicate = hasFilter
            ? group => (string.IsNullOrEmpty(keyword)
                        || group.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        || group.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                       && (query.IsDefault is null || group.IsDefault == query.IsDefault)
            : null;

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<CustomerGroup> groups) => groups.OrderBy(group => group.Code),
            query.PageNumber,
            query.PageSize,
            predicate,
            CustomerGroupDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CustomerGroupDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => CustomerGroupDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<CustomerGroupDto> CreateAsync(
        CreateCustomerGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = InputGuard.RequireCode(request.Code, CodeMaxLength, "Mã nhóm khách");
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên nhóm khách");

        await EnsureCodeIsUniqueAsync(code, null, cancellationToken);

        if (request.IsDefault)
        {
            await ClearOtherDefaultsAsync(null, cancellationToken);
        }

        var group = new CustomerGroup
        {
            Code = code,
            Name = name,
            IsDefault = request.IsDefault
        };

        await _writeRepository.AddAsync(group, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "CustomerGroupCreated",
            "CustomerGroup",
            group.Id,
            newValues: new { group.Code, group.Name, group.IsDefault },
            cancellationToken: cancellationToken);

        return CustomerGroupDto.FromEntity(group);
    }

    /// <inheritdoc />
    public async Task<CustomerGroupDto> UpdateAsync(
        int id,
        UpdateCustomerGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await GetOrThrowAsync(id, cancellationToken);
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên nhóm khách");

        var oldValues = new { group.Name, group.IsDefault };

        if (request.IsDefault)
        {
            await ClearOtherDefaultsAsync(id, cancellationToken);
        }
        else if (group.IsDefault)
        {
            throw new BadRequestException(
                "Không thể bỏ cờ mặc định: hệ thống cần một nhóm mặc định cho khách vãng lai. Hãy đặt nhóm khác làm mặc định.");
        }

        group.Name = name;
        group.IsDefault = request.IsDefault;

        _writeRepository.Update(group);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "CustomerGroupUpdated",
            "CustomerGroup",
            group.Id,
            oldValues,
            new { group.Name, group.IsDefault },
            cancellationToken: cancellationToken);

        return CustomerGroupDto.FromEntity(group);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<CustomerGroup> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy nhóm khách Id = {id}.");

    /// <summary>Bỏ cờ mặc định của mọi nhóm khác để bảo đảm mỗi lúc chỉ một nhóm mặc định.</summary>
    private async Task ClearOtherDefaultsAsync(int? keepId, CancellationToken cancellationToken)
    {
        var groups = await _readRepository.ListAsync(cancellationToken);

        foreach (var other in groups.Where(item => item.IsDefault && item.Id != keepId))
        {
            other.IsDefault = false;
            _writeRepository.Update(other);
        }
    }

    /// <summary>UQ_CustomerGroups_Code — so khớp không phân biệt hoa thường.</summary>
    private async Task EnsureCodeIsUniqueAsync(string code, int? currentId, CancellationToken cancellationToken)
    {
        var groups = await _readRepository.ListAsync(cancellationToken);

        var duplicated = groups.Any(group =>
            group.Id != currentId
            && string.Equals(group.Code, code, StringComparison.OrdinalIgnoreCase));

        if (duplicated)
        {
            throw new ConflictException($"Mã nhóm khách {code} đã tồn tại.", "SQL-50005");
        }
    }
}
