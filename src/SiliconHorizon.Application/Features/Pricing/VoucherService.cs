using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Abstractions.Time;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Pricing.Dto;
using SiliconHorizon.Application.Security;
using SiliconHorizon.Domain.Entities.Pricing;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Pricing;

/// <summary>
/// Hiện thực use case voucher: kiểm tra mã qua <c>dbo.usp_Voucher_Validate</c> và CRUD bằng EF Core.
/// </summary>
/// <remarks>
/// Nguyên tắc nghiệp vụ:
/// <list type="bullet">
/// <item><b>Không tin client</b>: khách đã đăng nhập luôn bị ép <c>customerId</c> từ token; khách vãng lai
/// mới được truyền <c>customerId</c> (và là tùy chọn) — khi thiếu, proc dùng nhóm khách mặc định.</item>
/// <item><b>Mã duy nhất</b> (<c>UQ_Vouchers_Code</c>) được kiểm tra trước khi ghi để trả 409 thay vì lỗi SQL thô.</item>
/// <item><b>Không xóa cứng</b>: mã đã phát sinh lượt dùng chỉ được khóa bằng <c>isActive = false</c>.</item>
/// <item><b>Nhật ký</b>: mọi thay đổi cấu hình mã giảm giá đều gọi <c>usp_Audit_Write</c> (mục 10 của README).</item>
/// </list>
/// </remarks>
public sealed class VoucherService : IVoucherService
{
    /// <summary>Vouchers.Code varchar(40).</summary>
    private const int CodeMaxLength = 40;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly IReadRepository<Voucher> _readRepository;
    private readonly IWriteRepository<Voucher> _writeRepository;
    private readonly IReadRepository<CustomerGroup> _customerGroups;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogWriter _auditLogWriter;

