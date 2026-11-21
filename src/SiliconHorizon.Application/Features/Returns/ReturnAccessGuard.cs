using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Returns.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Returns;

/// <summary>
/// Chốt chặn phạm vi dữ liệu của module Đổi trả (Database/README.md mục 1.1, BRD mục 2.2):
/// khách chỉ thao tác yêu cầu của mình, nhân viên chỉ thao tác yêu cầu của chi nhánh mình, Admin bỏ qua giới hạn.
/// </summary>
/// <remarks>
/// Các proc đổi trả chỉ nhận <c>@UserId</c> để ghi nhật ký và <c>@CustomerId</c> để giới hạn khách —
/// <b>không</b> tự kiểm tra chi nhánh của yêu cầu (trừ <c>usp_Return_Refund</c> xử lý theo đơn).
/// Vì vậy API phải đi qua lớp này trước khi gọi proc.
/// <para>
/// <c>branchId</c>/<c>customerId</c> LUÔN lấy từ token (<see cref="ICurrentUser"/>), KHÔNG nhận từ client.
/// </para>
/// </remarks>
public sealed class ReturnAccessGuard
{
    /// <summary>Vai trò nhân viên (Admin có toàn quyền theo BRD 2.2).</summary>
    private static readonly string[] StaffRoles =
    {
        RoleCodes.Cashier,
        RoleCodes.TechAdvisor,
        RoleCodes.Storekeeper,
        RoleCodes.Manager,
        RoleCodes.Admin
    };

    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;

    public ReturnAccessGuard(ISqlExecutor sqlExecutor, ICurrentUser currentUser)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
    }

    /// <summary>Người gọi là nhân viên (Thu ngân/Tư vấn/Thủ kho/Quản lý/Admin) hay là khách hàng.</summary>
    public bool IsStaff => StaffRoles.Any(_currentUser.IsInRole);

    /// <summary>Chi nhánh bắt buộc để xử lý yêu cầu đổi trả; token không gắn chi nhánh → 403 kể cả Admin.</summary>
    public int RequireBranchId()
        => _currentUser.BranchId
           ?? throw new ForbiddenException(
               "Tài khoản chưa được gắn chi nhánh nên không thực hiện được nghiệp vụ đổi trả.");

    /// <summary>
    /// Phạm vi ĐỌC của nhân viên: Admin không gắn chi nhánh thì trả <c>null</c> (= toàn hệ thống);
    /// các vai trò khác bắt buộc có chi nhánh.
    /// </summary>
    public int? BranchScope()
        => _currentUser.IsInRole(RoleCodes.Admin) ? _currentUser.BranchId : RequireBranchId();

    /// <summary>Khách hàng phải có hồ sơ khách trong token (thiếu → 403) — KHÔNG nhận <c>customerId</c> từ client.</summary>
    public long RequireCustomerId()
        => _currentUser.CustomerId
           ?? throw new ForbiddenException(
               "Tài khoản khách hàng chưa gắn hồ sơ khách hàng nên không thực hiện được thao tác đổi trả.");

    /// <summary>Người dùng đang gọi — các proc đổi trả ghi <c>@UserId</c> vào lịch sử/nhật ký.</summary>
    public long RequireUserId()
        => _currentUser.UserId
           ?? throw new ForbiddenException("Token thiếu định danh người dùng nên không thực hiện được thao tác này.");

    /// <summary>
    /// Phạm vi đọc của <c>GET /api/v1/returns</c>: nhân viên → <c>@BranchId</c> (khách = null),
    /// khách hàng → <c>@CustomerId</c> (chi nhánh = null).
    /// </summary>
    public (int? BranchId, long? CustomerId) ResolveReadScope()
        => IsStaff ? (BranchScope(), null) : (null, RequireCustomerId());

    /// <summary>
    /// Đọc header yêu cầu (result set 1 của <c>usp_Return_Get</c>) để kiểm tra phạm vi.
    /// Không tồn tại → 404 (proc ném 50001).
    /// </summary>
    /// <param name="returnRequestId">Id yêu cầu đổi trả.</param>
    /// <param name="customerId">
    /// <c>null</c> khi người gọi là nhân viên (kiểm tra chi nhánh sau bằng <see cref="EnsureBranch"/>);
    /// khách hàng truyền <c>customerId</c> ép từ token để proc tự giới hạn quyền xem.
    /// </param>
    public async Task<ReturnRequestRow> LoadRequestAsync(
        long returnRequestId,
        long? customerId = null,
        CancellationToken cancellationToken = default)
        => await _sqlExecutor.QuerySingleOrDefaultAsync<ReturnRequestRow>(
                StoredProcedureNames.ReturnGet,
                new { ReturnRequestId = returnRequestId, CustomerId = customerId },
                cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu đổi trả Id = {returnRequestId}.");

    /// <summary>Nhân viên (không phải Admin) chỉ được xử lý yêu cầu của chi nhánh mình; nếu không → 403.</summary>
    public void EnsureBranch(int entityBranchId, string action)
    {
        if (_currentUser.IsInRole(RoleCodes.Admin))
        {
            return;
        }

        var branchId = RequireBranchId();

        if (entityBranchId != branchId)
        {
            throw new ForbiddenException(
                $"Bạn chỉ {action} của chi nhánh {branchId}; yêu cầu đang thao tác thuộc chi nhánh {entityBranchId}.");
        }
    }
}