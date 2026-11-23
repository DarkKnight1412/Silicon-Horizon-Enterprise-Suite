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
import { formatDateTime, formatVnd } from '../../../core/models/common.model';
import { PAYMENT_METHOD_LABELS } from '../../../core/models/order.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { priceValidator } from '../../../shared/validators/price.validator';
import { ReturnApi } from '../return-api.service';
import {
  INSPECTION_RESULT,
  INSPECTION_RESULT_CLASSES,
  INSPECTION_RESULT_LABELS,
  RETURN_STATUS,
  RETURN_STATUS_CLASSES,
  RETURN_STATUS_LABELS,
  type InspectReturnItemInput,
  type RefundReturnRequest,
  type ReturnRequestDetailDto,
} from '../return.model';

/** Nhóm control của một dòng kiểm tra hàng trả. */
type InspectLineGroup = FormGroup<{
  returnItemId: FormControl<number | null>;
  sku: FormControl<string>;
  productName: FormControl<string>;
  serialNumber: FormControl<string>;
  quantity: FormControl<number>;
  result: FormControl<number | null>;
}>;

/**
 * Chi tiết yêu cầu đổi trả + 3 bước xử lý (Database/README.md mục 7.2):
 * - `POST /returns/{id}/review`  — QL/AD duyệt hoặc từ chối (từ chối bắt buộc lý do), chỉ khi Chờ duyệt.
 * - `POST /returns/{id}/inspect` — TK/AD nhận hàng & kiểm tra TẤT CẢ dòng, chỉ khi Đã duyệt.
 * - `POST /returns/{id}/refund`  — QL/AD hoàn tiền, chỉ sau khi đã Nhận hàng & kiểm tra.
 *
 * Proc hoàn tiền cập nhật đơn (Trả một phần / Đã trả hết) và THU HỒI HOA HỒNG theo tỷ lệ số tiền đã
 * hoàn (KPI-04) — màn hình nhắc rõ để người duyệt biết hệ quả.
 */
