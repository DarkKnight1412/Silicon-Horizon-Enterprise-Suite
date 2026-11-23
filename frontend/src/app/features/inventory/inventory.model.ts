import type { SelectOption } from '../../core/models/common.model';

/**
 * DTO + nhãn tiếng Việt của nhóm KHO · SERIAL, khớp
 * `SiliconHorizon.Application.Features.Inventory.Dto`: StockDtos.cs, PurchaseReceiptDtos.cs,
 * StockTransferDtos.cs, StockTakeDtos.cs, SerialDtos.cs và bảng tên ở InventoryStatusText.cs.
 *
 * Quy ước backend (Database/README.md mục 6): API LUÔN ép `branchId` từ token nên request
 * KHÔNG có `branchId` — ngoại lệ duy nhất là `toBranchId` của phiếu chuyển kho (client chọn).
 */

// ── Tồn kho (GET /api/v1/stock, PUT /api/v1/stock/min) ──────────────────────

/** Một dòng tồn kho — `usp_Stock_GetLevels`. */
export interface StockLevelDto {
  branchId: number;
  branchName: string;
  productId: number;
  sku: string;
  productName: string;
  categoryId: number;
  /** Số đơn vị đang ở kho (với hàng serial: InStock + Allocated). */
  physicalQty: number;
  /** Đang giữ cho đơn chưa hoàn tất. */
  reservedQty: number;
  /** `physicalQty - reservedQty` (backend tính sẵn). */
  availableQty: number;
  /** Mức tồn tối thiểu hiệu lực; null = dùng `Products.MinStockDefault`. */
  minStock: number | null;
  isBelowMin: boolean;
}

/** Tham số lọc tồn kho — KHÔNG có `branchId` (API ép từ token). */
export interface StockLevelQuery {
  productId?: number | null;
  categoryId?: number | null;
  keyword?: string | null;
  /** true = chỉ liệt kê hàng có `availableQty` dưới mức tối thiểu (INV-02/UC-34). */
  belowMinOnly?: boolean;
  pageNumber: number;
  pageSize: number;
}

/** PUT /api/v1/stock/min — `minStock: null` = quay về mặc định của sản phẩm. */
export interface SetMinStockRequest {
  productId: number;
  minStock: number | null;
}

// ── Thẻ kho (GET /api/v1/stock/movements) ───────────────────────────────────

/** Một bút toán kho — `usp_Stock_GetMovements` (bảng append-only, không sửa/xóa được). */
export interface StockMovementDto {
  id: number;
  /** 0 Receipt … 9 SupplierReturn (xem MOVEMENT_TYPE_LABELS). */
  movementType: number;
  movementTypeName: string;
  physicalDelta: number;
  reservedDelta: number;
  /** Order, PurchaseReceipt, StockTransfer, StockTake, ReturnRequest, SerialItem… */
  refType: string;
  refId: number;
  note: string | null;
  createdAt: string;
  createdBy: number | null;
}

/** Tham số thẻ kho — proc bắt buộc có sản phẩm; chi nhánh ép từ token. */
export interface StockMovementQuery {
  productId: number;
  /** ISO UTC, chặn dưới. */
  fromDate?: string | null;
  /** ISO UTC, chặn trên KHÔNG bao gồm — gửi mốc đầu ngày kế tiếp. */
  toDate?: string | null;
  pageNumber: number;
  pageSize: number;
}

// ── Nhập kho (POST/GET /api/v1/purchase-receipts) ───────────────────────────

/** Một dòng hàng của phiếu nhập; `serials` bắt buộc với hàng quản lý serial và phải đủ `quantity`. */
export interface PurchaseReceiptItemInput {
  productId: number;
  quantity: number;
  /** Đơn giá nhập, VND. */
  unitCost: number;
  serials?: string[] | null;
}

/** POST /api/v1/purchase-receipts — không có `branchId` (chi nhánh nhập ép từ token). */
export interface CreatePurchaseReceiptRequest {
  supplierId: number;
  supplierInvoiceNo: string | null;
  items: PurchaseReceiptItemInput[];
}

/** Kết quả nhập kho — `usp_PurchaseReceipt_Create`. */
export interface CreatedPurchaseReceiptDto {
  receiptId: number;
  receiptCode: string;
  totalAmount: number;
}

