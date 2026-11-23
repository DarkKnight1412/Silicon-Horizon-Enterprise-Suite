import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../../core/auth/auth.store';
import { APPROVER_ROLES, SALES_ROLES } from '../../../core/models/auth.model';
import { formatVnd } from '../../../core/models/common.model';
import { ORDER_STATUS_LABELS } from '../../../core/models/order.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { priceValidator } from '../../../shared/validators/price.validator';
import { PaymentApi } from '../payment-api.service';
import {
  RECORD_PAYMENT_METHOD_OPTIONS,
  canRefundOrder,
  remainingAmount,
  type RecordedPaymentDto,
} from '../payment.model';

/** Hộp thoại đang mở của khối thanh toán (`null` = đóng). */
type PaymentDialog = 'payment' | 'refund';

/**
 * Khối THANH TOÁN nhúng trong chi tiết đơn: nút "Ghi nhận thanh toán" / "Hoàn tiền",
 * hiển thị số tiền còn lại và cảnh báo `needsReview` sau khi ghi nhận.
 *
 * Component KHÔNG tự nạp lại đơn — chỉ phát `changed` để màn hình cha quyết định (giữ quy ước
 * "component chỉ trình bày, cha sở hữu luồng dữ liệu" như `app-product-picker`).
 *
 * Phân quyền theo đúng backend:
 * - Ghi nhận thanh toán: TN/TV/QL/AD (`SALES_ROLES`, `OrderRoleGroups.SalesCounter`).
 * - Hoàn tiền: QL/AD (`APPROVER_ROLES`, `OrderRoleGroups.Approval`) VÀ đơn phải ở trạng thái
 *   Giao thất bại (6) hoặc Đã hủy (8) — hoàn tiền do đổi trả đi qua quy trình đổi trả.
 */
