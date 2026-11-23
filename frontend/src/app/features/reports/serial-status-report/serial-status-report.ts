import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLES } from '../../../core/models/auth.model';
import type { BranchDto } from '../../../core/models/category.model';
import { formatNumber } from '../../../core/models/common.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { AdminApi } from '../../admin/admin-api.service';
import { ReportApi } from '../report-api.service';
import { ReportBranchSelect } from '../branch-select/report-branch-select';
import { SERIAL_STATUS_CLASSES, type SerialStatusCountDto } from '../report.model';

/**
 * Báo cáo serial theo trạng thái — GET /api/v1/reports/serial-status?productId=&branchId=
 * (usp_Report_SerialStatus, Database/README.md mục 9).
 *
 * Proc CHỈ trả các trạng thái đang có serial, nên trạng thái không có serial nào sẽ vắng mặt trong
 * kết quả (màn hình hiển thị đúng những dòng backend trả về, không tự bịa số 0).
 *
 * Quyền: TK, QL (bị ép về chi nhánh trong token), AD (chọn được chi nhánh).
 */
@Component({
  selector: 'app-serial-status-report',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, ReportBranchSelect],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './serial-status-report.html',
})
export class SerialStatusReport {
  private readonly reportApi = inject(ReportApi);
  private readonly adminApi = inject(AdminApi);
  private readonly authStore = inject(AuthStore);

  protected readonly formatCount = formatNumber;
  protected readonly statusClasses = SERIAL_STATUS_CLASSES;

  protected readonly loading = signal(false);
  protected readonly rows = signal<readonly SerialStatusCountDto[]>([]);
  protected readonly branches = signal<readonly BranchDto[]>([]);

  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));

  protected readonly filters = new FormGroup({
    productId: new FormControl<number | null>(null, { validators: [Validators.min(1)] }),
  });

  protected readonly branchId = new FormControl<number | null>(null);

  /** Tổng số serial đang có theo trạng thái (cho mọi sản phẩm nếu không lọc). */
  protected readonly totalQuantity = computed(() =>
    this.rows().reduce((sum, row) => sum + row.quantity, 0),
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

    this.loading.set(true);

    this.reportApi
      .getSerialStatus({
        productId: this.filters.controls.productId.value,
        branchId: this.isAdmin() ? this.branchId.value : null,
      })
      .subscribe({
        next: (rows) => {
          this.rows.set(rows);
          this.loading.set(false);
        },
        error: () => {
          this.rows.set([]);
          this.loading.set(false);
        },
      });
  }

  protected resetFilters(): void {
    this.filters.reset({ productId: null });
    this.branchId.setValue(null);
    this.load();
  }

  /** Tỷ lệ % của một trạng thái trên tổng số serial đang có. */
  protected sharePercent(quantity: number): number {
    const total = this.totalQuantity();

    return total === 0 ? 0 : Math.round((quantity / total) * 100);
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
