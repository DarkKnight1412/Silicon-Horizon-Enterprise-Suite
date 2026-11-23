import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { APPROVER_ROLES } from '../../../core/models/auth.model';
import { formatNumber } from '../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { InventoryApi } from '../inventory-api.service';
import type { StockLevelDto } from '../inventory.model';

/**
 * Tồn kho theo chi nhánh + sản phẩm (GET /api/v1/stock — `usp_Stock_GetLevels`).
 *
 * Ghi chú quan trọng: backend ÉP chi nhánh từ token và KHÔNG nhận `branchId` từ query string
 * (StockController + `StockLevelQuery`), nên màn hình chỉ hiển thị chi nhánh mà KHÔNG cho lọc theo
 * chi nhánh. Admin không gắn chi nhánh sẽ thấy tồn toàn hệ thống.
 *
 * Quyền đặt mức tồn tối thiểu (`PUT /api/v1/stock/min`) là QL/AD — nút chỉ hiện với `APPROVER_ROLES`.
 */
@Component({
  selector: 'app-stock-levels',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stock-levels.html',
})
export class StockLevels {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly formatQuantity = formatNumber;
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly result = signal<PagedResult<StockLevelDto>>(emptyPagedResult<StockLevelDto>(50));

  /** Dòng đang mở ô nhập mức tồn tối thiểu (null = đang đóng). */
  protected readonly minTarget = signal<StockLevelDto | null>(null);

  /** Ô nhập mức tối thiểu mới; rỗng = dùng lại mặc định của sản phẩm. */
  protected readonly minStock = new FormControl<number | null>(null);

  protected readonly filters = new FormGroup({
    keyword: new FormControl('', { nonNullable: true }),
    belowMinOnly: new FormControl(false, { nonNullable: true }),
  });

  /** Chỉ QL/AD được gọi PUT /api/v1/stock/min (InventoryRoles.Approvers). */
  protected readonly canSetMinStock = computed(() => this.authStore.hasAnyRole(APPROVER_ROLES));

  constructor() {
    this.load(1, this.result().pageSize);
  }

  protected load(pageNumber: number, pageSize: number): void {
    const value = this.filters.getRawValue();
    const keyword = value.keyword.trim();

    this.loading.set(true);

    this.inventoryApi
      .listStockLevels({
        keyword: keyword.length > 0 ? keyword : null,
        belowMinOnly: value.belowMinOnly,
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
    this.filters.reset({ keyword: '', belowMinOnly: false });
    this.load(1, this.result().pageSize);
  }

  protected openMinStock(row: StockLevelDto): void {
    this.minStock.setValue(row.minStock);
    this.minTarget.set(row);
  }

  protected closeMinStock(): void {
    this.minTarget.set(null);
  }

  protected async saveMinStock(): Promise<void> {
    const target = this.minTarget();

    if (target === null) {
      return;
    }

    const raw = this.minStock.value;
    const parsed = raw === null ? null : Number(raw);

    if (parsed !== null && (!Number.isFinite(parsed) || parsed < 0)) {
      this.notifications.warning('Mức tồn tối thiểu không hợp lệ', 'Nhập số nguyên không âm.');
      return;
    }

    const accepted = await this.confirm.ask({
      title: 'Đặt mức tồn tối thiểu',
      message:
        parsed === null
          ? `${target.productName} sẽ dùng lại mức mặc định của sản phẩm.`
          : `${target.productName} tại ${target.branchName} → tối thiểu ${parsed} đơn vị.`,
      confirmLabel: 'Lưu',
    });

    if (!accepted) {
      return;
    }

    this.saving.set(true);

    this.inventoryApi.setMinStock({ productId: target.productId, minStock: parsed }).subscribe({
      next: () => {
        this.saving.set(false);
        this.minTarget.set(null);
        this.notifications.success('Đã cập nhật mức tồn tối thiểu', target.productName);
        this.load(this.result().pageNumber, this.result().pageSize);
      },
      error: () => this.saving.set(false),
    });
  }
}
