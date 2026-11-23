/**
 * DTO của module NHẬT KÝ HỆ THỐNG, khớp `SiliconHorizon.Application.Features.Audit.Dto.AuditLogDtos.cs`
 * (Database/README.md mục 8, UC-29/AUD-04).
 *
 * Bảng `AuditLogs` là sổ append-only ⇒ module CHỈ có API đọc, không có endpoint sửa/xoá.
 * Quy ước phạm vi: Quản lý chi nhánh chỉ thấy nhật ký chi nhánh mình (API ép `branchId` từ token),
 * Admin xem toàn hệ thống và lọc được theo `branchId`.
 */

/** Một dòng nhật ký — GET /api/v1/audit-logs. */
export interface AuditLogDto {
  id: number;
  /** Người thực hiện; `null` = hệ thống/tác vụ nền. */
  userId: number | null;
  username: string | null;
  branchId: number | null;
  /** Mã hành động: PriceChanged, OrderCancelled, AttendanceAdjusted… */
  action: string;
  /** Loại đối tượng: Order, Product, StockTake, Attendance, ReturnRequest… */
  entityType: string;
  entityId: number;
  /** JSON giá trị TRƯỚC khi sửa (giữ nguyên chuỗi để không mất dữ liệu khi JSON không hợp lệ). */
  oldValues: string | null;
  /** JSON giá trị SAU khi sửa. */
  newValues: string | null;
  reason: string | null;
  ipAddress: string | null;
  /** UTC (ISO 8601). */
  createdAt: string;
}

/** Tham số GET /api/v1/audit-logs — mọi tham số tùy chọn, kết hợp AND. */
export interface AuditLogQuery {
  /** Tối đa 50 ký tự (`@EntityType varchar(50)`). */
  entityType?: string | null;
  entityId?: number | null;
  userId?: number | null;
  /** Tối đa 60 ký tự (`@Action varchar(60)`). */
  action?: string | null;
  /** UTC, chặn dưới BAO GỒM. */
  fromDate?: string | null;
  /** UTC, chặn trên KHÔNG bao gồm — truyền đầu ngày kế tiếp để lấy trọn một ngày. */
  toDate?: string | null;
  pageNumber: number;
  pageSize: number;
}

/** Các `entityType` thường gặp (gợi ý cho ô lọc — backend không trả danh mục cố định). */
export const AUDIT_ENTITY_TYPE_SUGGESTIONS: readonly string[] = [
  'Order',
  'Product',
  'PurchaseReceipt',
  'StockTake',
  'StockTransfer',
  'SerialItem',
  'ReturnRequest',
  'WarrantyTicket',
  'Attendance',
  'EmployeeSchedule',
  'CommissionEntry',
  'Customer',
  'Voucher',
];

/**
 * Làm đẹp chuỗi JSON của `oldValues`/`newValues` để đọc trong hộp thoại chi tiết.
 * Nếu chuỗi không phải JSON hợp lệ thì trả nguyên văn (đúng quy ước DTO giữ chuỗi thô).
 */
export function prettyJson(value: string | null): string {
  if (value === null || value.trim().length === 0) {
    return '—';
  }

  try {
    const parsed: unknown = JSON.parse(value);
    const text: string | undefined = JSON.stringify(parsed, null, 2);

    return text ?? value;
  } catch {
    return value;
  }
}
