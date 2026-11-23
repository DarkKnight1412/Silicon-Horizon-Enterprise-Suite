import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type {
  CustomerLedgerDto,
  CustomerLedgerQuery,
  RecordedPaymentDto,
  RecordPaymentRequest,
  RefundRequest,
  RefundResultDto,
} from './payment.model';

/**
 * Lời gọi HTTP của module THANH TOÁN / CÔNG NỢ (Database/README.md mục 5.3).
 *
 * Quyền (đọc từ `OrdersController` / `CustomerLedgerController` + `OrderRoleGroups.cs`):
 * - `recordPayment`: TN, TV, QL, AD (`OrderRoleGroups.SalesCounter`).
 * - `refund`: QL, AD (`OrderRoleGroups.Approval`) — chỉ đơn Cancelled/DeliveryFailed.
 * - `getCustomerLedger`: khách (sao kê của mình), QL, AD (`OrderRoleGroups.LedgerViewers`).
 *
 * Mọi lời gọi đặt `silent: true` để màn hình tự hiển thị lỗi theo ngữ cảnh (ví dụ: phương thức
 * thanh toán không hợp lệ với trạng thái đơn) thay vì toast lỗi chung của interceptor.
 */
@Injectable({ providedIn: 'root' })
export class PaymentApi {
  private readonly api = inject(ApiService);

  /** POST /api/v1/orders/{id}/payments — thu ngân xác nhận đã thu tiền. */
  recordPayment(orderId: number, request: RecordPaymentRequest): Observable<RecordedPaymentDto> {
    return this.api.post<RecordedPaymentDto>(`/v1/orders/${orderId}/payments`, request, {
      silent: true,
    });
  }

  /** POST /api/v1/orders/{id}/refunds — hoàn tiền thủ công (QL/AD). */
  refund(orderId: number, request: RefundRequest): Observable<RefundResultDto> {
    return this.api.post<RefundResultDto>(`/v1/orders/${orderId}/refunds`, request, {
      silent: true,
    });
  }

  /** GET /api/v1/customers/{id}/ledger — sao kê công nợ khách (số dư + bút toán, có phân trang). */
  getCustomerLedger(
    customerId: number,
    query: CustomerLedgerQuery,
  ): Observable<CustomerLedgerDto> {
    return this.api.get<CustomerLedgerDto>(`/v1/customers/${customerId}/ledger`, {
      params: { pageNumber: query.pageNumber, pageSize: query.pageSize },
    });
  }
}
