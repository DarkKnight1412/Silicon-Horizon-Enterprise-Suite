import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { formatDateTime, formatVnd } from '../../../core/models/common.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { PaymentApi } from '../payment-api.service';
import {
  ledgerEntryTypeLabel,
  type CustomerLedgerDto,
} from '../payment.model';

/**
 * Tra cứu công nợ khách hàng — GET /api/v1/customers/{id}/ledger (usp_Customer_GetLedger).
 *
 * Quyền: KH (sao kê của mình), QL, AD (`OrderRoleGroups.LedgerViewers`). Màn tra cứu theo ID này
 * dành cho nhân viên; khách xem công nợ của mình qua chính ID của tài khoản.
 *
 * Lưu ý phạm vi (ghi trong CustomerLedgerController): proc không có tham số chi nhánh nên phạm vi
 * chi nhánh của Quản lý KHÔNG được áp dụng — QL tra cứu được khách hàng bất kỳ.
 */
@Component({
  selector: 'app-customer-ledger',
  imports: [ReactiveFormsModule, RouterLink, Pagination, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './customer-ledger.html',
})
export class CustomerLedger {
  private readonly paymentApi = inject(PaymentApi);

  protected readonly formatPrice = formatVnd;
  protected readonly formatMoment = formatDateTime;
  protected readonly entryTypeLabel = ledgerEntryTypeLabel;

  protected readonly loading = signal(false);
  protected readonly ledger = signal<CustomerLedgerDto | null>(null);

  protected readonly form = new FormGroup({
    customerId: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
  });

  /** Id khách đang xem (giữ lại để phân trang không cần nhập lại). */
  private currentCustomerId = 0;

  protected search(pageNumber = 1, pageSize = 50): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const customerId = this.form.controls.customerId.value;

    if (customerId === null || customerId <= 0) {
      return;
    }

    this.currentCustomerId = customerId;
    this.load(customerId, pageNumber, pageSize);
  }

  protected onPageChange(pageNumber: number): void {
    const ledger = this.ledger();

    if (this.currentCustomerId <= 0) {
      return;
    }

    this.load(this.currentCustomerId, pageNumber, ledger?.pageSize ?? 50);
  }

  protected onPageSizeChange(pageSize: number): void {
    if (this.currentCustomerId <= 0) {
      return;
    }

    this.load(this.currentCustomerId, 1, pageSize);
  }

  /** Tỷ lệ dùng hạn mức (0..100) để vẽ thanh tiến độ; 0 khi chưa được cấp hạn mức. */
  protected usagePercent(ledger: CustomerLedgerDto): number {
    if (ledger.creditLimit <= 0) {
      return 0;
    }

    const percent = Math.round(ledger.creditUsageRatio * 100);

    return percent < 0 ? 0 : percent > 100 ? 100 : percent;
  }

  private load(customerId: number, pageNumber: number, pageSize: number): void {
    this.loading.set(true);

    this.paymentApi.getCustomerLedger(customerId, { pageNumber, pageSize }).subscribe({
      next: (ledger) => {
        this.ledger.set(ledger);
        this.loading.set(false);
      },
      error: () => {
        this.ledger.set(null);
        this.loading.set(false);
      },
    });
  }
}
