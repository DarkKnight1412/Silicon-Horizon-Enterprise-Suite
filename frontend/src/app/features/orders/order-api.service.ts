import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { PagedResult } from '../../core/models/paged-result.model';
import type {
  CancelOrderRequest,
  CreateOrderRequest,
  CreatedOrderDto,
  DeliveryFailedRequest,
  OrderCancelResultDto,
  OrderDetailDto,
  OrderListItemDto,
  OrderListQuery,
} from '../../core/models/order.model';
import type { ValidateVoucherRequest, VoucherValidationDto } from '../../core/models/voucher.model';

/**
 * Lời gọi HTTP của module đơn hàng + kiểm tra voucher (Database/README.md mục 5).
 *
 * Lưu ý quyền:
 * - `listMyOrders` và `createOrder` chỉ dùng được với vai trò **Customer** (backend: [Authorize(Roles="Customer")]).
 * - Các bước xử lý đơn (confirm/start-packing/ready/complete/cancel-approved) dành cho nhân viên,
 *   backend ép `BranchId` từ token nên client KHÔNG gửi chi nhánh.
 */
@Injectable({ providedIn: 'root' })
export class OrderApi {
  private readonly api = inject(ApiService);

  /** POST /api/v1/orders — đặt hàng từ các dòng đã tick trong giỏ. */
  createOrder(request: CreateOrderRequest): Observable<CreatedOrderDto> {
    return this.api.post<CreatedOrderDto>('/v1/orders', request, { silent: true });
  }

  /** GET /api/v1/orders — danh sách đơn của chính khách đang đăng nhập. */
  listMyOrders(query: OrderListQuery): Observable<PagedResult<OrderListItemDto>> {
    return this.api.get<PagedResult<OrderListItemDto>>('/v1/orders', {
      params: {
        status: query.status ?? null,
        fromDate: query.fromDate ?? null,
        toDate: query.toDate ?? null,
        keyword: query.keyword ?? null,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /** GET /api/v1/orders/{id} — chi tiết đơn (6 result set). */
  getOrder(id: number): Observable<OrderDetailDto> {
    return this.api.get<OrderDetailDto>(`/v1/orders/${id}`);
  }

  /** POST /api/v1/orders/{id}/cancel — khách tự hủy đơn ở trạng thái Chờ thanh toán / Chờ xác nhận. */
  cancelOrder(id: number, request: CancelOrderRequest): Observable<OrderCancelResultDto> {
    return this.api.post<OrderCancelResultDto>(`/v1/orders/${id}/cancel`, request, { silent: true });
  }

  /** POST /api/v1/orders/{id}/confirm — Pending → Confirmed (Thu ngân/Tư vấn/Quản lý/Admin). */
  confirmOrder(id: number): Observable<void> {
    return this.api.post<void>(`/v1/orders/${id}/confirm`);
  }

  /** POST /api/v1/orders/{id}/start-packing — Confirmed → Packing (Thủ kho/Admin). */
  startPacking(id: number): Observable<void> {
    return this.api.post<void>(`/v1/orders/${id}/start-packing`);
  }

  /** POST /api/v1/orders/{id}/ready — đóng gói xong (cần đủ serial cho hàng quản lý serial). */
  markReady(id: number): Observable<void> {
    return this.api.post<void>(`/v1/orders/${id}/ready`);
  }

  /** POST /api/v1/orders/{id}/complete — giao thành công / khách nhận hàng. */
  complete(id: number): Observable<void> {
    return this.api.post<void>(`/v1/orders/${id}/complete`);
  }

  /** POST /api/v1/orders/{id}/delivery-failed — ghi nhận giao thất bại (bắt buộc lý do). */
  markDeliveryFailed(id: number, request: DeliveryFailedRequest): Observable<void> {
    return this.api.post<void>(`/v1/orders/${id}/delivery-failed`, request);
  }

  /** POST /api/v1/orders/{id}/retry-delivery — giao lại. */
  retryDelivery(id: number): Observable<void> {
    return this.api.post<void>(`/v1/orders/${id}/retry-delivery`);
  }

  /** POST /api/v1/orders/{id}/cancel-approved — Quản lý/Admin hủy đơn đã xác nhận (bắt buộc lý do). */
  cancelApproved(id: number, request: CancelOrderRequest): Observable<OrderCancelResultDto> {
    return this.api.post<OrderCancelResultDto>(`/v1/orders/${id}/cancel-approved`, request, {
      silent: true,
    });
  }

  /**
   * POST /api/v1/vouchers/validate — kiểm tra mã giảm giá trước khi đặt.
   * Đặt `silent: true` vì mã sai vẫn trả 200 với `isValid = false`; lỗi khác do errorInterceptor xử lý.
   */
  validateVoucher(request: ValidateVoucherRequest): Observable<VoucherValidationDto> {
    return this.api.post<VoucherValidationDto>('/v1/vouchers/validate', request, { silent: true });
  }
}
