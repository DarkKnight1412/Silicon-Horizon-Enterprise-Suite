import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { isApiError, type ApiError } from '../../../core/models/api-error.model';
import { formatVnd, type SelectOption } from '../../../core/models/common.model';
import {
  PAYMENT_METHOD_LABELS,
  type CreateOrderRequest,
} from '../../../core/models/order.model';
import type { VoucherValidationDto } from '../../../core/models/voucher.model';
import { CartStore } from '../../../core/services/cart.store';
import { NotificationService } from '../../../core/services/notification.service';
import { priceValidator } from '../../../shared/validators/price.validator';
import { vietnamesePhoneValidator } from '../../../shared/validators/vietnamese-phone.validator';
import { OrderApi } from '../order-api.service';

/**
 * Đặt hàng (POST /api/v1/orders — chỉ vai trò Customer).
 *
 * Nghiệp vụ cần lưu ý:
 * - Đơn có voucher ⇒ backend tự chốt lại giá và áp voucher (chia tiền giảm cho các dòng rồi phân bổ
 *   phần dư cho dòng lớn nhất), nên tổng tiền hiển thị ở đây chỉ là TẠM TÍNH.
 * - Voucher chưa đủ điều kiện (chưa đạt giá trị đơn tối thiểu) vẫn được gửi: proc tự áp khi đủ điều kiện.
 * - `branchId` là bắt buộc vì backend chưa có proc tự chọn chi nhánh xuất hàng
 *   (ghi chú ⚠️ trong CreateOrderRequest) — khách hàng nên xem tồn theo chi nhánh ở trang chi tiết sản phẩm.
 */
@Component({
  selector: 'app-checkout',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './checkout.html',
})
export class Checkout {
  private readonly orderApi = inject(OrderApi);
  private readonly cart = inject(CartStore);
  private readonly authStore = inject(AuthStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected readonly formatPrice = formatVnd;
  protected readonly items = this.cart.items;
  protected readonly totals = this.cart.totals;

  protected readonly submitting = signal(false);
  protected readonly voucherChecking = signal(false);
  protected readonly voucherResult = signal<VoucherValidationDto | null>(null);
  protected readonly apiError = signal<ApiError | null>(null);

  protected readonly fulfillmentOptions: readonly SelectOption<number>[] = [
    { value: 0, label: 'Giao tận nơi' },
    { value: 1, label: 'Nhận tại cửa hàng' },
  ];

  /** Phương thức thanh toán cho đơn online (bỏ "Tiền mặt/POS" vì chỉ dùng cho đơn tại quầy). */
  protected readonly paymentOptions: readonly SelectOption<number>[] = Object.entries(
    PAYMENT_METHOD_LABELS,
  )
    .filter(([value]) => value !== '3')
    .map(([value, label]) => ({ value: Number(value), label }));

  protected readonly canSubmit = computed(() => this.totals().selectedCount > 0 && !this.submitting());

  protected readonly form = new FormGroup({
    branchId: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    fulfillmentType: new FormControl(1, { nonNullable: true, validators: [Validators.required] }),
    recipientName: new FormControl('', { nonNullable: true }),
    recipientPhone: new FormControl('', {
      nonNullable: true,
      validators: [vietnamesePhoneValidator()],
    }),
    shippingAddress: new FormControl('', { nonNullable: true }),
    shippingFee: new FormControl<number | null>(0, [
      priceValidator({ min: 0, max: 10_000_000 }),
    ]),
    paymentMethod: new FormControl(0, { nonNullable: true, validators: [Validators.required] }),
    voucherCode: new FormControl('', { nonNullable: true }),
    note: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    // Nhân viên đã có chi nhánh trong token — điền sẵn để đỡ phải nhập lại.
    const branchId = this.authStore.account()?.branchId ?? null;
    if (branchId !== null) {
      this.form.controls.branchId.setValue(branchId);
    }

    this.applyDeliveryValidators(this.form.controls.fulfillmentType.value);
    this.form.controls.fulfillmentType.valueChanges.subscribe((type) =>
      this.applyDeliveryValidators(type),
    );

    if (this.items().length === 0) {
      this.cart.load().subscribe({ error: () => undefined });
    }
  }

  /** Giao tận nơi bắt buộc có tên/SĐT/địa chỉ người nhận (proc báo 50004 nếu thiếu). */
  protected isDelivery(): boolean {
    return this.form.controls.fulfillmentType.value === 0;
  }

  /** Kiểm tra mã giảm giá trước khi đặt (POST /api/v1/vouchers/validate — mã sai vẫn trả 200). */
  protected checkVoucher(): void {
    const code = this.form.controls.voucherCode.value.trim();

    if (code.length === 0) {
      this.voucherResult.set(null);
      return;
    }

    this.voucherChecking.set(true);

    this.orderApi
      .validateVoucher({
        code,
        orderAmount: this.totals().subTotal,
        hasPromotion: this.items().some((item) => item.promotionId !== null),
        customerId: this.authStore.account()?.customerId ?? null,
      })
      .subscribe({
        next: (result) => {
          this.voucherResult.set(result);
          this.voucherChecking.set(false);
        },
        error: () => {
          this.voucherResult.set(null);
          this.voucherChecking.set(false);
        },
      });
  }

  protected submit(): void {
    if (!this.canSubmit()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    if (value.branchId === null) {
      return;
    }

    const request: CreateOrderRequest = {
      branchId: value.branchId,
      fulfillmentType: value.fulfillmentType,
      recipientName: this.optional(value.recipientName),
      recipientPhone: this.optional(value.recipientPhone),
      shippingAddress: this.optional(value.shippingAddress),
      shippingFee: value.shippingFee ?? 0,
      paymentMethod: value.paymentMethod,
      voucherCode: this.optional(value.voucherCode),
      note: this.optional(value.note),
    };

    this.submitting.set(true);
    this.apiError.set(null);

    this.orderApi.createOrder(request).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.notifications.success(
          'Đặt hàng thành công',
          `Đơn ${created.orderCode} — ${formatVnd(created.grandTotal)}`,
        );

        // Backend đã xoá các dòng đã đặt khỏi giỏ ⇒ nạp lại để badge trên header đúng.
        this.cart.load().subscribe({ error: () => undefined });

        void this.router.navigate(['/orders', created.orderId]);
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.apiError.set(isApiError(error) ? error : null);
      },
    });
  }

  /** Thông điệp lỗi 400 của backend cho một trường của form đặt hàng. */
  protected fieldError(field: string): string | null {
    const errors = this.apiError()?.fieldErrors ?? null;

    if (errors === null) {
      return null;
    }

    for (const [key, messages] of Object.entries(errors)) {
      if (key.toLowerCase() === field.toLowerCase()) {
        return messages[0] ?? null;
      }
    }

    return null;
  }

  private optional(value: string): string | null {
    const trimmed = value.trim();

    return trimmed.length === 0 ? null : trimmed;
  }

  /** Thêm/bỏ `Validators.required` cho nhóm trường giao hàng khi đổi hình thức nhận hàng. */
  private applyDeliveryValidators(fulfillmentType: number): void {
    const required = fulfillmentType === 0;

    this.setRequired(this.form.controls.recipientName, required);
    this.setRequired(this.form.controls.recipientPhone, required);
    this.setRequired(this.form.controls.shippingAddress, required);
  }

  private setRequired(control: FormControl<string>, required: boolean): void {
    if (required) {
      if (!control.hasValidator(Validators.required)) {
        control.addValidators(Validators.required);
      }
    } else {
      control.removeValidators(Validators.required);
    }

    control.updateValueAndValidity({ emitEvent: false });
  }
}
