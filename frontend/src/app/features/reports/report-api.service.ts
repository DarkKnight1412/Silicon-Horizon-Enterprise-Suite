import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type {
  DebtReportDto,
  InventoryReportDto,
  InventoryReportQuery,
  RevenueReportDto,
  RevenueReportQuery,
  SerialStatusCountDto,
  SerialStatusReportQuery,
  WarrantyReportDto,
  WarrantyReportQuery,
} from './report.model';

/**
 * Lời gọi HTTP của module BÁO CÁO (Database/README.md mục 9; BRD mục 11).
 *
 * Quyền (đọc từ `ReportsController` + `ReportRoles.cs`):
 * - `getRevenue` / `getWarranty`: QL (chi nhánh mình), AD.
 * - `getInventory` / `getSerialStatus`: TK, QL, AD.
 * - `getDebt`: CHỈ Admin (proc không có tham số chi nhánh nên không giới hạn được theo chi nhánh).
 *
 * Không gửi `branchId` khi người dùng không phải Admin: backend ép phạm vi từ token và bỏ qua giá
 * trị thừa, nhưng gửi đúng giúp query string phản ánh đúng phạm vi đang xem.
 */
@Injectable({ providedIn: 'root' })
export class ReportApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/reports/revenue?from=&to=&groupBy=&branchId= */
  getRevenue(query: RevenueReportQuery): Observable<RevenueReportDto> {
    return this.api.get<RevenueReportDto>('/v1/reports/revenue', {
      params: {
        from: query.fromDate,
        to: query.toDate,
        groupBy: query.groupBy,
        branchId: query.branchId ?? null,
      },
    });
  }

  /** GET /api/v1/reports/inventory?slowDays=&branchId= — không có tham số ngày. */
  getInventory(query: InventoryReportQuery): Observable<InventoryReportDto> {
    return this.api.get<InventoryReportDto>('/v1/reports/inventory', {
      params: { slowDays: query.slowDays ?? null, branchId: query.branchId ?? null },
    });
  }

  /** GET /api/v1/reports/serial-status?productId=&branchId= */
  getSerialStatus(query: SerialStatusReportQuery): Observable<SerialStatusCountDto[]> {
    return this.api.get<SerialStatusCountDto[]>('/v1/reports/serial-status', {
      params: { productId: query.productId ?? null, branchId: query.branchId ?? null },
    });
  }

  /** GET /api/v1/reports/debt — chỉ Admin, không có tham số. */
  getDebt(): Observable<DebtReportDto> {
    return this.api.get<DebtReportDto>('/v1/reports/debt');
  }

  /** GET /api/v1/reports/warranty?from=&to=&branchId= */
  getWarranty(query: WarrantyReportQuery): Observable<WarrantyReportDto> {
    return this.api.get<WarrantyReportDto>('/v1/reports/warranty', {
      params: {
        from: query.fromDate,
        to: query.toDate,
        branchId: query.branchId ?? null,
      },
    });
  }
}
