import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import {
  FormControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { APPROVER_ROLES } from '../../../core/models/auth.model';
import { formatDateTime, formatVnd } from '../../../core/models/common.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { priceValidator } from '../../../shared/validators/price.validator';
import { SupplierApi } from '../supplier-api.service';
import {
  SUPPLIER_LEDGER_TYPE_LABELS,
  supplierActiveLabel,
  type SupplierDto,
  type SupplierLedgerDto,
} from '../supplier.model';

/**
 * Chi tiết nhà cung cấp + sao kê công nợ phải trả + ghi nhận thanh toán
 * (GET `/suppliers/{id}`, GET `/{id}/ledger`, POST `/{id}/payments`).
 *
 * Quyền (đọc từ SuppliersController):
 * - `GET /suppliers/{id}`: Thủ kho + Quản lý + Admin.
 * - `/{id}/ledger` và `/{id}/payments`: Quản lý + Admin (`InventoryRoles.Approvers`) ⇒ khối công nợ
 *   chỉ gọi API và hiển thị khi người dùng thuộc nhóm này, tránh 403 cho thủ kho.
 * - Proc thanh toán từ chối số tiền vượt công nợ hiện tại (50006 → 409) nên màn hình hiển thị số dư
 *   ngay cạnh ô nhập tiền.
 */
@Component({
  selector: 'app-supplier-detail',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './supplier-detail.html',
})
export class SupplierDetail implements OnInit {
  private readonly supplierApi = inject(SupplierApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly id = input('');

  protected readonly formatMoment = formatDateTime;
  protected readonly formatPrice = formatVnd;
  protected readonly ledgerTypeLabels = SUPPLIER_LEDGER_TYPE_LABELS;

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly supplier = signal<SupplierDto | null>(null);
  protected readonly ledger = signal<SupplierLedgerDto | null>(null);

  /** Công nợ & thanh toán chỉ dành cho QL/AD (`InventoryRoles.Approvers`). */
  protected readonly canManageDebt = computed(() => this.authStore.hasAnyRole(APPROVER_ROLES));

  protected readonly paymentForm = this.fb.group({
    amount: this.fb.control<number | null>(null, {
      validators: [Validators.required, priceValidator({ min: 1, max: 1_000_000_000_000 })],
    }),
    purchaseReceiptId: new FormControl<number | null>(null),
  });

  private supplierId = 0;

  ngOnInit(): void {
    const parsed = Number(this.id());

    if (!Number.isFinite(parsed) || parsed <= 0) {
      this.loading.set(false);
      return;
    }

    this.supplierId = parsed;
    this.loadSupplier(parsed);

    if (this.canManageDebt()) {
      this.loadLedger(1, 50);
    }
  }

  protected activeLabel(isActive: boolean): string {
    return supplierActiveLabel(isActive);
  }

  protected ledgerTypeLabel(entryType: number): string {
    return this.ledgerTypeLabels[entryType] ?? 'Không xác định';
  }

  /** Ghi nhận thanh toán cho NCC — proc trả về công nợ còn lại sau khi trả. */
  protected async recordPayment(): Promise<void> {
    if (this.paymentForm.invalid) {
      this.paymentForm.markAllAsTouched();
      return;
    }

    const value = this.paymentForm.getRawValue();
    const amount = value.amount;

    if (amount === null) {
      return;
    }

    const accepted = await this.confirm.ask({
      title: 'Ghi nhận thanh toán',
      message: `Thanh toán ${formatVnd(amount)} cho ${this.supplier()?.name ?? 'nhà cung cấp'}. Tiếp tục?`,
      confirmLabel: 'Ghi nhận',
      danger: true,
    });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    this.supplierApi
      .recordPayment(this.supplierId, { amount, purchaseReceiptId: value.purchaseReceiptId })
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.notifications.success(
            'Đã ghi nhận thanh toán',
            `Công nợ còn lại: ${formatVnd(result.balanceAfter)}`,
          );
          this.paymentForm.reset({ amount: null, purchaseReceiptId: null });
          this.loadLedger(1, this.ledger()?.pageSize ?? 50);
        },
        error: () => this.busy.set(false),
      });
  }

  protected loadLedger(pageNumber: number, pageSize: number): void {
    this.supplierApi.getLedger(this.supplierId, { pageNumber, pageSize }).subscribe({
      next: (ledger) => this.ledger.set(ledger),
      error: () => this.ledger.set(null),
    });
  }

  private loadSupplier(id: number): void {
    this.loading.set(true);

    this.supplierApi.getSupplier(id).subscribe({
      next: (supplier) => {
        this.supplier.set(supplier);
        this.loading.set(false);
      },
      error: () => {
        this.supplier.set(null);
        this.loading.set(false);
      },
    });
  }
}
