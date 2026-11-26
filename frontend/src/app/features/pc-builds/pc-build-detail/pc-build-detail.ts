import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { formatVnd } from '../../../core/models/common.model';
import {
  PC_BUILD_TIER_CLASSES,
  PC_BUILD_TIER_LABELS,
  type PcBuildComponentDto,
  type PcBuildDetailDto,
} from '../../../core/models/pc-build.model';
import { CartStore } from '../../../core/services/cart.store';
import { NotificationService } from '../../../core/services/notification.service';

/** Tình trạng tồn của một linh kiện trong bộ, tính theo số bộ khách đang chọn. */
type ComponentStock = 'ok' | 'low' | 'short';

/** Ngưỡng coi là "sắp hết" của một linh kiện. */
const LOW_COMPONENT_THRESHOLD = 5;

/**
 * Chi tiết bộ PC build sẵn (`/pc-builds/:id`).
 *
 * Dữ liệu do `pcBuildDetailResolver` nạp TRƯỚC khi vào trang nên màn hình không cần trạng thái "đang tải";
 * `withComponentInputBinding()` bind thẳng kết quả vào input `build`.
 *
 * Đây là nơi khách "đặt hàng theo bộ": chọn số bộ rồi bấm "Thêm vào giỏ" (ở lại xem tiếp) hoặc
 * "Mua ngay" (thêm rồi sang trang giỏ để kiểm tra và đặt hàng). Backend kiểm tra tồn CỦA TỪNG linh kiện
 * trong một giao dịch; thiếu tồn ⇒ không thêm gì và báo rõ tên linh kiện thiếu.
 */
@Component({
  selector: 'app-pc-build-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pc-build-detail.html',
})
export class PcBuildDetail {
  private readonly cart = inject(CartStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  /** Bộ PC build sẵn — resolver bind tự động vào input này. */
  readonly build = input.required<PcBuildDetailDto>();

  protected readonly formatPrice = formatVnd;

  /** Đang gọi API thêm vào giỏ. */
  protected readonly adding = signal(false);

  /** Số bộ khách muốn thêm. */
  protected readonly quantity = signal(1);

  protected readonly tierLabel = computed(() => PC_BUILD_TIER_LABELS[this.build().tier] ?? 'Khác');

  protected readonly tierClass = computed(
    () => PC_BUILD_TIER_CLASSES[this.build().tier] ?? 'bg-slate-500/15 text-slate-300',
  );

  /** Tổng tiền theo số bộ đang chọn — khớp số tiền sẽ vào giỏ. */
  protected readonly totalPrice = computed(() => this.build().partsPrice * this.quantity());

  /** Số bộ TỐI ĐA có thể thêm (bộ đang thiếu linh kiện ⇒ 0). */
  protected readonly maxQuantity = computed(() => Math.max(0, this.build().availableBuilds));

  /** Còn thêm được không (đủ linh kiện và không đang gọi API). */
  protected readonly canAdd = computed(() => this.maxQuantity() > 0 && !this.adding());

  /** Tổng số linh kiện sẽ được thêm vào giỏ theo số bộ đang chọn. */
  protected readonly totalUnits = computed(
    () => this.build().components.reduce((sum, component) => sum + component.quantity, 0) * this.quantity(),
  );

  /** Linh kiện không đủ tồn cho số bộ đang chọn (dùng cho khối cảnh báo ở đầu trang). */
  protected readonly shortComponents = computed(() =>
    this.build().components.filter((component) => this.componentStock(component) === 'short'),
  );

  protected changeQuantity(delta: number): void {
    const next = this.quantity() + delta;
    const max = this.maxQuantity();

    if (next < 1) {
      this.quantity.set(1);
      return;
    }

    this.quantity.set(max > 0 && next > max ? max : next);
  }

  protected onQuantityInput(event: Event): void {
    const target = event.target;

    if (!(target instanceof HTMLInputElement)) {
      return;
    }

    const parsed = Number(target.value);
    const max = this.maxQuantity();
    const safe = Number.isFinite(parsed) && parsed >= 1 ? Math.floor(parsed) : 1;

    this.quantity.set(max > 0 && safe > max ? max : safe);
  }

  /** Tình trạng tồn của một linh kiện theo số bộ đang chọn. */
  protected componentStock(component: PcBuildComponentDto): ComponentStock {
    if (component.availableQty < component.quantity * this.quantity()) {
      return 'short';
    }

    return component.availableQty <= LOW_COMPONENT_THRESHOLD ? 'low' : 'ok';
  }

  protected componentStockLabel(component: PcBuildComponentDto): string {
    switch (this.componentStock(component)) {
      case 'short':
        return 'Không đủ cho số bộ đã chọn';
      case 'low':
        return `Sắp hết (còn ${component.availableQty})`;
      default:
        return 'Còn hàng';
    }
  }

  protected componentStockClass(component: PcBuildComponentDto): string {
    switch (this.componentStock(component)) {
      case 'short':
        return 'bg-rose-500/15 text-rose-300';
      case 'low':
        return 'bg-amber-500/15 text-amber-300';
      default:
        return 'bg-emerald-500/15 text-emerald-300';
    }
  }

  /** Thêm cả bộ vào giỏ rồi Ở LẠI trang để khách xem tiếp. */
  protected addToCart(): void {
    this.add(false);
  }

  /** Thêm cả bộ rồi sang trang giỏ để kiểm tra và đặt hàng. */
  protected buyNow(): void {
    this.add(true);
  }

  /** Gọi API thêm cả bộ: backend xử lý mọi linh kiện trong một giao dịch. */
  private add(goToCart: boolean): void {
    const build = this.build();

    if (!this.canAdd()) {
      this.notifications.warning(
        'Chưa thể thêm bộ này',
        'Bộ đang thiếu tồn ở một linh kiện. Vui lòng giảm số bộ hoặc chọn bộ khác.',
      );
      return;
    }

    const quantity = this.quantity();
    this.adding.set(true);

    this.cart.addBuild(build.id, quantity).subscribe({
      next: () => {
        this.adding.set(false);
        this.notifications.success(
          'Đã thêm bộ PC vào giỏ hàng',
          `${build.name} × ${quantity} bộ — ${this.totalUnits()} linh kiện đã được thêm.`,
        );

        if (goToCart) {
          void this.router.navigate(['/cart']);
        }
      },
      error: () => this.adding.set(false),
    });
  }
}
