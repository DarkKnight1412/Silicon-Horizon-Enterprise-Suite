import type { SelectOption } from '../../core/models/common.model';

/**
 * DTO + nhãn của module ĐỔI TRẢ, khớp
 * `SiliconHorizon.Application.Features.Returns.Dto` (ReturnDtos.cs, ReturnStatusText.cs)
 * — Database/README.md mục 7.2, RET-01..05.
 *
 * Quy ước phạm vi: khách ép `customerId`, nhân viên ép `branchId` từ token — request KHÔNG có hai
 * trường này. `ReturnRequests.Status`: 0 Requested, 1 Approved, 2 Rejected, 3 GoodsReceived,
 * 4 Refunded, 5 Closed.
 */

/** Một dòng danh sách yêu cầu đổi trả (`usp_Return_List`). */
export interface ReturnListItemDto {
  id: number;
  returnCode: string;
  orderId: number;
  orderCode: string;
  customerId: number;
  branchId: number;
  status: number;
  statusName: string;
  /** Tiền hoàn dự kiến (chưa phải số đã hoàn). */
  refundAmount: number;
  createdAt: string;
}

/** Yêu cầu đổi trả (`usp_Return_Get`). */
export interface ReturnRequestDto {
  id: number;
  returnCode: string;
  orderId: number;
  orderCode: string;
  customerId: number;
  branchId: number;
  status: number;
  statusName: string;
  reason: string;
  refundAmount: number;
  approvedBy: number | null;
  approvedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

/** Một dòng hàng trả kèm kết quả kiểm tra. */
export interface ReturnItemDto {
  id: number;
  orderItemId: number;
  sku: string;
  productName: string;
  serialItemId: number | null;
  serialNumber: string | null;
  quantity: number;
  /** 0 Chưa kiểm tra, 1 Đạt, 2 Không đạt (xem INSPECTION_RESULT_LABELS). */
  inspectionResult: number;
  inspectionResultName: string;
}

/** Chi tiết yêu cầu đổi trả — GET /api/v1/returns/{id}. */
export interface ReturnRequestDetailDto {
  request: ReturnRequestDto;
  items: ReturnItemDto[];
}

/** Kết quả hoàn tiền — `usp_Return_Refund`. */
export interface ReturnRefundResultDto {
  returnRequestId: number;
  refundAmount: number;
  /** Phần hoàn bằng tiền mặt/chuyển khoản (không vượt quá số đã thu của đơn). */
  cashRefunded: number;
  /** Phần giảm trừ vào công nợ — chỉ khác 0 với đơn mua chịu (method = 4). */
  debtReduced: number;
  /** 9 Trả một phần, 10 Đã trả hết. */
  orderStatus: number;
  orderStatusName: string;
  fullyReturned: boolean;
}

/** POST /api/v1/returns/{id}/review — QL/AD duyệt hoặc từ chối (từ chối BẮT BUỘC có lý do). */
export interface ReviewReturnRequest {
  approve: boolean;
  note: string | null;
}

/** Kết quả kiểm tra một dòng hàng trả. */
export interface InspectReturnItemInput {
  /** Id dòng hàng trả (`ReturnItems.Id` từ GET /api/v1/returns/{id}). */
  returnItemId: number;
  /** 1 = đạt (serial về Trong kho, cộng tồn), 2 = không đạt (serial về Hàng lỗi). */
  result: number;
}

/** POST /api/v1/returns/{id}/inspect — phải nhập kết quả cho TẤT CẢ dòng của yêu cầu. */
export interface InspectReturnRequest {
  results: InspectReturnItemInput[];
}

/** POST /api/v1/returns/{id}/refund — hoàn tiền và thu hồi hoa hồng theo tỷ lệ. */
export interface RefundReturnRequest {
  /** 0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS, 4 Công nợ. */
  method: number;
  /** Bỏ trống (null) ⇒ hoàn đúng số dự kiến của yêu cầu. */
  amount: number | null;
}

/** Tham số danh sách — không có `branchId`/`customerId` (API ép từ token). */
export interface ReturnListQuery {
  status?: number | null;
  pageNumber: number;
  pageSize: number;
}

/** Trạng thái yêu cầu đổi trả (ReturnRequests.Status). */
export const RETURN_STATUS = {
  Requested: 0,
  Approved: 1,
  Rejected: 2,
  GoodsReceived: 3,
  Refunded: 4,
  Closed: 5,
} as const;

export const RETURN_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Chờ duyệt',
  1: 'Đã duyệt',
  2: 'Đã từ chối',
  3: 'Đã nhận hàng & kiểm tra',
  4: 'Đã hoàn tiền',
  5: 'Đã đóng',
};

export const RETURN_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-amber-100 text-amber-800',
  1: 'bg-sky-100 text-sky-800',
  2: 'bg-rose-100 text-rose-800',
  3: 'bg-indigo-100 text-indigo-800',
  4: 'bg-emerald-100 text-emerald-800',
  5: 'bg-slate-200 text-slate-700',
};

export const RETURN_STATUS_OPTIONS: readonly SelectOption<number>[] = Object.entries(
  RETURN_STATUS_LABELS,
).map(([value, label]) => ({ value: Number(value), label }));

/** Kết quả kiểm tra hàng trả (ReturnItems.InspectionResult). */
export const INSPECTION_RESULT = {
  Pending: 0,
  Pass: 1,
  Fail: 2,
} as const;

export const INSPECTION_RESULT_LABELS: Readonly<Record<number, string>> = {
  0: 'Chưa kiểm tra',
  1: 'Đạt',
  2: 'Không đạt',
};

export const INSPECTION_RESULT_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-slate-200 text-slate-700',
  1: 'bg-emerald-100 text-emerald-800',
  2: 'bg-rose-100 text-rose-800',
};

/** Lựa chọn kết quả kiểm tra (chỉ 2 giá trị gửi lên được). */
export const INSPECTION_RESULT_OPTIONS: readonly SelectOption<number>[] = [
  { value: INSPECTION_RESULT.Pass, label: 'Đạt — serial về Trong kho' },
  { value: INSPECTION_RESULT.Fail, label: 'Không đạt — serial về Hàng lỗi' },
];
