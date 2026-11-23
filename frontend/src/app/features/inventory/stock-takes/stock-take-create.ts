import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { WAREHOUSE_ROLES } from '../../../core/models/auth.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { InventoryApi } from '../inventory-api.service';

/**
 * Mở đợt kiểm kê mới (POST /api/v1/stock-takes — `usp_StockTake_Create`, UC-15).
 *
 * Endpoint KHÔNG nhận tham số: chi nhánh ép từ token (tài khoản chưa gắn chi nhánh → 403).
 * Backend cũng KHÔNG có API danh sách đợt kiểm kê nên màn hình kèm ô mở chi tiết theo ID —
 * cùng cách xử lý như màn chuyển kho.
 *
 * Cảnh báo nghiệp vụ hiển thị ngay trên màn hình: proc ghi `SystemQty` theo tồn HIỆN TẠI nên nên
 * kiểm kê lúc ít phát sinh xuất/nhập.
 */
@Component({
  selector: 'app-stock-take-create',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="space-y-4">
      <header class="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h1 class="sh-page-title">Kiểm kê kho</h1>
          <p class="text-sm text-slate-500">
            Mỗi đợt kiểm kê thuộc chi nhánh của người mở; nhập số đếm xong thì Quản lý duyệt để áp
            chênh lệch vào tồn.
          </p>
        </div>
        <a
          routerLink="/inventory/stock"
          class="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
        >
          ← Tồn kho
        </a>
      </header>

      <div class="sh-card space-y-3 p-4">
        <h2 class="text-sm font-semibold text-slate-700">Chi nhánh kiểm kê</h2>
        <p class="rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-700">
          @if (branchId(); as branch) {
            Chi nhánh #{{ branch }}
            <span class="text-xs text-slate-500">(ép từ tài khoản đăng nhập)</span>
          } @else {
            <span class="text-amber-700">
              Tài khoản chưa gắn chi nhánh — backend sẽ từ chối mở đợt kiểm kê (403).
            </span>
          }
        </p>

        <p class="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-900">
          Nên kiểm kê lúc ít phát sinh xuất/nhập: proc lấy tồn hệ thống ngay tại thời điểm gửi số đếm.
        </p>

        <button
          type="button"
          class="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
          [disabled]="creating()"
          (click)="create()"
        >
          {{ creating() ? 'Đang mở đợt…' : 'Mở đợt kiểm kê mới' }}
        </button>

        @if (!canCreate()) {
          <p class="text-sm text-amber-700">Chỉ Thủ kho hoặc Admin mở được đợt kiểm kê.</p>
        }
      </div>

      <div class="sh-card p-4">
        <h2 class="text-sm font-semibold text-slate-700">Mở đợt kiểm kê đã có</h2>
        <p class="mt-1 text-xs text-slate-500">
          Backend chưa có API danh sách đợt kiểm kê — nhập ID đợt để mở chi tiết.
        </p>
        <div class="mt-3 flex flex-wrap items-end gap-2">
          <div class="min-w-48">
            <label class="block text-sm font-medium text-slate-700" for="lookupId">ID đợt kiểm kê</label>
            <input
              id="lookupId"
              type="number"
              min="1"
              step="1"
              [formControl]="lookupId"
              class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
            />
          </div>
          <button
            type="button"
            class="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
            (click)="openById()"
          >
            Mở đợt
          </button>
        </div>
      </div>
    </section>
  `,
})
export class StockTakeCreate {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected readonly branchId = computed(() => this.authStore.account()?.branchId ?? null);
  protected readonly canCreate = computed(() => this.authStore.hasAnyRole(WAREHOUSE_ROLES));
  protected readonly creating = signal(false);
  protected readonly lookupId = new FormControl('', { nonNullable: true });

  protected async create(): Promise<void> {
    const accepted = await this.confirm.ask({
      title: 'Mở đợt kiểm kê',
      message: 'Đợt kiểm kê mới sẽ được tạo ở trạng thái Đang kiểm kê cho chi nhánh của bạn.',
      confirmLabel: 'Mở đợt',
    });

    if (!accepted) {
      return;
    }

    this.creating.set(true);

    this.inventoryApi.createStockTake().subscribe({
      next: (created) => {
        this.creating.set(false);
        this.notifications.success('Đã mở đợt kiểm kê', created.stockTakeCode);
        void this.router.navigate(['/inventory/stock-takes', created.stockTakeId]);
      },
      error: () => this.creating.set(false),
    });
  }

  protected openById(): void {
    const parsed = Number(this.lookupId.value.trim());

    if (!Number.isFinite(parsed) || parsed <= 0) {
      this.notifications.warning('Mã đợt không hợp lệ', 'Nhập ID đợt kiểm kê (số).');
      return;
    }

    void this.router.navigate(['/inventory/stock-takes', parsed]);
  }
}
