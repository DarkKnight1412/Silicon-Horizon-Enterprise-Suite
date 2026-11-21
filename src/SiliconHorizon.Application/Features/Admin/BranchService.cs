using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case chi nhánh (CRUD bằng EF Core).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Mã duy nhất</b> (<c>UQ_Branches_Code</c>) — kiểm tra trước khi ghi để trả 409.
/// Mã không đổi khi cập nhật để giữ toàn vẹn tham chiếu.</item>
/// <item><b>Tắt thay vì xóa</b>: các proc bán hàng chỉ xét chi nhánh đang hoạt động
/// (<c>JOIN Branches br ON br.IsActive = 1</c>), nên <c>isActive = false</c> là cách ngừng chi nhánh an toàn.</item>
/// <item><b>Nhật ký</b>: tạo/sửa/đổi trạng thái đều gọi <c>usp_Audit_Write</c>.</item>
/// </list>
/// </remarks>
public sealed class BranchService : IBranchService
{
    /// <summary>Branches.Code varchar(20).</summary>
    private const int CodeMaxLength = 20;

    /// <summary>Branches.Name nvarchar(150).</summary>
    private const int NameMaxLength = 150;

    /// <summary>Branches.Address nvarchar(300).</summary>
    private const int AddressMaxLength = 300;

    /// <summary>Branches.Phone varchar(20).</summary>
    private const int PhoneMaxLength = 20;

    private readonly IReadRepository<Branch> _readRepository;
    private readonly IWriteRepository<Branch> _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogWriter _auditLogWriter;

    public BranchService(
        IReadRepository<Branch> readRepository,
        IWriteRepository<Branch> writeRepository,
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
    public Task<PagedResult<BranchDto>> GetPagedAsync(
        BranchListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        var hasFilter = !string.IsNullOrEmpty(keyword) || query.IsActive.HasValue;

        Func<Branch, bool>? predicate = hasFilter
            ? branch => (string.IsNullOrEmpty(keyword)
                         || branch.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                         || branch.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        && (query.IsActive is null || branch.IsActive == query.IsActive)
            : null;

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<Branch> branches) => branches.OrderBy(branch => branch.Code),
            query.PageNumber,
            query.PageSize,
            predicate,
            BranchDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BranchDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => BranchDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<BranchDto> CreateAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = InputGuard.RequireCode(request.Code, CodeMaxLength, "Mã chi nhánh");
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên chi nhánh");
        var address = InputGuard.RequireText(request.Address, AddressMaxLength, "Địa chỉ chi nhánh");
        var phone = InputGuard.OptionalText(request.Phone, PhoneMaxLength, "Điện thoại chi nhánh");

        await EnsureCodeIsUniqueAsync(code, null, cancellationToken);

        var branch = new Branch
        {
            Code = code,
            Name = name,
            Address = address,
            Phone = phone,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(branch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "BranchCreated",
            "Branch",
            branch.Id,
            newValues: new { branch.Code, branch.Name, branch.Address, branch.Phone },
            cancellationToken: cancellationToken);

        return BranchDto.FromEntity(branch);
    }

    /// <inheritdoc />
    public async Task<BranchDto> UpdateAsync(
        int id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        var branch = await GetOrThrowAsync(id, cancellationToken);

        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên chi nhánh");
        var address = InputGuard.RequireText(request.Address, AddressMaxLength, "Địa chỉ chi nhánh");
        var phone = InputGuard.OptionalText(request.Phone, PhoneMaxLength, "Điện thoại chi nhánh");

        var oldValues = new { branch.Name, branch.Address, branch.Phone };

        branch.Name = name;
        branch.Address = address;
        branch.Phone = phone;
        branch.UpdatedAt = _clock.UtcNow;
        branch.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(branch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "BranchUpdated",
            "Branch",
            branch.Id,
            oldValues,
            new { branch.Name, branch.Address, branch.Phone },
            cancellationToken: cancellationToken);

        return BranchDto.FromEntity(branch);
    }

    /// <inheritdoc />
    public async Task SetStatusAsync(
        int id,
        SetBranchStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var branch = await GetOrThrowAsync(id, cancellationToken);

        if (branch.IsActive == request.IsActive)
        {
            return;
        }

        branch.IsActive = request.IsActive;
        branch.UpdatedAt = _clock.UtcNow;
        branch.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(branch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            request.IsActive ? "BranchActivated" : "BranchDeactivated",
            "Branch",
            branch.Id,
            oldValues: new { IsActive = !request.IsActive },
            newValues: new { branch.IsActive },
            cancellationToken: cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Branch> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy chi nhánh Id = {id}.");

    /// <summary>UQ_Branches_Code — so khớp không phân biệt hoa thường, bỏ qua bản ghi đang sửa.</summary>
    private async Task EnsureCodeIsUniqueAsync(string code, int? currentId, CancellationToken cancellationToken)
    {
        var branches = await _readRepository.ListAsync(cancellationToken);

        var duplicated = branches.Any(branch =>
            branch.Id != currentId
            && string.Equals(branch.Code, code, StringComparison.OrdinalIgnoreCase));

        if (duplicated)
        {
            throw new ConflictException($"Mã chi nhánh {code} đã tồn tại.", "SQL-50005");
        }
    }
}