@Component({
  selector: 'app-return-detail',
  imports: [ReactiveFormsModule, RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './return-detail.html',
})
export class ReturnDetail implements OnInit {
  private readonly returnApi = inject(ReturnApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly id = input('');

  protected readonly returnStatus = RETURN_STATUS;
  protected readonly inspectionResult = INSPECTION_RESULT;
  protected readonly formatMoment = formatDateTime;
  protected readonly formatPrice = formatVnd;
  protected readonly statusLabels = RETURN_STATUS_LABELS;
  protected readonly statusClasses = RETURN_STATUS_CLASSES;
  protected readonly inspectionLabels = INSPECTION_RESULT_LABELS;
  protected readonly inspectionClasses = INSPECTION_RESULT_CLASSES;
  protected readonly methodLabels = PAYMENT_METHOD_LABELS;

  /** Lựa chọn phương thức hoàn tiền — dùng chung quy ước `PaymentMethod` của đơn. */
  protected readonly methodOptions = Object.entries(PAYMENT_METHOD_LABELS).map(([value, label]) => ({
    value: Number(value),
    label,
  }));

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly detail = signal<ReturnRequestDetailDto | null>(null);

  protected readonly inspectLines = new FormArray<InspectLineGroup>([]);

  protected readonly reviewForm = this.fb.group({
    note: this.fb.control('', { validators: [Validators.maxLength(300)] }),
  });

  protected readonly refundForm = this.fb.group({
    method: this.fb.control<number | null>(null, { validators: [Validators.required] }),
    amount: this.fb.control<number | null>(null, {
      validators: [priceValidator({ min: 0, max: 1_000_000_000_000 })],
    }),
  });

  private readonly status = computed(() => this.detail()?.request.status ?? null);

  protected readonly canReview = computed(
    () => this.status() === RETURN_STATUS.Requested && this.authStore.hasAnyRole(APPROVER_ROLES),
  );
  protected readonly canInspect = computed(
    () => this.status() === RETURN_STATUS.Approved && this.authStore.hasAnyRole(WAREHOUSE_ROLES),
  );
  protected readonly canRefund = computed(
    () => this.status() === RETURN_STATUS.GoodsReceived && this.authStore.hasAnyRole(APPROVER_ROLES),
  );

  private returnId = 0;

  ngOnInit(): void {
    const parsed = Number(this.id());

    if (!Number.isFinite(parsed) || parsed <= 0) {
      this.loading.set(false);
      return;
    }

    this.returnId = parsed;
    this.reload();
  }

  protected statusLabel(status: number): string {
    return this.statusLabels[status] ?? 'Không xác định';
  }

  protected statusClass(status: number): string {
    return this.statusClasses[status] ?? 'bg-slate-200 text-slate-700';
  }

  protected inspectionLabel(result: number): string {
    return this.inspectionLabels[result] ?? 'Không xác định';
  }

  protected inspectionClass(result: number): string {
    return this.inspectionClasses[result] ?? 'bg-slate-200 text-slate-700';
  }

  /** QL/AD duyệt hoặc từ chối yêu cầu (từ chối bắt buộc có lý do — ghi nhật ký AUD-01). */
  protected async review(approve: boolean): Promise<void> {
    if (this.reviewForm.invalid) {
      this.reviewForm.markAsTouched();
      return;
    }

    const note = this.reviewForm.controls.note.value.trim();

    if (!approve && note.length === 0) {
      this.notifications.warning('Thiếu lý do từ chối', 'Nhập lý do trước khi từ chối yêu cầu.');
      return;
    }

    const accepted = await this.confirm.ask({
      title: approve ? 'Duyệt yêu cầu đổi trả' : 'Từ chối yêu cầu đổi trả',
      message: approve
        ? 'Yêu cầu chuyển sang Đã duyệt; thủ kho sẽ nhận hàng và kiểm tra từng dòng/serial.'
        : `Yêu cầu chuyển sang Đã từ chối. Lý do: "${note}"`,
      confirmLabel: approve ? 'Duyệt' : 'Từ chối',
      danger: !approve,
    });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    this.returnApi.review(this.returnId, { approve, note: note.length > 0 ? note : null }).subscribe({
      next: () => {
        this.busy.set(false);
        this.notifications.success(approve ? 'Đã duyệt yêu cầu' : 'Đã từ chối yêu cầu');
        this.reload();
      },
      error: () => this.busy.set(false),
    });
  }

  /** TK/AD nhận hàng và kiểm tra từng dòng — phải nhập kết quả cho TẤT CẢ dòng. */
  protected async inspect(): Promise<void> {
    this.inspectLines.markAllAsTouched();

    if (this.inspectLines.length === 0 || this.inspectLines.invalid) {
      this.notifications.warning('Chưa đủ kết quả kiểm tra', 'Chọn Đạt/Không đạt cho mọi dòng hàng.');
      return;
    }

    const results: InspectReturnItemInput[] = [];

    for (const line of this.inspectLines.controls) {
      const value = line.getRawValue();

      if (value.returnItemId === null || value.result === null) {
        return;
      }

      results.push({ returnItemId: value.returnItemId, result: value.result });
    }

    const failed = results.filter((item) => item.result === INSPECTION_RESULT.Fail).length;

    const accepted = await this.confirm.ask({
      title: 'Xác nhận kiểm tra hàng trả',
      message:
        failed > 0
          ? `${results.length} dòng, trong đó ${failed} dòng KHÔNG đạt: serial không đạt chuyển sang Hàng lỗi và không vào tồn bán được. Tiếp tục?`
          : `${results.length} dòng đều Đạt: serial về Trong kho và được cộng lại tồn. Tiếp tục?`,
      confirmLabel: 'Xác nhận',
      danger: failed > 0,
    });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    this.returnApi.inspect(this.returnId, { results }).subscribe({
      next: () => {
        this.busy.set(false);
        this.notifications.success('Đã ghi nhận kiểm tra hàng trả');
        this.reload();
      },
      error: () => this.busy.set(false),
    });
  }

  /** QL/AD hoàn tiền — chỉ sau khi hàng đã được nhận & kiểm tra (GoodsReceived). */
  protected async refund(): Promise<void> {
    const detail = this.detail();

    if (detail === null) {
      return;
    }

    if (this.refundForm.invalid) {
      this.refundForm.markAsTouched();
      return;
    }

    const value = this.refundForm.getRawValue();

    if (value.method === null) {
      return;
    }

    const amount = value.amount;

    const accepted = await this.confirm.ask({
      title: 'Hoàn tiền cho khách',
      message: `Hoàn ${amount === null ? 'đúng số dự kiến' : formatVnd(amount)} cho ${detail.request.returnCode}. Đơn chuyển sang Trả một phần / Đã trả hết và hoa hồng bị thu hồi theo tỷ lệ.`,
      confirmLabel: 'Hoàn tiền',
      danger: true,
    });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    const request: RefundReturnRequest = { method: value.method, amount };

    this.returnApi.refund(this.returnId, request).subscribe({
      next: (result) => {
        this.busy.set(false);
        this.notifications.success(
          'Đã hoàn tiền',
          `${result.orderStatusName} · tiền mặt ${formatVnd(result.cashRefunded)} · giảm nợ ${formatVnd(result.debtReduced)}`,
        );
        this.reload();
      },
      error: () => this.busy.set(false),
    });
  }

  private reload(): void {
    this.loading.set(true);

    this.returnApi.getReturn(this.returnId).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.buildInspectForm(detail);
        this.loading.set(false);
      },
      error: () => {
        this.detail.set(null);
        this.loading.set(false);
      },
    });
  }

  /** Dựng form kiểm tra từ các dòng hàng trả (mặc định theo kết quả đã lưu, 0 = chưa kiểm tra). */
  private buildInspectForm(detail: ReturnRequestDetailDto): void {
    this.inspectLines.clear();
    this.reviewForm.reset({ note: '' });
    this.refundForm.reset({ method: null, amount: null });

    for (const item of detail.items) {
      this.inspectLines.push(
        this.fb.group({
          returnItemId: this.fb.control<number | null>(item.id),
          sku: this.fb.control(item.sku),
          productName: this.fb.control(item.productName),
          serialNumber: this.fb.control(item.serialNumber ?? ''),
          quantity: this.fb.control(item.quantity),
          result: this.fb.control<number | null>(
            item.inspectionResult === 0 ? null : item.inspectionResult,
            { validators: [Validators.required] },
          ),
        }),
      );
    }
  }
}