/** Một dòng danh sách phiếu nhập — `usp_PurchaseReceipt_List`. */
export interface PurchaseReceiptListItemDto {
  id: number;
  receiptCode: string;
  supplierId: number;
  supplierName: string;
  branchId: number;
  /** 0 Nháp, 1 Hoàn tất, 2 Đã hủy. */
  status: number;
  statusName: string;
  totalAmount: number;
  receivedAt: string | null;
}

/** Tham số danh sách phiếu nhập — `toDate` là chặn trên KHÔNG bao gồm. */
export interface PurchaseReceiptListQuery {
  supplierId?: number | null;
  fromDate?: string | null;
  toDate?: string | null;
  pageNumber: number;
  pageSize: number;
}

/** Header phiếu nhập — result set 1 của `usp_PurchaseReceipt_Get`. */
export interface PurchaseReceiptHeaderDto {
  id: number;
  receiptCode: string;
  supplierId: number;
  supplierName: string;
  branchId: number;
  branchName: string;
  supplierInvoiceNo: string | null;
  status: number;
  statusName: string;
  totalAmount: number;
  receivedAt: string | null;
  createdAt: string;
  createdBy: number | null;
  updatedAt: string | null;
  updatedBy: number | null;
}

/** Dòng hàng phiếu nhập — result set 2. */
export interface PurchaseReceiptItemDto {
  id: number;
  productId: number;
  sku: string;
  name: string;
  quantity: number;
  unitCost: number;
  lineTotal: number;
}

/** Serial sinh ra từ phiếu nhập — result set 3. */
export interface PurchaseReceiptSerialDto {
  serialItemId: number;
  productId: number;
  serialNumber: string;
  status: number;
  statusName: string;
}

/** Chi tiết phiếu nhập — GET /api/v1/purchase-receipts/{id}. */
export interface PurchaseReceiptDetailDto {
  header: PurchaseReceiptHeaderDto;
  items: PurchaseReceiptItemDto[];
  serials: PurchaseReceiptSerialDto[];
}

// ── Chuyển kho (POST/GET /api/v1/stock-transfers, POST {id}/ship|receive|cancel) ──

/** Một dòng hàng của phiếu chuyển; hàng serial phải liệt kê đủ serial đang InStock ở chi nhánh đi. */
export interface StockTransferItemInput {
  productId: number;
  quantity: number;
  serials?: string[] | null;
}

/** POST /api/v1/stock-transfers — chi nhánh ĐI ép từ token, client chỉ chọn chi nhánh ĐẾN. */
export interface CreateStockTransferRequest {
  toBranchId: number;
  items: StockTransferItemInput[];
}

/** Kết quả tạo phiếu chuyển kho — `usp_StockTransfer_Create`. */
export interface CreatedStockTransferDto {
  transferId: number;
  transferCode: string;
}

/** Header phiếu chuyển — result set 1 của `usp_StockTransfer_Get`. */
export interface StockTransferHeaderDto {
  id: number;
  transferCode: string;
  fromBranchId: number;
  fromBranchName: string;
  toBranchId: number;
  toBranchName: string;
  /** 0 Nháp, 1 Đang chuyển, 2 Đã nhận, 3 Đã hủy. */
  status: number;
  statusName: string;
  createdAt: string;
  createdBy: number | null;
  updatedAt: string | null;
  updatedBy: number | null;
}

/** Dòng hàng phiếu chuyển — result set 2. */
export interface StockTransferItemDto {
  id: number;
  productId: number;
  sku: string;
  name: string;
  quantity: number;
}

/** Serial gắn vào phiếu chuyển — result set 3. */
export interface StockTransferSerialDto {
  transferItemId: number;
  serialItemId: number;
  serialNumber: string;
  status: number;
  statusName: string;
}

/** Chi tiết phiếu chuyển kho — GET /api/v1/stock-transfers/{id}. */
export interface StockTransferDetailDto {
  header: StockTransferHeaderDto;
  items: StockTransferItemDto[];
  serials: StockTransferSerialDto[];
}

// ── Kiểm kê (POST /api/v1/stock-takes, {id}/counts, {id}/approve) ───────────

/** Kết quả mở đợt kiểm kê — `usp_StockTake_Create` (chi nhánh ép từ token). */
export interface CreatedStockTakeDto {
  stockTakeId: number;
  stockTakeCode: string;
}

/** Một dòng số đếm gửi lên (`@CountsJson`). */
export interface StockTakeCountInput {
  productId: number;
  countedQty: number;
  reason: string | null;
}

