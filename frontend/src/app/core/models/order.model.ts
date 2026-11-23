import type { SelectOption } from './common.model';

/**
 * DTO đơn hàng — khớp `Features/Orders/Dto/OrderDtos.cs` và Database/README.md mục 5.
 * Quy ước backend: API luôn ép customerId (khách) / branchId (nhân viên) từ token.
 */

/** POST /api/v1/orders — khách đặt hàng từ các dòng đã tick trong giỏ (Channel = Online). */
export interface CreateOrderRequest {
  /** Chi nhánh xuất hàng / chi nhánh khách đến nhận (hiện chưa có proc tự chọn chi nhánh nên client phải gửi). */
  branchId: number;
  /** 0 Giao tận nơi, 1 Nhận tại cửa hàng. */
  fulfillmentType: number;
  recipientName: string | null;
  recipientPhone: string | null;
  shippingAddress: string | null;
  shippingFee: number;
  /** 0 COD, 1 Chuyển khoản, 2 Cổng thanh toán, 3 Tiền mặt/POS, 4 Công nợ. */
  paymentMethod: number;
  voucherCode: string | null;
  note: string | null;
}

/** Đơn vừa tạo (result set của usp_Order_Create). */
export interface CreatedOrderDto {
  orderId: number;
  orderCode: string;
  status: number;
  grandTotal: number;
  expiresAt: string | null;
}

/** Một dòng hàng của đơn (snapshot giá tại thời điểm đặt). */
export interface OrderItemDto {
  id: number;
  productId: number;
  sku: string;
  productName: string;
  unitPrice: number;
  discountAmount: number;
  quantity: number;
  lineTotal: number;
  warrantyMonths: number;
  requiresSerial: boolean;
  promotionId: number | null;
  serialAssignedCount: number;
}

/** Serial đã gán vào đơn. */
export interface OrderSerialDto {
  serialItemId: number;
  orderItemId: number;
  serialNumber: string;
  status: number;
  warrantyStartDate: string | null;
  warrantyEndDate: string | null;
}

/** Một giao dịch thanh toán/hoàn tiền của đơn. */
export interface OrderPaymentDto {
  id: number;
  /** 0 Payment (thu), 1 Refund (hoàn). */
  paymentType: number;
  method: number;
  amount: number;
  status: number;
  provider: string | null;
  providerTransactionId: string | null;
  parentPaymentId: number | null;
  returnRequestId: number | null;
  paidAt: string | null;
  createdAt: string;
  createdBy: number | null;
}

/** Một lần chuyển trạng thái của đơn. */
export interface OrderStatusHistoryDto {
  fromStatus: number | null;
  toStatus: number;
  reason: string | null;
  /** null khi do hệ thống (job hủy đơn quá hạn) thực hiện. */
  changedBy: number | null;
  changedAt: string;
}

/** Nhân viên tư vấn của đơn và tỷ lệ chia doanh số. */
export interface OrderSalesStaffDto {
  employeeId: number;
  fullName: string;
  shareRate: number;
  isPrimary: boolean;
}

/** Header đơn (result set 1 của usp_Order_Get). */
export interface OrderHeaderDto {
  id: number;
  orderCode: string;
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  branchId: number;
  branchName: string;
  /** 0 Online, 1 POS. */
  channel: number;
  fulfillmentType: number;
  /** 0..10 (xem ORDER_STATUS_LABELS). */
  status: number;
  recipientName: string | null;
  recipientPhone: string | null;
  shippingAddress: string | null;
  subTotal: number;
  discountTotal: number;
  shippingFee: number;
  grandTotal: number;
  paidAmount: number;
  paymentMethod: number;
  /** 0 Unpaid, 1 PartiallyPaid, 2 Paid, 3 Refunded. */
  paymentStatus: number;
  voucherId: number | null;
  expiresAt: string | null;
  completedAt: string | null;
  note: string | null;
  createdAt: string;
  createdBy: number | null;
  updatedAt: string | null;
  updatedBy: number | null;
}

/** Chi tiết đơn — gom 6 result set của usp_Order_Get. */
export interface OrderDetailDto {
  order: OrderHeaderDto;
  items: OrderItemDto[];
  serials: OrderSerialDto[];
  payments: OrderPaymentDto[];
  statusHistory: OrderStatusHistoryDto[];
  salesStaff: OrderSalesStaffDto[];
}

