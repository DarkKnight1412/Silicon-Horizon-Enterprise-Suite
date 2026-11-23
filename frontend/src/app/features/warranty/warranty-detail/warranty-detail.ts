import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLES } from '../../../core/models/auth.model';
import { formatDateTime, formatVnd } from '../../../core/models/common.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { WarrantyApi } from '../warranty-api.service';
import {
  WARRANTY_STATUS,
  WARRANTY_STATUS_CLASSES,
  WARRANTY_STATUS_LABELS,
  warrantyTargetOptions,
  type ChangeWarrantyStatusRequest,
  type WarrantyTicketDetailDto,
} from '../warranty.model';

/** Vai trò xử lý phiếu (`WarrantyRoles.Process`: TK, TV, AD). */
const WARRANTY_PROCESS_ROLES = [ROLES.Storekeeper, ROLES.TechAdvisor, ROLES.Admin];

/**
 * Chi tiết phiếu bảo hành + đổi trạng thái + đính kèm
 * (GET `/warranty-tickets/{id}`, POST `{id}/status`, POST `{id}/attachments` — WAR-06/WAR-07).
 *
 * Quy tắc backend:
 * - Đổi trạng thái: TK/TV/AD; đích 4 (Đổi mới) BẮT BUỘC có serial thay thế cùng sản phẩm đang
 *   Trong kho; đích 5 (Từ chối) BẮT BUỘC có lý do; cặp chuyển đổi sai → 409.
 * - Đính kèm: mọi nhân viên + Admin; backend chỉ nhận URL, không upload file.
 * - `fee = null` ⇒ proc giữ nguyên phí hiện tại của phiếu.
 */
@Component({
  selector: 'app-warranty-detail',
  imports: [ReactiveFormsModule, RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './warranty-detail.html',
})
export class WarrantyDetail implements OnInit {
  private readonly warrantyApi = inject(WarrantyApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly id = input('');

  protected readonly warrantyStatus = WARRANTY_STATUS;
  protected readonly formatMoment = formatDateTime;
  protected readonly formatPrice = formatVnd;

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly detail = signal<WarrantyTicketDetailDto | null>(null);

  protected readonly canProcess = computed(() => this.authStore.hasAnyRole(WARRANTY_PROCESS_ROLES));
  protected readonly canAttach = computed(
    () => this.authStore.isStaff() || this.authStore.hasRole(ROLES.Admin),
  );

  /** Trạng thái đích hợp lệ theo trạng thái hiện tại của phiếu. */
  protected readonly targetOptions = computed(() => {
    const detail = this.detail();
    return detail === null ? [] : warrantyTargetOptions(detail.ticket.status);
  });

  protected readonly statusForm = this.fb.group({
    toStatus: this.fb.control<number | null>(null, { validators: [Validators.required] }),
    note: this.fb.control('', { validators: [Validators.maxLength(500)] }),
    replacementSerialNumber: this.fb.control('', { validators: [Validators.maxLength(100)] }),
    rejectionReason: this.fb.control('', { validators: [Validators.maxLength(500)] }),
    fee: this.fb.control<number | null>(null),
  });

  protected readonly attachmentForm = this.fb.group({
    url: this.fb.control('', { validators: [Validators.required, Validators.maxLength(500)] }),
  });

  private ticketId = 0;

  ngOnInit(): void {
    const parsed = Number(this.id());

    if (!Number.isFinite(parsed) || parsed <= 0) {
      this.loading.set(false);
      return;
    }

    this.ticketId = parsed;
    this.reload();
  }

  protected statusLabel(status: number): string {
    return WARRANTY_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected statusClass(status: number): string {
    return WARRANTY_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }

  /** Đổi trạng thái phiếu — kiểm tra điều kiện riêng của đích 4 (đổi mới) và 5 (từ chối). */
  protected async changeStatus(): Promise<void> {
    const detail = this.detail();

    if (detail === null) {
      return;
    }

    if (this.statusForm.invalid) {
      this.statusForm.markAllAsTouched();
      return;
    }

    const value = this.statusForm.getRawValue();
    const toStatus = value.toStatus;

    if (toStatus === null) {
      return;
    }

    const replacement = value.replacementSerialNumber.trim();
    const rejection = value.rejectionReason.trim();

    if (toStatus === WARRANTY_STATUS.Replaced && replacement.length === 0) {
      this.notifications.warning(
        'Thiếu serial thay thế',
        'Đổi mới bắt buộc kèm số serial mới cùng sản phẩm và đang ở trạng thái Trong kho.',
      );
      return;
    }

    if (toStatus === WARRANTY_STATUS.Rejected && rejection.length === 0) {
      this.notifications.warning('Thiếu lý do từ chối', 'Từ chối bảo hành bắt buộc có lý do.');
      return;
    }

    const accepted = await this.confirm.ask({
      title: 'Đổi trạng thái phiếu bảo hành',
      message: `${detail.ticket.ticketCode}: "${this.statusLabel(detail.ticket.status)}" → "${this.statusLabel(toStatus)}". Tiếp tục?`,
      confirmLabel: 'Xác nhận',
      danger: toStatus === WARRANTY_STATUS.Rejected,
    });

    if (!accepted) {
      return;
    }

    const request: ChangeWarrantyStatusRequest = {
      toStatus,
      note: value.note.trim().length > 0 ? value.note.trim() : null,
      replacementSerialNumber: toStatus === WARRANTY_STATUS.Replaced ? replacement : null,
      rejectionReason: toStatus === WARRANTY_STATUS.Rejected ? rejection : null,
      fee: value.fee,
    };

    this.busy.set(true);

    this.warrantyApi.changeStatus(this.ticketId, request).subscribe({
      next: () => {
        this.busy.set(false);
        this.notifications.success('Đã đổi trạng thái phiếu', detail.ticket.ticketCode);
        this.statusForm.reset({
          toStatus: null,
          note: '',
          replacementSerialNumber: '',
          rejectionReason: '',
          fee: null,
        });
        this.reload();
      },
      error: () => this.busy.set(false),
    });
  }

  /** Đính kèm đường dẫn ảnh/tài liệu cho phiếu (backend chỉ nhận URL). */
  protected addAttachment(): void {
    if (this.attachmentForm.invalid) {
      this.attachmentForm.markAsTouched();
      return;
    }

    const url = this.attachmentForm.controls.url.value.trim();

    this.busy.set(true);

    this.warrantyApi.addAttachment(this.ticketId, { url }).subscribe({
      next: () => {
        this.busy.set(false);
        this.notifications.success('Đã đính kèm tài liệu', url);
        this.attachmentForm.reset({ url: '' });
        this.reload();
      },
      error: () => this.busy.set(false),
    });
  }

  private reload(): void {
    this.loading.set(true);

    this.warrantyApi.getTicket(this.ticketId).subscribe({
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
