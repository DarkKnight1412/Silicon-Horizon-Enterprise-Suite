using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.Inventory.Dto;

namespace SiliconHorizon.Application.Features.Inventory;

/// <summary>
/// Use case nhà cung cấp (SUP-01..03).
/// </summary>
/// <remarks>
/// Danh mục nhà cung cấp là bảng cấu hình nên CRUD đi bằng EF Core
/// (<c>IReadRepository&lt;Supplier&gt;</c>/<c>IWriteRepository&lt;Supplier&gt;</c>) — đúng ghi chú
/// "EF <c>Suppliers</c>" trong <c>src/SiliconHorizon.Application/Features/README.md</c>.
/// Nghiệp vụ công nợ/trả hàng bắt buộc dùng stored procedure vì phải khóa sổ và ghi bút toán.
/// </remarks>
public interface ISupplierService
{
    /// <summary>Danh sách nhà cung cấp (phân trang, lọc theo mã/tên và trạng thái) — GET /api/v1/suppliers.</summary>
    Task<PagedResult<SupplierDto>> ListAsync(SupplierListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Chi tiết nhà cung cấp — GET /api/v1/suppliers/{id}.</summary>
    Task<SupplierDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Tạo nhà cung cấp — POST /api/v1/suppliers (mã duy nhất, không xóa cứng sau này).</summary>
    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cập nhật/ngừng hợp tác nhà cung cấp — PUT /api/v1/suppliers/{id}.</summary>
    Task<SupplierDto> UpdateAsync(long id, UpdateSupplierRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trả hàng nhà cung cấp — POST /api/v1/supplier-returns (usp_SupplierReturn_Create).
    /// Serial trả phải đang ở trạng thái Defective tại chi nhánh của người gọi.
    /// </summary>
    Task<CreatedSupplierReturnDto> CreateSupplierReturnAsync(
        CreateSupplierReturnRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhận thanh toán cho nhà cung cấp — POST /api/v1/suppliers/{id}/payments
    /// (usp_SupplierPayment_Record); proc chặn thanh toán vượt công nợ hiện tại.
    /// </summary>
    Task<SupplierPaymentResultDto> RecordPaymentAsync(
        long supplierId,
        RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Sao kê công nợ phải trả — GET /api/v1/suppliers/{id}/ledger (usp_Supplier_GetLedger).</summary>
    Task<SupplierLedgerDto> GetLedgerAsync(
        long supplierId,
        SupplierLedgerQuery query,
        CancellationToken cancellationToken = default);
}
