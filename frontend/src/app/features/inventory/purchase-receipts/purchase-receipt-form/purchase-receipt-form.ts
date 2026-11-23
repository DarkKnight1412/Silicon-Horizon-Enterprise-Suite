import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { formatVnd } from '../../../../core/models/common.model';
import type { ProductListItemDto } from '../../../../core/models/product.model';
import { NotificationService } from '../../../../core/services/notification.service';
import { priceValidator } from '../../../../shared/validators/price.validator';
import type { PendingChangesAware } from '../../../admin/pending-changes.guard';
import { CatalogApi } from '../../../catalog/catalog-api.service';
import { SupplierApi } from '../../../suppliers/supplier-api.service';
import type { SupplierDto } from '../../../suppliers/supplier.model';
import { InventoryApi } from '../../inventory-api.service';
import type { PurchaseReceiptItemInput } from '../../inventory.model';
import { ProductPicker } from '../../product-picker/product-picker';

/** Nhóm control của một dòng hàng nhập kho. */
type ReceiptLineGroup = FormGroup<{
  productId: FormControl<number | null>;
  sku: FormControl<string>;
  name: FormControl<string>;
  requiresSerial: FormControl<boolean>;
  quantity: FormControl<number | null>;
  unitCost: FormControl<number | null>;
  serialsText: FormControl<string>;
}>;

/**
 * Tạo phiếu nhập kho (POST /api/v1/purchase-receipts — `usp_PurchaseReceipt_Create`, UC-13).
 *
 * Nghiệp vụ backend cần lưu ý (đọc từ PurchaseReceiptsController + DTO):
 * - KHÔNG gửi `branchId`: hàng luôn nhập về chi nhánh của thủ kho trong token.
 * - Proc tạo phiếu ở trạng thái Hoàn tất ngay trong MỘT giao dịch: cộng tồn, sinh serial, ghi
 *   `StockMovements` và ghi tăng công nợ phải trả theo `PaymentTermDays` của nhà cung cấp.
 * - Hàng `requiresSerial` phải kèm ĐỦ số serial, không trùng (thiếu → 400, trùng → 409). Màn hình
 *   đếm serial so với số lượng để chặn sớm, đỡ mất công người dùng.
 *
 * `hasUnsavedChanges()` phục vụ `pendingChangesGuard` (canDeactivate) khi form đã nhập dở.
 */
