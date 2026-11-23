import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { PagedResult } from '../../core/models/paged-result.model';
import type {
  CompatibilityCheckResultDto,
  CompatibilityItem,
  CreateProductRequest,
  ProductDetailDto,
  ProductListItemDto,
  ProductSearchQuery,
  ReferenceItemDto,
  SetProductStatusRequest,
  UpdateProductRequest,
} from '../../core/models/product.model';

/**
 * Module danh mục sản phẩm (module "Catalog" của backend).
 * Chỉ chứa lời gọi HTTP + kiểu dữ liệu; state nằm ở signal cục bộ của từng màn hình
 * (danh sách sản phẩm là dữ liệu chỉ phục vụ một trang ⇒ không cần store toàn cục).
 */
@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/products — tìm kiếm + lọc + sắp xếp + phân trang. */
  searchProducts(query: ProductSearchQuery): Observable<PagedResult<ProductListItemDto>> {
    return this.api.get<PagedResult<ProductListItemDto>>('/v1/products', {
      params: {
        keyword: query.keyword ?? null,
        categoryCode: query.categoryCode ?? null,
        brandId: query.brandId ?? null,
        customerGroupId: query.customerGroupId ?? null,
        minPrice: query.minPrice ?? null,
        maxPrice: query.maxPrice ?? null,
        inStockOnly: query.inStockOnly ?? false,
        onPromotion: query.onPromotion ?? false,
        specFilters: query.specFilters ?? null,
        sortBy: query.sortBy ?? 'newest',
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /** GET /api/v1/products/{id} — chi tiết kèm thông số, ảnh và tồn từng chi nhánh. */
  getProduct(id: number, customerGroupId?: number | null): Observable<ProductDetailDto> {
    return this.api.get<ProductDetailDto>(`/v1/products/${id}`, {
      params: { customerGroupId: customerGroupId ?? null },
    });
  }

  /** GET /api/v1/categories — danh mục đang hoạt động (dùng cho bộ lọc). */
  getCategories(): Observable<ReferenceItemDto[]> {
    return this.api.get<ReferenceItemDto[]>('/v1/categories');
  }

  /** GET /api/v1/brands — thương hiệu đang hoạt động (dùng cho bộ lọc). */
  getBrands(): Observable<ReferenceItemDto[]> {
    return this.api.get<ReferenceItemDto[]>('/v1/brands');
  }

  /** POST /api/v1/compatibility/check — chỉ trả cảnh báo, không chặn đặt hàng. */
  checkCompatibility(items: readonly CompatibilityItem[]): Observable<CompatibilityCheckResultDto> {
    return this.api.post<CompatibilityCheckResultDto>('/v1/compatibility/check', { items });
  }

  // ───────────────────────────────────────────────────────────────────────────
  // Quản trị sản phẩm (Admin) — /api/v1/admin/products
  // ───────────────────────────────────────────────────────────────────────────

  /** POST /api/v1/admin/products — trả về { productId } mới tạo. */
  createProduct(request: CreateProductRequest): Observable<{ productId: number }> {
    return this.api.post<{ productId: number }>('/v1/admin/products', request, { silent: true });
  }

  /** PUT /api/v1/admin/products/{id} — thay toàn bộ thông số/ảnh. */
  updateProduct(id: number, request: UpdateProductRequest): Observable<void> {
    return this.api.put<void>(`/v1/admin/products/${id}`, request, { silent: true });
  }

  /** PATCH /api/v1/admin/products/{id}/status — 0 Draft, 1 Active, 2 Discontinued. */
  setProductStatus(id: number, request: SetProductStatusRequest): Observable<void> {
    return this.api.patch<void>(`/v1/admin/products/${id}/status`, request);
  }

  /**
   * Kiểm tra SKU đã tồn tại chưa (dùng cho async validator ở form sản phẩm).
   * Backend chưa có endpoint tra riêng nên gọi tìm kiếm theo keyword (proc tìm cả đầu mã SKU)
   * rồi so khớp chính xác SKU. Lỗi tra cứu ⇒ trả true để không chặn người dùng; backend vẫn
   * kiểm tra trùng lần cuối (proc báo 50005 → 409).
   */
  isSkuAvailable(sku: string): Observable<boolean> {
    const wanted = sku.trim().toLowerCase();

    return this.api
      .get<PagedResult<ProductListItemDto>>('/v1/products', {
        params: { keyword: sku, pageNumber: 1, pageSize: 20 },
        silent: true,
      })
      .pipe(
        map((page) => !page.items.some((item) => item.sku.toLowerCase() === wanted)),
        catchError(() => of(true)),
      );
  }
}
