using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Warranty.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Warranty;

/// <summary>
/// Chốt chặn phạm vi dữ liệu của module Bảo hành (Database/README.md mục 1.1, BRD mục 2.2):
/// nhân viên chỉ thao tác phiếu của chi nhánh mình, khách chỉ thấy phiếu của mình, Admin bỏ qua giới hạn chi nhánh.
/// </summary>
/// <remarks>
/// Các proc bảo hành chỉ nhận <c>@UserId</c> để ghi nhật ký và <c>@CustomerId</c> để giới hạn khách —
/// <b>không</b> tự kiểm tra chi nhánh của phiếu. Vì vậy API phải đi qua lớp này trước khi gọi proc,
/// nếu không thủ kho/nhân viên chi nhánh A có thể đổi trạng thái phiếu của chi nhánh B bằng cách đoán id.
/// <para>
/// <c>branchId</c>/<c>customerId</c> LUÔN lấy từ token (<see cref="ICurrentUser"/>), KHÔNG nhận từ client.
/// </para>
/// </remarks>
public sealed class WarrantyAccessGuard
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

    public WarrantyAccessGuard(ISqlExecutor sqlExecutor, ICurrentUser currentUser)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
    }

    /// <summary>Người gọi là nhân viên (Thu ngân/Tư vấn/Thủ kho/Quản lý/Admin) hay là khách hàng.</summary>
    public bool IsStaff => StaffRoles.Any(_currentUser.IsInRole);

    /// <summary>
    /// Chi nhánh bắt buộc cho thao tác GHI (tiếp nhận, chuyển trạng thái, đính kèm):
    /// token không gắn chi nhánh → 403 kể cả Admin, vì proc cần một chi nhánh (hoặc để đối chiếu) cụ thể.
    /// </summary>
    public int RequireBranchId()
        => _currentUser.BranchId
           ?? throw new ForbiddenException(
               "Tài khoản chưa được gắn chi nhánh nên không thực hiện được nghiệp vụ bảo hành.");

    /// <summary>
    /// Phạm vi ĐỌC của nhân viên: Admin không gắn chi nhánh thì trả <c>null</c> (= toàn hệ thống,
    /// đúng ghi chú của <see cref="ICurrentUser.BranchId"/>); các vai trò khác bắt buộc có chi nhánh.
    /// </summary>
    public int? BranchScope()
        => _currentUser.IsInRole(RoleCodes.Admin) ? _currentUser.BranchId : RequireBranchId();

    /// <summary>Khách hàng phải có hồ sơ khách trong token (thiếu → 403) — KHÔNG nhận <c>customerId</c> từ client.</summary>
    public long RequireCustomerId()
        => _currentUser.CustomerId
           ?? throw new ForbiddenException(
               "Tài khoản khách hàng chưa gắn hồ sơ khách hàng nên không xem được phiếu bảo hành.");

    /// <summary>Người dùng đang gọi — các proc bảo hành ghi <c>@UserId</c> vào nhật ký/lịch sử phiếu.</summary>
    public long RequireUserId()
        => _currentUser.UserId
           ?? throw new ForbiddenException("Token thiếu định danh người dùng nên không thực hiện được thao tác này.");

    /// <summary>
    /// Phạm vi đọc của <c>GET /api/v1/warranty-tickets</c>: nhân viên → <c>@BranchId</c> (khách = null),
    /// khách hàng → <c>@CustomerId</c> (chi nhánh = null). Proc nhận cả hai và chỉ áp dụng giá trị khác null.
    /// </summary>
    public (int? BranchId, long? CustomerId) ResolveReadScope()
        => IsStaff ? (BranchScope(), null) : (null, RequireCustomerId());

    /// <summary>
    /// Đọc header phiếu (result set 1 của <c>usp_Warranty_Get</c>) để kiểm tra phạm vi.
    /// Không tồn tại → 404 (proc ném 50001).
    /// </summary>
    /// <param name="ticketId">Id phiếu bảo hành.</param>
    /// <param name="customerId">
    /// <c>null</c> khi người gọi là nhân viên (kiểm tra chi nhánh sau bằng <see cref="EnsureBranch"/>);
    /// khách hàng truyền <c>customerId</c> ép từ token để proc tự giới hạn quyền xem.
    /// </param>
    public async Task<WarrantyTicketRow> LoadTicketAsync(
        long ticketId,
        long? customerId = null,
        CancellationToken cancellationToken = default)
        => await _sqlExecutor.QuerySingleOrDefaultAsync<WarrantyTicketRow>(
                StoredProcedureNames.WarrantyGet,
                new { TicketId = ticketId, CustomerId = customerId },
                cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy phiếu bảo hành Id = {ticketId}.");

    /// <summary>Nhân viên (không phải Admin) chỉ được thao tác phiếu của chi nhánh mình; nếu không → 403.</summary>
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
                $"Bạn chỉ {action} của chi nhánh {branchId}; phiếu đang thao tác thuộc chi nhánh {entityBranchId}.");
        }
    }
}