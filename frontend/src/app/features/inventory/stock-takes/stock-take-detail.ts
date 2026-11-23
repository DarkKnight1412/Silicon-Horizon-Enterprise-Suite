import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { APPROVER_ROLES, WAREHOUSE_ROLES } from '../../../core/models/auth.model';
import { formatDateTime } from '../../../core/models/common.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { InventoryApi } from '../inventory-api.service';
import {
  STOCK_TAKE_STATUS,
  STOCK_TAKE_STATUS_CLASSES,
  STOCK_TAKE_STATUS_LABELS,
  type StockTakeCountInput,
  type StockTakeDetailDto,
} from '../inventory.model';

/** Nhóm control của một dòng số đếm kiểm kê. */
type CountLineGroup = FormGroup<{
  productId: FormControl<number | null>;
  sku: FormControl<string>;
  name: FormControl<string>;
  requiresSerial: FormControl<boolean>;
  systemQty: FormControl<number>;
  countedQty: FormControl<number | null>;
  reason: FormControl<string>;
}>;

/**
 * Chi tiết đợt kiểm kê + nhập số đếm + duyệt (GET /api/v1/stock-takes/{id},
 * POST `{id}/counts`, POST `{id}/approve` — UC-15/UC-21, INV-04).
 *
 * Ràng buộc backend:
 * - Chỉ nhập số đếm ở trạng thái Đang kiểm kê (1); `finalize = true` chuyển sang Chờ duyệt (2) và
 *   KHOÁ việc nhập tiếp.
 * - Tồn CHƯA đổi ở bước nhập số đếm; chỉ khi Quản lý/Admin duyệt mới áp chênh lệch vào tồn.
 * - Nếu có dòng `requiresSerial` mà `diffQty ≠ 0`, proc TỪ CHỐI duyệt: phải xử lý từng serial bằng
 *   `POST /api/v1/serials/{id}/status` rồi kiểm kê lại — màn hình cảnh báo trước để tránh 409.
 */
