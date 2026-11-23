import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { BranchDto } from '../../core/models/category.model';
import type { PageQuery, PagedResult } from '../../core/models/paged-result.model';
import type {
  ApproveStockTakeRequest,
  ChangeSerialStatusRequest,
  CreatePurchaseReceiptRequest,
  CreatedPurchaseReceiptDto,
  CreatedStockTakeDto,
  CreatedStockTransferDto,
  CreateStockTransferRequest,
  PurchaseReceiptDetailDto,
  PurchaseReceiptListItemDto,
  PurchaseReceiptListQuery,
  SerialLookupDto,
  SetMinStockRequest,
  StockLevelDto,
  StockLevelQuery,
  StockMovementDto,
  StockMovementQuery,
  StockTakeCountDto,
  StockTakeDetailDto,
  StockTransferDetailDto,
  SubmitStockTakeCountsRequest,
} from './inventory.model';

/**
 * Lời gọi HTTP của module Kho/Serial (Database/README.md mục 6).
 *
 * Ghi chú quyền & phạm vi (khớp `InventoryRoles.cs`):
 * - GET (tồn kho, thẻ kho, phiếu nhập/chuyển/kiểm kê) = TK, QL, AD.
 * - POST tạo phiếu nhập/chuyển/kiểm kê = TK, AD; `PUT /stock/min`, duyệt kiểm kê và đổi trạng thái
 *   serial = QL, AD.
 * - KHÔNG gửi `branchId`: backend ép từ token (BRD 2.2) — gửi thừa sẽ bị bỏ qua.
 * - `GET /api/v1/stock-transfers` và `GET /api/v1/stock-takes` KHÔNG tồn tại ở backend
 *   (chỉ có GET theo `{id}`) nên không có hàm liệt kê tương ứng.
 */
@Injectable({ providedIn: 'root' })
export class InventoryApi {
  private readonly api = inject(ApiService);

  // ── Tồn kho ───────────────────────────────────────────────────────────────

