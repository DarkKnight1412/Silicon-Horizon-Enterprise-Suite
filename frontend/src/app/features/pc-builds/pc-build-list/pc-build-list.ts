import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { formatVnd } from '../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import {
  PC_BUILD_SORT_OPTIONS,
  PC_BUILD_TIERS,
  PC_BUILD_TIER_CLASSES,
  PC_BUILD_TIER_LABELS,
  type PcBuildListItemDto,
  type PcBuildSearchQuery,
  type PcBuildSortBy,
} from '../../../core/models/pc-build.model';
import { CartStore } from '../../../core/services/cart.store';
import { NotificationService } from '../../../core/services/notification.service';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { PcBuildApi } from '../pc-build-api.service';

/** Số thẻ "xương" hiển thị trong lúc tải (skeleton). */
const SKELETON_COUNT = 6;

/** Ngưỡng hiển thị nhãn "Chỉ còn n bộ". */
const LOW_BUILDS_THRESHOLD = 3;

/**
 * Danh sách PC BUILD SẴN (`/pc-builds`).
 *
 * Ý tưởng nghiệp vụ: thay vì tự chọn 6-7 linh kiện, khách chọn một bộ cửa hàng đã dựng sẵn rồi bấm
 * "Thêm vào giỏ" — hệ thống thêm TẤT CẢ linh kiện của bộ trong MỘT lần (backend làm trong một giao dịch).
 *
 * Giá hiển thị = `partsPrice` = tổng giá linh kiện theo giá hiện hành ⇒ bằng đúng số tiền vào giỏ.
 * State cục bộ bằng signal (danh sách chỉ phục vụ màn hình này); giỏ hàng dùng `CartStore` toàn cục
 * nên badge trên header tự cập nhật sau khi thêm.
 */
