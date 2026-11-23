import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLES } from '../../../core/models/auth.model';
import type { BranchDto } from '../../../core/models/category.model';
import { formatNumber } from '../../../core/models/common.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { dateRangeValidator } from '../../../shared/validators/date-range.validator';
import { AdminApi } from '../../admin/admin-api.service';
import { ReportApi } from '../report-api.service';
import { ReportBranchSelect } from '../branch-select/report-branch-select';
import {
  WARRANTY_STATUS_CLASSES,
  firstDayOfMonth,
  today,
  type WarrantyReportDto,
} from '../report.model';

/**
 * Báo cáo bảo hành — GET /api/v1/reports/warranty?from=&to=&branchId= (usp_Report_Warranty,
 * Database/README.md mục 9).
 *
 * `from`/`to` BẮT BUỘC và tính theo ngày NHẬN phiếu (`WarrantyTickets.ReceivedAt`), bao gồm cả ngày
 * `to`. Kết quả gồm 3 nhóm: số phiếu theo trạng thái, top 20 sản phẩm lỗi nhiều (kèm số đã bán để
 * tính tỷ lệ lỗi) và thời gian xử lý trung bình (`null` khi chưa có phiếu nào hoàn tất).
 *
 * Quyền: QL (chi nhánh mình — backend ép), AD (chọn được chi nhánh).
 */
@Component({
  selector: 'app-warranty-report',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, ReportBranchSelect],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './warranty-report.html',
})
export class WarrantyReport {
  private readonly reportApi = inject(ReportApi);
  private readonly adminApi = inject(AdminApi);
  private readonly authStore = inject(AuthStore);

  protected readonly formatCount = formatNumber;
  protected readonly statusClasses = WARRANTY_STATUS_CLASSES;

  protected readonly loading = signal(false);
  protected readonly report = signal<WarrantyReportDto | null>(null);
  protected readonly branches = signal<readonly BranchDto[]>([]);

  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));

  protected readonly filters = new FormGroup(
    {
      fromDate: new FormControl(firstDayOfMonth(), {
        nonNullable: true,
        validators: [Validators.required],
      }),
      toDate: new FormControl(today(), { nonNullable: true, validators: [Validators.required] }),
    },
    { validators: [dateRangeValidator('fromDate', 'toDate')] },
  );

  protected readonly branchId = new FormControl<number | null>(null);

  /** Tổng số phiếu bảo hành tiếp nhận trong kỳ. */
  protected readonly totalTickets = computed(() =>
    (this.report()?.statuses ?? []).reduce((sum, row) => sum + row.tickets, 0),
  );

  constructor() {
    this.load();

    if (this.isAdmin()) {
      this.loadBranches();
    }
  }

  protected load(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }

    const value = this.filters.getRawValue();

    this.loading.set(true);

    this.reportApi
      .getWarranty({
        fromDate: value.fromDate,
        toDate: value.toDate,
        branchId: this.isAdmin() ? this.branchId.value : null,
      })
      .subscribe({
        next: (report) => {
          this.report.set(report);
          this.loading.set(false);
        },
        error: () => {
          this.report.set(null);
          this.loading.set(false);
        },
      });
  }

  protected resetFilters(): void {
    this.filters.reset({ fromDate: firstDayOfMonth(), toDate: today() });
    this.branchId.setValue(null);
    this.load();
  }

  /** Tỷ lệ lỗi (%) = số phiếu bảo hành / số lượng đã bán (0 khi chưa bán được chiếc nào). */
  protected defectRate(tickets: number, soldQuantity: number): string {
    if (soldQuantity <= 0) {
      return '—';
    }

    return `${((tickets / soldQuantity) * 100).toFixed(2)}%`;
  }

  protected statusClass(status: number): string {
    return this.statusClasses[status] ?? 'bg-slate-200 text-slate-700';
  }

  private loadBranches(): void {
    this.adminApi.listBranches({ isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({
      next: (page) => this.branches.set(page.items),
      error: () => this.branches.set([]),
    });
  }
}
