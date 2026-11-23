import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { formatDateTime, formatNumber } from '../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import type { ProductListItemDto } from '../../../core/models/product.model';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { dateRangeValidator } from '../../../shared/validators/date-range.validator';
import { CatalogApi } from '../../catalog/catalog-api.service';
import { InventoryApi } from '../inventory-api.service';
import { MOVEMENT_TYPE_LABELS, type StockMovementDto } from '../inventory.model';
import { ProductPicker } from '../product-picker/product-picker';

/**
 * Thẻ kho — lịch sử biến động tồn của MỘT sản phẩm (GET /api/v1/stock/movements,
 * `usp_Stock_GetMovements`, INV-03).
 *
 * Ràng buộc từ backend:
 * - `productId` là tham số BẮT BUỘC của proc ⇒ phải chọn sản phẩm trước khi xem.
 * - Chi nhánh ép từ token; Admin chưa gắn chi nhánh sẽ nhận 403 (proc yêu cầu `@BranchId`) — màn
 *   hình hiển thị rõ chi nhánh đang dùng để người dùng hiểu vì sao không có dữ liệu.
 * - `StockMovements` là sổ bất biến (append-only): màn hình chỉ đọc, không có nút sửa/xoá.
 * - `toDate` là chặn trên KHÔNG bao gồm ⇒ tự đẩy lên 23:59:59 UTC của ngày người dùng chọn.
 */
@Component({
  selector: 'app-stock-movements',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, Pagination, ProductPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stock-movements.html',
})
export class StockMovements {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly catalog = inject(CatalogApi);
  private readonly authStore = inject(AuthStore);
  private readonly notifications = inject(NotificationService);

  protected readonly formatMoment = formatDateTime;
  protected readonly formatQuantity = formatNumber;
  protected readonly movementLabels = MOVEMENT_TYPE_LABELS;

  /** Chi nhánh đang áp dụng (ép từ token — chỉ để hiển thị). */
  protected readonly branchId = this.authStore.account()?.branchId ?? null;

  protected readonly selectedProduct = signal<ProductListItemDto | null>(null);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);
  protected readonly productResults = signal<readonly ProductListItemDto[]>([]);

  protected readonly loading = signal(false);
  protected readonly result = signal<PagedResult<StockMovementDto>>(emptyPagedResult<StockMovementDto>(50));

  protected readonly productKeyword = new FormControl('', { nonNullable: true });

  protected readonly filters = new FormGroup(
    {
      fromDate: new FormControl('', { nonNullable: true }),
      toDate: new FormControl('', { nonNullable: true }),
    },
    { validators: [dateRangeValidator('fromDate', 'toDate')] },
  );

  protected searchProducts(): void {
    const keyword = this.productKeyword.value.trim();

    if (keyword.length === 0) {
      this.notifications.warning('Nhập từ khoá', 'Nhập tên sản phẩm hoặc đầu mã SKU để tìm.');
      return;
    }

    this.searching.set(true);

    this.catalog
      .searchProducts({ keyword, sortBy: 'name', pageNumber: 1, pageSize: 20 })
      .subscribe({
        next: (page) => {
          this.productResults.set(page.items);
          this.searching.set(false);
          this.searched.set(true);
        },
        error: () => {
          this.searching.set(false);
          this.searched.set(true);
        },
      });
  }

  protected pickProduct(product: ProductListItemDto): void {
    this.selectedProduct.set(product);
    this.productResults.set([]);
    this.searched.set(false);
    this.load(1, this.result().pageSize);
  }

  protected clearProduct(): void {
    this.selectedProduct.set(null);
    this.result.set(emptyPagedResult<StockMovementDto>(this.result().pageSize));
  }

  protected load(pageNumber: number, pageSize: number): void {
    const product = this.selectedProduct();

    if (product === null) {
      return;
    }

    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }

    const value = this.filters.getRawValue();

    this.loading.set(true);

    this.inventoryApi
      .listStockMovements({
        productId: product.id,
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
    this.filters.reset({ fromDate: '', toDate: '' });
    this.load(1, this.result().pageSize);
  }

  protected movementLabel(movementType: number): string {
    return this.movementLabels[movementType] ?? 'Không xác định';
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
