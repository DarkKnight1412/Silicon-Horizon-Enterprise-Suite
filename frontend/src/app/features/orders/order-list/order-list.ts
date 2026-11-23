import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { formatDateTime, formatVnd } from '../../../core/models/common.model';
import {
  ORDER_STATUS_CLASSES,
  ORDER_STATUS_LABELS,
  ORDER_STATUS_OPTIONS,
  PAYMENT_METHOD_LABELS,
  PAYMENT_STATUS_LABELS,
  canCustomerCancel,
  type OrderListItemDto,
  type OrderListQuery,
} from '../../../core/models/order.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { dateRangeValidator } from '../../../shared/validators/date-range.validator';
import { OrderApi } from '../order-api.service';

/**
 * Danh sách đơn của khách đang đăng nhập (GET /api/v1/orders — backend ép CustomerId từ token).
 *
 * `toDate` của backend là CHẶN TRÊN KHÔNG BAO GỒM nên màn hình tự chuyển ngày người dùng chọn
 * thành 23:59:59 UTC của ngày đó để bao trọn ngày cuối cùng.
 */
@Component({
  selector: 'app-order-list',
  imports: [ReactiveFormsModule, RouterLink, Pagination, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './order-list.html',
})
export class OrderListComponent {
  private readonly orderApi = inject(OrderApi);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly statusOptions = ORDER_STATUS_OPTIONS;
  protected readonly formatPrice = formatVnd;
  protected readonly formatMoment = formatDateTime;
  protected readonly loading = signal(false);
  protected readonly result = signal<PagedResult<OrderListItemDto>>(
    emptyPagedResult<OrderListItemDto>(environment.defaultPageSize),
  );

  /** Đơn đang mở hộp thoại hủy (null = đang đóng). */
  protected readonly cancelTarget = signal<OrderListItemDto | null>(null);

  protected readonly cancelReason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(300)],
  });

  protected readonly filters = new FormGroup(
    {
      status: new FormControl<number | null>(null),
      fromDate: new FormControl('', { nonNullable: true }),
      toDate: new FormControl('', { nonNullable: true }),
      keyword: new FormControl('', { nonNullable: true }),
    },
    { validators: [dateRangeValidator('fromDate', 'toDate')] },
  );

  constructor() {
    this.search(1, this.result().pageSize);
  }

  protected search(pageNumber: number, pageSize: number): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }

    const value = this.filters.getRawValue();
    const keyword = value.keyword.trim();

    const query: OrderListQuery = {
      status: value.status,
      fromDate: toIsoStart(value.fromDate),
      toDate: toIsoEnd(value.toDate),
      keyword: keyword.length > 0 ? keyword : null,
      pageNumber,
      pageSize,
    };

    this.loading.set(true);

    this.orderApi.listMyOrders(query).subscribe({
      next: (page) => {
        this.result.set(page);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected resetFilters(): void {
    this.filters.reset({ status: null, fromDate: '', toDate: '', keyword: '' });
    this.search(1, this.result().pageSize);
  }

  protected onPageChange(pageNumber: number): void {
    this.search(pageNumber, this.result().pageSize);
  }

  protected onPageSizeChange(pageSize: number): void {
    this.search(1, pageSize);
  }

  protected openCancel(order: OrderListItemDto): void {
    this.cancelReason.reset('');
    this.cancelTarget.set(order);
  }

  protected closeCancel(): void {
    this.cancelTarget.set(null);
  }

  /** Hủy đơn ở trạng thái Chờ thanh toán / Chờ xác nhận (bắt buộc lý do — ORD-08). */
  protected async confirmCancel(): Promise<void> {
    const target = this.cancelTarget();

    if (target === null) {
      return;
    }

    if (this.cancelReason.invalid) {
      this.cancelReason.markAsTouched();
      return;
    }

    const accepted = await this.confirm.ask({
      title: 'Hủy đơn hàng',
      message: `Đơn ${target.orderCode} sẽ bị hủy và phần tồn đang giữ được nhả ra. Lý do: "${this.cancelReason.value}"`,
      confirmLabel: 'Hủy đơn',
      danger: true,
    });

    if (!accepted) {
      return;
    }

    this.orderApi.cancelOrder(target.id, { reason: this.cancelReason.value.trim() }).subscribe({
      next: (result) => {
        this.cancelTarget.set(null);

        if (result.refundRequired) {
          this.notifications.warning(
            'Đơn đã hủy nhưng đã thu tiền',
            'Cần hoàn tiền thủ công cho khách (POST /api/v1/orders/{id}/refunds).',
          );
        } else {
          this.notifications.success('Đã hủy đơn', target.orderCode);
        }

        this.search(this.result().pageNumber, this.result().pageSize);
      },
      error: () => this.cancelTarget.set(null),
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

  protected canCancel(order: OrderListItemDto): boolean {
    return canCustomerCancel(order.status);
  }
}

/** 'YYYY-MM-DD' → ISO mốc đầu ngày UTC; rỗng → null. */
function toIsoStart(value: string): string | null {
  return value.length === 0 ? null : new Date(`${value}T00:00:00.000Z`).toISOString();
}

/** 'YYYY-MM-DD' → ISO 23:59:59 UTC (bao trọn ngày cuối) vì backend coi toDate là chặn trên không bao gồm. */
function toIsoEnd(value: string): string | null {
  return value.length === 0 ? null : new Date(`${value}T23:59:59.000Z`).toISOString();
}
