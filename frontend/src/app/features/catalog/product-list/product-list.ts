import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { CatalogApi } from '../catalog-api.service';

/**
 * Danh sách sản phẩm (GET /api/v1/products).
 *
 * State: bộ lọc + kết quả là SIGNAL CỤC BỘ của màn hình (không đưa lên store toàn cục) nhưng được
 * đồng bộ vào query param của URL ⇒ F5 hoặc chia sẻ link vẫn giữ đúng điều kiện tìm kiếm.
 * Hỗ trợ cả lọc theo thông số kỹ thuật: `specFilters=memoryType:DDR5,capacityGbPerKit:32`.
 */
@Component({
  selector: 'app-product-list',
  imports: [ReactiveFormsModule, RouterLink, Pagination, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-list.html',
})
export class ProductList {
  private readonly catalog = inject(CatalogApi);
  private readonly cart = inject(CartStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Danh mục/thương hiệu cho bộ lọc (nạp một lần khi mở màn hình). */
  protected readonly categories = signal<ReferenceItemDto[]>([]);
  protected readonly brands = signal<ReferenceItemDto[]>([]);

  protected readonly sortOptions = PRODUCT_SORT_OPTIONS;
  protected readonly loading = signal(false);
  protected readonly result = signal<PagedResult<ProductListItemDto>>(
    emptyPagedResult<ProductListItemDto>(environment.defaultPageSize),
  );
  protected readonly addingProductId = signal<number | null>(null);

  /** Hàm định dạng tiền dùng trong template (Intl.NumberFormat tạo một lần ở helper dùng chung). */
  protected readonly formatPrice = formatVnd;

  /** Bộ lọc dùng typed reactive form. */
  protected readonly filters = new FormGroup({
    keyword: new FormControl('', { nonNullable: true }),
    categoryCode: new FormControl('', { nonNullable: true }),
    brandId: new FormControl<number | null>(null),
    minPrice: new FormControl<number | null>(null),
    maxPrice: new FormControl<number | null>(null),
    specFilters: new FormControl('', { nonNullable: true }),
    inStockOnly: new FormControl(false, { nonNullable: true }),
    onPromotion: new FormControl(false, { nonNullable: true }),
    sortBy: new FormControl<ProductSortBy>('newest', { nonNullable: true }),
  });

  constructor() {
    this.catalog.getCategories().subscribe({
      next: (items) => this.categories.set(items),
      error: () => this.categories.set([]),
    });
    this.catalog.getBrands().subscribe({
      next: (items) => this.brands.set(items),
      error: () => this.brands.set([]),
    });

    this.applyQueryParams();
    this.search(1, this.result().pageSize);
  }

  protected search(pageNumber: number, pageSize: number): void {
    const value = this.filters.getRawValue();
    const keyword = value.keyword.trim();
    const specFilters = value.specFilters.trim();

    const query: ProductSearchQuery = {
      keyword: keyword.length > 0 ? keyword : null,
      categoryCode: value.categoryCode.length > 0 ? value.categoryCode : null,
      brandId: value.brandId,
      minPrice: value.minPrice,
      maxPrice: value.maxPrice,
      specFilters: specFilters.length > 0 ? specFilters : null,
      inStockOnly: value.inStockOnly,
      onPromotion: value.onPromotion,
      sortBy: value.sortBy,
      pageNumber,
      pageSize,
    };

    this.loading.set(true);
    this.syncQueryParams(query);

    this.catalog.searchProducts(query).subscribe({
      next: (page) => {
        this.result.set(page);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  /** Bỏ toàn bộ điều kiện lọc và tải lại từ trang 1. */
  protected resetFilters(): void {
    this.filters.reset({
      keyword: '',
      categoryCode: '',
      brandId: null,
      minPrice: null,
      maxPrice: null,
      specFilters: '',
      inStockOnly: false,
      onPromotion: false,
      sortBy: 'newest',
    });

    this.search(1, this.result().pageSize);
  }

  protected onPageChange(pageNumber: number): void {
    this.search(pageNumber, this.result().pageSize);
  }

  protected onPageSizeChange(pageSize: number): void {
    this.search(1, pageSize);
  }

  /** Thêm vào giỏ hàng (CART-01) — CartStore tự cập nhật badge trên header. */
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

  /** Đọc bộ lọc từ query param để giữ nguyên trạng thái khi tải lại trang. */
  private applyQueryParams(): void {
    const params = this.route.snapshot.queryParamMap;

    this.filters.patchValue({
      keyword: params.get('keyword') ?? '',
      categoryCode: params.get('categoryCode') ?? '',
      brandId: toNumberOrNull(params.get('brandId')),
      minPrice: toNumberOrNull(params.get('minPrice')),
      maxPrice: toNumberOrNull(params.get('maxPrice')),
      specFilters: params.get('specFilters') ?? '',
      inStockOnly: params.get('inStockOnly') === 'true',
      onPromotion: params.get('onPromotion') === 'true',
      sortBy: toSortBy(params.get('sortBy')),
    });
  }

  /** Ghi điều kiện lọc hiện tại vào URL (replaceUrl để không làm rối lịch sử trình duyệt). */
  private syncQueryParams(query: ProductSearchQuery): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        keyword: query.keyword,
        categoryCode: query.categoryCode,
        brandId: query.brandId,
        minPrice: query.minPrice,
        maxPrice: query.maxPrice,
        specFilters: query.specFilters,
        inStockOnly: query.inStockOnly === true ? 'true' : null,
        onPromotion: query.onPromotion === true ? 'true' : null,
        sortBy: query.sortBy,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }
}

function toNumberOrNull(value: string | null): number | null {
  if (value === null || value.length === 0) {
    return null;
  }

  const parsed = Number(value);

  return Number.isFinite(parsed) ? parsed : null;
}

function toSortBy(value: string | null): ProductSortBy {
  const option = PRODUCT_SORT_OPTIONS.find((item) => item.value === value);

  return option?.value ?? 'newest';
}
