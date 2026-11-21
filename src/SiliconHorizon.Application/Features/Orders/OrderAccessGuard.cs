using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;
using SiliconHorizon.Application.Common.Sql;
using SiliconHorizon.Application.Features.Orders.Dto;
using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Orders;

/// <summary>
/// Chốt chặn phạm vi dữ liệu của module đơn hàng (Database/README.md mục 1.1 và BRD mục 2.2):
/// khách chỉ với đơn của mình, nhân viên chỉ với đơn thuộc chi nhánh của mình (Admin bỏ qua).
/// </summary>
/// <remarks>
/// Các proc <c>usp_Order_*</c> chỉ nhận <c>@UserId</c> để ghi nhật ký, KHÔNG tự kiểm tra phạm vi, nên mọi endpoint
/// chuyển trạng thái/thanh toán phải đi qua lớp này trước khi gọi proc — nếu không, nhân viên chi nhánh này có thể
/// thao tác đơn của chi nhánh khác bằng cách đoán id.
/// <para>
/// Đọc header bằng <c>usp_Order_Get</c> (proc này trả 6 result set; Dapper đọc tập đầu tiên rồi giải phóng reader,
/// các tập còn lại bị bỏ qua).
/// </para>
/// </remarks>
public sealed class OrderAccessGuard
{
    private readonly ISqlExecutor _sqlExecutor;
    private readonly ICurrentUser _currentUser;

    public OrderAccessGuard(ISqlExecutor sqlExecutor, ICurrentUser currentUser)
    {
        _sqlExecutor = sqlExecutor;
        _currentUser = currentUser;
    }

    /// <summary>Đọc header đơn theo Id; đơn không tồn tại → 404 (proc báo 50001).</summary>
    public async Task<OrderHeaderRow> LoadAsync(long orderId, CancellationToken cancellationToken = default)
        => await _sqlExecutor.QuerySingleOrDefaultAsync<OrderHeaderRow>(
                StoredProcedureNames.OrderGet,
                new { OrderId = orderId },
                cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đơn hàng Id = {orderId}.");

    /// <summary>Đọc header đơn theo mã đơn (<c>DH...</c>); không tồn tại → 404.</summary>
    public async Task<OrderHeaderRow> LoadByCodeAsync(string orderCode, CancellationToken cancellationToken = default)
        => await _sqlExecutor.QuerySingleOrDefaultAsync<OrderHeaderRow>(
                StoredProcedureNames.OrderGet,
                new { OrderCode = orderCode },
                cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đơn hàng có mã {orderCode}.");

    /// <summary>Khách đang gọi phải là chủ đơn; nếu không → 403.</summary>
    public async Task<OrderHeaderRow> ForCustomerAsync(long orderId, CancellationToken cancellationToken = default)
    {
        var header = await LoadAsync(orderId, cancellationToken);
        EnsureCustomerOwns(header);

        return header;
    }

    /// <summary>Đảm bảo đơn thuộc về khách trong token (ép <c>CustomerId</c> — không tin giá trị client gửi).</summary>
    public void EnsureCustomerOwns(OrderHeaderRow header)
    {
        var customerId = _currentUser.CustomerId
            ?? throw new ForbiddenException("Tài khoản hiện tại không gắn hồ sơ khách hàng nên không thao tác được đơn hàng.");

        if (header.CustomerId != customerId)
        {
            throw new ForbiddenException("Đơn hàng không thuộc tài khoản khách hàng của bạn.");
        }
    }

    /// <summary>Nhân viên đang gọi phải thuộc chi nhánh của đơn (Admin bỏ qua); nếu không → 403.</summary>
    public async Task<OrderHeaderRow> ForStaffAsync(long orderId, CancellationToken cancellationToken = default)
    {
        var header = await LoadAsync(orderId, cancellationToken);
        EnsureStaffBranch(header);

        return header;
    }

    /// <summary>Đảm bảo nhân viên chỉ thao tác đơn của chi nhánh mình (ép <c>BranchId</c> từ token — BRD 2.2).</summary>
    public void EnsureStaffBranch(OrderHeaderRow header)
    {
        if (_currentUser.IsInRole(RoleCodes.Admin))
        {
            return;
        }

        var branchId = _currentUser.BranchId
            ?? throw new ForbiddenException("Tài khoản nhân viên chưa được gắn chi nhánh nên không thao tác được đơn hàng.");

        if (header.BranchId != branchId)
        {
            throw new ForbiddenException(
                $"Đơn hàng thuộc chi nhánh {header.BranchId}; bạn chỉ thao tác được đơn của chi nhánh {branchId}.");
        }
    }
}
