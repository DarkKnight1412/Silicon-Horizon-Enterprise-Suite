import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { APPROVER_ROLES, SALES_ROLES, WAREHOUSE_ROLES } from '../../../core/models/auth.model';
import { formatDateTime, formatVnd } from '../../../core/models/common.model';
import {
  FULFILLMENT_TYPE_LABELS,
  ORDER_CHANNEL_LABELS,
  ORDER_STATUS_CLASSES,
  ORDER_STATUS_LABELS,
  PAYMENT_METHOD_LABELS,
  PAYMENT_STATUS_LABELS,
  SERIAL_STATUS_LABELS,
  canCustomerCancel,
  canStaffCancel,
  type OrderDetailDto,
} from '../../../core/models/order.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { OrderPaymentPanel } from '../../payments/order-payment-panel/order-payment-panel';
import { OrderApi } from '../order-api.service';

/** Hành động cần lý do (bắt buộc theo ORD-08). */
type ReasonAction = 'cancel' | 'cancel-approved' | 'delivery-failed';

/**
 * Chi tiết đơn (GET /api/v1/orders/{id}, 6 result set: header, dòng hàng, serial, thanh toán,
 * lịch sử trạng thái, nhân viên tư vấn).
 *
 * Dữ liệu nạp trước bằng resolver và bind vào input `order`. Sau mỗi hành động, component nạp lại
 * vào một signal cục bộ (`refreshed`) vì input của component là chỉ-đọc theo thiết kế của Angular.
 *
 * Nút hành động chỉ hiện khi ĐÚNG vai trò và ĐÚNG trạng thái của backend, tránh người dùng bấm rồi
 * nhận lỗi 403/409.
 */
