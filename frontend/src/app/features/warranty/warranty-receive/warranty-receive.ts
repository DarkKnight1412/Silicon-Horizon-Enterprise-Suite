import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
  type ValidationErrors,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NotificationService } from '../../../core/services/notification.service';
import type { PendingChangesAware } from '../../admin/pending-changes.guard';
import { WarrantyApi } from '../warranty-api.service';
import type { ReceiveWarrantyTicketRequest } from '../warranty.model';

/**
 * Tiếp nhận bảo hành (POST /api/v1/warranty-tickets — `usp_Warranty_Receive`, WAR-04/WAR-05).
 *
 * Nghiệp vụ backend:
 * - Chi nhánh tiếp nhận ép từ token; KHÔNG gửi `branchId`.
 * - Hàng có serial: truyền `serialNumber` (serial phải đang Sold). Hàng KHÔNG quản lý serial: truyền
 *   `orderItemId` của đơn đã hoàn tất ⇒ form bắt buộc phải có MỘT trong hai.
 * - Nếu hết hạn bảo hành (WAR-05) hoặc có `rejectReason`, proc tạo phiếu ở trạng thái Từ chối (5)
 *   và trả `rejected = true` — màn hình báo cảnh báo thay vì thông báo thành công thông thường.
 */
@Component({
  selector: 'app-warranty-receive',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './warranty-receive.html',
})
export class WarrantyReceive implements PendingChangesAware {
  private readonly warrantyApi = inject(WarrantyApi);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly submitting = signal(false);

  private readonly dirty = signal(false);

  protected readonly form = this.fb.group(
    {
      serialNumber: this.fb.control('', { validators: [Validators.maxLength(100)] }),
      productId: this.fb.control<number | null>(null),
      orderItemId: this.fb.control<number | null>(null),
      issueDescription: this.fb.control('', {
        validators: [Validators.required, Validators.maxLength(1000)],
      }),
      conditionNotes: this.fb.control('', { validators: [Validators.maxLength(1000)] }),
      rejectReason: this.fb.control('', { validators: [Validators.maxLength(500)] }),
    },
    { validators: [atLeastOneIdentifier] },
  );

  constructor() {
    this.form.valueChanges.subscribe(() => this.dirty.set(true));
  }

  hasUnsavedChanges(): boolean {
    return this.dirty() && !this.submitting();
  }

  /** Gửi yêu cầu tiếp nhận; `rejected = true` ⇒ phiếu vào thẳng trạng thái Từ chối. */
  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const serialNumber = value.serialNumber.trim();
    const issue = value.issueDescription.trim();
    const notes = value.conditionNotes.trim();
    const reject = value.rejectReason.trim();

    const request: ReceiveWarrantyTicketRequest = {
      serialNumber: serialNumber.length > 0 ? serialNumber : null,
      productId: value.productId,
      orderItemId: value.orderItemId,
      issueDescription: issue,
      conditionNotes: notes.length > 0 ? notes : null,
      rejectReason: reject.length > 0 ? reject : null,
    };

    this.submitting.set(true);

    this.warrantyApi.receiveTicket(request).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.dirty.set(false);

        if (created.rejected) {
          this.notifications.warning(
            'Phiếu bảo hành bị từ chối',
            created.rejectionReason ?? 'Hết hạn bảo hành hoặc theo lý do đã nhập.',
          );
        } else {
          this.notifications.success('Đã tiếp nhận bảo hành', created.ticketCode);
        }

        void this.router.navigate(['/warranty/tickets', created.ticketId]);
      },
      error: () => this.submitting.set(false),
    });
  }
}

/**
 * Validator cấp GROUP: phải có SỐ SERIAL hoặc DÒNG HÀNG CỦA ĐƠN.
 * Backend yêu cầu đúng một trong hai tuỳ sản phẩm có quản lý serial hay không.
 */
function atLeastOneIdentifier(group: AbstractControl): ValidationErrors | null {
  const serialNumber = String(group.get('serialNumber')?.value ?? '').trim();
  const orderItemId = group.get('orderItemId')?.value;

  if (serialNumber.length > 0 || (typeof orderItemId === 'number' && orderItemId > 0)) {
    return null;
  }

  return { identifier: { message: 'Nhập số serial (hàng có serial) hoặc ID dòng hàng của đơn.' } };
}