/** POST /api/v1/stock-takes/{id}/counts — `finalize: true` = gửi duyệt (khóa nhập tiếp). */
export interface SubmitStockTakeCountsRequest {
  counts: StockTakeCountInput[];
  finalize: boolean;
}

/** Dòng kiểm kê sau khi nhập số đếm. */
export interface StockTakeCountDto {
  productId: number;
  sku: string;
  name: string;
  systemQty: number;
  countedQty: number;
  /** `countedQty - systemQty`. */
  diffQty: number;
  reason: string | null;
}

/** POST /api/v1/stock-takes/{id}/approve — `approve: false` = từ chối (không đổi tồn). */
export interface ApproveStockTakeRequest {
  approve: boolean;
  note: string | null;
}

/** Header đợt kiểm kê — result set 1 của `usp_StockTake_Get`. */
export interface StockTakeHeaderDto {
  id: number;
  stockTakeCode: string;
  branchId: number;
  /** 0 Nháp, 1 Đang kiểm kê, 2 Chờ duyệt, 3 Đã duyệt, 4 Đã từ chối. */
  status: number;
  statusName: string;
  approvedBy: number | null;
  approvedAt: string | null;
  createdAt: string;
  createdBy: number | null;
  updatedAt: string | null;
  updatedBy: number | null;
}

/** Dòng kiểm kê (kèm chênh lệch) — result set 2. */
export interface StockTakeItemDto {
  productId: number;
  sku: string;
  name: string;
  /** Hàng serial lệch KHÔNG duyệt được: phải xử lý từng serial rồi kiểm kê lại (INV-04). */
  requiresSerial: boolean;
  systemQty: number;
  countedQty: number;
  diffQty: number;
  reason: string | null;
}

/** Chi tiết đợt kiểm kê — GET /api/v1/stock-takes/{id}. */
export interface StockTakeDetailDto {
  header: StockTakeHeaderDto;
  items: StockTakeItemDto[];
}

// ── Serial (GET /api/v1/serials/{serialNumber}, POST /api/v1/serials/{id}/status) ──

/** Thông tin serial — result set 1 của `usp_Serial_Lookup`. */
export interface SerialInfoDto {
  serialItemId: number;
  serialNumber: string;
  /** 0 InStock … 9 Scrapped (xem SERIAL_STATUS_LABELS). */
  status: number;
  statusName: string;
  branchId: number;
  productId: number;
  sku: string;
  productName: string;
  soldAt: string | null;
  warrantyStartDate: string | null;
  warrantyEndDate: string | null;
  /** Số ngày còn lại (âm = đã hết hạn). */
  warrantyDaysLeft: number | null;
  inWarranty: boolean;
  orderId: number | null;
  orderCode: string | null;
  purchaseDate: string | null;
  customerId: number | null;
  customerName: string | null;
  customerPhone: string | null;
  replacedBySerialId: number | null;
}

/** Một lần đổi trạng thái serial — result set 2 (bảng append-only). */
export interface SerialStatusHistoryDto {
  fromStatus: number | null;
  fromStatusName: string | null;
  toStatus: number;
  toStatusName: string;
  refType: string | null;
  refId: number | null;
  note: string | null;
  changedAt: string;
}

/** Phiếu bảo hành gắn với serial — result set 3. */
export interface SerialWarrantyTicketDto {
  id: number;
  ticketCode: string;
  status: number;
  issueDescription: string | null;
  receivedAt: string | null;
  returnedAt: string | null;
}

/** Kết quả tra cứu serial (3 result set của `usp_Serial_Lookup`). */
export interface SerialLookupDto {
  serial: SerialInfoDto;
  statusHistory: SerialStatusHistoryDto[];
  warrantyTickets: SerialWarrantyTicketDto[];
}

/** POST /api/v1/serials/{id}/status — bắt buộc có lý do (proc ghi nhật ký AUD-01). */
export interface ChangeSerialStatusRequest {
  /** Chỉ nhận 0 (Trong kho), 7 (Hàng lỗi), 9 (Đã hủy). */
  toStatus: number;
  reason: string;
}

// ── Nhãn + lớp badge tiếng Việt (khớp InventoryStatusText.cs) ───────────────

/** Trạng thái phiếu nhập (PurchaseReceipts.Status). */
export const RECEIPT_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Nháp',
  1: 'Hoàn tất',
  2: 'Đã hủy',
};

export const RECEIPT_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-slate-200 text-slate-700',
  1: 'bg-emerald-100 text-emerald-800',
  2: 'bg-rose-100 text-rose-800',
};

