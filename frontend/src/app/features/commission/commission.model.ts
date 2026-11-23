/**
 * DTO + nhãn của module HOA HỒNG, khớp `SiliconHorizon.Application.Features.Commission.Dto`
 * (CommissionDtos.cs, CommissionStatusText.cs) — Database/README.md mục 8 (KPI-01..06, UC-28).
 *
 * Quy ước phạm vi (BRD 2.2) — API tự ép, client KHÔNG gửi khi không phải Admin:
 * - Nhân viên: chỉ xem hoa hồng của mình (ép `employeeId` từ token).
 * - Quản lý chi nhánh: chỉ xem trong chi nhánh mình (ép `branchId` từ token).
 * - Admin: xem toàn hệ thống, lọc được theo `employeeId`/`branchId`.
 */

/** Kỳ hoa hồng (CommissionPeriods) — `null` khi tháng chưa phát sinh bút toán nào. */
export interface CommissionPeriodDto {
  periodId: number;
  /** `yyyy-MM`. */
  yearMonth: string;
  /** 0 Open, 1 Closed. */
  status: number;
  statusName: string;
  /** `true` = kỳ đã chốt, không điều chỉnh được nữa. */
  isClosed: boolean;
}

/** Tổng hợp hoa hồng của một nhân viên trong kỳ. */
export interface CommissionEmployeeSummaryDto {
  employeeId: number;
  fullName: string;
  branchId: number;
  /** Tổng hoa hồng phát sinh (EntryType = 0). */
  earned: number;
  /** Tổng thu hồi do đổi trả (EntryType = 1, giá trị âm). */
  clawback: number;
  /** Tổng điều chỉnh thủ công (EntryType = 2). */
  adjustment: number;
  /** Thực nhận của kỳ = earned + clawback + adjustment. */
  total: number;
}

/** Một bút toán hoa hồng (CommissionEntries). */
export interface CommissionEntryDto {
  id: number;
  employeeId: number;
  orderId: number;
  orderCode: string;
  /** Yêu cầu đổi trả làm phát sinh bút toán thu hồi (chỉ có với `entryType = 1`). */
  returnRequestId: number | null;
  /** 0 Earn, 1 Clawback, 2 Adjustment. */
  entryType: number;
  entryTypeName: string;
  /** Doanh thu/lợi nhuận gộp dùng làm cơ sở tính (0 với bút toán điều chỉnh). */
  baseAmount: number;
  /** Tỷ lệ hoa hồng áp dụng (0 với bút toán điều chỉnh). */
  ratePercent: number;
  /** Số tiền bút toán — ÂM với Clawback. */
  amount: number;
  createdAt: string;
}

/** Kết quả GET /api/v1/commissions?yearMonth= — kỳ + tổng hợp theo nhân viên + chi tiết bút toán. */
export interface CommissionSummaryDto {
  /** `null` khi tháng chưa có kỳ hoa hồng. */
  period: CommissionPeriodDto | null;
  employees: CommissionEmployeeSummaryDto[];
  entries: CommissionEntryDto[];
}

/** Tham số GET /api/v1/commissions — `yearMonth` bắt buộc (`yyyy-MM`). */
export interface CommissionSummaryQuery {
  yearMonth: string;
  /** Chỉ Admin. */
  employeeId?: number | null;
  /** Chỉ Admin. */
  branchId?: number | null;
}

/** POST /api/v1/admin/commissions/adjustments — điều chỉnh thủ công (chỉ Admin). */
export interface AdjustCommissionRequest {
  employeeId: number;
  /** Đơn hàng gắn với bút toán (bắt buộc — CommissionEntries.OrderId NOT NULL). */
  orderId: number;
  /** Dương = cộng thêm, âm = trừ bớt; phải khác 0 (bằng 0 → 400). */
  amount: number;
  /** Lý do — bắt buộc, tối đa 300 ký tự, được ghi vào nhật ký AUD-01. */
  reason: string;
}

/** Kết quả điều chỉnh hoa hồng — Id bút toán vừa tạo. */
export interface CommissionAdjustmentResultDto {
  entryId: number;
}

/**
 * Nhân viên dùng cho bộ lọc/điều chỉnh (rút gọn `EmployeeDto`).
 * ⚠️ `GET /api/v1/admin/employees` chỉ Admin gọi được — chỉ màn điều chỉnh của Admin cần danh sách này.
 */
export interface CommissionEmployeeDto {
  id: number;
  employeeCode: string;
  fullName: string;
  branchId: number;
  positionName: string;
}

/** Loại bút toán hoa hồng (CommissionEntries.EntryType) — khớp `CommissionStatusText.EntryTypeText`. */
export const COMMISSION_ENTRY_TYPE_LABELS: Readonly<Record<number, string>> = {
  0: 'Hoa hồng phát sinh',
  1: 'Thu hồi hoa hồng (Clawback)',
  2: 'Điều chỉnh thủ công',
};

/** Trạng thái kỳ hoa hồng (CommissionPeriods.Status). */
export const COMMISSION_PERIOD_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Đang mở',
  1: 'Đã chốt',
};

/** Nhãn loại bút toán hoa hồng; trả nhãn dự phòng khi mã lạ. */
export function commissionEntryTypeLabel(entryType: number): string {
  return COMMISSION_ENTRY_TYPE_LABELS[entryType] ?? 'Không xác định';
}

/** Nhãn trạng thái kỳ hoa hồng; trả nhãn dự phòng khi mã lạ. */
export function commissionPeriodStatusLabel(status: number): string {
  return COMMISSION_PERIOD_STATUS_LABELS[status] ?? 'Không xác định';
}

/** `Date` → `yyyy-MM` giờ địa phương (giá trị mặc định cho ô "tháng"). */
export function commissionYearMonth(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');

  return `${date.getFullYear()}-${month}`;
}
