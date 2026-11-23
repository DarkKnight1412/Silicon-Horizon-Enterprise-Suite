/**
 * DTO + nhãn của module NHÀ CUNG CẤP, khớp `SiliconHorizon.Application.Features.Inventory.Dto.SupplierDtos.cs`
 * và `SuppliersController` / `SupplierReturnsController` (Database/README.md mục 6).
 *
 * Lưu ý quyền (đọc từ controller):
 * - GET `/suppliers` và `GET /suppliers/{id}`: Thủ kho + Quản lý + Admin.
 * - POST/PUT `/suppliers`: CHỈ Admin (dữ liệu chủ như sản phẩm).
 * - `POST /suppliers/{id}/payments` và `GET /suppliers/{id}/ledger`: Quản lý + Admin.
 * - `POST /supplier-returns`: Thủ kho + Quản lý + Admin.
 */

/** Nhà cung cấp (bảng cấu hình `dbo.Suppliers`). */
export interface SupplierDto {
  id: number;
  code: string;
  name: string;
  taxCode: string | null;
  phone: string | null;
  address: string | null;
  /** Số ngày được nợ — proc nhập kho dùng để tính `SupplierLedger.DueDate`. */
  paymentTermDays: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

/** Tham số lọc danh sách nhà cung cấp — `isActive: null` = lấy cả NCC đã ngừng hợp tác. */
export interface SupplierListQuery {
  keyword?: string | null;
  isActive?: boolean | null;
  pageNumber: number;
  pageSize: number;
}

/** POST /api/v1/suppliers — mã NCC duy nhất (trùng → 409). */
export interface CreateSupplierRequest {
  code: string;
  name: string;
  taxCode: string | null;
  phone: string | null;
  address: string | null;
  paymentTermDays: number;
}

/** PUT /api/v1/suppliers/{id} — KHÔNG đổi mã; `isActive: false` = ngừng hợp tác. */
export interface UpdateSupplierRequest {
  name: string;
  taxCode: string | null;
  phone: string | null;
  address: string | null;
  paymentTermDays: number;
  isActive: boolean;
}

/** Một dòng hàng trả NCC; serial phải đang ở trạng thái Hàng lỗi (7) tại chi nhánh trả. */
export interface SupplierReturnItemInput {
  productId: number;
  quantity: number;
  serials?: string[] | null;
}

/** POST /api/v1/supplier-returns — chi nhánh trả hàng ép từ token. */
export interface CreateSupplierReturnRequest {
  supplierId: number;
  items: SupplierReturnItemInput[];
  /** Số tiền ghi giảm công nợ phải trả (>= 0); 0 = không ghi giảm. */
  creditAmount: number;
}

/** Kết quả trả hàng NCC (`usp_SupplierReturn_Create`). */
export interface CreatedSupplierReturnDto {
  supplierReturnId: number;
  returnCode: string;
}

/** POST /api/v1/suppliers/{id}/payments — proc từ chối thanh toán vượt công nợ (409). */
export interface RecordSupplierPaymentRequest {
  /** VND, phải > 0. */
  amount: number;
  /** Phiếu nhập được thanh toán (tuỳ chọn) — nếu truyền, proc kiểm tra đúng nhà cung cấp. */
  purchaseReceiptId: number | null;
}

/** Kết quả ghi nhận thanh toán — công nợ còn lại sau khi trả. */
export interface SupplierPaymentResultDto {
  balanceAfter: number;
}

/** Một dòng sao kê công nợ phải trả (`usp_Supplier_GetLedger`). */
export interface SupplierLedgerEntryDto {
  id: number;
  /** 0 Payable (tăng nợ), 1 Payment, 2 ReturnCredit (giảm nợ). */
  entryType: number;
  entryTypeName: string;
  /** Dương = tăng nợ; âm = giảm nợ. */
  amount: number;
  dueDate: string | null;
  purchaseReceiptId: number | null;
  supplierReturnId: number | null;
  createdAt: string;
}

/** Tham số phân trang sao kê công nợ (proc tự cắt tối đa 200). */
export interface SupplierLedgerQuery {
  pageNumber: number;
  pageSize: number;
}

/** Sao kê công nợ phải trả — GET /api/v1/suppliers/{id}/ledger. */
export interface SupplierLedgerDto {
  supplierId: number;
  /** Tổng công nợ hiện tại (dương = còn nợ nhà cung cấp). */
  balance: number;
  entries: SupplierLedgerEntryDto[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

/** Loại bút toán công nợ NCC (SupplierLedger.EntryType). */
export const SUPPLIER_LEDGER_TYPE_LABELS: Readonly<Record<number, string>> = {
  0: 'Ghi tăng nợ phải trả',
  1: 'Thanh toán',
  2: 'Ghi giảm do trả hàng',
};

/** Nhãn trạng thái hợp tác của nhà cung cấp (dùng ở danh sách/chi tiết). */
export function supplierActiveLabel(isActive: boolean): string {
  return isActive ? 'Đang hợp tác' : 'Ngừng hợp tác';
}