@Component({
  selector: 'app-purchase-receipt-form',
  imports: [ReactiveFormsModule, RouterLink, ProductPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './purchase-receipt-form.html',
})
export class PurchaseReceiptForm implements PendingChangesAware {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly supplierApi = inject(SupplierApi);
  private readonly catalog = inject(CatalogApi);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly formatPrice = formatVnd;

  protected readonly suppliers = signal<readonly SupplierDto[]>([]);
  protected readonly submitting = signal(false);
  protected readonly productResults = signal<readonly ProductListItemDto[]>([]);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);

  protected readonly productKeyword = new FormControl('', { nonNullable: true });

  /** Theo dõi thay đổi chưa lưu cho `canDeactivate`. */
  private readonly dirty = signal(false);

  protected readonly form = this.fb.group({
    supplierId: this.fb.control<number | null>(null, { validators: [Validators.required] }),
    supplierInvoiceNo: this.fb.control('', { validators: [Validators.maxLength(50)] }),
  });

  protected readonly lines = new FormArray<ReceiptLineGroup>([]);

  /** Tổng tiền tạm tính (số lượng × đơn giá) để đối chiếu với `totalAmount` proc trả về. */
  protected readonly estimatedTotal = computed(() =>
    this.lines.controls.reduce((sum, line) => {
      const quantity = line.controls.quantity.value ?? 0;
      const unitCost = line.controls.unitCost.value ?? 0;
      return sum + quantity * unitCost;
    }, 0),
  );

  constructor() {
    this.loadSuppliers();
    this.form.valueChanges.subscribe(() => this.dirty.set(true));
    // `FormArray` không nằm trong `form.valueChanges` nên phải theo dõi riêng cho cờ "chưa lưu".
    this.lines.valueChanges.subscribe(() => this.dirty.set(true));
  }

  protected loadSuppliers(): void {
    this.supplierApi
      .listSuppliers({ isActive: true, pageNumber: 1, pageSize: 100 })
      .subscribe({
        next: (page) => this.suppliers.set(page.items),
        error: () => this.suppliers.set([]),
      });
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

  /** Thêm dòng hàng cho sản phẩm vừa chọn (không cho trùng sản phẩm trong cùng phiếu). */
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
        unitCost: this.fb.control<number | null>(null, {
          validators: [Validators.required, priceValidator({ min: 0, max: 1_000_000_000 })],
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

  protected serialCount(line: ReceiptLineGroup): number {
    return parseSerials(line.controls.serialsText.value).length;
  }

  /** true khi hàng cần serial mà số serial đã nhập chưa khớp số lượng. */
  protected serialMismatch(line: ReceiptLineGroup): boolean {
    if (!line.controls.requiresSerial.value) {
      return false;
    }

    return this.serialCount(line) !== (line.controls.quantity.value ?? 0);
  }

  protected duplicateSerials(line: ReceiptLineGroup): boolean {
    const serials = parseSerials(line.controls.serialsText.value);
    return new Set(serials).size !== serials.length;
  }

  /** Hợp đồng với `pendingChangesGuard` (canDeactivate) — chống mất phiếu đang nhập dở. */
  hasUnsavedChanges(): boolean {
    return this.dirty() && !this.submitting();
  }

  /** Kiểm tra toàn bộ phiếu rồi gửi lên backend (proc tự kiểm tra lại lần cuối). */
  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const supplierId = this.form.controls.supplierId.value;

    if (supplierId === null) {
      return;
    }

    if (this.lines.length === 0) {
      this.notifications.warning('Phiếu chưa có dòng hàng', 'Chọn ít nhất một sản phẩm để nhập kho.');
      return;
    }

    this.lines.markAllAsTouched();

    if (this.lines.invalid) {
      return;
    }

    const items: PurchaseReceiptItemInput[] = [];
    const allSerials: string[] = [];

    for (const line of this.lines.controls) {
      const value = line.getRawValue();
      const { productId, quantity, unitCost } = value;

      if (productId === null || quantity === null || unitCost === null) {
        return;
      }

      const serials = value.requiresSerial ? parseSerials(value.serialsText) : [];

      if (value.requiresSerial) {
        if (serials.length !== quantity) {
          this.notifications.warning(
            'Thiếu hoặc thừa serial',
            `${value.name}: cần đúng ${quantity} serial, đang có ${serials.length}.`,
          );
          return;
        }

        if (new Set(serials).size !== serials.length) {
          this.notifications.warning('Serial bị lặp trong cùng một dòng', value.name);
          return;
        }
      }

      allSerials.push(...serials);
      items.push({ productId, quantity, unitCost, serials: value.requiresSerial ? serials : null });
    }

    if (new Set(allSerials).size !== allSerials.length) {
      this.notifications.warning('Serial bị lặp giữa các dòng hàng', 'Mỗi serial chỉ dùng một lần.');
      return;
    }

    const invoiceNo = this.form.controls.supplierInvoiceNo.value.trim();

    this.submitting.set(true);

    this.inventoryApi
      .createPurchaseReceipt({
        supplierId,
        supplierInvoiceNo: invoiceNo.length > 0 ? invoiceNo : null,
        items,
      })
      .subscribe({
        next: (created) => {
          this.submitting.set(false);
          this.dirty.set(false);
          this.notifications.success(
            'Đã nhập kho',
            `${created.receiptCode} · tổng tiền ${formatVnd(created.totalAmount)}`,
          );
          void this.router.navigate(['/inventory/receipts', created.receiptId]);
        },
        error: () => this.submitting.set(false),
      });
  }
}

/**
 * Tách danh sách serial người dùng dán vào (mỗi dòng một serial, chấp nhận cả dấu phẩy/chấm phẩy)
 * và bỏ phần tử rỗng — backend không tự chuẩn hoá nên làm sạch ở đây tránh lỗi "thiếu/trùng" vô nghĩa.
 */
function parseSerials(text: string): string[] {
  return text
    .split(/[\n,;]+/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}
