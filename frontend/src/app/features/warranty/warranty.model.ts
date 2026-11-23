import type { SelectOption } from '../../core/models/common.model';

/**
 * DTO + nhãn của module BẢO HÀNH, khớp
 * `SiliconHorizon.Application.Features.Warranty.Dto` (WarrantyDtos.cs, WarrantyStatusText.cs)
 * — Database/README.md mục 7.1, WAR-01..08.
 *
 * Quy ước phạm vi: nhân viên ép `branchId` từ token, khách ép `customerId` — request KHÔNG có hai
 * trường này.
 */

/** Phiếu bảo hành đầy đủ (`WarrantyTicketDto`). */
export interface WarrantyTicketDto {
  id: number;
  ticketCode: string;
  /** Hàng có serial (null với hàng không quản lý serial). */
  serialItemId: number | null;
  /** Dòng hàng của đơn (dùng cho hàng không serial). */
  orderItemId: number | null;
  customerId: number;
  branchId: number;
  /** 0 Đã tiếp nhận … 6 Đã trả khách (xem WARRANTY_STATUS_LABELS). */
  status: number;
  statusName: string;
  issueDescription: string;
  conditionNotes: string | null;
  rejectionReason: string | null;
  replacementSerialId: number | null;
  replacementSerialNumber: string | null;
  vendorSentAt: string | null;
  vendorReturnedAt: string | null;
  receivedAt: string;
  returnedAt: string | null;
  /** Phí sửa chữa thu của khách (0 nếu trong hạn bảo hành). */
  fee: number;
  serialNumber: string | null;
  sku: string | null;
  productName: string | null;
  customerName: string;
  customerPhone: string | null;
  createdAt: string;
  updatedAt: string | null;
}

/** Một lần chuyển trạng thái phiếu. */
export interface WarrantyTicketHistoryDto {
  fromStatus: number | null;
  fromStatusName: string | null;
  toStatus: number;
  toStatusName: string;
  note: string | null;
  changedBy: number | null;
  changedAt: string;
}

/** Ảnh/tài liệu đính kèm của phiếu. */
export interface WarrantyTicketAttachmentDto {
  id: number;
  url: string;
}

/** Chi tiết phiếu — GET /api/v1/warranty-tickets/{id} (3 result set). */
export interface WarrantyTicketDetailDto {
  ticket: WarrantyTicketDto;
  history: WarrantyTicketHistoryDto[];
  attachments: WarrantyTicketAttachmentDto[];
}

/** Một dòng danh sách phiếu — GET /api/v1/warranty-tickets. */
export interface WarrantyTicketListItemDto {
  id: number;
  ticketCode: string;
  status: number;
  statusName: string;
  branchId: number;
  customerId: number;
  customerName: string;
  serialNumber: string | null;
  receivedAt: string;
  returnedAt: string | null;
}

/** POST /api/v1/warranty-tickets — tiếp nhận bảo hành (chi nhánh ép từ token). */
export interface ReceiveWarrantyTicketRequest {
  /** Bắt buộc khi hàng quản lý theo serial (serial phải đang Sold). */
  serialNumber: string | null;
  /** Chỉ cần khi cùng một số serial tồn tại ở nhiều sản phẩm. */
  productId: number | null;
  /** Bắt buộc khi hàng KHÔNG quản lý theo serial. */
  orderItemId: number | null;
  issueDescription: string;
  conditionNotes: string | null;
  /** Có giá trị ⇒ proc tạo phiếu ở trạng thái Từ chối ngay (không chuyển serial sang RMA). */
  rejectReason: string | null;
}

/** Kết quả tiếp nhận — `rejected = true` khi hết hạn bảo hành hoặc bị từ chối ngay. */
export interface ReceivedWarrantyTicketDto {
  ticketId: number;
  ticketCode: string;
  status: number;
  statusName: string;
  rejectionReason: string | null;
  rejected: boolean;
}

