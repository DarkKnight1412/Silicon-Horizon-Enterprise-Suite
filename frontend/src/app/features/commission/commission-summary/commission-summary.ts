import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLES } from '../../../core/models/auth.model';
import { isApiError } from '../../../core/models/api-error.model';
import { formatDateTime, formatVnd } from '../../../core/models/common.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { CommissionApi } from '../commission-api.service';
import {
  commissionEntryTypeLabel,
  commissionYearMonth,
  type AdjustCommissionRequest,
  type CommissionEmployeeDto,
  type CommissionEntryDto,
  type CommissionSummaryDto,
} from '../commission.model';

/**
 * Tổng hợp hoa hồng theo kỳ — GET /api/v1/commissions?yearMonth= (usp_Commission_Summary, 3 result set:
 * kỳ hoa hồng · tổng hợp theo nhân viên · chi tiết bút toán) kèm hai thao tác CHỈ Admin:
 * điều chỉnh hoa hồng (`POST /api/v1/admin/commissions/adjustments`) và chốt kỳ
 * (`POST /api/v1/admin/commission-periods/{id}/close`) — Database/README.md mục 8, KPI-01..06.
 *
 * Phạm vi: NV chỉ thấy hoa hồng của mình, QL chỉ thấy chi nhánh mình (API ép từ token) nên ô lọc
 * `employeeId` chỉ hiện với Admin. Kỳ đã chốt là bất biến — bút toán điều chỉnh mới sẽ tự rơi vào
 * kỳ đang mở kế tiếp.
 */
@Component({
  selector: 'app-commission-summary',
  imports: [ReactiveFormsModule, RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './commission-summary.html',
})
export class CommissionSummary {
  private readonly commissionApi = inject(CommissionApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly formatPrice = formatVnd;
  protected readonly formatMoment = formatDateTime;
  protected readonly entryTypeLabel = commissionEntryTypeLabel;

  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly summary = signal<CommissionSummaryDto | null>(null);

  protected readonly adjustOpen = signal(false);

  /** Chỉ Admin lọc theo nhân viên, điều chỉnh và chốt kỳ (`CommissionRoles.Admin`). */
  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));

  protected readonly employees = signal<readonly CommissionEmployeeDto[]>([]);

  /** Lọc cục bộ bảng bút toán theo nhân viên đang chọn trong bảng tổng hợp (không gọi lại API). */
  protected readonly entryEmployeeId = signal<number | null>(null);

  protected readonly filters = new FormGroup({
    yearMonth: new FormControl(commissionYearMonth(new Date()), {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\d{4}-(0[1-9]|1[0-2])$/)],
    }),
    employeeId: new FormControl<number | null>(null),
  });

  protected readonly adjustForm = new FormGroup({
    employeeId: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    orderId: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    amount: new FormControl<number | null>(null, { validators: [Validators.required] }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  /** Bút toán sau khi lọc theo nhân viên (nếu chọn). */
  protected readonly entryRows = computed<readonly CommissionEntryDto[]>(() => {
    const entries = this.summary()?.entries ?? [];
    const employeeId = this.entryEmployeeId();

    return employeeId === null ? entries : entries.filter((entry) => entry.employeeId === employeeId);
  });

  constructor() {
    this.load();

    if (this.isAdmin()) {
      this.loadEmployees();
    }
  }

  protected load(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }

    const value = this.filters.getRawValue();

    this.loading.set(true);

    this.commissionApi
      .getSummary({
        yearMonth: value.yearMonth,
        employeeId: this.isAdmin() ? value.employeeId : null,
      })
      .subscribe({
        next: (summary) => {
          this.summary.set(summary);
          this.loading.set(false);
        },
        error: () => {
          this.summary.set(null);
          this.loading.set(false);
        },
      });
  }

  protected onEntryEmployeeChange(event: Event): void {
    const target = event.target;

    if (!(target instanceof HTMLSelectElement)) {
      return;
    }

    const parsed = Number(target.value);

    this.entryEmployeeId.set(target.value.length === 0 || !Number.isFinite(parsed) ? null : parsed);
  }

  protected employeeLabel(employee: CommissionEmployeeDto): string {
    return `${employee.employeeCode} · ${employee.fullName} (${employee.positionName})`;
  }

  /** Chốt kỳ hoa hồng (chỉ Admin) — sau khi chốt, kỳ không điều chỉnh được nữa. */
  protected async closePeriod(): Promise<void> {
    const period = this.summary()?.period ?? null;

    if (period === null) {
      return;
    }

    const accepted = await this.confirm.ask({
      title: 'Chốt kỳ hoa hồng',
      message: `Chốt kỳ ${period.yearMonth}? Sau khi chốt, mọi bút toán mới sẽ rơi vào kỳ đang mở kế tiếp và kỳ này không sửa được nữa.`,
      confirmLabel: 'Chốt kỳ',
      danger: true,
    });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    this.commissionApi.closePeriod(period.periodId).subscribe({
      next: () => {
        this.busy.set(false);
        this.notifications.success('Đã chốt kỳ hoa hồng', period.yearMonth);
        this.load();
      },
      error: (error: unknown) => this.reportError(error),
    });
  }

  protected openAdjust(): void {
    this.adjustForm.reset({ employeeId: null, orderId: null, amount: null, reason: '' });
    this.adjustOpen.set(true);
  }

  protected closeAdjust(): void {
    this.adjustOpen.set(false);
  }

  /** Tạo bút toán điều chỉnh hoa hồng (Admin) — số tiền phải khác 0 và có lý do. */
  protected submitAdjust(): void {
    if (this.adjustForm.invalid) {
      this.adjustForm.markAllAsTouched();
      return;
    }

    const value = this.adjustForm.getRawValue();

    if (value.employeeId === null || value.orderId === null || value.amount === null) {
      return;
    }

    if (value.amount === 0) {
      this.notifications.warning('Số tiền không hợp lệ', 'Số tiền điều chỉnh phải khác 0.');
      return;
    }

    const request: AdjustCommissionRequest = {
      employeeId: value.employeeId,
      orderId: value.orderId,
      amount: value.amount,
      reason: value.reason.trim(),
    };

    this.busy.set(true);

    this.commissionApi.adjust(request).subscribe({
      next: (result) => {
        this.busy.set(false);
        this.adjustOpen.set(false);
        this.notifications.success('Đã tạo bút toán điều chỉnh', `Mã bút toán #${result.entryId}`);
        this.load();
      },
      error: (error: unknown) => this.reportError(error),
    });
  }

  private reportError(error: unknown): void {
    this.busy.set(false);

    if (isApiError(error)) {
      this.notifications.fromApiError(error);
    }
  }

  private loadEmployees(): void {
    this.commissionApi.listEmployees(this.authStore.account()?.branchId ?? null).subscribe({
      next: (page) => this.employees.set(page.items),
      error: () => this.employees.set([]),
    });
  }
}
