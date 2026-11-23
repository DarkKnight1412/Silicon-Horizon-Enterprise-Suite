import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import {
  APPROVER_ROLES,
  ROLES,
  SALES_ROLES,
  WAREHOUSE_ROLES,
  type RoleCode,
} from '../../../core/models/auth.model';
import { formatDateTime, formatVnd } from '../../../core/models/common.model';
import {
  ORDER_STATUS_CLASSES,
  ORDER_STATUS_LABELS,
  ORDER_STATUS_OPTIONS,
  PAYMENT_METHOD_LABELS,
  PAYMENT_STATUS_LABELS,
  canStaffCancel,
  type OrderListItemDto,
  type OrderListQuery,
} from '../../../core/models/order.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { dateRangeValidator } from '../../../shared/validators/date-range.validator';
import { OrderApi } from '../../orders/order-api.service';
import { StaffOrderApi } from '../staff-order-api.service';

/** Thao tác cần lý do (bắt buộc theo ORD-08). */
type ReasonAction = 'cancel-approved' | 'delivery-failed';

/**
 * TN/TV/TK/AD — ghi nhận giao thất bại, giao lại, hoàn tất đơn.
 * Khớp `OrderRoleGroups.Delivery` = `SalesDesk` (TN+TV) + `Storekeeper` + `Admin`.
 */
const DELIVERY_ROLES: readonly RoleCode[] = [
  ROLES.Cashier,
  ROLES.TechAdvisor,
  ROLES.Storekeeper,
  ROLES.Admin,
];

/** Đang mở hộp thoại lý do cho đơn nào. */
interface ReasonTarget {
  id: number;
  orderCode: string;
  action: ReasonAction;
}

/**
 * Danh sách đơn của CHI NHÁNH cho nhân viên — GET /api/v1/staff/orders (usp_Order_List, ép `branchId`
 * từ token) kèm các bước chuyển trạng thái như màn quản trị.
 *
 * Nút thao tác chỉ hiện khi ĐÚNG vai trò VÀ ĐÚNG trạng thái mà backend cho phép
 * (`OrderRoleGroups` + `OrderDetail` đang dùng cùng quy tắc), tránh người dùng bấm rồi nhận 403/409:
 * - Xác nhận đơn (Pending → Confirmed): TN/TV/QL/AD.
 * - Đóng gói / đóng gói xong / giao lại: TK/AD.
 * - Hoàn tất / giao thất bại: TN/TV/TK/AD.
 * - Hủy đơn đã xác nhận: QL/AD (bắt buộc lý do).
 *
 * `toDate` của backend là chặn trên KHÔNG bao gồm nên màn hình chuyển ngày người dùng chọn thành
 * 23:59:59 UTC để bao trọn ngày cuối.
 */