/** POST /api/v1/warranty-tickets/{id}/status — chuyển trạng thái phiếu. */
export interface ChangeWarrantyStatusRequest {
  /** 1 Chẩn đoán, 2 Gửi hãng, 3 Sửa xong, 4 Đổi mới, 5 Từ chối, 6 Trả khách. */
  toStatus: number;
  note: string | null;
  /** BẮT BUỘC khi `toStatus = 4`: serial thay thế cùng sản phẩm, đang Trong kho tại chi nhánh. */
  replacementSerialNumber: string | null;
  /** BẮT BUỘC khi `toStatus = 5`. */
  rejectionReason: string | null;
  /** Bỏ trống (null) ⇒ proc giữ nguyên phí hiện tại của phiếu. */
  fee: number | null;
}

/** POST /api/v1/warranty-tickets/{id}/attachments — đính kèm ảnh/tài liệu. */
export interface AddWarrantyAttachmentRequest {
  /** Đường dẫn tài liệu, tối đa 500 ký tự. */
  url: string;
}

/** Tham số danh sách phiếu — không có `branchId`/`customerId` (API ép từ token). */
export interface WarrantyTicketListQuery {
  status?: number | null;
  keyword?: string | null;
  pageNumber: number;
  pageSize: number;
}

// ── Nhãn + sơ đồ chuyển trạng thái (khớp WarrantyStatusText.cs) ────────────

/** Trạng thái phiếu bảo hành (WarrantyTickets.Status). */
export const WARRANTY_STATUS = {
  Received: 0,
  Diagnosing: 1,
  SentToVendor: 2,
  Repaired: 3,
  Replaced: 4,
  Rejected: 5,
  ReturnedToCustomer: 6,
} as const;

export const WARRANTY_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Đã tiếp nhận',
  1: 'Đang chẩn đoán',
  2: 'Đã gửi hãng',
  3: 'Đã sửa xong',
  4: 'Đã đổi mới',
  5: 'Từ chối bảo hành',
  6: 'Đã trả khách',
};

export const WARRANTY_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-sky-100 text-sky-800',
  1: 'bg-indigo-100 text-indigo-800',
  2: 'bg-violet-100 text-violet-800',
  3: 'bg-emerald-100 text-emerald-800',
  4: 'bg-teal-100 text-teal-800',
  5: 'bg-rose-100 text-rose-800',
  6: 'bg-slate-200 text-slate-700',
};

export const WARRANTY_STATUS_OPTIONS: readonly SelectOption<number>[] = Object.entries(
  WARRANTY_STATUS_LABELS,
).map(([value, label]) => ({ value: Number(value), label }));

/**
 * Sơ đồ chuyển trạng thái hợp lệ (Database/05_sp_warranty_returns_commission.sql):
 * 0→1,5 | 1→2,3,4,5 | 2→3,4,5 | 3,4,5→6. Cặp chuyển đổi sai vẫn do proc quyết định (50002 → 409).
 */
export const WARRANTY_TRANSITIONS: Readonly<Record<number, readonly number[]>> = {
  [WARRANTY_STATUS.Received]: [WARRANTY_STATUS.Diagnosing, WARRANTY_STATUS.Rejected],
  [WARRANTY_STATUS.Diagnosing]: [
    WARRANTY_STATUS.SentToVendor,
    WARRANTY_STATUS.Repaired,
    WARRANTY_STATUS.Replaced,
    WARRANTY_STATUS.Rejected,
  ],
  [WARRANTY_STATUS.SentToVendor]: [
    WARRANTY_STATUS.Repaired,
    WARRANTY_STATUS.Replaced,
    WARRANTY_STATUS.Rejected,
  ],
  [WARRANTY_STATUS.Repaired]: [WARRANTY_STATUS.ReturnedToCustomer],
  [WARRANTY_STATUS.Replaced]: [WARRANTY_STATUS.ReturnedToCustomer],
  [WARRANTY_STATUS.Rejected]: [WARRANTY_STATUS.ReturnedToCustomer],
};

/** Lựa chọn trạng thái đích theo trạng thái HIỆN TẠI của phiếu (rỗng = phiếu đã đóng). */
export function warrantyTargetOptions(currentStatus: number): readonly SelectOption<number>[] {
  const targets = WARRANTY_TRANSITIONS[currentStatus] ?? [];

  return targets.map((value) => ({ value, label: WARRANTY_STATUS_LABELS[value] ?? String(value) }));
}

