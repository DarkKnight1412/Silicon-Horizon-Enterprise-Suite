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
import { AuthStore } from '../../../../core/auth/auth.store';
import { ROLES, WAREHOUSE_ROLES } from '../../../../core/models/auth.model';
import type { BranchDto } from '../../../../core/models/category.model';
import type { ProductListItemDto } from '../../../../core/models/product.model';
import { NotificationService } from '../../../../core/services/notification.service';
import type { PendingChangesAware } from '../../../admin/pending-changes.guard';
import { CatalogApi } from '../../../catalog/catalog-api.service';
import { InventoryApi } from '../../inventory-api.service';
import type { StockTransferItemInput } from '../../inventory.model';
import { ProductPicker } from '../../product-picker/product-picker';

/** Nhóm control của một dòng hàng chuyển kho. */
type TransferLineGroup = FormGroup<{
  productId: FormControl<number | null>;
  sku: FormControl<string>;
  name: FormControl<string>;
  requiresSerial: FormControl<boolean>;
  quantity: FormControl<number | null>;
  serialsText: FormControl<string>;
}>;

/**
 * Tạo phiếu chuyển kho nội bộ (POST /api/v1/stock-transfers — `usp_StockTransfer_Create`, UC-14).
 *
 * Vì sao có ô "mở phiếu theo ID": backend KHÔNG có endpoint danh sách phiếu chuyển
 * (`StockTransfersController` chỉ có POST và GET `{id}`), nên màn hình này vừa tạo phiếu vừa là
 * lối vào chi tiết phiếu đã có.
 *
 * Quy tắc chi nhánh (đọc từ controller + DTO):
 * - Chi nhánh ĐI luôn ép từ token ⇒ chỉ hiển thị, không gửi lên.
 * - Chi nhánh ĐẾN do client chọn. Danh mục chi nhánh CHỈ Admin đọc được
 *   (`GET /api/v1/admin/branches`) nên Admin chọn từ danh sách, vai trò khác nhập mã chi nhánh.
 * - Phiếu tạo ra ở trạng thái Nháp, CHƯA trừ tồn — chỉ trừ khi gọi `/ship` ở màn chi tiết.
 */
@Component({
  selector: 'app-stock-transfer-create',
  imports: [ReactiveFormsModule, RouterLink, ProductPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stock-transfer-create.html',
})
export class StockTransferCreate implements PendingChangesAware {
  private readonly inventoryApi = inject(InventoryApi);
  private readonly catalog = inject(CatalogApi);
  private readonly authStore = inject(AuthStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  /** Chi nhánh đi = chi nhánh của tài khoản đang đăng nhập (backend ép từ token). */
  protected readonly fromBranchId = computed(() => this.authStore.account()?.branchId ?? null);
  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));
  protected readonly canSubmit = computed(() => this.authStore.hasAnyRole(WAREHOUSE_ROLES));

  protected readonly branches = signal<readonly BranchDto[]>([]);
  protected readonly submitting = signal(false);
  protected readonly productResults = signal<readonly ProductListItemDto[]>([]);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);

  protected readonly productKeyword = new FormControl('', { nonNullable: true });
  protected readonly lookupId = new FormControl('', { nonNullable: true });

  private readonly dirty = signal(false);

  protected readonly form = this.fb.group({
    toBranchId: this.fb.control<number | null>(null, { validators: [Validators.required] }),
  });

  protected readonly lines = new FormArray<TransferLineGroup>([]);

  constructor() {
    if (this.isAdmin()) {
      this.loadBranches();
    }

    this.form.valueChanges.subscribe(() => this.dirty.set(true));
    this.lines.valueChanges.subscribe(() => this.dirty.set(true));
  }

  /** Chỉ Admin gọi được endpoint danh mục chi nhánh; lỗi thì để người dùng nhập mã bằng tay. */
  protected loadBranches(): void {
    this.inventoryApi.listBranches({ pageNumber: 1, pageSize: 200 }).subscribe({
      next: (page) => this.branches.set(page.items),
      error: () => this.branches.set([]),
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

  protected serialCount(line: TransferLineGroup): number {
    return parseSerials(line.controls.serialsText.value).length;
  }

  protected serialMismatch(line: TransferLineGroup): boolean {
    if (!line.controls.requiresSerial.value) {
      return false;
    }

    return this.serialCount(line) !== (line.controls.quantity.value ?? 0);
  }

  /** Mở chi tiết phiếu đã có (thay cho danh sách mà backend không cung cấp). */
  protected openById(): void {
    const parsed = Number(this.lookupId.value.trim());

    if (!Number.isFinite(parsed) || parsed <= 0) {
      this.notifications.warning('Mã phiếu không hợp lệ', 'Nhập ID phiếu chuyển kho (số).');
      return;
    }

    void this.router.navigate(['/inventory/transfers', parsed]);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const toBranchId = this.form.controls.toBranchId.value;

    if (toBranchId === null) {
      return;
    }

    if (toBranchId === this.fromBranchId()) {
      this.notifications.warning(
        'Chi nhánh đích trùng chi nhánh đi',
        'Proc từ chối phiếu chuyển nội bộ trong cùng một chi nhánh.',
      );
      return;
    }

    if (this.lines.length === 0) {
      this.notifications.warning('Phiếu chưa có dòng hàng', 'Chọn ít nhất một sản phẩm để chuyển.');
      return;
    }

    this.lines.markAllAsTouched();

    if (this.lines.invalid) {
      return;
    }

    const items: StockTransferItemInput[] = [];

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
          `${value.name}: cần đúng ${quantity} serial đang ở kho chi nhánh đi, hiện có ${serials.length}.`,
        );
        return;
      }

      items.push({ productId, quantity, serials: value.requiresSerial ? serials : null });
    }

    this.submitting.set(true);

    this.inventoryApi.createStockTransfer({ toBranchId, items }).subscribe({
      next: (created) => {
        this.submitting.set(false);
        this.dirty.set(false);
        this.notifications.success('Đã tạo phiếu chuyển kho (Nháp)', created.transferCode);
        void this.router.navigate(['/inventory/transfers', created.transferId]);
      },
      error: () => this.submitting.set(false),
    });
  }
}

/** Tách serial dán vào thành mảng (mỗi dòng một serial, chấp nhận cả dấu phẩy). */
function parseSerials(text: string): string[] {
  return text
    .split(/[\n,;]+/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}
