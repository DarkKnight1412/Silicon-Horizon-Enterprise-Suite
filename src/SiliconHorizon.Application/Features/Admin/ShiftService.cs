using SiliconHorizon.Application.Abstractions.Persistence;
using SiliconHorizon.Application.Common.Audit;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Common.Persistence;
using SiliconHorizon.Application.Common.Validation;
using SiliconHorizon.Application.Features.Admin.Dto;
using SiliconHorizon.Domain.Entities.Hr;

namespace SiliconHorizon.Application.Features.Admin;

/// <summary>
/// Hiện thực use case danh mục ca làm việc (CRUD bằng EF Core).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Mã duy nhất</b> <c>UQ_Shifts_Code</c>; mã không đổi khi cập nhật
/// (<c>EmployeeSchedules.ShiftId</c> đang trỏ tới ca này).</item>
/// <item><b>Khung giờ</b> dùng <see cref="TimeOnly"/> (SQL <c>time(0)</c>); yêu cầu <c>endTime &gt; startTime</c>
/// vì dữ liệu khởi tạo ở mục 13 của Database/table_and_data.sql không có ca qua đêm.</item>
/// <item><b>Bảng không có cột trạng thái/audit</b> (<c>Shifts</c> chỉ có Id, Code, Name, StartTime, EndTime)
/// nên vẫn ghi nhật ký qua <c>usp_Audit_Write</c> nhưng không set <c>updatedAt</c>/<c>updatedBy</c>.</item>
/// </list>
/// </remarks>
public sealed class ShiftService : IShiftService
{
    /// <summary>Shifts.Code varchar(20).</summary>
    private const int CodeMaxLength = 20;

    /// <summary>Shifts.Name nvarchar(50).</summary>
    private const int NameMaxLength = 50;

    private readonly IReadRepository<Shift> _readRepository;
    private readonly IWriteRepository<Shift> _writeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogWriter _auditLogWriter;

    public ShiftService(
        IReadRepository<Shift> readRepository,
        IWriteRepository<Shift> writeRepository,
        IUnitOfWork unitOfWork,
        IAuditLogWriter auditLogWriter)
    {
        _readRepository = readRepository;
        _writeRepository = writeRepository;
        _unitOfWork = unitOfWork;
        _auditLogWriter = auditLogWriter;
    }

    /// <inheritdoc />
    public Task<PagedResult<ShiftDto>> GetPagedAsync(
        ShiftListQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Keyword?.Trim();

        Func<Shift, bool>? predicate = string.IsNullOrEmpty(keyword)
            ? null
            : shift => shift.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                       || shift.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);

        return PagedListBuilder.BuildAsync(
            _readRepository,
            (IQueryable<Shift> shifts) => shifts.OrderBy(shift => shift.StartTime).ThenBy(shift => shift.Code),
            query.PageNumber,
            query.PageSize,
            predicate,
            ShiftDto.FromEntity,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ShiftDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => ShiftDto.FromEntity(await GetOrThrowAsync(id, cancellationToken));

    /// <inheritdoc />
    public async Task<ShiftDto> CreateAsync(
        CreateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = InputGuard.RequireCode(request.Code, CodeMaxLength, "Mã ca");
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên ca");

        EnsureTimeRange(request.StartTime, request.EndTime);
        await EnsureCodeIsUniqueAsync(code, null, cancellationToken);

        var shift = new Shift
        {
            Code = code,
            Name = name,
            StartTime = request.StartTime,
            EndTime = request.EndTime
        };

        await _writeRepository.AddAsync(shift, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "ShiftCreated",
            "Shift",
            shift.Id,
            newValues: new { shift.Code, shift.Name, shift.StartTime, shift.EndTime },
            cancellationToken: cancellationToken);

        return ShiftDto.FromEntity(shift);
    }

    /// <inheritdoc />
    public async Task<ShiftDto> UpdateAsync(
        int id,
        UpdateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var shift = await GetOrThrowAsync(id, cancellationToken);
        var name = InputGuard.RequireText(request.Name, NameMaxLength, "Tên ca");

        EnsureTimeRange(request.StartTime, request.EndTime);

        var oldValues = new { shift.Name, shift.StartTime, shift.EndTime };

        shift.Name = name;
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;

        _writeRepository.Update(shift);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.WriteAsync(
            "ShiftUpdated",
            "Shift",
            shift.Id,
            oldValues,
            new { shift.Name, shift.StartTime, shift.EndTime },
            cancellationToken: cancellationToken);

        return ShiftDto.FromEntity(shift);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tiện ích nội bộ
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<Shift> GetOrThrowAsync(int id, CancellationToken cancellationToken)
        => await _readRepository.GetByIdAsync(id, cancellationToken)
           ?? throw new NotFoundException($"Không tìm thấy ca làm việc Id = {id}.");

    /// <summary>Ca phải có giờ kết thúc sau giờ bắt đầu (không hỗ trợ ca qua đêm).</summary>
    private static void EnsureTimeRange(TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
        {
            throw new BadRequestException("Giờ kết thúc ca (endTime) phải sau giờ bắt đầu (startTime).");
        }
    }

    /// <summary>UQ_Shifts_Code — so khớp không phân biệt hoa thường, bỏ qua bản ghi đang sửa.</summary>
    private async Task EnsureCodeIsUniqueAsync(string code, int? currentId, CancellationToken cancellationToken)
    {
        var shifts = await _readRepository.ListAsync(cancellationToken);

        var duplicated = shifts.Any(shift =>
            shift.Id != currentId
            && string.Equals(shift.Code, code, StringComparison.OrdinalIgnoreCase));

        if (duplicated)
        {
            throw new ConflictException($"Mã ca {code} đã tồn tại.", "SQL-50005");
        }
    }
}
