import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { PagedResult } from '../../core/models/paged-result.model';
import type { SerialLookupDto } from '../inventory/inventory.model';
import type {
  AddWarrantyAttachmentRequest,
  ChangeWarrantyStatusRequest,
  ReceivedWarrantyTicketDto,
  ReceiveWarrantyTicketRequest,
  WarrantyTicketDetailDto,
  WarrantyTicketListItemDto,
  WarrantyTicketListQuery,
} from './warranty.model';

/**
 * Lời gọi HTTP của module BẢO HÀNH (Database/README.md mục 7.1, WAR-01..08).
 *
 * Quyền (đọc từ `WarrantyTicketsController` + `WarrantyRoles.cs`):
 * - `listTickets` / `getTicket`: mọi nhân viên + khách (phiếu của mình) + Admin.
 * - `receiveTicket`: Thu ngân + Tư vấn kỹ thuật + Admin.
 * - `changeStatus`: Thủ kho + Tư vấn kỹ thuật + Admin.
 * - `addAttachment`: mọi nhân viên + Admin.
 * - `lookupBySerial`: mọi nhân viên + khách (hàng của mình) + Admin — CÙNG proc với
 *   `GET /api/v1/serials/{serialNumber}` nên dùng lại kiểu `SerialLookupDto` của feature kho.
 */
@Injectable({ providedIn: 'root' })
export class WarrantyApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/warranty-tickets */
  listTickets(query: WarrantyTicketListQuery): Observable<PagedResult<WarrantyTicketListItemDto>> {
    return this.api.get<PagedResult<WarrantyTicketListItemDto>>('/v1/warranty-tickets', {
      params: {
        status: query.status ?? null,
        keyword: query.keyword ?? null,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /** GET /api/v1/warranty-tickets/{id} — phiếu + lịch sử trạng thái + đính kèm. */
  getTicket(id: number): Observable<WarrantyTicketDetailDto> {
    return this.api.get<WarrantyTicketDetailDto>(`/v1/warranty-tickets/${id}`);
  }

  /** POST /api/v1/warranty-tickets — tiếp nhận (có thể tự từ chối nếu hết hạn bảo hành). */
  receiveTicket(request: ReceiveWarrantyTicketRequest): Observable<ReceivedWarrantyTicketDto> {
    return this.api.post<ReceivedWarrantyTicketDto>('/v1/warranty-tickets', request, {
      silent: true,
    });
  }

  /** POST /api/v1/warranty-tickets/{id}/status */
  changeStatus(id: number, request: ChangeWarrantyStatusRequest): Observable<void> {
    return this.api.post<void>(`/v1/warranty-tickets/${id}/status`, request, { silent: true });
  }

  /** POST /api/v1/warranty-tickets/{id}/attachments */
  addAttachment(id: number, request: AddWarrantyAttachmentRequest): Observable<void> {
    return this.api.post<void>(`/v1/warranty-tickets/${id}/attachments`, request, { silent: true });
  }

  /** GET /api/v1/warranty/lookup?serial= — dùng chung `usp_Serial_Lookup` với module kho. */
  lookupBySerial(serial: string, productId?: number | null): Observable<SerialLookupDto> {
    return this.api.get<SerialLookupDto>('/v1/warranty/lookup', {
      params: { serial, productId: productId ?? null },
      silent: true,
    });
  }
}
