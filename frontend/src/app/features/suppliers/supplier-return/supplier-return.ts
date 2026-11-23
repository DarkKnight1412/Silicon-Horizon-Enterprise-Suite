import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { formatVnd } from '../../../core/models/common.model';
import type { ProductListItemDto } from '../../../core/models/product.model';
import { NotificationService } from '../../../core/services/notification.service';
import { priceValidator } from '../../../shared/validators/price.validator';
import type { PendingChangesAware } from '../../admin/pending-changes.guard';
import { CatalogApi } from '../../catalog/catalog-api.service';
import { ProductPicker } from '../../inventory/product-picker/product-picker';
import { SupplierApi } from '../supplier-api.service';
import type { CreateSupplierReturnRequest, SupplierDto } from '../supplier.model';

/** Nhóm control của một dòng hàng trả nhà cung cấp. */
type ReturnLineGroup = FormGroup<{
  productId: FormControl<number | null>;
  sku: FormControl<string>;
  name: FormControl<string>;
  requiresSerial: FormControl<boolean>;
  quantity: FormControl<number | null>;
  serialsText: FormControl<string>;
}>;

/**
 * Trả hàng nhà cung cấp (POST /api/v1/supplier-returns — `usp_SupplierReturn_Create`, SUP-02).
 *
 * Nghiệp vụ backend:
 * - Chi nhánh trả hàng ép từ token; KHÔNG gửi `branchId`.
 * - Serial trả PHẢI đang ở trạng thái Hàng lỗi (7) tại chi nhánh đó; hàng không quản lý serial thì
 *   proc trừ thẳng tồn thực tế.
 * - `creditAmount > 0` sẽ ghi `SupplierLedger` EntryType 2 (giảm công nợ phải trả).
 * - Backend chỉ trả `{ supplierReturnId, returnCode }` (chưa có endpoint chi tiết) nên sau khi tạo,
 *   màn hình chuyển về chi tiết nhà cung cấp để xem sổ công nợ cập nhật.
 */
@Component({
  selector: 'app-supplier-return',
  imports: [ReactiveFormsModule, RouterLink, ProductPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './supplier-return.html',
})
export class SupplierReturn implements OnInit, PendingChangesAware {
  private readonly supplierApi = inject(SupplierApi);
  private readonly catalog = inject(CatalogApi);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  /** Route param `:id` — nhà cung cấp nhận hàng trả. */
  readonly id = input('');

  protected readonly formatPrice = formatVnd;
  protected readonly submitting = signal(false);
  protected readonly supplier = signal<SupplierDto | null>(null);
  protected readonly productResults = signal<readonly ProductListItemDto[]>([]);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);

  protected readonly productKeyword = new FormControl('', { nonNullable: true });

  private readonly dirty = signal(false);

  protected readonly form = this.fb.group({
    creditAmount: this.fb.control<number | null>(0, {
      validators: [priceValidator({ min: 0, max: 1_000_000_000_000 })],
    }),
  });

  protected readonly lines = new FormArray<ReturnLineGroup>([]);

  constructor() {
    this.form.valueChanges.subscribe(() => this.dirty.set(true));
    this.lines.valueChanges.subscribe(() => this.dirty.set(true));
  }

  ngOnInit(): void {
    const parsed = Number(this.id());

    if (!Number.isFinite(parsed) || parsed <= 0) {
      return;
    }

    this.supplierApi.getSupplier(parsed).subscribe({
      next: (supplier) => this.supplier.set(supplier),
      error: () => this.supplier.set(null),
    });
  }

  hasUnsavedChanges(): boolean {
    return this.dirty() && !this.submitting();
  }

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

  protected addLine(product: ProductListItemDto): void {
    const existed = this.lines.controls.some((line) => line.controls.productId.value === product.id);

    if (existed) {
      this.notifications.warning('Sản phẩm đã có trong phiếu', product.name);
      return;
    }

    this.lines.push(
      this.fb.group({
        productId: this.fb.control<number | null>(product.id),
        sku: this.fb.control(product.sku),
        name: this.fb.control(product.name),
        requiresSerial: this.fb.control(product.requiresSerial),
        quantity: this.fb.control<number | null>(1, {
          validators: [Validators.required, Validators.min(1)],
        }),
        serialsText: this.fb.control(''),
      }),
    );

    this.productResults.set([]);
    this.searched.set(false);
  }

  protected removeLine(index: number): void {
    this.lines.removeAt(index);
  }

  protected serialCount(line: ReturnLineGroup): number {
    return parseSerials(line.controls.serialsText.value).length;
  }

  /** Hàng quản lý serial phải liệt kê đủ serial đang ở trạng thái Hàng lỗi tại chi nhánh. */
  protected serialMismatch(line: ReturnLineGroup): boolean {
    if (!line.controls.requiresSerial.value) {
      return false;
    }

    return this.serialCount(line) !== (line.controls.quantity.value ?? 0);
  }

  protected submit(): void {
    const supplierId = this.targetSupplierId();

    if (supplierId === null) {
      this.notifications.warning('Thiếu nhà cung cấp', 'Mở màn này từ chi tiết nhà cung cấp.');
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.lines.length === 0) {
      this.notifications.warning('Phiếu chưa có dòng hàng', 'Chọn ít nhất một sản phẩm để trả.');
      return;
    }

    this.lines.markAllAsTouched();

    if (this.lines.invalid) {
      return;
    }

    const items: CreateSupplierReturnRequest['items'] = [];

    for (const line of this.lines.controls) {
      const value = line.getRawValue();
      const { productId, quantity } = value;

      if (productId === null || quantity === null) {
        return;
      }

      const serials = value.requiresSerial ? parseSerials(value.serialsText) : [];

      if (value.requiresSerial && serials.length !== quantity) {
        this.notifications.warning(
          'Thiếu hoặc thừa serial',
          `${value.name}: cần đúng ${quantity} serial Hàng lỗi tại chi nhánh, hiện có ${serials.length}.`,
        );
        return;
      }

      items.push({ productId, quantity, serials: value.requiresSerial ? serials : null });
    }

    const creditAmount = this.form.controls.creditAmount.value ?? 0;

    this.submitting.set(true);

    this.supplierApi.createSupplierReturn({ supplierId, items, creditAmount }).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.dirty.set(false);
        this.notifications.success(
          'Đã tạo phiếu trả hàng nhà cung cấp',
          `${created.returnCode}${creditAmount > 0 ? ` · giảm công nợ ${formatVnd(creditAmount)}` : ''}`,
        );
        void this.router.navigate(['/suppliers', supplierId]);
      },
      error: () => this.submitting.set(false),
    });
  }

  /** Mục tiêu: nhà cung cấp trên route param (dùng ở template). */
  protected targetSupplierId(): number | null {
    const parsed = Number(this.id());
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  }
}

/** Tách serial dán vào thành mảng (mỗi dòng một serial, chấp nhận cả dấu phẩy). */
function parseSerials(text: string): string[] {
  return text
    .split(/[\n,;]+/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}
