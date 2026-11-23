import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
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
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLES } from '../../../core/models/auth.model';
import { isApiError } from '../../../core/models/api-error.model';
import { formatVnd } from '../../../core/models/common.model';
import type { ProductListItemDto } from '../../../core/models/product.model';
import { NotificationService } from '../../../core/services/notification.service';
import { priceValidator } from '../../../shared/validators/price.validator';
import { CatalogApi } from '../../catalog/catalog-api.service';
import { ProductPicker } from '../../inventory/product-picker/product-picker';
import type { PendingChangesAware } from '../../admin/pending-changes.guard';
import { StaffOrderApi } from '../staff-order-api.service';
import {
  POS_FULFILLMENT_OPTIONS,
  POS_PAYMENT_METHOD_OPTIONS,
  POS_SHARE_RATE_TOTAL,
  totalShareRate,
  type PosCustomerDto,
  type PosEmployeeDto,
  type PosOrderItemInput,
  type PosSalesStaffInput,
} from '../staff-order.model';

/** Nhóm control của một dòng hàng tại quầy. */
type PosLineGroup = FormGroup<{
  productId: FormControl<number | null>;
  sku: FormControl<string>;
  name: FormControl<string>;
  unitPrice: FormControl<number>;
  quantity: FormControl<number | null>;
}>;

/** Nhóm control của một phần chia doanh số cho nhân viên. */
type PosStaffGroup = FormGroup<{
  employeeId: FormControl<number | null>;
  shareRate: FormControl<number | null>;
}>;

/**
 * POS — tạo đơn tại quầy: `POST /api/v1/pos/orders` (`usp_Order_Create` với `@ItemsJson`, `@Channel = 1`).
 *
 * Nghiệp vụ backend cần lưu ý (đọc từ `PosOrdersController` + `CreatePosOrderRequest`):
 * - KHÔNG gửi `branchId`: API ép `@BranchId`/`@UserId` từ token (ORD-03).
 * - `customerId` BẮT BUỘC — khách vãng lai là bản ghi `Customers` có `UserId = NULL`.
 *   ⚠️ `GET /api/v1/customers` chỉ dành cho Admin, nên CHỈ Admin có ô tra cứu khách; vai trò khác
 *   nhập trực tiếp mã khách (lấy từ màn khách hàng/đơn cũ).
 * - Bỏ trống phần chia doanh số ⇒ proc gán nhân viên tạo đơn 100% (KPI-02); nếu nhập thì tổng
 *   `shareRate` phải BẰNG 100, sai → 400. Màn hình chặn sớm để tránh mất công nhập lại.
 * - Đơn COD/tiền mặt vào thẳng Pending (1); chuyển khoản/cổng thanh toán vào PendingPayment (0).
 *
 * Giá hiển thị là TẠM TÍNH theo bảng giá hiện hành — proc tự chốt giá khi tạo đơn.
 */
