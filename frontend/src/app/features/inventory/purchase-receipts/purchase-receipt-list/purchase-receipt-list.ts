import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { formatDateTime, formatVnd } from '../../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../../core/models/paged-result.model';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../../shared/components/pagination/pagination';
import { dateRangeValidator } from '../../../../shared/validators/date-range.validator';
import { SupplierApi } from '../../../suppliers/supplier-api.service';
import type { SupplierDto } from '../../../suppliers/supplier.model';
import { InventoryApi } from '../../inventory-api.service';
import {
  RECEIPT_STATUS_CLASSES,
  RECEIPT_STATUS_LABELS,
  type PurchaseReceiptListItemDto,
} from '../../inventory.model';

/**
 * Danh sách phiếu nhập kho (GET /api/v1/purchase-receipts — `usp_PurchaseReceipt_List`).
 *
 * Phạm vi: nhân viên chỉ thấy phiếu của chi nhánh mình, Admin thấy tất cả (ép ở backend).
 * Bộ lọc nhà cung cấp dùng chung `GET /api/v1/suppliers` (backend cho TK/QL/AD đọc).
 */
@Component({
  selector: 'app-purchase-receipt-list',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './purchase-receipt-list.html',
})
export class PurchaseReceiptList {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly supplierApi = inject(SupplierApi);

  protected readonly formatMoment = formatDateTime;
  protected readonly formatPrice = formatVnd;
  protected readonly statusLabels = RECEIPT_STATUS_LABELS;
  protected readonly statusClasses = RECEIPT_STATUS_CLASSES;

  protected readonly loading = signal(false);
  protected readonly suppliers = signal<readonly SupplierDto[]>([]);
  protected readonly result = signal<PagedResult<PurchaseReceiptListItemDto>>(
    emptyPagedResult<PurchaseReceiptListItemDto>(20),
  );

  protected readonly filters = new FormGroup(
    {
      supplierId: new FormControl<number | null>(null),
      fromDate: new FormControl('', { nonNullable: true }),
      toDate: new FormControl('', { nonNullable: true }),
    },
    { validators: [dateRangeValidator('fromDate', 'toDate')] },
  );

  constructor() {
    this.loadSuppliers();
    this.load(1, this.result().pageSize);
  }

  /** Nạp danh mục NCC cho bộ lọc — lỗi ở đây không chặn danh sách phiếu (đã `silent` ở tầng API). */
  protected loadSuppliers(): void {
    this.supplierApi
      .listSuppliers({ pageNumber: 1, pageSize: 100 })
      .subscribe({
        next: (page) => this.suppliers.set(page.items),
        error: () => this.suppliers.set([]),
      });
  }

  protected load(pageNumber: number, pageSize: number): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }

    const value = this.filters.getRawValue();

    this.loading.set(true);

    this.inventoryApi
      .listPurchaseReceipts({
        supplierId: value.supplierId,
        fromDate: toIsoStart(value.fromDate),
        toDate: toIsoEnd(value.toDate),
        pageNumber,
        pageSize,
      })
      .subscribe({
        next: (page) => {
          this.result.set(page);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  protected resetFilters(): void {
    this.filters.reset({ supplierId: null, fromDate: '', toDate: '' });
    this.load(1, this.result().pageSize);
  }

  protected statusLabel(status: number): string {
    return this.statusLabels[status] ?? 'Không xác định';
  }

  protected statusClass(status: number): string {
    return this.statusClasses[status] ?? 'bg-slate-200 text-slate-700';
  }
}

/** 'YYYY-MM-DD' → ISO mốc đầu ngày UTC; rỗng → null. */
function toIsoStart(value: string): string | null {
  return value.length === 0 ? null : new Date(`${value}T00:00:00.000Z`).toISOString();
}

/** 'YYYY-MM-DD' → ISO 23:59:59 UTC vì backend coi `toDate` là chặn trên không bao gồm. */
function toIsoEnd(value: string): string | null {
  return value.length === 0 ? null : new Date(`${value}T23:59:59.000Z`).toISOString();
}
