import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../../core/auth/auth.store';
import { ROLES, WAREHOUSE_ROLES } from '../../../../core/models/auth.model';
import { formatDateTime } from '../../../../core/models/common.model';
import { ConfirmService } from '../../../../core/services/confirm.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { InventoryApi } from '../../inventory-api.service';
import {
  SERIAL_STATUS_CLASSES,
  SERIAL_STATUS_LABELS,
  TRANSFER_STATUS,
  TRANSFER_STATUS_CLASSES,
  TRANSFER_STATUS_LABELS,
  type StockTransferDetailDto,
  type StockTransferSerialDto,
} from '../../inventory.model';

/** Vai trò được HUỶ phiếu chuyển (backend dùng `InventoryRoles.StockDesk`: TK, QL, AD). */
const TRANSFER_CANCEL_ROLES = [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin];

/** Hành động trên phiếu chuyển kho — dùng chung một luồng xác nhận. */
type TransferAction = 'ship' | 'receive' | 'cancel';

/**
 * Chi tiết phiếu chuyển kho + 3 thao tác vòng đời (GET /api/v1/stock-transfers/{id} và
 * POST `{id}/ship|receive|cancel` — Database/README.md mục 6, UC-14).
 *
 * Quy tắc backend (đọc từ StockTransfersController):
 * - Vòng đời Nháp (0) → Đang chuyển (1) → Đã nhận (2); chỉ phiếu Nháp mới huỷ được (3).
 * - Xuất kho trừ tồn CHI NHÁNH ĐI và chuyển serial InStock → InTransfer; nhận hàng cộng tồn CHI NHÁNH
 *   ĐẾN và đổi `BranchId` của serial. Nút chỉ hiện khi đúng trạng thái để tránh 409 vô ích.
 * - `id` nhận từ route param qua `withComponentInputBinding`.
 */
@Component({
  selector: 'app-stock-transfer-detail',
  imports: [RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stock-transfer-detail.html',
})
export class StockTransferDetail implements OnInit {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  readonly id = input('');

  protected readonly formatMoment = formatDateTime;
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly detail = signal<StockTransferDetailDto | null>(null);

  private readonly status = computed(() => this.detail()?.header.status ?? null);

  protected readonly canShip = computed(
    () => this.status() === TRANSFER_STATUS.Draft && this.authStore.hasAnyRole(WAREHOUSE_ROLES),
  );
  protected readonly canReceive = computed(
    () => this.status() === TRANSFER_STATUS.InTransit && this.authStore.hasAnyRole(WAREHOUSE_ROLES),
  );
  protected readonly canCancel = computed(
    () => this.status() === TRANSFER_STATUS.Draft && this.authStore.hasAnyRole(TRANSFER_CANCEL_ROLES),
  );

  ngOnInit(): void {
    const parsed = Number(this.id());

    if (!Number.isFinite(parsed) || parsed <= 0) {
      this.loading.set(false);
      return;
    }

    this.reload(parsed);
  }

  protected transferStatusLabel(status: number): string {
    return TRANSFER_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected transferStatusClass(status: number): string {
    return TRANSFER_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }

  protected serialStatusLabel(status: number): string {
    return SERIAL_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected serialStatusClass(status: number): string {
    return SERIAL_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }

  protected serialsOfItem(transferItemId: number): readonly StockTransferSerialDto[] {
    const detail = this.detail();
    return detail === null
      ? []
      : detail.serials.filter((serial) => serial.transferItemId === transferItemId);
  }

  /** Xuất kho / nhận hàng / huỷ — hỏi xác nhận trước khi gọi API (huỷ và xuất kho là nguy hiểm). */
  protected async run(action: TransferAction): Promise<void> {
    const detail = this.detail();

    if (detail === null) {
      return;
    }

    const messages: Record<TransferAction, { title: string; message: string; danger: boolean }> = {
      ship: {
        title: 'Xuất kho chuyển hàng',
        message: `Trừ tồn chi nhánh ${detail.header.fromBranchName} và chuyển serial sang trạng thái Đang chuyển kho. Tiếp tục?`,
        danger: true,
      },
      receive: {
        title: 'Nhận hàng chuyển kho',
        message: `Cộng tồn chi nhánh ${detail.header.toBranchName} và đưa serial về Trong kho. Tiếp tục?`,
        danger: false,
      },
      cancel: {
        title: 'Huỷ phiếu chuyển kho',
        message: `${detail.header.transferCode} sẽ bị huỷ. Chỉ phiếu Nháp mới huỷ được. Tiếp tục?`,
        danger: true,
      },
    };

    const accepted = await this.confirm.ask({ ...messages[action], confirmLabel: 'Xác nhận' });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    const request$ =
      action === 'ship'
        ? this.inventoryApi.shipStockTransfer(detail.header.id)
        : action === 'receive'
          ? this.inventoryApi.receiveStockTransfer(detail.header.id)
          : this.inventoryApi.cancelStockTransfer(detail.header.id);

    request$.subscribe({
      next: () => {
        this.busy.set(false);
        this.notifications.success(
          action === 'cancel' ? 'Đã huỷ phiếu chuyển kho' : 'Đã cập nhật phiếu chuyển kho',
          detail.header.transferCode,
        );
        this.reload(detail.header.id);
      },
      error: () => this.busy.set(false),
    });
  }

  private reload(id: number): void {
    this.loading.set(true);

    this.inventoryApi.getStockTransfer(id).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.loading.set(false);
      },
      error: () => {
        this.detail.set(null);
        this.loading.set(false);
      },
    });
  }
}