@Component({
  selector: 'app-stock-take-detail',
  imports: [ReactiveFormsModule, RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stock-take-detail.html',
})
export class StockTakeDetail implements OnInit {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly id = input('');

  protected readonly formatMoment = formatDateTime;
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly detail = signal<StockTakeDetailDto | null>(null);

  protected readonly counts = new FormArray<CountLineGroup>([]);

  protected readonly approveNote = new FormControl('', {
    nonNullable: true,
    validators: [Validators.maxLength(300)],
  });

  private readonly status = computed(() => this.detail()?.header.status ?? null);

  /** Thủ kho/Admin nhập số đếm — chỉ khi đợt đang kiểm kê. */
  protected readonly canCount = computed(
    () => this.status() === STOCK_TAKE_STATUS.Counting && this.authStore.hasAnyRole(WAREHOUSE_ROLES),
  );

  /** Quản lý/Admin duyệt — chỉ khi đợt đang Chờ duyệt. */
  protected readonly canApprove = computed(
    () =>
      this.status() === STOCK_TAKE_STATUS.PendingApproval &&
      this.authStore.hasAnyRole(APPROVER_ROLES),
  );

  /** Có dòng hàng serial lệch ⇒ proc sẽ từ chối duyệt (INV-04). */
  protected readonly hasSerialDiff = computed(() =>
    (this.detail()?.items ?? []).some((item) => item.requiresSerial && item.diffQty !== 0),
  );

  ngOnInit(): void {
    const parsed = Number(this.id());

    if (!Number.isFinite(parsed) || parsed <= 0) {
      this.loading.set(false);
      return;
    }

    this.reload(parsed);
  }

  protected statusLabel(status: number): string {
    return STOCK_TAKE_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected statusClass(status: number): string {
    return STOCK_TAKE_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }

  /** Gửi số đếm; `finalize = true` để gửi duyệt và khoá nhập tiếp. */
  protected async submitCounts(finalize: boolean): Promise<void> {
    const detail = this.detail();

    if (detail === null) {
      return;
    }

    this.counts.markAllAsTouched();

    if (this.counts.invalid) {
      return;
    }

    const payload: StockTakeCountInput[] = [];

    for (const line of this.counts.controls) {
      const value = line.getRawValue();

      if (value.productId === null || value.countedQty === null) {
        return;
      }

      const reason = value.reason.trim();

      payload.push({
        productId: value.productId,
        countedQty: value.countedQty,
        reason: reason.length > 0 ? reason : null,
      });
    }

    // Chốt an toàn: mọi dòng đều 0 trong khi hệ thống có hàng ⇒ gần như chắc chắn là quên nhập.
    const suspicious =
      payload.length > 0 &&
      payload.every((item) => item.countedQty === 0) &&
      this.counts.controls.some((line) => line.controls.systemQty.value > 0);

    if (suspicious) {
      this.notifications.warning(
        'Chưa nhập số đếm nào',
        'Tất cả dòng đang là 0 trong khi tồn hệ thống khác 0. Kiểm tra lại trước khi gửi.',
      );
      return;
    }

    const accepted = await this.confirm.ask({
      title: finalize ? 'Gửi số đếm để duyệt' : 'Lưu số đếm',
      message: finalize
        ? 'Sau khi gửi duyệt, bạn KHÔNG nhập tiếp số đếm cho tới khi Quản lý xử lý.'
        : 'Số đếm được lưu ở trạng thái Đang kiểm kê, tồn chưa thay đổi.',
      confirmLabel: finalize ? 'Gửi duyệt' : 'Lưu',
    });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    this.inventoryApi
      .submitStockTakeCounts(detail.header.id, { counts: payload, finalize })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.notifications.success(
            finalize ? 'Đã gửi số đếm để duyệt' : 'Đã lưu số đếm',
            detail.header.stockTakeCode,
          );
          this.reload(detail.header.id);
        },
        error: () => this.busy.set(false),
      });
  }

  /** Duyệt (áp chênh lệch vào tồn) hoặc từ chối đợt kiểm kê — QL/AD. */
  protected async decide(approve: boolean): Promise<void> {
    const detail = this.detail();

    if (detail === null) {
      return;
    }

    if (this.approveNote.invalid) {
      this.approveNote.markAsTouched();
      return;
    }

    if (!approve && this.approveNote.value.trim().length === 0) {
      this.notifications.warning(
        'Thiếu lý do từ chối',
        'Lý do từ chối được proc ghi vào nhật ký AUD-01.',
      );
      return;
    }

    const accepted = await this.confirm.ask({
      title: approve ? 'Duyệt đợt kiểm kê' : 'Từ chối đợt kiểm kê',
      message: approve
        ? 'Chênh lệch của mọi dòng sẽ được áp vào tồn thực tế. Hành động này không hoàn tác được.'
        : 'Đợt kiểm kê chuyển sang Đã từ chối và KHÔNG thay đổi tồn.',
      confirmLabel: approve ? 'Duyệt' : 'Từ chối',
      danger: !approve,
    });

    if (!accepted) {
      return;
    }

    const note = this.approveNote.value.trim();

    this.busy.set(true);

    this.inventoryApi
      .approveStockTake(detail.header.id, { approve, note: note.length > 0 ? note : null })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.notifications.success(
            approve ? 'Đã duyệt đợt kiểm kê' : 'Đã từ chối đợt kiểm kê',
            detail.header.stockTakeCode,
          );
          this.reload(detail.header.id);
        },
        error: () => this.busy.set(false),
      });
  }

  private reload(id: number): void {
    this.loading.set(true);

    this.inventoryApi.getStockTake(id).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.buildCountsForm(detail);
        this.loading.set(false);
      },
      error: () => {
        this.detail.set(null);
        this.loading.set(false);
      },
    });
  }

  /** Dựng form số đếm từ danh sách dòng của đợt (khởi tạo bằng số đếm đã lưu, nếu có). */
  private buildCountsForm(detail: StockTakeDetailDto): void {
    this.counts.clear();
    this.approveNote.reset('');

    for (const item of detail.items) {
      this.counts.push(
        this.fb.group({
          productId: this.fb.control<number | null>(item.productId),
          sku: this.fb.control(item.sku),
          name: this.fb.control(item.name),
          requiresSerial: this.fb.control(item.requiresSerial),
          systemQty: this.fb.control(item.systemQty),
          countedQty: this.fb.control<number | null>(item.countedQty, {
            validators: [Validators.required, Validators.min(0)],
          }),
          reason: this.fb.control(item.reason ?? ''),
        }),
      );
    }
  }
}