    public VoucherService(
        ISqlExecutor sqlExecutor,
        IReadRepository<Voucher> readRepository,
        IWriteRepository<Voucher> writeRepository,
        IReadRepository<CustomerGroup> customerGroups,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IAuditLogWriter auditLogWriter)
    {
        _sqlExecutor = sqlExecutor;
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _customerGroups = customerGroups;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public async Task<VoucherValidationDto> ValidateAsync(
        ValidateVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = InputGuard.RequireText(request.Code, CodeMaxLength, "Mã giảm giá").ToUpperInvariant();

        if (request.OrderAmount < 0)
        {
            throw new BadRequestException("Tạm tính của đơn (orderAmount) không được âm.");
        }

        var customerId = ResolveCustomerIdForValidation(request.CustomerId);

        // usp_Voucher_Validate tự quyết định nhóm khách (theo khách, hoặc nhóm mặc định khi khách vãng lai).
        var row = await _sqlExecutor.QuerySingleOrDefaultAsync<VoucherValidationRow>(
            StoredProcedureNames.VoucherValidate,
            new
            {
                Code = code,
                CustomerId = customerId,
                OrderAmount = request.OrderAmount,
                HasPromotion = request.HasPromotion
            },
            cancellationToken);

        // Proc luôn trả đúng một dòng (fn_VoucherCheck dùng LEFT JOIN nên mã sai vẫn có dòng báo lỗi).
        return row is null
            ? new VoucherValidationDto { IsValid = false, Message = "Mã giảm giá không tồn tại.", DiscountAmount = 0m }
            : VoucherValidationDto.FromRow(row);
    }

    /// <inheritdoc />
    public Task<PagedResult<VoucherDto>> GetPagedAsync(
        VoucherListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        var hasFilter = !string.IsNullOrEmpty(keyword)
                        || query.IsActive.HasValue
                        || query.CustomerGroupId.HasValue;

        Func<Voucher, bool>? predicate = hasFilter
            ? voucher => (string.IsNullOrEmpty(keyword)
                          || voucher.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                         && (query.IsActive is null || voucher.IsActive == query.IsActive)
                         && (query.CustomerGroupId is null || voucher.CustomerGroupId == query.CustomerGroupId)
            : null;

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<Voucher> vouchers) => vouchers.OrderBy(voucher => voucher.Code),
            query.PageNumber,
            query.PageSize,
            predicate,
            VoucherDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VoucherDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => VoucherDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<VoucherDto> CreateAsync(
        CreateVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = InputGuard.RequireCode(request.Code, CodeMaxLength, "Mã giảm giá");

        ValidateBusinessRules(
            request.DiscountType,
            request.DiscountValue,
            request.StartAt,
            request.EndAt,
            request.MinOrderAmount,
            request.MaxDiscountAmount,
            request.MaxUses);

        await EnsureCustomerGroupExistsAsync(request.CustomerGroupId, cancellationToken);
        await EnsureCodeIsUniqueAsync(code, null, cancellationToken);

        var voucher = new Voucher
        {
            Code = code,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MinOrderAmount = request.MinOrderAmount,
            MaxDiscountAmount = request.MaxDiscountAmount,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            MaxUses = request.MaxUses,
            UsedCount = 0,
            CustomerGroupId = request.CustomerGroupId,
            IsStackable = request.IsStackable,
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _writeRepository.AddAsync(voucher, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "VoucherCreated",
            "Voucher",
            voucher.Id,
            newValues: new { voucher.Code, voucher.DiscountType, voucher.DiscountValue, voucher.StartAt, voucher.EndAt },
            cancellationToken: cancellationToken);

        return VoucherDto.FromEntity(voucher);
    }

    /// <inheritdoc />
    public async Task<VoucherDto> UpdateAsync(
        long id,
        UpdateVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        var voucher = await GetOrThrowAsync(id, cancellationToken);

        ValidateBusinessRules(
            request.DiscountType,
            request.DiscountValue,
            request.StartAt,
            request.EndAt,
            request.MinOrderAmount,
            request.MaxDiscountAmount,
            request.MaxUses);

        await EnsureCustomerGroupExistsAsync(request.CustomerGroupId, cancellationToken);

        var oldValues = new
        {
            voucher.DiscountType,
            voucher.DiscountValue,
            voucher.MinOrderAmount,
            voucher.MaxDiscountAmount,
            voucher.StartAt,
            voucher.EndAt,
            voucher.MaxUses,
            voucher.CustomerGroupId,
            voucher.IsStackable
        };

        voucher.DiscountType = request.DiscountType;
        voucher.DiscountValue = request.DiscountValue;
        voucher.MinOrderAmount = request.MinOrderAmount;
        voucher.MaxDiscountAmount = request.MaxDiscountAmount;
        voucher.StartAt = request.StartAt;
        voucher.EndAt = request.EndAt;
        voucher.MaxUses = request.MaxUses;
        voucher.CustomerGroupId = request.CustomerGroupId;
        voucher.IsStackable = request.IsStackable;
        voucher.UpdatedAt = _clock.UtcNow;
        voucher.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(voucher);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "VoucherUpdated",
            "Voucher",
            voucher.Id,
            oldValues,
            new
            {
                voucher.DiscountType,
                voucher.DiscountValue,
                voucher.MinOrderAmount,
                voucher.MaxDiscountAmount,
                voucher.StartAt,
                voucher.EndAt,
                voucher.MaxUses,
                voucher.CustomerGroupId,
                voucher.IsStackable
            },
            cancellationToken: cancellationToken);

        return VoucherDto.FromEntity(voucher);
    }

    /// <inheritdoc />
    public async Task SetStatusAsync(
        long id,
        SetVoucherStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var voucher = await GetOrThrowAsync(id, cancellationToken);

        if (voucher.IsActive == request.IsActive)
        {
            return;
        }

        voucher.IsActive = request.IsActive;
        voucher.UpdatedAt = _clock.UtcNow;
        voucher.UpdatedBy = _currentUser.UserId;

        _writeRepository.Update(voucher);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            request.IsActive ? "VoucherActivated" : "VoucherDeactivated",
            "Voucher",
            voucher.Id,
            oldValues: new { IsActive = !request.IsActive },
            newValues: new { voucher.IsActive },
            cancellationToken: cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Đọc voucher theo Id; không tồn tại → 404.</summary>
    private async Task<Voucher> GetOrThrowAsync(long id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy voucher Id = {id}.");

    /// <summary>
    /// Khách đã đăng nhập: ép <c>customerId</c> từ token. Khách vãng lai (hoặc nhân viên kiểm tra hộ):
    /// nhận <c>customerId</c> tùy chọn — thiếu thì để <c>null</c> cho proc tự dùng nhóm khách mặc định.
    /// </summary>
    private long? ResolveCustomerIdForValidation(long? requestedCustomerId)
    {
        if (_currentUser.IsInRole(RoleCodes.Customer))
        {
            return _currentUser.CustomerId
                   ?? throw new ForbiddenException("Tài khoản khách hàng chưa gắn hồ sơ khách nên không kiểm tra được voucher.");
        }

        if (requestedCustomerId is null)
        {
            return null;
        }

        InputGuard.EnsurePositive(requestedCustomerId.Value, "customerId");

        return requestedCustomerId;
    }

    /// <summary>Kiểm tra ràng buộc của <c>CK_Vouchers_Type/Value/Dates/Used</c> trước khi ghi.</summary>
    private static void ValidateBusinessRules(
        DiscountType discountType,
        decimal discountValue,
        DateTime startAt,
        DateTime endAt,
        decimal? minOrderAmount,
        decimal? maxDiscountAmount,
        int? maxUses)
    {
        InputGuard.EnsureDefined(discountType, "Loại giảm giá");
        InputGuard.EnsurePositiveAmount(discountValue, "Giá trị giảm");
        InputGuard.EnsureDateRange(startAt, endAt, "startAt", "endAt");

        if (minOrderAmount.HasValue)
        {
            InputGuard.EnsureNotNegative(minOrderAmount.Value, "Giá trị đơn tối thiểu");
        }

        if (maxDiscountAmount.HasValue)
        {
            InputGuard.EnsureNotNegative(maxDiscountAmount.Value, "Mức giảm tối đa");
        }

        if (maxUses.HasValue)
        {
            InputGuard.EnsurePositive(maxUses.Value, "Số lượt dùng tối đa");
        }
    }

    /// <summary>Voucher trỏ tới nhóm khách bằng khóa ngoại nên nhóm phải tồn tại.</summary>
    private async Task EnsureCustomerGroupExistsAsync(int? customerGroupId, CancellationToken cancellationToken)
    {
        if (customerGroupId is null)
        {
            return;
        }

        InputGuard.EnsurePositive(customerGroupId.Value, "customerGroupId");

        var groups = await _customerGroups.ListAsync(cancellationToken);

        if (groups.All(group => group.Id != customerGroupId.Value))
        {
            throw new BadRequestException($"Nhóm khách Id = {customerGroupId} không tồn tại.");
        }
    }

    /// <summary>UQ_Vouchers_Code — so khớp không phân biệt hoa thường, bỏ qua chính bản ghi đang sửa.</summary>
    private async Task EnsureCodeIsUniqueAsync(string code, long? currentId, CancellationToken cancellationToken)
    {
        var vouchers = await _readRepository.ListAsync(cancellationToken);

        var duplicated = vouchers.Any(voucher =>
            voucher.Id != currentId
            && string.Equals(voucher.Code, code, StringComparison.OrdinalIgnoreCase));

        if (duplicated)
        {
            throw new ConflictException($"Mã giảm giá {code} đã tồn tại.", "SQL-50005");
        }
    }
}
