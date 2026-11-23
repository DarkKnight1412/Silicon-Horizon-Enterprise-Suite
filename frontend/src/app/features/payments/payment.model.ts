import type { SelectOption } from '../../core/models/common.model';

/**
 * DTO + nhãn của module THANH TOÁN / CÔNG NỢ, khớp
 * `SiliconHorizon.Application.Features.Orders.Dto.PaymentDtos.cs` (Database/README.md mục 5.3, PAY-01..05).
 *
 * Quy ước backend:
 * - Ghi nhận thanh toán dùng `POST /api/v1/orders/{id}/payments` (usp_Payment_Record). Body KHÔNG có
 *   `provider`/`providerTransactionId` — giao dịch cổng thanh toán đi qua callback riêng
 *   (`POST /api/v1/payments/callback/{provider}`) nên chỉ luồng đó mới được đánh dấu trùng.
 * - Hoàn tiền thủ công dùng `POST /api/v1/orders/{id}/refunds`, CHỈ áp dụng cho đơn Đã hủy (8) hoặc
 *   Giao thất bại (6); hoàn tiền do đổi trả đi qua luồng đổi trả (`usp_Return_Refund`).
 * - API LUÔN ép phạm vi từ token nên request không có `branchId`/`customerId`.
 */

/** POST /api/v1/orders/{id}/payments — khớp `RecordPaymentRequest`. */
export interface RecordPaymentRequest {
  /** 0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS, 4 Công nợ. */
  method: number;
  /** Số tiền thu (VND) — phải lớn hơn 0, nếu không proc báo 50004 → 400. */
  amount: number;
  /** ISO 8601 UTC; `null` = lấy thời điểm hiện tại. */
  paidAt: string | null;
}

/** Kết quả ghi nhận thanh toán (`usp_Payment_Record`) — `needsReview` cần đối soát thủ công. */
export interface RecordedPaymentDto {
  paymentId: number;
  /** Cùng `(provider, providerTransactionId)` đã ghi nhận trước đó ⇒ không cộng tiền lần hai. */
  isDuplicate: boolean;
  /** Tổng đã thu của đơn SAU khi ghi nhận; `null` khi trả về do trùng giao dịch. */
  paidAmount: number | null;
  /** Trạng thái đơn sau khi ghi nhận; `null` khi trả về do trùng giao dịch. */
  orderStatus: number | null;
  /** `true` = tiền về cho đơn đã hủy hoặc thanh toán thừa ⇒ cần đối soát/hoàn tiền thủ công. */
  needsReview: boolean;
}

/** POST /api/v1/orders/{id}/refunds — khớp `RefundRequest`. */
export interface RefundRequest {
  /** VND, phải > 0 và không vượt quá số đã thanh toán của đơn (vượt → 422). */
  amount: number;
  /** Nên trùng phương thức gốc (PAY-04): 0 COD … 4 Công nợ. */
  method: number;
  /** Lý do hoàn tiền, tối đa 300 ký tự; rỗng → 400. */
  reason: string;
}

/** Kết quả hoàn tiền — Id bản ghi hoàn trong bảng Payments. */
export interface RefundResultDto {
  paymentId: number;
}

/** Một bút toán công nợ của khách (`CustomerLedgerEntryDto`). */
export interface CustomerLedgerEntryDto {
  id: number;
  /** 0 Charge (phát sinh nợ), 1 Payment (giảm nợ), 2 Adjustment (điều chỉnh). */
  entryType: number;
  /** Dương: tăng nợ; âm: giảm nợ. */
  amount: number;
  /** Hạn thanh toán của khoản ghi nợ (mua chịu). */
  dueDate: string | null;
  orderId: number | null;
  paymentId: number | null;
  createdAt: string;
}

/** Sao kê công nợ khách — GET /api/v1/customers/{id}/ledger (2 result set của usp_Customer_GetLedger). */
export interface CustomerLedgerDto {
  customerId: number;
  /** Tổng công nợ hiện tại (dương = khách còn nợ). */
  balance: number;
  /** Hạn mức công nợ được cấp (0 = chưa cấp). */
  creditLimit: number;
  /** Tỷ lệ đã dùng trên hạn mức (0..1; 0 khi chưa cấp hạn mức). */
  creditUsageRatio: number;
  entries: CustomerLedgerEntryDto[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Tham số phân trang sao kê công nợ (proc tự cắt tối đa 200). */
export interface CustomerLedgerQuery {
  pageNumber: number;
  pageSize: number;
}

/** Nhãn loại bút toán công nợ khách (CustomerLedger.EntryType). */
export const LEDGER_ENTRY_TYPE_LABELS: Readonly<Record<number, string>> = {
  0: 'Phát sinh nợ',
  1: 'Thanh toán',
  2: 'Điều chỉnh',
};

/** Phương thức thực thu/hoàn tiền dùng trong form ghi nhận (Orders.PaymentMethod). */
export const RECORD_PAYMENT_METHOD_OPTIONS: readonly SelectOption<number>[] = [
  { value: 0, label: 'COD (thu khi giao)' },
  { value: 1, label: 'Chuyển khoản' },
  { value: 2, label: 'Cổng thanh toán' },
  { value: 3, label: 'Tiền mặt / POS' },
  { value: 4, label: 'Công nợ' },
];

/** Nhãn loại bút toán công nợ; trả về nhãn dự phòng khi mã lạ. */
export function ledgerEntryTypeLabel(entryType: number): string {
  return LEDGER_ENTRY_TYPE_LABELS[entryType] ?? 'Không xác định';
}

/** Số tiền còn phải thu của đơn (không bao giờ âm vì khách có thể đã trả thừa). */
export function remainingAmount(grandTotal: number, paidAmount: number): number {
  const remaining = grandTotal - paidAmount;

  return remaining > 0 ? remaining : 0;
}

/**
 * Đơn được phép hoàn tiền thủ công: Đã hủy (8) hoặc Giao thất bại (6).
 * (Hoàn tiền do đổi trả đi qua quy trình đổi trả, không dùng endpoint refunds.)
 */
export function canRefundOrder(status: number): boolean {
  return status === 6 || status === 8;
}