/** Loại bút toán kho (StockMovements.MovementType). */
export const MOVEMENT_TYPE_LABELS: Readonly<Record<number, string>> = {
  0: 'Nhập kho',
  1: 'Giữ hàng cho đơn',
  2: 'Nhả hàng giữ',
  3: 'Xuất bán',
  4: 'Xuất chuyển kho',
  5: 'Nhập chuyển kho',
  6: 'Điều chỉnh kiểm kê',
  7: 'Điều chỉnh thủ công',
  8: 'Khách trả hàng',
  9: 'Trả nhà cung cấp',
};

/** Trạng thái phiếu chuyển kho (StockTransfers.Status). */
export const TRANSFER_STATUS = {
  Draft: 0,
  InTransit: 1,
  Received: 2,
  Cancelled: 3,
} as const;

export const TRANSFER_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Nháp',
  1: 'Đang chuyển',
  2: 'Đã nhận',
  3: 'Đã hủy',
};

export const TRANSFER_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-slate-200 text-slate-700',
  1: 'bg-amber-100 text-amber-800',
  2: 'bg-emerald-100 text-emerald-800',
  3: 'bg-rose-100 text-rose-800',
};

/** Trạng thái đợt kiểm kê (StockTakes.Status). */
export const STOCK_TAKE_STATUS = {
  Draft: 0,
  Counting: 1,
  PendingApproval: 2,
  Approved: 3,
  Rejected: 4,
} as const;

export const STOCK_TAKE_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Nháp',
  1: 'Đang kiểm kê',
  2: 'Chờ duyệt',
  3: 'Đã duyệt',
  4: 'Đã từ chối',
};

export const STOCK_TAKE_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-slate-200 text-slate-700',
  1: 'bg-sky-100 text-sky-800',
  2: 'bg-amber-100 text-amber-800',
  3: 'bg-emerald-100 text-emerald-800',
  4: 'bg-rose-100 text-rose-800',
};

/** Trạng thái serial (SerialItems.Status). */
export const SERIAL_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Trong kho',
  1: 'Đã giữ cho đơn',
  2: 'Đang chuyển kho',
  3: 'Đã bán',
  4: 'Đang bảo hành',
  5: 'Khách đã trả',
  6: 'Đã đổi mới',
  7: 'Hàng lỗi',
  8: 'Đã trả nhà cung cấp',
  9: 'Đã hủy',
};

export const SERIAL_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-emerald-100 text-emerald-800',
  1: 'bg-sky-100 text-sky-800',
  2: 'bg-amber-100 text-amber-800',
  3: 'bg-indigo-100 text-indigo-800',
  4: 'bg-violet-100 text-violet-800',
  5: 'bg-orange-100 text-orange-800',
  6: 'bg-teal-100 text-teal-800',
  7: 'bg-rose-100 text-rose-800',
  8: 'bg-slate-300 text-slate-800',
  9: 'bg-slate-200 text-slate-700',
};

export const SERIAL_STATUS_IN_STOCK = 0;
export const SERIAL_STATUS_DEFECTIVE = 7;
export const SERIAL_STATUS_SCRAPPED = 9;

/**
 * Ba chuyển đổi DUY NHẤT mà `usp_Serial_ChangeStatus` chấp nhận:
 * InStock(0) → Defective(7), Defective(7) → Scrapped(9), Defective(7) → InStock(0).
 */
export const SERIAL_MANUAL_TARGETS: Readonly<Record<number, readonly number[]>> = {
  [SERIAL_STATUS_IN_STOCK]: [SERIAL_STATUS_DEFECTIVE],
  [SERIAL_STATUS_DEFECTIVE]: [SERIAL_STATUS_IN_STOCK, SERIAL_STATUS_SCRAPPED],
};

/** Lựa chọn trạng thái đích theo trạng thái hiện tại của serial. */
export function serialStatusTargetOptions(currentStatus: number): readonly SelectOption<number>[] {
  const targets = SERIAL_MANUAL_TARGETS[currentStatus] ?? [];

  return targets.map((value) => ({ value, label: SERIAL_STATUS_LABELS[value] ?? String(value) }));
}

/** true khi còn ít nhất một trạng thái đích hợp lệ (dùng để ẩn form đổi trạng thái). */
export function canChangeSerialStatus(currentStatus: number): boolean {
  return serialStatusTargetOptions(currentStatus).length > 0;
}



