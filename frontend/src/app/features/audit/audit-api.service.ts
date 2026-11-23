import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { PagedResult } from '../../core/models/paged-result.model';
import type { AuditLogDto, AuditLogQuery } from './audit.model';

/**
 * Lời gọi HTTP của module NHẬT KÝ HỆ THỐNG (Database/README.md mục 8, UC-29/AUD-04).
 *
 * Quyền: `AuditRoles.Viewers` = QL (chỉ chi nhánh mình — backend ép `branchId` từ token), AD.
 * Chỉ có endpoint ĐỌC vì `AuditLogs` là sổ append-only.
 */
@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/audit-logs — tra cứu nhật ký có phân trang (usp_Audit_Search). */
  search(query: AuditLogQuery): Observable<PagedResult<AuditLogDto>> {
    return this.api.get<PagedResult<AuditLogDto>>('/v1/audit-logs', {
      params: {
        entityType: query.entityType ?? null,
        entityId: query.entityId ?? null,
        userId: query.userId ?? null,
        action: query.action ?? null,
        fromDate: query.fromDate ?? null,
        toDate: query.toDate ?? null,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }
}