/** Một dòng trong danh sách đơn (usp_Order_List). */
export interface OrderListItemDto {
  id: number;
  orderCode: string;
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  branchId: number;
  channel: number;
  fulfillmentType: number;
  status: number;
  grandTotal: number;
  paidAmount: number;
  paymentMethod: number;
  paymentStatus: number;
  createdAt: string;
  expiresAt: string | null;
}

/** Tham số GET /api/v1/orders (OrderListRequest) — không có customerId/branchId vì API ép từ token. */
export interface OrderListQuery {
  status?: number | null;
  fromDate?: string | null;
  toDate?: string | null;
  keyword?: string | null;
  pageNumber: number;
  pageSize: number;
}

/** POST /api/v1/orders/{id}/cancel và /cancel-approved — bắt buộc lý do. */
export interface CancelOrderRequest {
  reason: string;
}

/** POST /api/v1/orders/{id}/delivery-failed — bắt buộc lý do. */
export interface DeliveryFailedRequest {
  reason: string;
}

/** Kết quả hủy đơn (usp_Order_Cancel). */
export interface OrderCancelResultDto {
  orderId: number;
  /** 8 = Cancelled. */
  status: number;
  paidAmount: number;
  /** true = đơn đã thu tiền, phải hoàn qua POST /api/v1/orders/{id}/refunds. */
  refundRequired: boolean;
}

/** Trạng thái đơn 0..10 (BRD mục 6.4). */
export const ORDER_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Chờ thanh toán',
  1: 'Chờ xác nhận',
  2: 'Đã xác nhận',
  3: 'Đang đóng gói',
  4: 'Chờ khách nhận',
  5: 'Đang giao',
  6: 'Giao thất bại',
  7: 'Hoàn tất',
  8: 'Đã hủy',
  9: 'Đổi trả một phần',
  10: 'Đã đổi trả toàn bộ',
};

/** Lớp Tailwind cho badge trạng thái đơn (đọc ở template bằng `statusClass(...)`). */
export const ORDER_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-amber-100 text-amber-800',
  1: 'bg-sky-100 text-sky-800',
  2: 'bg-indigo-100 text-indigo-800',
  3: 'bg-violet-100 text-violet-800',
  4: 'bg-teal-100 text-teal-800',
  5: 'bg-cyan-100 text-cyan-800',
  6: 'bg-rose-100 text-rose-800',
  7: 'bg-emerald-100 text-emerald-800',
  8: 'bg-slate-200 text-slate-700',
  9: 'bg-orange-100 text-orange-800',
  10: 'bg-orange-200 text-orange-900',
};

export const ORDER_STATUS_OPTIONS: readonly SelectOption<number>[] = Object.entries(ORDER_STATUS_LABELS).map(
  ([value, label]) => ({ value: Number(value), label }),
);

/** Phương thức thanh toán (Orders.PaymentMethod). */
export const PAYMENT_METHOD_LABELS: Readonly<Record<number, string>> = {
  0: 'COD (thu khi giao)',
  1: 'Chuyển khoản',
  2: 'Cổng thanh toán',
  3: 'Tiền mặt / POS',
  4: 'Công nợ',
};

/** Trạng thái thanh toán (Orders.PaymentStatus). */
export const PAYMENT_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Chưa thanh toán',
  1: 'Thanh toán một phần',
  2: 'Đã thanh toán',
  3: 'Đã hoàn tiền',
};

/** Hình thức nhận hàng (Orders.FulfillmentType). */
export const FULFILLMENT_TYPE_LABELS: Readonly<Record<number, string>> = {
  0: 'Giao tận nơi',
  1: 'Nhận tại cửa hàng',
};

/** Kênh bán (Orders.Channel). */
export const ORDER_CHANNEL_LABELS: Readonly<Record<number, string>> = {
  0: 'Online',
  1: 'Tại quầy (POS)',
};

/** Trạng thái serial (SerialItems.Status). */
export const SERIAL_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Trong kho',
  1: 'Đã giữ cho đơn',
  2: 'Đang bảo hành',
  3: 'Đã bán',
  4: 'Lỗi / chờ xử lý',
  5: 'Đã trả nhà cung cấp',
};

/** Khách chỉ tự hủy được đơn ở PendingPayment (0) hoặc Pending (1) — OrdersController.Cancel. */
export function canCustomerCancel(status: number): boolean {
  return status === 0 || status === 1;
}

/** Nhân viên hủy được từ Pending (1) trở đi tới DeliveryFailed (6) — OrdersController.CancelApproved. */
export function canStaffCancel(status: number): boolean {
  return status >= 1 && status <= 6;
}
