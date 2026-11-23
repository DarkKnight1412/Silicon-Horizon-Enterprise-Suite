import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type {
  CreatedOrderDto,
  OrderListItemDto,
  OrderListQuery,
} from '../../core/models/order.model';
import type { PagedResult } from '../../core/models/paged-result.model';
import type {
  CreatePosOrderRequest,
  PosCustomerDto,
  PosCustomerQuery,
  PosEmployeeDto,
} from './staff-order.model';

/**
 * Lời gọi HTTP của nhóm ĐƠN HÀNG NHÂN VIÊN + POS (Database/README.md mục 5.2).
 *
 * Quyền (đọc từ `StaffOrdersController` / `PosOrdersController` / `CustomersController`):
 * - `listBranchOrders`: NV (mọi nhân viên) + AD — API ép `branchId` từ token, tài khoản chưa gắn
 *   chi nhánh sẽ nhận 403 nên màn hình có xử lý lỗi riêng.
 * - `createPosOrder`: TN, TV, QL, AD (`OrderRoleGroups.SalesCounter`).
 * - `searchCustomers`: CHỈ Admin — dùng để chọn khách tại quầy; quyền khác nhập tay mã khách.
 *
 * Các bước chuyển trạng thái đơn dùng lại `OrderApi` của feature orders (cùng endpoint
 * `POST /api/v1/orders/{id}/...`) để không nhân đôi định nghĩa nghiệp vụ.
 */
@Injectable({ providedIn: 'root' })
export class StaffOrderApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/staff/orders — đơn của chi nhánh nhân viên (ép `branchId` từ token). */
  listBranchOrders(query: OrderListQuery): Observable<PagedResult<OrderListItemDto>> {
    return this.api.get<PagedResult<OrderListItemDto>>('/v1/staff/orders', {
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

  /** POST /api/v1/pos/orders — tạo đơn tại quầy, trả 201 kèm thông tin đơn vừa tạo. */
  createPosOrder(request: CreatePosOrderRequest): Observable<CreatedOrderDto> {
    return this.api.post<CreatedOrderDto>('/v1/pos/orders', request, { silent: true });
  }

  /** GET /api/v1/customers — CHỈ Admin; dùng để chọn khách cho đơn tại quầy. */
  searchCustomers(query: PosCustomerQuery): Observable<PagedResult<PosCustomerDto>> {
    return this.api.get<PagedResult<PosCustomerDto>>('/v1/customers', {
      params: {
        keyword: query.keyword ?? null,
        isActive: true,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
      silent: true,
    });
  }

  /**
   * GET /api/v1/admin/employees — CHỈ Admin; danh sách nhân viên đang làm việc để chia doanh số.
   * Vai trò khác không gọi được endpoint này nên màn POS cho nhập trực tiếp `employeeId`.
   */
  listEmployees(branchId: number | null): Observable<PagedResult<PosEmployeeDto>> {
    return this.api.get<PagedResult<PosEmployeeDto>>('/v1/admin/employees', {
      params: { branchId: branchId ?? null, status: 0, pageNumber: 1, pageSize: 200 },
      silent: true,
    });
  }
}
