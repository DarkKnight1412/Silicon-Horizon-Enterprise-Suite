import type { SelectOption } from '../../core/models/common.model';
import { PAYMENT_METHOD_LABELS } from '../../core/models/order.model';

/**
 * DTO riêng của nhóm ĐƠN HÀNG NHÂN VIÊN + POS, khớp
 * `SiliconHorizon.Application.Features.Orders.Dto.OrderDtos.cs` (`CreatePosOrderRequest`) và
 * `Customers/Dto/CustomerDtos.cs` (tra cứu khách cho POS) — Database/README.md mục 5.2.
 *
 * Lưu ý quan trọng (đọc từ `PosOrdersController`):
 * - Body POST /api/v1/pos/orders KHÔNG có `branchId`: API ép `@BranchId` và `@UserId` từ token, và
 *   đơn tại quầy luôn dùng `@Channel = 1` cùng danh sách sản phẩm truyền trực tiếp (`items`).
 * - `customerId` là BẮT BUỘC: đơn tại quầy luôn gắn với một hồ sơ khách (khách vãng lai = bản ghi
 *   `Customers` có `UserId = NULL`). Backend chưa có endpoint tra cứu khách cho thu ngân nên khi
 *   người dùng KHÔNG phải Admin, màn POS cho nhập trực tiếp mã khách.
 * - Không truyền `salesStaff` thì proc tự gán nhân viên tạo đơn 100% (KPI-02); nếu truyền thì tổng
 *   `shareRate` bắt buộc bằng 100, sai → 400.
 */

/** Một dòng sản phẩm gửi qua `@ItemsJson` (camelCase). */
export interface PosOrderItemInput {
  productId: number;
  quantity: number;
}

/** Một phần chia doanh số gửi qua `@SalesStaffJson` (camelCase). */
export interface PosSalesStaffInput {
  employeeId: number;
  shareRate: number;
}

/** POST /api/v1/pos/orders — khớp `CreatePosOrderRequest`. */
export interface CreatePosOrderRequest {
  /** Khách được phục vụ tại quầy (bắt buộc). */
  customerId: number;
  /** 0 Giao tận nơi, 1 Nhận tại cửa hàng (mặc định 1 — bán tại quầy). */
  fulfillmentType: number;
  recipientName: string | null;
  recipientPhone: string | null;
  shippingAddress: string | null;
  shippingFee: number;
  /** 0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS, 4 Công nợ. */
  paymentMethod: number;
  voucherCode: string | null;
  note: string | null;
  /** Bắt buộc có ít nhất một dòng. */
  items: PosOrderItemInput[];
  /** `null` = gán 100% cho nhân viên tạo đơn; nếu truyền thì tổng phải bằng 100. */
  salesStaff: PosSalesStaffInput[] | null;
}

/**
 * Khách hàng dùng trong màn POS (rút gọn của `CustomerDto`).
 * `GET /api/v1/customers` chỉ Admin gọi được (`CustomersController` = `[Authorize(Roles = Admin)]`).
 */
export interface PosCustomerDto {
  id: number;
  /** `null` = khách vãng lai (chưa có tài khoản đăng nhập). */
  userId: number | null;
  fullName: string;
  phone: string;
  email: string | null;
  /** Hạn mức công nợ (dùng khi thanh toán bằng Công nợ). */
  creditLimit: number;
  /** Số ngày được nợ. */
  paymentTermDays: number;
  isActive: boolean;
}

/** Tham số tra cứu khách cho POS (chỉ Admin). */
export interface PosCustomerQuery {
  keyword?: string | null;
  pageNumber: number;
  pageSize: number;
}

/**
 * Nhân viên dùng cho ô "chia hoa hồng" của đơn tại quầy (rút gọn `EmployeeDto`).
 * `GET /api/v1/admin/employees` chỉ Admin gọi được — vai trò khác nhập trực tiếp `employeeId`.
 */
export interface PosEmployeeDto {
  id: number;
  employeeCode: string;
  fullName: string;
  branchId: number;
  /** 0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager. */
  position: number;
  positionName: string;
  /** 0 Active, 1 Terminated. */
  status: number;
  statusName: string;
}

/** Tổng tỷ lệ chia doanh số bắt buộc của một đơn (proc kiểm tra: sai → 400). */
export const POS_SHARE_RATE_TOTAL = 100;

/** Hình thức nhận hàng của đơn tại quầy (mặc định Nhận tại cửa hàng). */
export const POS_FULFILLMENT_OPTIONS: readonly SelectOption<number>[] = [
  { value: 1, label: 'Nhận tại cửa hàng' },
  { value: 0, label: 'Giao tận nơi' },
];

/** Phương thức thanh toán tại quầy — POS dùng được cả 5 phương thức (kể cả Tiền mặt/POS). */
export const POS_PAYMENT_METHOD_OPTIONS: readonly SelectOption<number>[] = Object.entries(
  PAYMENT_METHOD_LABELS,
).map(([value, label]) => ({ value: Number(value), label }));

/** Tổng tỷ lệ chia doanh số của các dòng đã nhập (để chặn sớm trước khi gọi API). */
export function totalShareRate(rows: readonly PosSalesStaffInput[]): number {
  return rows.reduce((sum, row) => sum + (Number.isFinite(row.shareRate) ? row.shareRate : 0), 0);
}
