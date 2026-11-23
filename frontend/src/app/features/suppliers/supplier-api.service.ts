import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { PagedResult } from '../../core/models/paged-result.model';
import type {
  CreateSupplierRequest,
  CreateSupplierReturnRequest,
  CreatedSupplierReturnDto,
  RecordSupplierPaymentRequest,
  SupplierDto,
  SupplierLedgerDto,
  SupplierLedgerQuery,
  SupplierListQuery,
  SupplierPaymentResultDto,
  UpdateSupplierRequest,
} from './supplier.model';

/**
 * Lời gọi HTTP của module NHÀ CUNG CẤP (Database/README.md mục 6, SUP-01..03).
 *
 * Quyền (đọc từ `SuppliersController` / `SupplierReturnsController`):
 * - `listSuppliers` / `getSupplier`: TK, QL, AD → dùng được cho cả bộ lọc ở màn nhập kho.
 * - `createSupplier` / `updateSupplier`: CHỈ Admin.
 * - `recordPayment` / `getLedger`: QL, AD.
 * - `createSupplierReturn`: TK, QL, AD.
 */
@Injectable({ providedIn: 'root' })
export class SupplierApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/suppliers */
  listSuppliers(query: SupplierListQuery): Observable<PagedResult<SupplierDto>> {
    return this.api.get<PagedResult<SupplierDto>>('/v1/suppliers', {
      params: {
        keyword: query.keyword ?? null,
        isActive: query.isActive ?? null,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /** GET /api/v1/suppliers/{id} */
  getSupplier(id: number): Observable<SupplierDto> {
    return this.api.get<SupplierDto>(`/v1/suppliers/${id}`);
  }

  /** POST /api/v1/suppliers — chỉ Admin. */
  createSupplier(request: CreateSupplierRequest): Observable<SupplierDto> {
    return this.api.post<SupplierDto>('/v1/suppliers', request, { silent: true });
  }

  /** PUT /api/v1/suppliers/{id} — chỉ Admin; `isActive = false` để ngừng hợp tác. */
  updateSupplier(id: number, request: UpdateSupplierRequest): Observable<SupplierDto> {
    return this.api.put<SupplierDto>(`/v1/suppliers/${id}`, request, { silent: true });
  }

  /** POST /api/v1/supplier-returns — trả hàng NCC (serial phải đang Hàng lỗi tại chi nhánh). */
  createSupplierReturn(
    request: CreateSupplierReturnRequest,
  ): Observable<CreatedSupplierReturnDto> {
    return this.api.post<CreatedSupplierReturnDto>('/v1/supplier-returns', request, {
      silent: true,
    });
  }

  /** POST /api/v1/suppliers/{id}/payments — QL/AD; trả công nợ còn lại sau khi trả. */
  recordPayment(
    id: number,
    request: RecordSupplierPaymentRequest,
  ): Observable<SupplierPaymentResultDto> {
    return this.api.post<SupplierPaymentResultDto>(`/v1/suppliers/${id}/payments`, request, {
      silent: true,
    });
  }

  /** GET /api/v1/suppliers/{id}/ledger — QL/AD. */
  getLedger(id: number, query: SupplierLedgerQuery): Observable<SupplierLedgerDto> {
    return this.api.get<SupplierLedgerDto>(`/v1/suppliers/${id}/ledger`, {
      params: { pageNumber: query.pageNumber, pageSize: query.pageSize },
    });
  }
}
