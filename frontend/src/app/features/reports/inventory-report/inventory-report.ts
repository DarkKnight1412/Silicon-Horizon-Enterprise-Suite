import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLES } from '../../../core/models/auth.model';
import type { BranchDto } from '../../../core/models/category.model';
import { formatDateTime, formatNumber, formatVnd } from '../../../core/models/common.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { AdminApi } from '../../admin/admin-api.service';
import { ReportApi } from '../report-api.service';
import { ReportBranchSelect } from '../branch-select/report-branch-select';
import { type InventoryReportDto } from '../report.model';

/**
 * Báo cáo tồn kho — GET /api/v1/reports/inventory?slowDays=&branchId= (usp_Report_Inventory,
 * Database/README.md mục 9). Báo cáo KHÔNG có tham số ngày: giá trị tồn là số liệu hiện thời, còn
 * "hàng tồn lâu không xuất" tính theo `slowDays` (mặc định 90 ngày) so với lần xuất kho gần nhất.
 *
 * Quyền: TK, QL (bị ép về chi nhánh trong token), AD (chọn được chi nhánh).
 */
@Component({
  selector: 'app-inventory-report',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, ReportBranchSelect],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './inventory-report.html',
})
export class InventoryReport {
  private readonly reportApi = inject(ReportApi);
  private readonly adminApi = inject(AdminApi);
  private readonly authStore = inject(AuthStore);

  protected readonly formatPrice = formatVnd;
  protected readonly formatCount = formatNumber;
  protected readonly formatMoment = formatDateTime;

  protected readonly loading = signal(false);
  protected readonly report = signal<InventoryReportDto | null>(null);
  protected readonly branches = signal<readonly BranchDto[]>([]);

  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));

  protected readonly filters = new FormGroup({
    slowDays: new FormControl<number | null>(90, {
      validators: [Validators.required, Validators.min(1), Validators.max(3650)],
    }),
  });

  protected readonly branchId = new FormControl<number | null>(null);

  /** Tổng giá trị tồn & số lượng theo toàn bộ dòng của báo cáo. */
  protected readonly totals = computed(() => {
    const values = this.report()?.values ?? [];

    return values.reduce(
      (sum, row) => ({
        physicalQty: sum.physicalQty + row.physicalQty,
        reservedQty: sum.reservedQty + row.reservedQty,
        availableQty: sum.availableQty + row.availableQty,
        stockValue: sum.stockValue + row.stockValue,
      }),
      { physicalQty: 0, reservedQty: 0, availableQty: 0, stockValue: 0 },
    );
  });

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

    const slowDays = this.filters.controls.slowDays.value;

    this.loading.set(true);

    this.reportApi
      .getInventory({
        slowDays,
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
    this.filters.reset({ slowDays: 90 });
    this.branchId.setValue(null);
    this.load();
  }

  private loadBranches(): void {
    this.adminApi.listBranches({ isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({
      next: (page) => this.branches.set(page.items),
      error: () => this.branches.set([]),
    });
  }
}