@Component({
  selector: 'app-order-payment-panel',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sh-card p-4">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h2 class="text-sm font-semibold text-slate-700">Thanh toán &amp; hoàn tiền</h2>
        <span class="text-xs text-slate-500">
          Còn phải thu:
          <strong class="text-slate-800">{{ formatPrice(remaining()) }}</strong>
        </span>
      </div>

      @if (recorded(); as result) {
        <div
          class="mt-3 rounded-md px-3 py-2 text-sm"
          [class]="
            result.needsReview ? 'bg-amber-50 text-amber-900' : 'bg-emerald-50 text-emerald-800'
          "
        >
          @if (result.needsReview) {
            <p>
              Giao dịch đã ghi nhận nhưng cần <strong>đối soát thủ công</strong> (tiền về cho đơn đã
              hủy hoặc thanh toán thừa).
            </p>
          } @else if (result.isDuplicate) {
            <p>Giao dịch này đã được ghi nhận trước đó — không cộng tiền lần hai.</p>
          } @else {
            <p>Đã ghi nhận giao dịch {{ formatPrice(result.paidAmount) }}.</p>
          }
          @if (result.orderStatus !== null) {
            <p class="text-xs">
              Trạng thái đơn sau khi ghi nhận: {{ statusLabel(result.orderStatus) }}
            </p>
          }
        </div>
      }

      <div class="mt-3 flex flex-wrap gap-2">
        @if (canRecord()) {
          <button
            type="button"
            class="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
            [disabled]="busy()"
            (click)="openPayment()"
          >
            Ghi nhận thanh toán
          </button>
        }

        @if (canRefund()) {
          <button
            type="button"
            class="rounded-md bg-rose-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-rose-700 disabled:opacity-50"
            [disabled]="busy()"
            (click)="openRefund()"
          >
            Hoàn tiền
          </button>
        }

        @if (!canRecord() && !canRefund()) {
          <p class="text-sm text-slate-500">
            @if (refundBlockedByStatus()) {
              Chỉ hoàn tiền được cho đơn <strong>Giao thất bại</strong> hoặc <strong>Đã hủy</strong>.
            } @else {
              Vai trò hiện tại không có quyền ghi nhận thanh toán cho đơn này.
            }
          </p>
        }
      </div>

      @if (dialog() === 'payment') {
        <div class="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/40 p-4">
          <div class="sh-card w-full max-w-md p-5">
            <h3 class="sh-page-title">Ghi nhận thanh toán</h3>
            <p class="mt-1 text-xs text-slate-500">
              Đơn #{{ orderId() }} · tổng {{ formatPrice(grandTotal()) }} · đã thu
              {{ formatPrice(paidAmount()) }}
            </p>

            <form
              class="mt-3 space-y-3"
              [formGroup]="paymentForm"
              (ngSubmit)="submitPayment()"
              novalidate
            >
              <div>
                <label class="block text-sm font-medium text-slate-700" for="payMethod">
                  Phương thức thu <span class="text-rose-600">*</span>
                </label>
                <select
                  id="payMethod"
                  formControlName="method"
                  class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                >
                  @for (option of methodOptions; track option.value) {
                    <option [ngValue]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>

              <div>
                <label class="block text-sm font-medium text-slate-700" for="payAmount">
                  Số tiền thu (VND) <span class="text-rose-600">*</span>
                </label>
                <input
                  id="payAmount"
                  type="number"
                  min="1"
                  formControlName="amount"
                  class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                />
                @if (paymentForm.controls.amount.touched && paymentForm.controls.amount.invalid) {
                  <p class="sh-field-error">Vui lòng nhập số tiền lớn hơn 0.</p>
                }
              </div>

              <div>
                <label class="block text-sm font-medium text-slate-700" for="payAt">
                  Thời điểm tiền về (bỏ trống = lấy hiện tại)
                </label>
                <input
                  id="payAt"
                  type="datetime-local"
                  formControlName="paidAt"
                  class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                />
              </div>

              <div class="flex justify-end gap-2 pt-1">
                <button
                  type="button"
                  class="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
                  (click)="close()"
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  class="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
                  [disabled]="busy()"
                >
                  Ghi nhận
                </button>
              </div>
            </form>
          </div>
        </div>
      }

      @if (dialog() === 'refund') {
        <div class="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/40 p-4">
          <div class="sh-card w-full max-w-md p-5">
            <h3 class="sh-page-title">Hoàn tiền cho khách</h3>
            <p class="mt-1 text-xs text-slate-500">
              Đơn #{{ orderId() }} · đã thu {{ formatPrice(paidAmount()) }} — hoàn tiền thủ công chỉ
              áp dụng cho đơn Đã hủy / Giao thất bại.
            </p>

            <form
              class="mt-3 space-y-3"
              [formGroup]="refundForm"
              (ngSubmit)="submitRefund()"
              novalidate
            >
              <div>
                <label class="block text-sm font-medium text-slate-700" for="refundMethod">
                  Phương thức hoàn <span class="text-rose-600">*</span>
                </label>
                <select
                  id="refundMethod"
                  formControlName="method"
                  class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                >
                  @for (option of methodOptions; track option.value) {
                    <option [ngValue]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>

              <div>
                <label class="block text-sm font-medium text-slate-700" for="refundAmount">
                  Số tiền hoàn (VND) <span class="text-rose-600">*</span>
                </label>
                <input
                  id="refundAmount"
                  type="number"
                  min="1"
                  formControlName="amount"
                  class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                />
                @if (refundForm.controls.amount.touched && refundForm.controls.amount.invalid) {
                  <p class="sh-field-error">
                    Vui lòng nhập số tiền hoàn lớn hơn 0 (không vượt quá số đã thu).
                  </p>
                }
              </div>

              <div>
                <label class="block text-sm font-medium text-slate-700" for="refundReason">
                  Lý do hoàn tiền <span class="text-rose-600">*</span>
                </label>
                <textarea
                  id="refundReason"
                  rows="3"
                  formControlName="reason"
                  class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                ></textarea>
                @if (refundForm.controls.reason.touched && refundForm.controls.reason.invalid) {
                  <p class="sh-field-error">Vui lòng nhập lý do (tối đa 300 ký tự).</p>
                }
              </div>

              <div class="flex justify-end gap-2 pt-1">
                <button
                  type="button"
                  class="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
                  (click)="close()"
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  class="rounded-md bg-rose-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-rose-700 disabled:opacity-50"
                  [disabled]="busy()"
                >
                  Hoàn tiền
                </button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
})
export class OrderPaymentPanel {
  private readonly paymentApi = inject(PaymentApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  /** Id đơn cần ghi nhận thanh toán / hoàn tiền. */
  readonly orderId = input.required<number>();
  /** Tổng tiền của đơn (`grandTotal`). */
  readonly grandTotal = input.required<number>();
  /** Số đã thu của đơn (`paidAmount`). */
  readonly paidAmount = input.required<number>();
  /** Trạng thái đơn hiện tại — dùng để biết có được hoàn tiền hay không. */
  readonly orderStatus = input.required<number>();
  /** Phương thức thanh toán của đơn — dùng làm giá trị mặc định cho form. */
  readonly orderPaymentMethod = input.required<number>();

  /** Phát ra sau khi ghi nhận/hoàn tiền thành công để cha nạp lại đơn. */
  readonly changed = output<void>();

  protected readonly methodOptions = RECORD_PAYMENT_METHOD_OPTIONS;
  protected readonly formatPrice = formatVnd;

  protected readonly busy = signal(false);
  protected readonly dialog = signal<PaymentDialog | null>(null);
  protected readonly recorded = signal<RecordedPaymentDto | null>(null);

  protected readonly remaining = computed(() =>
    remainingAmount(this.grandTotal(), this.paidAmount()),
  );

  protected readonly canRecord = computed(() => this.authStore.hasAnyRole(SALES_ROLES));

  protected readonly canRefund = computed(
    () => this.authStore.hasAnyRole(APPROVER_ROLES) && canRefundOrder(this.orderStatus()),
  );

  /** Có vai trò duyệt nhưng trạng thái đơn không cho phép hoàn tiền ⇒ hiện gợi ý thay vì nút. */
  protected readonly refundBlockedByStatus = computed(
    () => this.authStore.hasAnyRole(APPROVER_ROLES) && !canRefundOrder(this.orderStatus()),
  );

  protected readonly paymentForm = new FormGroup({
    method: new FormControl(3, { nonNullable: true, validators: [Validators.required] }),
    amount: new FormControl<number | null>(null, {
      validators: [Validators.required, priceValidator({ min: 1, allowZero: false })],
    }),
    paidAt: new FormControl('', { nonNullable: true }),
  });

  protected readonly refundForm = new FormGroup({
    method: new FormControl(3, { nonNullable: true, validators: [Validators.required] }),
    amount: new FormControl<number | null>(null, {
      validators: [Validators.required, priceValidator({ min: 1, allowZero: false })],
    }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  protected statusLabel(status: number): string {
    return ORDER_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected openPayment(): void {
    this.recorded.set(null);
    this.paymentForm.reset({
      method: this.orderPaymentMethod(),
      amount: this.remaining(),
      paidAt: '',
    });
    this.dialog.set('payment');
  }

  protected openRefund(): void {
    this.recorded.set(null);
    this.refundForm.reset({
      method: this.orderPaymentMethod(),
      amount: this.paidAmount(),
      reason: '',
    });
    this.dialog.set('refund');
  }

  protected close(): void {
    this.dialog.set(null);
  }

  /** Ghi nhận thanh toán (xác nhận số tiền với người dùng trước khi gọi API). */
  protected async submitPayment(): Promise<void> {
    if (this.paymentForm.invalid) {
      this.paymentForm.markAllAsTouched();
      return;
    }

    const value = this.paymentForm.getRawValue();
    const amount = value.amount;

    if (amount === null) {
      return;
    }

    const accepted = await this.confirm.ask({
      title: 'Ghi nhận thanh toán',
      message: `Thu ${formatVnd(amount)} cho đơn #${this.orderId()}. Tiếp tục?`,
      confirmLabel: 'Ghi nhận',
    });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    this.paymentApi
      .recordPayment(this.orderId(), {
        method: value.method,
        amount,
        paidAt: toIsoUtc(value.paidAt),
      })
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.dialog.set(null);
          this.recorded.set(result);

          if (result.needsReview) {
            this.notifications.warning(
              'Giao dịch cần đối soát',
              'Tiền về cho đơn đã hủy hoặc thanh toán thừa — kiểm tra và hoàn tiền thủ công nếu cần.',
            );
          } else {
            this.notifications.success('Đã ghi nhận thanh toán', formatVnd(amount));
          }

          this.changed.emit();
        },
        error: () => this.busy.set(false),
      });
  }

  /** Hoàn tiền thủ công — thao tác nguy hiểm nên luôn xác nhận kèm lý do. */
  protected async submitRefund(): Promise<void> {
    if (this.refundForm.invalid) {
      this.refundForm.markAllAsTouched();
      return;
    }

    const value = this.refundForm.getRawValue();
    const amount = value.amount;

    if (amount === null) {
      return;
    }

    const reason = value.reason.trim();

    const accepted = await this.confirm.ask({
      title: 'Hoàn tiền cho khách',
      message: `Hoàn ${formatVnd(amount)} cho đơn #${this.orderId()} — lý do: "${reason}"`,
      confirmLabel: 'Hoàn tiền',
      danger: true,
    });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    this.paymentApi.refund(this.orderId(), { amount, method: value.method, reason }).subscribe({
      next: () => {
        this.busy.set(false);
        this.dialog.set(null);
        this.notifications.success('Đã hoàn tiền', `${formatVnd(amount)} · ${reason}`);
        this.changed.emit();
      },
      error: () => this.busy.set(false),
    });
  }
}

/** 'YYYY-MM-DDTHH:mm' (giờ địa phương) → ISO UTC; chuỗi rỗng → null (backend lấy thời điểm hiện tại). */
function toIsoUtc(value: string): string | null {
  if (value.length === 0) {
    return null;
  }

  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}
