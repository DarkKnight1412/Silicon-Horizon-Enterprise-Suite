import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { type Observable } from 'rxjs';
import { AuthStore } from '../../../core/auth/auth.store';
import { CART_WARNING_LABELS, type CartDto, type CartItemDto } from '../../../core/models/cart.model';
import { formatVnd } from '../../../core/models/common.model';
import { CartStore } from '../../../core/services/cart.store';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';

/** Số dòng "xương" hiển thị trong lúc tải giỏ. */
const SKELETON_ROWS = 3;

/**
 * GIỎ HÀNG — giao diện KHÁCH HÀNG (nằm trong `PublicLayout`, cùng theme với trang `/shop`).
 *
 * Toàn bộ state lấy từ `CartStore` (state toàn cục) nên badge trên header và trang này luôn đồng bộ.
 * Backend tự kiểm tra tồn khi đọc giỏ: món hết hàng bị bỏ tick, món vượt tồn bị giảm số lượng và trả
 * về trong `warnings` — màn hình hiển thị rõ các cảnh báo này.
 * Khách VÃNG LAI vẫn dùng được giỏ (mã phiên X-Cart-Session); bấm đặt hàng sẽ được đưa tới `/login`.
 */
@Component({
  selector: 'app-cart-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cart-page.html',
})
export class CartPage {
  private readonly cart = inject(CartStore);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected readonly formatPrice = formatVnd;
  protected readonly items = this.cart.items;
  protected readonly totals = this.cart.totals;
  protected readonly warnings = this.cart.warnings;
  protected readonly loading = this.cart.loading;
  protected readonly isAuthenticated = this.authStore.isAuthenticated;

  /** Dòng đang gọi API (để khoá nút tăng/giảm của riêng dòng đó). */
  protected readonly busyProductId = signal<number | null>(null);

  /** Dòng "xương" cho trạng thái đang tải. */
  protected readonly skeletonRows: readonly number[] = Array.from(
    { length: SKELETON_ROWS },
    (_, i) => i,
  );

  /** Id các dòng có ảnh tải lỗi ⇒ dùng icon thay thế thay vì ảnh vỡ. */
  private readonly failedImages = signal<ReadonlySet<number>>(new Set<number>());

  protected readonly canCheckout = computed(() => this.totals().selectedCount > 0);
  protected readonly allSelected = computed(() => {
    const items = this.items();
    return items.length > 0 && items.every((item) => item.isSelected);
  });

  constructor() {
    this.cart.load().subscribe({ error: () => undefined });
  }

  /** Có hiển thị ảnh không (không có ảnh hoặc ảnh tải lỗi ⇒ dùng icon thay thế). */
  protected showImage(imageUrl: string | null, key: number): boolean {
    return imageUrl !== null && !this.failedImages().has(key);
  }

  /** Ghi nhận ảnh tải lỗi để lần render sau dùng icon. */
  protected onImageError(key: number): void {
    this.failedImages.update((ids) => new Set(ids).add(key));
  }

  protected increase(item: CartItemDto): void {
    if (item.quantity >= item.availableQty) {
      this.notifications.warning(
        'Vượt tồn khả dụng',
        `${item.name} chỉ còn ${item.availableQty} sản phẩm.`,
      );
      return;
    }

    this.mutate(item.productId, this.cart.updateQuantity(item.productId, item.quantity + 1));
  }

  protected decrease(item: CartItemDto): void {
    if (item.quantity <= 1) {
      void this.remove(item);
      return;
    }

    this.mutate(item.productId, this.cart.updateQuantity(item.productId, item.quantity - 1));
  }

  protected toggleSelect(item: CartItemDto): void {
    this.mutate(item.productId, this.cart.setSelected(item.productId, !item.isSelected));
  }

  protected async remove(item: CartItemDto): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Xoá sản phẩm khỏi giỏ',
      message: `Bạn chắc chắn muốn xoá "${item.name}" khỏi giỏ hàng?`,
      confirmLabel: 'Xoá',
      danger: true,
    });

    if (!confirmed) {
      return;
    }

    this.mutate(item.productId, this.cart.remove(item.productId));
  }

  protected selectAll(isSelected: boolean): void {
    this.busyProductId.set(0);
    this.cart.selectAll(isSelected).subscribe({
      next: () => this.busyProductId.set(null),
      error: () => this.busyProductId.set(null),
    });
  }

  /** Bước tiếp theo là ĐẶT HÀNG (route yêu cầu đăng nhập + vai trò Customer). */
  protected goToCheckout(): void {
    void this.router.navigate(['/orders/checkout']);
  }

  protected warningLabel(code: string): string {
    return CART_WARNING_LABELS[code] ?? code;
  }

  private mutate(productId: number, request$: Observable<CartDto>): void {
    this.busyProductId.set(productId);

    request$.subscribe({
      next: () => this.busyProductId.set(null),
      error: () => this.busyProductId.set(null),
    });
  }
}