@Component({
  selector: 'app-pos-create',
  imports: [ReactiveFormsModule, RouterLink, ProductPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pos-create.html',
})
export class PosCreate implements PendingChangesAware {
  private readonly staffOrderApi = inject(StaffOrderApi);
  private readonly catalog = inject(CatalogApi);
  private readonly authStore = inject(AuthStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly formatPrice = formatVnd;
  protected readonly fulfillmentOptions = POS_FULFILLMENT_OPTIONS;
  protected readonly paymentMethodOptions = POS_PAYMENT_METHOD_OPTIONS;
  protected readonly shareRateTotal = POS_SHARE_RATE_TOTAL;

  protected readonly submitting = signal(false);
  protected readonly productResults = signal<readonly ProductListItemDto[]>([]);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);

  protected readonly productKeyword = new FormControl('', { nonNullable: true });

  /** Admin tra cứu khách/nhân viên qua endpoint quản trị; vai trò khác nhập tay ID. */
  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));

  protected readonly customerKeyword = new FormControl('', { nonNullable: true });
  protected readonly customerResults = signal<readonly PosCustomerDto[]>([]);
  protected readonly searchingCustomers = signal(false);
  protected readonly selectedCustomer = signal<PosCustomerDto | null>(null);

  protected readonly employees = signal<readonly PosEmployeeDto[]>([]);

  protected readonly lines = new FormArray<PosLineGroup>([]);
  protected readonly staffShares = new FormArray<PosStaffGroup>([]);

  /** Theo dõi thay đổi chưa lưu cho `canDeactivate`. */
  private readonly dirty = signal(false);

  protected readonly form = this.fb.group({
    customerId: this.fb.control<number | null>(null, { validators: [Validators.min(1)] }),
    fulfillmentType: this.fb.control(1, { validators: [Validators.required] }),
    recipientName: this.fb.control(''),
    recipientPhone: this.fb.control(''),
    shippingAddress: this.fb.control(''),
    shippingFee: this.fb.control<number | null>(0, {
      validators: [priceValidator({ min: 0 })],
    }),
    paymentMethod: this.fb.control(3, { validators: [Validators.required] }),
    voucherCode: this.fb.control(''),
    note: this.fb.control(''),
  });

  /** Tổng tạm tính (số lượng × giá hiện hành) để thu ngân đối chiếu với tổng tiền proc trả về. */
  protected readonly estimatedTotal = computed(() =>
    this.lines.controls.reduce((sum, line) => {
      const quantity = line.controls.quantity.value ?? 0;
      return sum + quantity * line.controls.unitPrice.value;
    }, 0),
  );

  /** Tổng tỷ lệ chia doanh số đã nhập (phải bằng 100 nếu có ít nhất một dòng). */
  protected readonly shareTotal = computed(() =>
    totalShareRate(
      this.staffShares.controls.map((row) => ({
        employeeId: row.controls.employeeId.value ?? 0,
        shareRate: row.controls.shareRate.value ?? 0,
      })),
    ),
  );

  protected readonly shareInvalid = computed(
    () => this.staffShares.length > 0 && this.shareTotal() !== POS_SHARE_RATE_TOTAL,
  );

  constructor() {
    this.form.valueChanges.subscribe(() => this.dirty.set(true));
    // `FormArray` không nằm trong `form.valueChanges` nên phải theo dõi riêng cho cờ "chưa lưu".
    this.lines.valueChanges.subscribe(() => this.dirty.set(true));
    this.staffShares.valueChanges.subscribe(() => this.dirty.set(true));

    if (this.isAdmin()) {
      this.loadEmployees();
    }
  }

  // ── Sản phẩm trong đơn ─────────────────────────────────────────────────────

  protected searchProducts(): void {
    const keyword = this.productKeyword.value.trim();

    if (keyword.length === 0) {
      this.notifications.warning('Nhập từ khoá', 'Nhập tên sản phẩm hoặc đầu mã SKU để tìm.');
      return;
    }

    this.searching.set(true);

    this.catalog.searchProducts({ keyword, sortBy: 'name', pageNumber: 1, pageSize: 20 }).subscribe({
      next: (page) => {
        this.productResults.set(page.items);
        this.searching.set(false);
        this.searched.set(true);
      },
      error: () => {
        this.searching.set(false);
        this.searched.set(true);
      },
    });
  }

  /** Thêm sản phẩm vào đơn; sản phẩm đã có thì cộng thêm 1 (không tạo dòng trùng). */
  protected addLine(product: ProductListItemDto): void {
    const existed = this.lines.controls.find(
      (line) => line.controls.productId.value === product.id,
    );

    if (existed !== undefined) {
      const quantity = existed.controls.quantity.value ?? 0;
      existed.controls.quantity.setValue(quantity + 1);
      existed.controls.quantity.markAsTouched();
      return;
    }

    this.lines.push(
      this.fb.group({
        productId: this.fb.control<number | null>(product.id, { validators: [Validators.required] }),
        sku: this.fb.control(product.sku),
        name: this.fb.control(product.name),
        unitPrice: this.fb.control(product.price),
        quantity: this.fb.control<number | null>(1, {
          validators: [Validators.required, Validators.min(1)],
        }),
      }),
    );
  }

  protected removeLine(index: number): void {
    this.lines.removeAt(index);
  }

  protected lineTotal(line: PosLineGroup): number {
    return (line.controls.quantity.value ?? 0) * line.controls.unitPrice.value;
  }

  // ── Chia doanh số cho nhân viên (tổng phải bằng 100 nếu có nhập) ───────────

  protected addStaffShare(): void {
    this.staffShares.push(
      this.fb.group({
        employeeId: this.fb.control<number | null>(null, {
          validators: [Validators.required, Validators.min(1)],
        }),
        shareRate: this.fb.control<number | null>(null, {
          validators: [Validators.required, Validators.min(1), Validators.max(100)],
        }),
      }),
    );
  }

  protected removeStaffShare(index: number): void {
    this.staffShares.removeAt(index);
  }

  protected employeeLabel(employee: PosEmployeeDto): string {
    return `${employee.employeeCode} · ${employee.fullName} (${employee.positionName})`;
  }

  // ── Khách hàng ─────────────────────────────────────────────────────────────

  /** Tra cứu khách để chọn (CHỈ gọi được khi là Admin — endpoint quản trị). */
  protected searchCustomers(): void {
    const keyword = this.customerKeyword.value.trim();

    this.searchingCustomers.set(true);

    this.staffOrderApi
      .searchCustomers({
        keyword: keyword.length > 0 ? keyword : null,
        pageNumber: 1,
        pageSize: 20,
      })
      .subscribe({
        next: (page) => {
          this.customerResults.set(page.items);
          this.searchingCustomers.set(false);
        },
        error: () => {
          this.customerResults.set([]);
          this.searchingCustomers.set(false);
        },
      });
  }

  protected pickCustomer(customer: PosCustomerDto): void {
    this.selectedCustomer.set(customer);
    this.customerResults.set([]);
    this.customerKeyword.setValue('');
    this.dirty.set(true);
  }

  protected clearCustomer(): void {
    this.selectedCustomer.set(null);
    this.customerResults.set([]);
  }

  /** Nhãn loại khách của hồ sơ: khách vãng lai = `userId === null` (Customers.UserId NULL). */
  protected customerKindLabel(customer: PosCustomerDto): string {
    return customer.userId === null ? 'Khách vãng lai' : 'Khách có tài khoản';
  }

  /** Hợp đồng với `pendingChangesGuard` (canDeactivate) — chống mất đơn đang nhập dở. */
  hasUnsavedChanges(): boolean {
    return this.dirty() && !this.submitting();
  }

  // ── Tạo đơn ────────────────────────────────────────────────────────────────

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    const customerId = this.resolveCustomerId();

    if (customerId === null) {
      this.notifications.warning(
        'Chưa chọn khách hàng',
        'Đơn tại quầy bắt buộc gắn với một hồ sơ khách (khách vãng lai cũng cần hồ sơ trong hệ thống).',
      );
      return;
    }

    if (this.lines.length === 0) {
      this.notifications.warning('Đơn chưa có sản phẩm', 'Chọn ít nhất một sản phẩm để bán.');
      return;
    }

    this.lines.markAllAsTouched();

    if (this.lines.invalid || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.shareInvalid()) {
      this.notifications.warning(
        'Tổng tỷ lệ chia không hợp lệ',
        `Tổng shareRate phải bằng ${POS_SHARE_RATE_TOTAL}% (đang là ${this.shareTotal()}%).`,
      );
      return;
    }

    const value = this.form.getRawValue();

    if (
      value.fulfillmentType === 0 &&
      (value.recipientName.trim().length === 0 ||
        value.recipientPhone.trim().length === 0 ||
        value.shippingAddress.trim().length === 0)
    ) {
      this.notifications.warning(
        'Thiếu thông tin giao hàng',
        'Giao tận nơi cần tên, số điện thoại và địa chỉ người nhận.',
      );
      return;
    }

    const items: PosOrderItemInput[] = [];

    for (const line of this.lines.controls) {
      const productId = line.controls.productId.value;
      const quantity = line.controls.quantity.value;

      if (productId === null || quantity === null || quantity <= 0) {
        this.notifications.warning(
          'Dòng hàng chưa hợp lệ',
          'Kiểm tra lại số lượng của từng sản phẩm trong đơn.',
        );
        return;
      }

      items.push({ productId, quantity });
    }

    const salesStaff: PosSalesStaffInput[] | null =
      this.staffShares.length === 0
        ? null
        : this.staffShares.controls.map((row) => ({
            employeeId: row.controls.employeeId.value ?? 0,
            shareRate: row.controls.shareRate.value ?? 0,
          }));

    this.submitting.set(true);

    this.staffOrderApi
      .createPosOrder({
        customerId,
        fulfillmentType: value.fulfillmentType,
        recipientName: this.optional(value.recipientName),
        recipientPhone: this.optional(value.recipientPhone),
        shippingAddress: this.optional(value.shippingAddress),
        shippingFee: value.shippingFee ?? 0,
        paymentMethod: value.paymentMethod,
        voucherCode: this.optional(value.voucherCode),
        note: this.optional(value.note),
        items,
        salesStaff,
      })
      .subscribe({
        next: (created) => {
          this.submitting.set(false);
          this.dirty.set(false);
          this.notifications.success(
            'Đã tạo đơn tại quầy',
            `${created.orderCode} · ${formatVnd(created.grandTotal)}`,
          );
          void this.router.navigate(['/orders', created.orderId]);
        },
        error: (error: unknown) => {
          this.submitting.set(false);

          // Service đặt `silent: true` nên màn hình tự hiển thị lỗi (hết tồn, vượt hạn mức công nợ...).
          if (isApiError(error)) {
            this.notifications.fromApiError(error);
          }
        },
      });
  }

  /** Admin chọn khách qua ô tra cứu; vai trò khác nhập trực tiếp `customerId`. */
  private resolveCustomerId(): number | null {
    if (this.isAdmin()) {
      return this.selectedCustomer()?.id ?? null;
    }

    const typed = this.form.controls.customerId.value;

    return typed !== null && typed > 0 ? typed : null;
  }

  /** Nạp danh sách nhân viên cùng chi nhánh (chỉ Admin gọi được endpoint này). */
  private loadEmployees(): void {
    this.staffOrderApi.listEmployees(this.authStore.account()?.branchId ?? null).subscribe({
      next: (page) => this.employees.set(page.items),
      error: () => this.employees.set([]),
    });
  }

  private optional(value: string): string | null {
    const trimmed = value.trim();

    return trimmed.length === 0 ? null : trimmed;
  }
}