@Component({
  selector: 'app-staff-order-list',
  imports: [ReactiveFormsModule, RouterLink, Pagination, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './staff-order-list.html',
})
export class StaffOrderList {
  private readonly staffOrderApi = inject(StaffOrderApi);
  private readonly orderApi = inject(OrderApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly statusOptions = ORDER_STATUS_OPTIONS;
  protected readonly formatPrice = formatVnd;
  protected readonly formatMoment = formatDateTime;

  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly result = signal<PagedResult<OrderListItemDto>>(
    emptyPagedResult<OrderListItemDto>(20),
  );

  /** Đơn đang chờ nhập lý do (hủy đã xác nhận / giao thất bại). */
  protected readonly reasonTarget = signal<ReasonTarget | null>(null);

  protected readonly reason = new FormControl('', {
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

  /** POS tạo đơn tại quầy chỉ dành cho nhóm bán hàng (TN/TV/QL/AD). */
  protected readonly canUsePos = computed(() => this.authStore.hasAnyRole(SALES_ROLES));

  constructor() {
    this.load(1, this.result().pageSize);
  }

  protected load(pageNumber: number, pageSize: number): void {
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

    this.staffOrderApi.listBranchOrders(query).subscribe({
      next: (page) => {
        this.result.set(page);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected resetFilters(): void {
    this.filters.reset({ status: null, fromDate: '', toDate: '', keyword: '' });
    this.load(1, this.result().pageSize);
  }

  protected reload(): void {
    this.load(this.result().pageNumber, this.result().pageSize);
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

  // ───────────────────────────────────────────────────────────────────────────
  // Quyền thao tác theo vai trò + trạng thái (khớp backend, xem docblock của class)
  // ───────────────────────────────────────────────────────────────────────────

  protected canConfirm(order: OrderListItemDto): boolean {
    return this.authStore.hasAnyRole(SALES_ROLES) && order.status === 1;
  }

  protected canStartPacking(order: OrderListItemDto): boolean {
    return this.authStore.hasAnyRole(WAREHOUSE_ROLES) && order.status === 2;
  }

  protected canMarkReady(order: OrderListItemDto): boolean {
    return this.authStore.hasAnyRole(WAREHOUSE_ROLES) && order.status === 3;
  }

  protected canComplete(order: OrderListItemDto): boolean {
    return this.authStore.hasAnyRole(DELIVERY_ROLES) && (order.status === 4 || order.status === 5);
  }

  protected canRetryDelivery(order: OrderListItemDto): boolean {
    return this.authStore.hasAnyRole(WAREHOUSE_ROLES) && order.status === 6;
  }

  protected canCancelApproved(order: OrderListItemDto): boolean {
    return this.authStore.hasAnyRole(APPROVER_ROLES) && canStaffCancel(order.status);
  }

  protected hasAnyAction(order: OrderListItemDto): boolean {
    return (
      this.canConfirm(order) ||
      this.canStartPacking(order) ||
      this.canMarkReady(order) ||
      this.canComplete(order) ||
      this.canRetryDelivery(order) ||
      this.canCancelApproved(order)
    );
  }

  // ───────────────────────────────────────────────────────────────────────────
  // Thao tác
  // ───────────────────────────────────────────────────────────────────────────

  protected confirmOrder(order: OrderListItemDto): void {
    this.run(order, this.orderApi.confirmOrder(order.id), 'Đã xác nhận đơn');
  }

  protected startPacking(order: OrderListItemDto): void {
    this.run(order, this.orderApi.startPacking(order.id), 'Đã bắt đầu đóng gói');
  }

  protected markReady(order: OrderListItemDto): void {
    this.run(order, this.orderApi.markReady(order.id), 'Đơn đã sẵn sàng giao / chờ khách nhận');
  }

  protected complete(order: OrderListItemDto): void {
    this.run(order, this.orderApi.complete(order.id), 'Đơn đã hoàn tất');
  }

  protected retryDelivery(order: OrderListItemDto): void {
    this.run(order, this.orderApi.retryDelivery(order.id), 'Đã ghi nhận giao lại');
  }

  protected openReason(order: OrderListItemDto, action: ReasonAction): void {
    this.reason.reset('');
    this.reasonTarget.set({ id: order.id, orderCode: order.orderCode, action });
  }

  protected closeReason(): void {
    this.reasonTarget.set(null);
  }

  /** Gửi hành động kèm lý do (hủy đơn đã xác nhận của QL/AD, hoặc ghi nhận giao thất bại). */
  protected async submitReason(): Promise<void> {
    const target = this.reasonTarget();

    if (target === null) {
      return;
    }

    if (this.reason.invalid) {
      this.reason.markAsTouched();
      return;
    }

    const reason = this.reason.value.trim();

    const accepted = await this.confirm.ask({
      title: target.action === 'delivery-failed' ? 'Ghi nhận giao thất bại' : 'Hủy đơn đã xác nhận',
      message: `${target.orderCode} — lý do: "${reason}"`,
      confirmLabel: 'Xác nhận',
      danger: target.action === 'cancel-approved',
    });

    if (!accepted) {
      return;
    }

    if (target.action === 'delivery-failed') {
      this.busy.set(true);

      this.orderApi.markDeliveryFailed(target.id, { reason }).subscribe({
        next: () => {
          this.busy.set(false);
          this.reasonTarget.set(null);
          this.notifications.success('Đã ghi nhận giao thất bại', target.orderCode);
          this.reload();
        },
        error: () => this.busy.set(false),
      });

      return;
    }

    this.busy.set(true);

    this.orderApi.cancelApproved(target.id, { reason }).subscribe({
      next: (result) => {
        this.busy.set(false);
        this.reasonTarget.set(null);

        if (result.refundRequired) {
          this.notifications.warning(
            'Đơn đã hủy nhưng đã thu tiền',
            'Cần hoàn tiền thủ công cho khách qua POST /api/v1/orders/{id}/refunds.',
          );
        } else {
          this.notifications.success('Đã hủy đơn', target.orderCode);
        }

        this.reload();
      },
      error: () => this.busy.set(false),
    });
  }

  /** Chạy một bước chuyển trạng thái không cần lý do rồi nạp lại danh sách. */
  private run(
    order: OrderListItemDto,
    request$: ReturnType<OrderApi['confirmOrder']>,
    successMessage: string,
  ): void {
    this.busy.set(true);

    request$.subscribe({
      next: () => {
        this.busy.set(false);
        this.notifications.success(successMessage, order.orderCode);
        this.reload();
      },
      error: () => this.busy.set(false),
    });
  }
}

/** 'YYYY-MM-DD' → ISO mốc đầu ngày UTC; rỗng → null. */
function toIsoStart(value: string): string | null {
  return value.length === 0 ? null : new Date(`${value}T00:00:00.000Z`).toISOString();
}

/** 'YYYY-MM-DD' → ISO 23:59:59 UTC vì backend coi `toDate` là chặn trên KHÔNG bao gồm. */
function toIsoEnd(value: string): string | null {
  return value.length === 0 ? null : new Date(`${value}T23:59:59.000Z`).toISOString();
}
