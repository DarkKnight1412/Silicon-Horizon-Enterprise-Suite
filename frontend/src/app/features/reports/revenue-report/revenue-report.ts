import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLES } from '../../../core/models/auth.model';
import type { BranchDto } from '../../../core/models/category.model';
import { formatNumber, formatVnd } from '../../../core/models/common.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { dateRangeValidator } from '../../../shared/validators/date-range.validator';
import { AdminApi } from '../../admin/admin-api.service';
import { ReportApi } from '../report-api.service';
import { ReportBranchSelect } from '../branch-select/report-branch-select';
import {
  REVENUE_GROUP_BY_OPTIONS,
  firstDayOfMonth,
  revenueGroupKeyLabel,
  today,
  type RevenueReportDto,
} from '../report.model';

/**
 * Báo cáo doanh thu — GET /api/v1/reports/revenue?from=&to=&groupBy=&branchId=
 * (usp_Report_Revenue — Database/README.md mục 9).
 *
 * Nghiệp vụ: `from`/`to` BẮT BUỘC, theo giờ Việt Nam và bao gồm cả ngày `to`; doanh thu ghi nhận theo
 * ngày HOÀN TẤT đơn và không tính phí vận chuyển. Bốn cách gộp: `day` | `month` | `branch` | `category`
 * — với `category` proc trả danh sách danh mục còn các trường hợp khác trả danh sách nhóm.
 *
 * Quyền: QL (chi nhánh mình — backend ép), AD (toàn hệ thống, chọn được chi nhánh). Ô chọn chi nhánh
 * chỉ hiện với Admin vì `GET /api/v1/admin/branches` là Admin-only.
 */
@Component({
  selector: 'app-revenue-report',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, ReportBranchSelect],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './revenue-report.html',
})
export class RevenueReport {
  private readonly reportApi = inject(ReportApi);
  private readonly adminApi = inject(AdminApi);
  private readonly authStore = inject(AuthStore);

  protected readonly groupByOptions = REVENUE_GROUP_BY_OPTIONS;
  protected readonly formatPrice = formatVnd;
  protected readonly formatCount = formatNumber;

  protected readonly loading = signal(false);
  protected readonly report = signal<RevenueReportDto | null>(null);
  protected readonly branches = signal<readonly BranchDto[]>([]);

  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));

  protected readonly filters = new FormGroup(
    {
      fromDate: new FormControl(firstDayOfMonth(), { nonNullable: true, validators: [Validators.required] }),
      toDate: new FormControl(today(), { nonNullable: true, validators: [Validators.required] }),
      groupBy: new FormControl('day', { nonNullable: true, validators: [Validators.required] }),
    },
    { validators: [dateRangeValidator('fromDate', 'toDate')] },
  );

  /** Ô chọn chi nhánh tách khỏi nhóm lọc vì chỉ Admin mới có trên form. */
  protected readonly branchId = new FormControl<number | null>(null);

  /** Tổng hợp toàn kỳ (tính từ danh sách nhóm — rỗng khi gộp theo danh mục). */
  protected readonly totals = computed(() => {
    const groups = this.report()?.groups ?? [];

    return groups.reduce(
      (sum, group) => ({
        orders: sum.orders + group.orders,
        grossRevenue: sum.grossRevenue + group.grossRevenue,
        refunds: sum.refunds + group.refunds,
        netRevenue: sum.netRevenue + group.netRevenue,
        cost: sum.cost + group.cost,
        grossProfit: sum.grossProfit + group.grossProfit,
      }),
      { orders: 0, grossRevenue: 0, refunds: 0, netRevenue: 0, cost: 0, grossProfit: 0 },
    );
  });

  /** Tổng doanh thu theo danh mục (dùng khi `groupBy = category`). */
  protected readonly categoryTotals = computed(() => {
    const categories = this.report()?.categories ?? [];

    return categories.reduce(
      (sum, row) => ({ quantity: sum.quantity + row.quantity, revenue: sum.revenue + row.revenue }),
      { quantity: 0, revenue: 0 },
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

    const value = this.filters.getRawValue();

    this.loading.set(true);

    this.reportApi
      .getRevenue({
        fromDate: value.fromDate,
        toDate: value.toDate,
        groupBy: value.groupBy,
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
    this.filters.reset({ fromDate: firstDayOfMonth(), toDate: today(), groupBy: 'day' });
    this.branchId.setValue(null);
    this.load();
  }

  protected groupKeyLabel(groupKey: string): string {
    return revenueGroupKeyLabel(this.filters.controls.groupBy.value, groupKey);
  }

  private loadBranches(): void {
    this.adminApi.listBranches({ isActive: true, pageNumber: 1, pageSize: 100 }).subscribe({
      next: (page) => this.branches.set(page.items),
      error: () => this.branches.set([]),
    });
  }
}
