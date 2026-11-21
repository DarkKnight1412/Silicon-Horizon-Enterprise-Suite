using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Inventory.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Inventory;

/// <summary>
/// Chốt chặn phạm vi chi nhánh của module Kho (Database/README.md mục 1.1, BRD mục 2.2):
/// nhân viên chỉ thao tác dữ liệu kho của chi nhánh mình, Admin bỏ qua giới hạn chi nhánh.
/// </summary>
/// <remarks>
/// Các proc kho <b>không</b> tự kiểm tra quyền theo chi nhánh — chúng chỉ nhận <c>@UserId</c> để ghi nhật ký
/// (trừ <c>usp_StockTransfer_*</c> giới hạn theo chi nhánh đi/đến một phần). Vì vậy API phải đi qua lớp này
/// trước khi gọi proc, nếu không thủ kho chi nhánh A có thể xuất/nhận/duyệt dữ liệu của chi nhánh B bằng cách đoán id.
/// <para>
/// <c>branchId</c> LUÔN lấy từ token (<see cref="ICurrentUser.BranchId"/>), KHÔNG bao giờ nhận từ client.
/// </para>
/// </remarks>
public sealed class InventoryAccessGuard
{
    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;

    public InventoryAccessGuard(ISqlExecutor sqlExecutor, ICurrentUser currentUser)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Chi nhánh bắt buộc cho thao tác GHI (nhập kho, chuyển kho, kiểm kê, trả nhà cung cấp):
    /// token không gắn chi nhánh → 403 kể cả Admin, vì proc cần một chi nhánh cụ thể.
    /// </summary>
    public int RequireBranchId()
        => _currentUser.BranchId
           ?? throw new ForbiddenException(
               "Tài khoản chưa được gắn chi nhánh nên không thực hiện được nghiệp vụ kho.");

    /// <summary>
    /// Phạm vi ĐỌC: Admin không gắn chi nhánh thì trả <c>null</c> (= toàn hệ thống, đúng ghi chú của
    /// <see cref="ICurrentUser.BranchId"/>); các vai trò khác bắt buộc có chi nhánh.
    /// </summary>
    public int? BranchScope()
        => _currentUser.IsInRole(RoleCodes.Admin) ? _currentUser.BranchId : RequireBranchId();

    /// <summary>Nhân viên (không phải Admin) chỉ được thao tác dữ liệu của chi nhánh mình; nếu không → 403.</summary>
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
                $"Bạn chỉ {action} dữ liệu của chi nhánh {branchId}; dữ liệu đang thao tác thuộc chi nhánh {entityBranchId}.");
        }
    }

    /// <summary>Đọc header phiếu chuyển kho (result set 1 của <c>usp_StockTransfer_Get</c>); không tồn tại → 404.</summary>
    public async Task<StockTransferHeaderRow> LoadTransferAsync(long transferId, CancellationToken cancellationToken = default)
        => await _sqlExecutor.QuerySingleOrDefaultAsync<StockTransferHeaderRow>(
                StoredProcedureNames.StockTransferGet,
                new { TransferId = transferId },
                cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy phiếu chuyển kho Id = {transferId}.");

    /// <summary>
    /// Xuất kho: chỉ thủ kho (hoặc Admin) của CHI NHÁNH ĐI mới được đẩy phiếu sang trạng thái Đang chuyển —
    /// đúng cột "TK (chi nhánh đi)" của Database/README.md mục 6.
    /// </summary>
    public void EnsureTransferShip(StockTransferHeaderRow header) => EnsureBranch(header.FromBranchId, "xuất kho phiếu chuyển");

    /// <summary>Nhận hàng: chỉ thủ kho (hoặc Admin) của CHI NHÁNH ĐẾN mới được xác nhận đã nhận.</summary>
    public void EnsureTransferReceive(StockTransferHeaderRow header) => EnsureBranch(header.ToBranchId, "nhận hàng phiếu chuyển");

    /// <summary>Hủy phiếu nháp: phiếu chưa xuất kho thuộc chi nhánh đi nên chỉ chi nhánh đi được hủy.</summary>
    public void EnsureTransferCancel(StockTransferHeaderRow header) => EnsureBranch(header.FromBranchId, "hủy phiếu chuyển");

    /// <summary>Xem chi tiết phiếu chuyển: nhân viên phải thuộc chi nhánh đi HOẶC chi nhánh đến (Admin xem tất cả).</summary>
    public void EnsureTransferView(StockTransferHeaderRow header)
    {
        if (_currentUser.IsInRole(RoleCodes.Admin))
        {
            return;
        }

        var branchId = RequireBranchId();

        if (header.FromBranchId != branchId && header.ToBranchId != branchId)
        {
            throw new ForbiddenException(
                $"Phiếu chuyển kho không thuộc chi nhánh {branchId} nên bạn không xem được.");
        }
    }

    /// <summary>Đọc header đợt kiểm kê (result set 1 của <c>usp_StockTake_Get</c>); không tồn tại → 404.</summary>
    public async Task<StockTakeHeaderRow> LoadStockTakeAsync(long stockTakeId, CancellationToken cancellationToken = default)
        => await _sqlExecutor.QuerySingleOrDefaultAsync<StockTakeHeaderRow>(
                StoredProcedureNames.StockTakeGet,
                new { StockTakeId = stockTakeId },
                cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy phiếu kiểm kê Id = {stockTakeId}.");

    /// <summary>Đợt kiểm kê thuộc chi nhánh nào thì chỉ thủ kho/quản lý chi nhánh đó (hoặc Admin) thao tác.</summary>
    public void EnsureStockTakeBranch(StockTakeHeaderRow header, string action) => EnsureBranch(header.BranchId, action);
}
