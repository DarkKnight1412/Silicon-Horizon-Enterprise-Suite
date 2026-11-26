import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { formatVnd } from '../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import {
  PRODUCT_SORT_OPTIONS,
  type ProductListItemDto,
  type ProductSearchQuery,
  type ProductSortBy,
  type ReferenceItemDto,
} from '../../../core/models/product.model';
import {
  PC_BUILD_TIERS,
  PC_BUILD_TIER_CLASSES,
  PC_BUILD_TIER_LABELS,
  type PcBuildDetailDto,
  type PcBuildListItemDto,
} from '../../../core/models/pc-build.model';
import { CartStore } from '../../../core/services/cart.store';
import { NotificationService } from '../../../core/services/notification.service';
import { CatalogApi } from '../../catalog/catalog-api.service';
import { PcBuildApi } from '../../pc-builds/pc-build-api.service';
import { PcBuilder } from './pc-builder/pc-builder';

/** Ngưỡng hiển thị nhãn "Sắp hết" trên thẻ sản phẩm. */
const LOW_STOCK_THRESHOLD = 5;

/** Số thẻ "xương" hiển thị trong lúc tải (skeleton). */
const SKELETON_COUNT = 8;

/** Chế độ xem của màn mua sắm: tự chọn linh kiện rời · mua bộ dựng sẵn · tự build từng linh kiện. */
type ShopMode = 'parts' | 'builds' | 'builder';

/** Số bộ PC build sẵn mỗi TRANG ở chế độ "Mua theo PC" (bên dưới có nút chuyển trang). */
const SHOP_BUILD_PAGE_SIZE = 12;

