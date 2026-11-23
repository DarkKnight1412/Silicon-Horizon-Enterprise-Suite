import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
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
import { CartStore } from '../../../core/services/cart.store';
import { NotificationService } from '../../../core/services/notification.service';
import { CatalogApi } from '../../catalog/catalog-api.service';

/** Ngưỡng hiển thị nhãn "Sắp hết" trên thẻ sản phẩm. */
const LOW_STOCK_THRESHOLD = 5;

/** Số thẻ "xương" hiển thị trong lúc tải (skeleton). */
const SKELETON_COUNT = 8;

/**
 * MUA SẮM (công khai) — trang bán hàng cho KHÁCH HÀNG, nằm trong `PublicLayout`
 * (nút "Đăng nhập" vẫn ở góc phải trên).
 *
 * Bốn khối: tiêu đề trang → Category Tabs → Filter Toolbar → Product Card Grid.
 * Dữ liệu từ API CÔNG KHAI `/api/v1/products`, `/api/v1/categories`, `/api/v1/brands`.
 * Chuyển tab / áp bộ lọc chỉ gọi lại API (SPA) — KHÔNG reload trang.
 * Thêm giỏ dùng `CartStore` nên khách VÃNG LAI vẫn mua được (mã phiên `X-Cart-Session`).
 */
@Component({
  selector: 'app-shop',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shop.html',
})
export class Shop {
  private readonly catalog = inject(CatalogApi);
  private readonly cart = inject(CartStore);
  private readonly notifications = inject(NotificationService);

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