@Component({
  selector: 'app-pc-build-list',
  imports: [ReactiveFormsModule, RouterLink, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pc-build-list.html',
})
export class PcBuildList {
  private readonly api = inject(PcBuildApi);
  private readonly cart = inject(CartStore);
  private readonly notifications = inject(NotificationService);

  protected readonly tiers = PC_BUILD_TIERS;
  protected readonly sortOptions = PC_BUILD_SORT_OPTIONS;
  protected readonly skeletons: readonly number[] = Array.from({ length: SKELETON_COUNT }, (_, i) => i);
  protected readonly formatPrice = formatVnd;

  protected readonly loading = signal(false);
  protected readonly loadFailed = signal(false);
  /** Lần tìm gần nhất CÓ điều kiện lọc ⇒ phân biệt "chưa có bộ" với "không tìm thấy". */
  protected readonly filtered = signal(false);
  protected readonly result = signal<PagedResult<PcBuildListItemDto>>(
    emptyPagedResult<PcBuildListItemDto>(environment.defaultPageSize),
  );

  /** Id bộ đang thêm vào giỏ (khoá nút để tránh bấm hai lần). */
  protected readonly addingBuildId = signal<number | null>(null);

  /** Hạng đang chọn (tab); `null` = "Tất cả". */
  protected readonly activeTier = signal<number | null>(null);

  /** Tên bộ vừa thêm LỖI ⇒ hiện dòng nhắc thử lại (chi tiết lỗi do interceptor hiện toast). */
  protected readonly lastError = signal<string | null>(null);

  /** Bộ lọc — kết hợp được với nhau (từ khoá · sắp xếp · chỉ bộ còn hàng). */
  protected readonly filters = new FormGroup({
    keyword: new FormControl('', { nonNullable: true }),
    sortBy: new FormControl<PcBuildSortBy>('order', { nonNullable: true }),
    inStockOnly: new FormControl(false, { nonNullable: true }),
  });

  /** Lớp dùng lại trên nền tối (giống trang /shop để giao diện đồng nhất). */
  protected readonly labelClass = 'block text-sm font-medium text-slate-300';
  protected readonly inputClass =
    'mt-1 w-full rounded-md border border-white/15 bg-slate-900 px-3 py-2 text-sm text-slate-100 placeholder:text-slate-500 focus:border-brand-400 focus:outline-none';

  constructor() {
    this.search(1, environment.defaultPageSize);
  }

  /** Nhãn hạng tiếng Việt (bảng tra ở `pc-build.model.ts`). */
  protected tierLabel(tier: number): string {
    return PC_BUILD_TIER_LABELS[tier] ?? 'Khác';
  }

  /** Màu nhãn hạng. */
  protected tierClass(tier: number): string {
    return PC_BUILD_TIER_CLASSES[tier] ?? 'bg-slate-500/15 text-slate-300';
  }

  /** Lớp cho tab hạng (tab đang chọn nổi bật). */
  protected tabClass(tier: number | null): string {
    const base = 'shrink-0 whitespace-nowrap rounded-md border px-3 py-1.5 text-sm transition';

    return this.activeTier() === tier
      ? `${base} border-brand-500 bg-brand-600 text-white`
      : `${base} border-white/15 text-slate-300 hover:bg-white/10`;
  }

  protected selectTier(tier: number | null): void {
    this.activeTier.set(tier);
    this.search(1, this.result().pageSize);
  }

  protected applyFilters(): void {
    this.search(1, this.result().pageSize);
  }

  protected resetFilters(): void {
    this.filters.reset({ keyword: '', sortBy: 'order', inStockOnly: false });
    this.activeTier.set(null);
    this.search(1, this.result().pageSize);
  }

  protected changePage(page: number): void {
    this.search(page, this.result().pageSize);
  }

  protected changePageSize(pageSize: number): void {
    this.search(1, pageSize);
  }

  protected retry(): void {
    this.search(this.result().pageNumber, this.result().pageSize);
  }

  /** Nhãn tình trạng theo số bộ còn lắp được (tồn nhỏ nhất của các linh kiện). */
  protected stockLabel(availableBuilds: number): string {
    if (availableBuilds <= 0) {
      return 'Tạm hết linh kiện';
    }

    return availableBuilds <= LOW_BUILDS_THRESHOLD ? `Chỉ còn ${availableBuilds} bộ` : 'Sẵn sàng lắp';
  }

  protected stockClass(availableBuilds: number): string {
    if (availableBuilds <= 0) {
      return 'bg-rose-500/15 text-rose-300';
    }

    return availableBuilds <= LOW_BUILDS_THRESHOLD
      ? 'bg-amber-500/15 text-amber-300'
      : 'bg-emerald-500/15 text-emerald-300';
  }

  /** Id bộ có ảnh tải lỗi ⇒ dùng icon thay thế thay vì ảnh vỡ. */
  private readonly failedImages = signal<ReadonlySet<number>>(new Set<number>());

  protected showImage(build: PcBuildListItemDto): boolean {
    return build.imageUrl !== null && !this.failedImages().has(build.id);
  }

  protected onImageError(buildId: number): void {
    this.failedImages.update((ids) => new Set(ids).add(buildId));
  }

  /**
   * Thêm CẢ BỘ vào giỏ hàng (POST /api/v1/pc-builds/{id}/add-to-cart).
   * Backend kiểm tra tồn từng linh kiện: thiếu ⇒ 409 và KHÔNG thêm gì (interceptor hiện toast lỗi),
   * tại đây chỉ hiện dòng nhắc thử lại cho đúng thẻ vừa bấm.
   */
  protected addToCart(build: PcBuildListItemDto): void {
    if (build.availableBuilds <= 0) {
      this.notifications.warning(
        'Bộ này tạm hết linh kiện',
        `${build.name} đang thiếu hàng ở một linh kiện nào đó. Vui lòng chọn bộ khác.`,
      );
      return;
    }

    this.addingBuildId.set(build.id);
    this.lastError.set(null);

    this.cart.addBuild(build.id, 1).subscribe({
      next: () => {
        this.addingBuildId.set(null);
        this.notifications.success(
          'Đã thêm bộ PC vào giỏ hàng',
          `${build.name} — ${build.componentCount} linh kiện đã được thêm.`,
        );
      },
      error: () => {
        this.addingBuildId.set(null);
        this.lastError.set(build.name);
      },
    });
  }

  /** Gọi API danh sách rồi cập nhật loading / lỗi / trạng thái "có lọc". */
  private search(pageNumber: number, pageSize: number): void {
    const query = this.buildQuery(pageNumber, pageSize);

    this.filtered.set(hasConditions(query));
    this.loading.set(true);
    this.loadFailed.set(false);

    this.api.searchBuilds(query).subscribe({
      next: (page) => {
        this.result.set(page);
        this.loading.set(false);
      },
      error: () => {
        this.result.set(emptyPagedResult<PcBuildListItemDto>(pageSize));
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** Ghép bộ lọc + tab hạng thành tham số GET /api/v1/pc-builds. */
  private buildQuery(pageNumber: number, pageSize: number): PcBuildSearchQuery {
    const value = this.filters.getRawValue();
    const keyword = value.keyword.trim();

    return {
      keyword: keyword.length === 0 ? null : keyword,
      tier: this.activeTier(),
      inStockOnly: value.inStockOnly,
      sortBy: value.sortBy,
      pageNumber,
      pageSize,
    };
  }
}

/** Có điều kiện lọc nào đang bật không (không tính sắp xếp). */
function hasConditions(query: PcBuildSearchQuery): boolean {
  return (query.keyword ?? '') !== '' || query.tier !== null || query.inStockOnly === true;
}
