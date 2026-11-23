import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatVnd } from '../../../core/models/common.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { ReportApi } from '../report-api.service';
import { type DebtReportDto } from '../report.model';

/**
 * Báo cáo công nợ — GET /api/v1/reports/debt? (usp_Report_Debt, Database/README.md mục 9).
 *
 * ⚠️ CHỈ Admin: proc trả TOÀN BỘ đối tượng còn số dư và KHÔNG có tham số chi nhánh nên không thể
 * giới hạn theo phạm vi chi nhánh của Quản lý (`ReportRoles.AdminOnly`). Không có tham số lọc nào.
 *
 * Kết quả gồm 2 nhóm: phải thu của khách (kèm hạn mức, phần quá hạn) và phải trả nhà cung cấp.
 */
@Component({
  selector: 'app-debt-report',
  imports: [RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './debt-report.html',
})
export class DebtReport {
  private readonly reportApi = inject(ReportApi);

  protected readonly formatPrice = formatVnd;

  protected readonly loading = signal(false);
  protected readonly report = signal<DebtReportDto | null>(null);

  /** Tổng phải thu / phải trả / quá hạn của cả hai nhóm. */
  protected readonly totals = computed(() => {
    const data = this.report();

    if (data === null) {
      return { customerBalance: 0, customerOverdue: 0, supplierBalance: 0, supplierOverdue: 0 };
    }

    return {
      customerBalance: data.customers.reduce((sum, row) => sum + row.balance, 0),
      customerOverdue: data.customers.reduce((sum, row) => sum + row.overdue, 0),
      supplierBalance: data.suppliers.reduce((sum, row) => sum + row.balance, 0),
      supplierOverdue: data.suppliers.reduce((sum, row) => sum + row.overdue, 0),
    };
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    this.reportApi.getDebt().subscribe({
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
}
