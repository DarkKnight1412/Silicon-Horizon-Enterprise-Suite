import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLES } from '../../../core/models/auth.model';
import { formatVnd } from '../../../core/models/common.model';
import {
  COMPATIBILITY_CODE_LABELS,
  PRODUCT_STATUS_LABELS,
  type CompatibilityCheckResultDto,
  type ProductDetailDto,
} from '../../../core/models/product.model';
import { SPEC_TYPE_LABELS } from '../../../core/models/spec.model';
import { CartStore } from '../../../core/services/cart.store';
import { NotificationService } from '../../../core/services/notification.service';
import { CatalogApi } from '../catalog-api.service';

/**
 * Chi tiết sản phẩm (GET /api/v1/products/{id}).
 *
 * Dữ liệu được nạp TRƯỚC khi vào route bằng resolver (`resolve: { product: productDetailResolver }`);
 * nhờ `withComponentInputBinding()` kết quả resolver được bind thẳng vào input `product` —
 * component không cần tự gọi API nên template không phải xử lý trạng thái "đang tải".
 */
@Component({
  selector: 'app-product-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-detail.html',
})
export class ProductDetail {
  private readonly catalog = inject(CatalogApi);
  private readonly cart = inject(CartStore);
  private readonly notifications = inject(NotificationService);
  private readonly authStore = inject(AuthStore);

  /** Dữ liệu do resolver cung cấp (bound qua withComponentInputBinding). */
  readonly product = input.required<ProductDetailDto>();

  protected readonly quantity = signal(1);
  protected readonly adding = signal(false);
  protected readonly checking = signal(false);
  protected readonly compatibility = signal<CompatibilityCheckResultDto | null>(null);

  protected readonly formatPrice = formatVnd;

  /** Nhân viên Admin thấy thêm liên kết sửa sản phẩm. */
  protected readonly canManage = computed(() => this.authStore.hasRole(ROLES.Admin));

  protected readonly specTypeLabel = computed(
    () => SPEC_TYPE_LABELS[this.product().specType] ?? 'Thông số chung',
  );

  protected readonly statusLabel = computed(
    () => PRODUCT_STATUS_LABELS[this.product().status] ?? 'Không xác định',
  );

  /** Thông số kỹ thuật trả về dạng "tên cột → giá trị" nên đưa về mảng để hiển thị bảng. */
  protected readonly specifications = computed(() =>
    Object.entries(this.product().specifications ?? {}).filter(([, value]) => value !== null),
  );

  /** Cổng kết nối của màn hình (Specs_Monitor_Ports). */
  protected readonly monitorPorts = computed(() =>
    this.product().monitorPorts.map((row) => ({
      portType: String(row['PortType'] ?? row['portType'] ?? ''),
      quantity: Number(row['Quantity'] ?? row['quantity'] ?? 0),
    })),
  );

  protected readonly totalPrice = computed(() => this.product().price * this.quantity());

  protected changeQuantity(delta: number): void {
    const next = this.quantity() + delta;
    const max = Math.max(1, this.product().availableQty);

    this.quantity.set(Math.min(Math.max(1, next), max));
  }

  protected onQuantityInput(event: Event): void {
    const target = event.target;

    if (!(target instanceof HTMLInputElement)) {
      return;
    }

    const parsed = Number(target.value);
    const max = Math.max(1, this.product().availableQty);

    this.quantity.set(Number.isFinite(parsed) ? Math.min(Math.max(1, parsed), max) : 1);
  }

  /** Thêm vào giỏ với số lượng đang chọn (đặt đúng số lượng: replace = true). */
  protected addToCart(): void {
    if (this.product().availableQty <= 0) {
      this.notifications.warning('Sản phẩm đã hết hàng', this.product().name);
      return;
    }

    this.adding.set(true);

    this.cart.add(this.product().id, this.quantity(), true).subscribe({
      next: () => {
        this.adding.set(false);
        this.notifications.success(
          'Đã thêm vào giỏ hàng',
          `${this.product().name} (SL: ${this.quantity()})`,
        );
      },
      error: () => this.adding.set(false),
    });
  }

  /** Kiểm tra tương thích nhanh cho riêng sản phẩm này (PRD-10: chỉ cảnh báo, không chặn mua). */
  protected checkCompatibility(): void {
    this.checking.set(true);

    this.catalog.checkCompatibility([{ productId: this.product().id, quantity: this.quantity() }]).subscribe({
      next: (result) => {
        this.compatibility.set(result);
        this.checking.set(false);
      },
      error: () => this.checking.set(false),
    });
  }

  protected warningLabel(code: string): string {
    return COMPATIBILITY_CODE_LABELS[code] ?? code;
  }
}