@Component({
  selector: 'app-shop',
  imports: [ReactiveFormsModule, RouterLink, PcBuilder],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shop.html',
})
export class Shop {
  private readonly catalog = inject(CatalogApi);
  private readonly pcBuilds = inject(PcBuildApi);
  private readonly cart = inject(CartStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  // ── MỤC "PC BUILD SẴN" hiển thị ngay trong màn mua sắm ─────────────────────
  /** Vài bộ tiêu biểu (bộ ghim ở trang chủ) hiển thị ở đầu trang; rỗng ⇒ ẩn cả mục. */
  protected readonly builds = signal<PcBuildListItemDto[]>([]);
  protected readonly buildsLoading = signal(true);
  protected readonly buildsFailed = signal(false);
  /** Id bộ đang thêm vào giỏ (khoá nút để tránh bấm hai lần). */
  protected readonly addingBuildId = signal<number | null>(null);
  /** Tổng số bộ (để hiện "Tổng n bộ — đang hiển thị m bộ đầu tiên"). */
  protected readonly buildsTotalCount = signal(0);

  /**
   * CHẾ ĐỘ XEM — tab chọn ngay trên dàn bộ lọc:
   * `parts` = mua linh kiện rời (mặc định, giữ nguyên luồng cũ) · `builds` = mua theo bộ PC dựng sẵn.
   */
  protected readonly mode = signal<ShopMode>('parts');

  /** Hạng bộ đang lọc ở chế độ "Mua theo PC"; `null` = tất cả. */
  protected readonly buildTier = signal<number | null>(null);
  protected readonly buildTiers = PC_BUILD_TIERS;

  /** Đã nạp danh sách bộ lần nào chưa (chỉ nạp khi khách mở tab "Mua theo PC" — nạp lười). */
  private buildsLoadedOnce = false;

  /** Trang đang xem của danh sách bộ (mỗi trang `SHOP_BUILD_PAGE_SIZE` bộ). */
  protected readonly buildPage = signal(1);

  /** Tổng số trang danh sách bộ — dùng cho nút chuyển trang. */
  protected readonly buildsTotalPages = computed(() =>
    this.buildsTotalCount() <= 0 ? 1 : Math.ceil(this.buildsTotalCount() / SHOP_BUILD_PAGE_SIZE),
  );

  /** Bộ đang MỞ RỘNG bảng linh kiện trong thẻ (null = không bộ nào mở). */
  protected readonly expandedBuildId = signal<number | null>(null);
  /** Chi tiết của bộ đang mở rộng (nạp từ GET /api/v1/pc-builds/{id} khi bấm "Xem linh kiện"). */
  protected readonly buildDetail = signal<PcBuildDetailDto | null>(null);
  protected readonly buildDetailLoading = signal(false);
  /** Nhớ chi tiết theo id ⇒ bấm mở lại không gọi API lần nữa. */
  private readonly buildDetailCache = new Map<number, PcBuildDetailDto>();
  /** Id bộ có ảnh tải lỗi ⇒ dùng icon thay thế thay vì ảnh vỡ. */
  private readonly failedBuildImages = signal<ReadonlySet<number>>(new Set<number>());

  /** Danh mục (Category Tabs) và thương hiệu (bộ lọc). */
  protected readonly categories = signal<ReferenceItemDto[]>([]);
  protected readonly brands = signal<ReferenceItemDto[]>([]);
  protected readonly sortOptions = PRODUCT_SORT_OPTIONS;
  protected readonly skeletons: readonly number[] = Array.from({ length: SKELETON_COUNT }, (_, i) => i);

  protected readonly loading = signal(false);
  protected readonly loadFailed = signal(false);
  /** Lần tìm gần nhất CÓ điều kiện lọc ⇒ phân biệt "chưa có sản phẩm" với "không tìm thấy". */
  protected readonly filtered = signal(false);
  protected readonly result = signal<PagedResult<ProductListItemDto>>(
    emptyPagedResult<ProductListItemDto>(environment.defaultPageSize),
  );
  protected readonly addingProductId = signal<number | null>(null);

  /** Tab danh mục đang chọn; '' = "Tất cả". */
  protected readonly activeCategory = signal('');

  /** Định dạng tiền dùng trong template (helper dùng chung). */
  protected readonly formatPrice = formatVnd;

  /** Lớp dùng lại trên nền tối, tránh lặp class dài trong HTML. */
  protected readonly labelClass = 'block text-sm font-medium text-slate-300';
  protected readonly inputClass =
    'mt-1 w-full rounded-md border border-white/15 bg-slate-900 px-3 py-2 text-sm text-slate-100 placeholder:text-slate-500 focus:border-brand-400 focus:outline-none';

  /** Bộ lọc dùng typed reactive form — các điều kiện KẾT HỢP được với nhau. */
  protected readonly filters = new FormGroup({
    keyword: new FormControl('', { nonNullable: true }),
    brandId: new FormControl<number | null>(null),
    minPrice: new FormControl<number | null>(null),
    maxPrice: new FormControl<number | null>(null),
    inStockOnly: new FormControl(false, { nonNullable: true }),
    sortBy: new FormControl<ProductSortBy>('newest', { nonNullable: true }),
  });

  /** Dòng "Hiển thị a–b trong n sản phẩm". */
  protected readonly rangeText = computed(() => {
    const page = this.result();
    if (page.totalCount === 0) {
      return '';
    }

    const from = (page.pageNumber - 1) * page.pageSize + 1;
    const to = Math.min(page.totalCount, from + page.pageSize - 1);

    return `Hiển thị ${from}–${to} trong ${page.totalCount} sản phẩm`;
  });

  constructor() {
    // Giỏ hàng: khách vãng lai vẫn có giỏ tạm nên badge trên header luôn đúng.
    this.cart.load().subscribe({ error: () => undefined });

    this.catalog.getCategories().subscribe({
      next: (items) => this.categories.set(items),
      error: () => this.categories.set([]),
    });
    this.catalog.getBrands().subscribe({
      next: (items) => this.brands.set(items),
      error: () => this.brands.set([]),
    });

    // Danh sách bộ PC build sẵn được NẠP LƯỜI: chỉ tải khi khách mở tab "Mua theo PC"
    // (tab mặc định là "Mua linh kiện" nên không tốn request lúc mới vào trang).

    this.search(1, this.result().pageSize);
  }

  /** Lớp Tailwind cho một tab danh mục — tab đang chọn có trạng thái Active rõ ràng. */
  protected tabClass(code: string): string {
    const base = 'whitespace-nowrap rounded-full border px-4 py-1.5 text-sm transition';

    return this.activeCategory() === code
      ? `${base} border-brand-500 bg-brand-600 font-semibold text-white`
      : `${base} border-white/15 text-slate-300 hover:border-white/30 hover:bg-white/10`;
  }

  /** Chọn tab danh mục: chỉ tải lại dữ liệu, KHÔNG reload trang. */
  protected selectCategory(code: string): void {
    if (this.activeCategory() === code) {
      return;
    }

    this.activeCategory.set(code);
    this.search(1, this.result().pageSize);
  }

  /** Áp dụng bộ lọc (submit form hoặc bấm "Tìm kiếm"). */
  protected applyFilters(): void {
    this.search(1, this.result().pageSize);
  }

  /** Xoá toàn bộ điều kiện lọc và quay về tab "Tất cả". */
  protected resetFilters(): void {
    this.filters.reset({
      keyword: '',
      brandId: null,
      minPrice: null,
      maxPrice: null,
      inStockOnly: false,
      sortBy: 'newest',
    });
    this.activeCategory.set('');
    this.search(1, this.result().pageSize);
  }

  protected onPageChange(pageNumber: number): void {
    this.search(pageNumber, this.result().pageSize);
  }

  /** Tải lại khi lần gọi API trước thất bại. */
  protected retry(): void {
    this.search(1, this.result().pageSize);
  }

  /** Nhãn tình trạng tồn kho của thẻ sản phẩm. */
  protected stockLabel(availableQty: number): string {
    if (availableQty <= 0) {
      return 'Hết hàng';
    }

    return availableQty <= LOW_STOCK_THRESHOLD ? `Sắp hết · còn ${availableQty}` : 'Còn hàng';
  }

  /** Lớp màu cho nhãn tình trạng — chỉ dùng 3 gam màu trạng thái để giao diện gọn. */
  protected stockClass(availableQty: number): string {
    if (availableQty <= 0) {
      return 'bg-rose-500/15 text-rose-300';
    }

    return availableQty <= LOW_STOCK_THRESHOLD
      ? 'bg-amber-500/15 text-amber-300'
      : 'bg-emerald-500/15 text-emerald-300';
  }

  // ───────────────────────────────────────────────────────────────────────────
  // MỤC "PC BUILD SẴN" (combo linh kiện) — hiện ở đầu màn mua sắm
  // ───────────────────────────────────────────────────────────────────────────

  /** Đổi chế độ xem: "Mua linh kiện" (rời) ↔ "Mua theo PC" (bộ dựng sẵn). */
  protected setMode(mode: ShopMode): void {
    if (this.mode() === mode) {
      return;
    }

    this.mode.set(mode);

    // Lần đầu mở tab "Mua theo PC" mới tải danh sách bộ (nạp lười, không tốn request vô ích).
    if (mode === 'builds' && !this.buildsLoadedOnce) {
      this.loadBuilds();
    }
  }

  /** Lớp Tailwind cho 2 tab chế độ (tab đang chọn nổi bật giống tab danh mục). */
  protected modeClass(mode: ShopMode): string {
    const base = 'rounded-full border px-4 py-1.5 text-sm transition';

    return this.mode() === mode
      ? `${base} border-brand-500 bg-brand-600 font-semibold text-white`
      : `${base} border-white/15 text-slate-300 hover:border-white/30 hover:bg-white/10`;
  }

  /** Lọc bộ theo hạng ngay trong chế độ "Mua theo PC" (luôn quay về trang 1). */
  protected selectBuildTier(tier: number | null): void {
    if (this.buildTier() === tier) {
      return;
    }

    this.buildTier.set(tier);
    this.buildPage.set(1);
    this.loadBuilds();
  }

  /** Lớp cho tab hạng bộ (Tất cả · Văn phòng · Gaming · Đồ hoạ - Render · Cao cấp). */
  protected buildTabClass(tier: number | null): string {
    const base = 'shrink-0 whitespace-nowrap rounded-full border px-3 py-1.5 text-xs transition';

    return this.buildTier() === tier
      ? `${base} border-brand-500 bg-brand-600 text-white`
      : `${base} border-white/15 text-slate-300 hover:bg-white/10`;
  }

  /** Chuyển trang danh sách bộ trong chế độ "Mua theo PC". */
  protected onBuildPageChange(page: number): void {
    if (page < 1 || page > this.buildsTotalPages() || page === this.buildPage()) {
      return;
    }

    this.buildPage.set(page);
    this.loadBuilds();
  }

  /**
   * Mở/đóng bảng linh kiện của một bộ NGAY TRONG THẺ (không rời màn mua sắm — không còn trang riêng).
   * Chi tiết lấy từ GET /api/v1/pc-builds/{id} và được nhớ theo id nên bấm lại không gọi API nữa.
   */
  protected toggleBuildDetail(build: PcBuildListItemDto): void {
    if (this.expandedBuildId() === build.id) {
      this.expandedBuildId.set(null);
      this.buildDetail.set(null);
      return;
    }

    this.expandedBuildId.set(build.id);

    const cached = this.buildDetailCache.get(build.id);

    if (cached) {
      this.buildDetail.set(cached);
      return;
    }

    this.buildDetail.set(null);
    this.buildDetailLoading.set(true);

    this.pcBuilds.getBuild(build.id).subscribe({
      next: (detail) => {
        this.buildDetailCache.set(build.id, detail);
        this.buildDetail.set(detail);
        this.buildDetailLoading.set(false);
      },
      error: () => this.buildDetailLoading.set(false),
    });
  }

  /**
   * Tải danh sách bộ PC build sẵn theo hạng đang chọn và TRANG đang xem (mặc định 12 bộ/trang).
   * Chỉ chạy ở chế độ "Mua theo PC"; lỗi ở đây KHÔNG chặn danh sách linh kiện — hai chế độ độc lập nhau.
   */
  private loadBuilds(): void {
    this.buildsLoading.set(true);
    this.buildsFailed.set(false);

    this.pcBuilds
      .searchBuilds({
        tier: this.buildTier(),
        sortBy: 'order',
        pageNumber: this.buildPage(),
        pageSize: SHOP_BUILD_PAGE_SIZE,
      })
      .subscribe({
        next: (page) => {
          this.builds.set(page.items);
          this.buildsTotalCount.set(page.totalCount);
          this.buildsLoadedOnce = true;
          this.buildsLoading.set(false);
        },
        error: () => {
          this.builds.set([]);
          this.buildsTotalCount.set(0);
          this.buildsLoading.set(false);
          this.buildsFailed.set(true);
        },
      });
  }

  protected retryBuilds(): void {
    this.loadBuilds();
  }

  /** Nhãn hạng của bộ (Văn phòng · Gaming · Đồ hoạ - Render · Cao cấp). */
  protected tierLabel(tier: number): string {
    return PC_BUILD_TIER_LABELS[tier] ?? 'Khác';
  }

  protected tierClass(tier: number): string {
    return PC_BUILD_TIER_CLASSES[tier] ?? 'bg-slate-500/15 text-slate-300';
  }

  /** Nhãn tình trạng của một BỘ — theo số bộ còn lắp được (tồn nhỏ nhất của các linh kiện). */
  protected buildStockLabel(availableBuilds: number): string {
    if (availableBuilds <= 0) {
      return 'Tạm hết linh kiện';
    }

    return availableBuilds <= LOW_STOCK_THRESHOLD ? `Chỉ còn ${availableBuilds} bộ` : 'Sẵn sàng lắp';
  }

  protected buildStockClass(availableBuilds: number): string {
    if (availableBuilds <= 0) {
      return 'bg-rose-500/15 text-rose-300';
    }

    return availableBuilds <= LOW_STOCK_THRESHOLD
      ? 'bg-amber-500/15 text-amber-300'
      : 'bg-emerald-500/15 text-emerald-300';
  }

  protected showBuildImage(build: PcBuildListItemDto): boolean {
    return build.imageUrl !== null && !this.failedBuildImages().has(build.id);
  }

  protected onBuildImageError(buildId: number): void {
    this.failedBuildImages.update((ids) => new Set(ids).add(buildId));
  }

  /**
   * "Đặt hàng" một bộ PC build sẵn: backend thêm TẤT CẢ linh kiện của bộ vào giỏ trong MỘT giao dịch
   * (thiếu tồn ở bất kỳ món nào ⇒ 409 và không thêm gì), rồi chuyển sang trang giỏ để khách kiểm tra
   * từng linh kiện và chốt đơn.
   */
  protected orderBuild(build: PcBuildListItemDto): void {
    if (build.availableBuilds <= 0) {
      this.notifications.warning(
        'Bộ này tạm hết linh kiện',
        `${build.name} đang thiếu hàng ở một linh kiện. Vui lòng chọn bộ khác.`,
      );
      return;
    }

    this.addingBuildId.set(build.id);

    this.cart.addBuild(build.id, 1).subscribe({
      next: () => {
        this.addingBuildId.set(null);
        this.notifications.success(
          'Đã thêm bộ PC vào giỏ hàng',
          `${build.name} — ${build.componentCount} linh kiện đã được thêm.`,
        );
        void this.router.navigate(['/cart']);
      },
      error: () => this.addingBuildId.set(null),
    });
  }

  /** Sản phẩm đang khuyến mãi (có chương trình KM hoặc giá bán thấp hơn giá niêm yết). */
  protected isPromotion(product: ProductListItemDto): boolean {
    return product.promotionId !== null || product.price < product.basePrice;
  }

  /** Id các sản phẩm có ảnh tải lỗi ⇒ hiển thị icon thay thế thay vì ảnh vỡ. */
  private readonly failedImages = signal<ReadonlySet<number>>(new Set<number>());

  /** Có hiển thị ảnh sản phẩm không (ảnh lỗi/không có ⇒ rơi về icon). */
  protected showImage(product: ProductListItemDto): boolean {
    return product.imageUrl !== null && !this.failedImages().has(product.id);
  }

  /** Ghi nhận ảnh tải lỗi để lần render sau dùng icon thay thế. */
  protected onImageError(productId: number): void {
    this.failedImages.update((ids) => new Set(ids).add(productId));
  }

  protected addToCart(product: ProductListItemDto): void {
    if (product.availableQty <= 0) {
      this.notifications.warning(
        'Sản phẩm đã hết hàng',
        `${product.name} hiện không còn tồn khả dụng.`,
      );
      return;
    }

    this.addingProductId.set(product.id);

    this.cart.add(product.id, 1, false).subscribe({
      next: () => {
        this.addingProductId.set(null);
        this.notifications.success('Đã thêm vào giỏ hàng', `${product.name} (SL: 1)`);
      },
      error: () => this.addingProductId.set(null),
    });
  }

  /** Gọi API tìm kiếm rồi cập nhật các trạng thái loading / lỗi / tìm thấy. */
  private search(pageNumber: number, pageSize: number): void {
    const query = this.buildQuery(pageNumber, pageSize);

    this.filtered.set(hasConditions(query));
    this.loading.set(true);
    this.loadFailed.set(false);

    this.catalog.searchProducts(query).subscribe({
      next: (page) => {
        this.result.set(page);
        this.loading.set(false);
      },
      error: () => {
        this.result.set(emptyPagedResult<ProductListItemDto>(pageSize));
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** Ghép bộ lọc + tab danh mục thành tham số GET /api/v1/products. */
  private buildQuery(pageNumber: number, pageSize: number): ProductSearchQuery {
    const value = this.filters.getRawValue();
    const keyword = value.keyword.trim();
    const categoryCode = this.activeCategory();

    return {
      keyword: keyword.length === 0 ? null : keyword,
      categoryCode: categoryCode.length === 0 ? null : categoryCode,
      brandId: value.brandId,
      minPrice: value.minPrice,
      maxPrice: value.maxPrice,
      inStockOnly: value.inStockOnly,
      sortBy: value.sortBy,
      pageNumber,
      pageSize,
    };
  }
}

/** Có điều kiện lọc nào đang bật không (không tính sắp xếp). */
function hasConditions(query: ProductSearchQuery): boolean {
  return (
    (query.keyword ?? '') !== '' ||
    (query.categoryCode ?? '') !== '' ||
    query.brandId !== null ||
    query.minPrice !== null ||
    query.maxPrice !== null ||
    query.inStockOnly === true
  );
}