@Component({
  selector: 'app-order-detail',
  imports: [RouterLink, ReactiveFormsModule, OrderPaymentPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './order-detail.html',
})
export class OrderDetailComponent {
  private readonly orderApi = inject(OrderApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  /** Đơn do resolver cung cấp. */
  readonly order = input.required<OrderDetailDto>();

  private readonly refreshed = signal<OrderDetailDto | null>(null);

  /** Bản dữ liệu mới nhất (resolver hoặc sau khi nạp lại). */
  protected readonly current = computed(() => this.refreshed() ?? this.order());
  protected readonly header = computed(() => this.current().order);

  protected readonly busy = signal(false);
  protected readonly reasonAction = signal<ReasonAction | null>(null);

  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(300)],
  });

  protected readonly formatPrice = formatVnd;
  protected readonly formatMoment = formatDateTime;

  protected readonly canCancelMine = computed(
    () => this.authStore.isCustomer() && canCustomerCancel(this.header().status),
  );
  protected readonly canConfirm = computed(
    () => this.authStore.hasAnyRole(SALES_ROLES) && this.header().status === 1,
  );
  protected readonly canStartPacking = computed(
    () => this.authStore.hasAnyRole(WAREHOUSE_ROLES) && this.header().status === 2,
  );
  protected readonly canMarkReady = computed(
    () => this.authStore.hasAnyRole(WAREHOUSE_ROLES) && this.header().status === 3,
  );
  protected readonly canComplete = computed(
    () =>
      this.authStore.hasAnyRole(WAREHOUSE_ROLES) &&
      (this.header().status === 4 || this.header().status === 5),
  );
  protected readonly canRetryDelivery = computed(
    () => this.authStore.hasAnyRole(WAREHOUSE_ROLES) && this.header().status === 6,
  );
  protected readonly canCancelApproved = computed(
    () => this.authStore.hasAnyRole(APPROVER_ROLES) && canStaffCancel(this.header().status),
  );
  protected readonly hasAnyAction = computed(
    () =>
      this.canCancelMine() ||
      this.canConfirm() ||
      this.canStartPacking() ||
      this.canMarkReady() ||
      this.canComplete() ||
      this.canRetryDelivery() ||
      this.canCancelApproved(),
  );

  // ───────────────────────────────────────────────────────────────────────────
  // Hành động theo trạng thái
  // ───────────────────────────────────────────────────────────────────────────

  protected confirmOrder(): void {
    this.run(this.orderApi.confirmOrder(this.header().id), 'Đã xác nhận đơn');
  }

  protected startPacking(): void {
    this.run(this.orderApi.startPacking(this.header().id), 'Bắt đầu đóng gói');
  }

  protected markReady(): void {
    this.run(this.orderApi.markReady(this.header().id), 'Đơn đã sẵn sàng giao / chờ khách nhận');
  }

  protected complete(): void {
    this.run(this.orderApi.complete(this.header().id), 'Đơn đã hoàn tất');
  }

  protected retryDelivery(): void {
    this.run(this.orderApi.retryDelivery(this.header().id), 'Đã ghi nhận giao lại');
  }

  /** Sau khi khối thanh toán ghi nhận/hoàn tiền thành công thì nạp lại đơn để số liệu khớp. */
  protected onPaymentChanged(): void {
    this.reload();
  }

  protected openReason(action: ReasonAction): void {
    this.reason.reset('');
    this.reasonAction.set(action);
  }

  protected closeReason(): void {
    this.reasonAction.set(null);
  }

  /** Gửi hành động kèm lý do (hủy đơn của khách, hủy đã xác nhận, giao thất bại). */
  protected async submitReason(): Promise<void> {
    const action = this.reasonAction();

    if (action === null) {
      return;
    }

    if (this.reason.invalid) {
      this.reason.markAsTouched();
      return;
    }

    const reason = this.reason.value.trim();
    const id = this.header().id;

    const confirmed = await this.confirm.ask({
      title: action === 'delivery-failed' ? 'Ghi nhận giao thất bại' : 'Xác nhận hủy đơn',
      message: `${this.header().orderCode} — lý do: "${reason}"`,
      confirmLabel: 'Xác nhận',
      danger: action !== 'delivery-failed',
    });

    if (!confirmed) {
      return;
    }

    if (action === 'delivery-failed') {
      this.run(this.orderApi.markDeliveryFailed(id, { reason }), 'Đã ghi nhận giao thất bại');
      return;
    }

    this.busy.set(true);

    const cancelRequest$ =
      action === 'cancel'
        ? this.orderApi.cancelOrder(id, { reason })
        : this.orderApi.cancelApproved(id, { reason });

    cancelRequest$.subscribe({
      next: (result) => {
        this.reasonAction.set(null);
        this.notifyCancel(result.refundRequired);
        this.reload();
      },
      error: () => {
        this.busy.set(false);
        this.reasonAction.set(null);
      },
    });
  }

  protected statusLabel(status: number): string {
    return ORDER_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected statusClass(status: number): string {
    return ORDER_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }

  protected paymentMethodLabel(method: number): string {
    return PAYMENT_METHOD_LABELS[method] ?? '—';
  }

  protected paymentStatusLabel(status: number): string {
    return PAYMENT_STATUS_LABELS[status] ?? '—';
  }

  protected fulfillmentLabel(type: number): string {
    return FULFILLMENT_TYPE_LABELS[type] ?? '—';
  }

  protected channelLabel(channel: number): string {
    return ORDER_CHANNEL_LABELS[channel] ?? '—';
  }

  protected serialStatusLabel(status: number): string {
    return SERIAL_STATUS_LABELS[status] ?? '—';
  }

  /** Nạp lại đơn sau khi thao tác (resolver chỉ chạy một lần khi vào route). */
  private reload(): void {
    this.orderApi.getOrder(this.header().id).subscribe({
      next: (detail) => {
        this.refreshed.set(detail);
        this.busy.set(false);
      },
      error: () => this.busy.set(false),
    });
  }

  private run(request$: ReturnType<OrderApi['confirmOrder']>, successMessage: string): void {
    this.busy.set(true);

    request$.subscribe({
      next: () => {
        this.notifications.success(successMessage, this.header().orderCode);
        this.reload();
      },
      error: () => this.busy.set(false),
    });
  }

  private notifyCancel(refundRequired: boolean): void {
    if (refundRequired) {
      this.notifications.warning(
        'Đơn đã hủy nhưng đã thu tiền',
        'Cần hoàn tiền cho khách qua POST /api/v1/orders/{id}/refunds.',
      );
    } else {
      this.notifications.success('Đã hủy đơn', this.header().orderCode);
    }
  }
}