  /** GET /api/v1/stock */
  listStockLevels(query: StockLevelQuery): Observable<PagedResult<StockLevelDto>> {
    return this.api.get<PagedResult<StockLevelDto>>('/v1/stock', {
      params: {
        productId: query.productId ?? null,
        categoryId: query.categoryId ?? null,
        keyword: query.keyword ?? null,
        belowMinOnly: query.belowMinOnly ?? false,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /** PUT /api/v1/stock/min — `minStock = null` để dùng lại mặc định của sản phẩm. */
  setMinStock(request: SetMinStockRequest): Observable<void> {
    return this.api.put<void>('/v1/stock/min', request, { silent: true });
  }

  /** GET /api/v1/stock/movements — `productId` bắt buộc. */
  listStockMovements(query: StockMovementQuery): Observable<PagedResult<StockMovementDto>> {
    return this.api.get<PagedResult<StockMovementDto>>('/v1/stock/movements', {
      params: {
        productId: query.productId,
        fromDate: query.fromDate ?? null,
        toDate: query.toDate ?? null,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  // ── Nhập kho ──────────────────────────────────────────────────────────────

  /** POST /api/v1/purchase-receipts — trả 201 kèm mã phiếu để mở chi tiết. */
  createPurchaseReceipt(
    request: CreatePurchaseReceiptRequest,
  ): Observable<CreatedPurchaseReceiptDto> {
    return this.api.post<CreatedPurchaseReceiptDto>('/v1/purchase-receipts', request, {
      silent: true,
    });
  }

  /** GET /api/v1/purchase-receipts */
  listPurchaseReceipts(
    query: PurchaseReceiptListQuery,
  ): Observable<PagedResult<PurchaseReceiptListItemDto>> {
    return this.api.get<PagedResult<PurchaseReceiptListItemDto>>('/v1/purchase-receipts', {
      params: {
        supplierId: query.supplierId ?? null,
        fromDate: query.fromDate ?? null,
        toDate: query.toDate ?? null,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /** GET /api/v1/purchase-receipts/{id} — header + dòng hàng + serial đã sinh. */
  getPurchaseReceipt(id: number): Observable<PurchaseReceiptDetailDto> {
    return this.api.get<PurchaseReceiptDetailDto>(`/v1/purchase-receipts/${id}`);
  }

  // ── Chuyển kho ────────────────────────────────────────────────────────────

  /** POST /api/v1/stock-transfers — tạo phiếu Nháp (chưa trừ tồn). */
  createStockTransfer(request: CreateStockTransferRequest): Observable<CreatedStockTransferDto> {
    return this.api.post<CreatedStockTransferDto>('/v1/stock-transfers', request, { silent: true });
  }

  /** GET /api/v1/stock-transfers/{id} */
  getStockTransfer(id: number): Observable<StockTransferDetailDto> {
    return this.api.get<StockTransferDetailDto>(`/v1/stock-transfers/${id}`);
  }

  /** POST /api/v1/stock-transfers/{id}/ship — trừ tồn chi nhánh đi, serial thành Đang chuyển kho. */
  shipStockTransfer(id: number): Observable<void> {
    return this.api.post<void>(`/v1/stock-transfers/${id}/ship`);
  }

  /** POST /api/v1/stock-transfers/{id}/receive — cộng tồn chi nhánh đến, serial về Trong kho. */
  receiveStockTransfer(id: number): Observable<void> {
    return this.api.post<void>(`/v1/stock-transfers/${id}/receive`);
  }

  /** POST /api/v1/stock-transfers/{id}/cancel — chỉ hủy được phiếu Nháp. */
  cancelStockTransfer(id: number): Observable<void> {
    return this.api.post<void>(`/v1/stock-transfers/${id}/cancel`);
  }

  // ── Kiểm kê ───────────────────────────────────────────────────────────────

  /** POST /api/v1/stock-takes — không tham số, chi nhánh ép từ token. */
  createStockTake(): Observable<CreatedStockTakeDto> {
    return this.api.post<CreatedStockTakeDto>('/v1/stock-takes', null, { silent: true });
  }

  /** GET /api/v1/stock-takes/{id} */
  getStockTake(id: number): Observable<StockTakeDetailDto> {
    return this.api.get<StockTakeDetailDto>(`/v1/stock-takes/${id}`);
  }

  /** POST /api/v1/stock-takes/{id}/counts — trả danh sách dòng kèm chênh lệch. */
  submitStockTakeCounts(
    id: number,
    request: SubmitStockTakeCountsRequest,
  ): Observable<StockTakeCountDto[]> {
    return this.api.post<StockTakeCountDto[]>(`/v1/stock-takes/${id}/counts`, request, {
      silent: true,
    });
  }

  /** POST /api/v1/stock-takes/{id}/approve — QL/AD duyệt (áp chênh lệch) hoặc từ chối. */
  approveStockTake(id: number, request: ApproveStockTakeRequest): Observable<void> {
    return this.api.post<void>(`/v1/stock-takes/${id}/approve`, request, { silent: true });
  }

  // ── Serial ────────────────────────────────────────────────────────────────

  /** GET /api/v1/serials/{serialNumber} — `productId` chỉ cần khi serial trùng giữa nhiều sản phẩm. */
  lookupSerial(serialNumber: string, productId?: number | null): Observable<SerialLookupDto> {
    return this.api.get<SerialLookupDto>(`/v1/serials/${encodeURIComponent(serialNumber)}`, {
      params: { productId: productId ?? null },
      silent: true,
    });
  }

  /** POST /api/v1/serials/{id}/status — `id` là SerialItemId; bắt buộc có lý do. */
  changeSerialStatus(id: number, request: ChangeSerialStatusRequest): Observable<void> {
    return this.api.post<void>(`/v1/serials/${id}/status`, request, { silent: true });
  }

  // ── Dữ liệu dùng chung ────────────────────────────────────────────────────

  /**
   * GET /api/v1/admin/branches — CHỈ Admin gọi được (BranchesController dùng
   * `[Authorize(Roles = RoleCodes.Admin)]`). Vì vậy màn hình chuyển kho chỉ cho CHỌN chi nhánh
   * khi người dùng là Admin; vai trò khác phải nhập mã chi nhánh đích bằng tay.
   */
  listBranches(page: PageQuery): Observable<PagedResult<BranchDto>> {
    return this.api.get<PagedResult<BranchDto>>('/v1/admin/branches', {
      params: { isActive: true, pageNumber: page.pageNumber, pageSize: page.pageSize },
      silent: true,
    });
  }
}
