import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { PagedResult } from '../../core/models/paged-result.model';
import type {
  InspectReturnRequest,
  RefundReturnRequest,
  ReturnListItemDto,
  ReturnListQuery,
  ReturnRefundResultDto,
  ReturnRequestDetailDto,
  ReviewReturnRequest,
} from './return.model';

/**
 * Lời gọi HTTP của module ĐỔI TRẢ (Database/README.md mục 7.2, RET-01..05).
 *
 * Quyền (đọc từ `ReturnsController` + `ReturnsRoles.cs`):
 * - `listReturns` / `getReturn`: mọi nhân viên + khách (yêu cầu của mình) + Admin.
 * - `review` và `refund`: Quản lý + Admin (`ReturnsRoles.Review/Refund`).
 * - `inspect`: Thủ kho + Admin.
 *
 * Khách tạo yêu cầu bằng `POST /api/v1/orders/{id}/returns` (nằm ở luồng đơn hàng, không thuộc màn
 * quản trị này) nên service không có hàm `createReturn`.
 */
@Injectable({ providedIn: 'root' })
export class ReturnApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/returns */
  listReturns(query: ReturnListQuery): Observable<PagedResult<ReturnListItemDto>> {
    return this.api.get<PagedResult<ReturnListItemDto>>('/v1/returns', {
      params: {
        status: query.status ?? null,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /** GET /api/v1/returns/{id} — yêu cầu + các dòng hàng trả. */
  getReturn(id: number): Observable<ReturnRequestDetailDto> {
    return this.api.get<ReturnRequestDetailDto>(`/v1/returns/${id}`);
  }

  /** POST /api/v1/returns/{id}/review — QL/AD duyệt/từ chối (chỉ khi yêu cầu đang Chờ duyệt). */
  review(id: number, request: ReviewReturnRequest): Observable<void> {
    return this.api.post<void>(`/v1/returns/${id}/review`, request, { silent: true });
  }

  /** POST /api/v1/returns/{id}/inspect — TK/AD kiểm tra TẤT CẢ dòng (chỉ khi đã Duyệt). */
  inspect(id: number, request: InspectReturnRequest): Observable<void> {
    return this.api.post<void>(`/v1/returns/${id}/inspect`, request, { silent: true });
  }

  /** POST /api/v1/returns/{id}/refund — QL/AD hoàn tiền (chỉ sau khi đã nhận & kiểm tra hàng). */
  refund(id: number, request: RefundReturnRequest): Observable<ReturnRefundResultDto> {
    return this.api.post<ReturnRefundResultDto>(`/v1/returns/${id}/refund`, request, { silent: true });
  }
}
