import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../../core/auth/auth.store';
import { APPROVER_ROLES } from '../../../../core/models/auth.model';
import { formatDate, formatDateTime } from '../../../../core/models/common.model';
import type { ApiError } from '../../../../core/models/api-error.model';
import { ConfirmService } from '../../../../core/services/confirm.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { InventoryApi } from '../../inventory-api.service';
import {
  SERIAL_STATUS_CLASSES,
  SERIAL_STATUS_LABELS,
  serialStatusTargetOptions,
  type ChangeSerialStatusRequest,
  type SerialLookupDto,
} from '../../inventory.model';

/**
 * Tra cứu serial (GET /api/v1/serials/{serialNumber} — `usp_Serial_Lookup`, UC-07/WAR-02).
 * Cũng là màn dùng chung cho `GET /api/v1/warranty/lookup?serial=` vì backend dùng CÙNG một proc.
 *
 * Ghi chú quyền:
 * - Xem: mọi nhân viên + khách (hàng của mình) + Admin.
 * - ĐỔI TRẠNG THÁI (`POST /api/v1/serials/{id}/status`): chỉ Quản lý/Admin, và chỉ 3 chuyển đổi được
 *   phép: Trong kho → Hàng lỗi, Hàng lỗi → Đã hủy, Hàng lỗi → Trong kho. Bắt buộc có lý do.
 * - Serial trùng giữa nhiều sản phẩm: backend trả 400 và yêu cầu truyền thêm `productId`.
 */
@Component({
  selector: 'app-serial-lookup',
  imports: [ReactiveFormsModule, RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './serial-lookup.html',
})
export class SerialLookup {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly formatMoment = formatDateTime;
  protected readonly formatDay = formatDate;

  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly result = signal<SerialLookupDto | null>(null);
  protected readonly apiError = signal<ApiError | null>(null);

  protected readonly filters = new FormGroup({
    serialNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    productId: new FormControl<number | null>(null),
  });

  protected readonly statusForm = new FormGroup({
    toStatus: new FormControl<number | null>(null, { validators: [Validators.required] }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  /** Chỉ QL/AD đổi được trạng thái serial (`InventoryRoles.Approvers`). */
  protected readonly canChangeStatus = computed(() => this.authStore.hasAnyRole(APPROVER_ROLES));

  /** Trạng thái đích hợp lệ theo trạng thái HIỆN TẠI của serial (rỗng = không đổi được). */
  protected readonly targetOptions = computed(() => {
    const current = this.result();

    return current === null ? [] : serialStatusTargetOptions(current.serial.status);
  });

  protected lookup(): void {
    if (this.filters.controls.serialNumber.invalid) {
      this.filters.controls.serialNumber.markAsTouched();
      return;
    }

    const serialNumber = this.filters.controls.serialNumber.value.trim();

    this.loading.set(true);
    this.apiError.set(null);

    this.inventoryApi.lookupSerial(serialNumber, this.filters.controls.productId.value).subscribe({
      next: (data) => {
        this.result.set(data);
        this.statusForm.reset({ toStatus: null, reason: '' });
        this.loading.set(false);
      },
      error: (error: ApiError) => {
        this.result.set(null);
        this.apiError.set(error);
        this.loading.set(false);
      },
    });
  }

  protected reset(): void {
    this.filters.reset({ serialNumber: '', productId: null });
    this.result.set(null);
    this.apiError.set(null);
    this.statusForm.reset({ toStatus: null, reason: '' });
  }

  protected statusLabel(status: number): string {
    return SERIAL_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected statusClass(status: number): string {
    return SERIAL_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }

  /** Đổi trạng thái serial thủ công — cần lý do để proc ghi nhật ký AUD-01. */
  protected async changeStatus(): Promise<void> {
    const current = this.result();

    if (current === null) {
      return;
    }

    if (this.statusForm.invalid) {
      this.statusForm.markAllAsTouched();
      return;
    }

    const value = this.statusForm.getRawValue();

    if (value.toStatus === null) {
      return;
    }

    const reason = value.reason.trim();

    const accepted = await this.confirm.ask({
      title: 'Đổi trạng thái serial',
      message: `${current.serial.serialNumber}: "${this.statusLabel(current.serial.status)}" → "${this.statusLabel(value.toStatus)}". Lý do: "${reason}"`,
      confirmLabel: 'Đổi trạng thái',
      danger: value.toStatus !== 0,
    });

    if (!accepted) {
      return;
    }

    const request: ChangeSerialStatusRequest = { toStatus: value.toStatus, reason };

    this.busy.set(true);

    this.inventoryApi.changeSerialStatus(current.serial.serialItemId, request).subscribe({
      next: () => {
        this.busy.set(false);
        this.notifications.success('Đã đổi trạng thái serial', current.serial.serialNumber);
        this.lookup();
      },
      error: () => this.busy.set(false),
    });
  }
}
