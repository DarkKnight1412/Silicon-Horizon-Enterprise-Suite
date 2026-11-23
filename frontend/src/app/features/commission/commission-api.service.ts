import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { PagedResult } from '../../core/models/paged-result.model';
import type {
  AdjustCommissionRequest,
  CommissionAdjustmentResultDto,
  CommissionEmployeeDto,
  CommissionSummaryDto,
  CommissionSummaryQuery,
} from './commission.model';

/**
 * Lời gọi HTTP của module HOA HỒNG (Database/README.md mục 8; KPI-01..06).
 *
 * Quyền (đọc từ `CommissionsController` / `AdminCommissionsController` / `CommissionPeriodsController`):
 * - `getSummary`: NV (của mình), QL (chi nhánh), AD — `employeeId`/`branchId` chỉ có hiệu lực với Admin.
 * - `adjust`: CHỈ Admin. Bút toán điều chỉnh luôn rơi vào kỳ ĐANG MỞ của ngày hiện tại nên dữ liệu
 *   kỳ đã chốt không bị sửa (KPI-05/KPI-06).
 * - `closePeriod`: CHỈ Admin. Chốt lại kỳ đã chốt → 409.
 * - `listEmployees`: CHỈ Admin (endpoint quản trị) — dùng cho dropdown chọn nhân viên.
 */
@Injectable({ providedIn: 'root' })
export class CommissionApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/commissions?yearMonth=yyyy-MM */
  getSummary(query: CommissionSummaryQuery): Observable<CommissionSummaryDto> {
    return this.api.get<CommissionSummaryDto>('/v1/commissions', {
      params: {
        yearMonth: query.yearMonth,
        employeeId: query.employeeId ?? null,
        branchId: query.branchId ?? null,
      },
      silent: true,
    });
  }

  /** POST /api/v1/admin/commissions/adjustments — tạo bút toán điều chỉnh (EntryType = 2). */
  adjust(request: AdjustCommissionRequest): Observable<CommissionAdjustmentResultDto> {
    return this.api.post<CommissionAdjustmentResultDto>(
      '/v1/admin/commissions/adjustments',
      request,
      { silent: true },
    );
  }

  /** POST /api/v1/admin/commission-periods/{id}/close — chốt kỳ (dữ liệu sau đó là bất biến). */
  closePeriod(periodId: number): Observable<void> {
    return this.api.post<void>(`/v1/admin/commission-periods/${periodId}/close`, null, {
      silent: true,
    });
  }

  /** GET /api/v1/admin/employees — CHỈ Admin (chọn nhân viên để điều chỉnh hoa hồng). */
  listEmployees(branchId: number | null): Observable<PagedResult<CommissionEmployeeDto>> {
    return this.api.get<PagedResult<CommissionEmployeeDto>>('/v1/admin/employees', {
      params: { branchId: branchId ?? null, status: 0, pageNumber: 1, pageSize: 200 },
      silent: true,
    });
  }
}
