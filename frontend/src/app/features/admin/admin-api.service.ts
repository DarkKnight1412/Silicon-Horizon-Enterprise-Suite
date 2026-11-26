import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type {
  BranchDto,
  BranchListQuery,
  BrandDto,
  BrandListQuery,
  CategoryDto,
  CreateBrandRequest,
  CreateCategoryRequest,
  SetBrandStatusRequest,
  SetCategoryStatusRequest,
  UpdateBrandRequest,
} from '../../core/models/category.model';
import type { PageQuery, PagedResult } from '../../core/models/paged-result.model';

/**
 * API quản trị danh mục cấu hình (chỉ Admin): danh mục, thương hiệu, chi nhánh.
 *
 * Lưu ý về endpoint:
 * - `GET /api/v1/admin/categories` trả `CategoryDto` CÓ `specType` — cần cho form sản phẩm động
 *   (endpoint công khai `/api/v1/categories` không trả trường này).
 * - Danh mục hiện chỉ có tạo + đổi trạng thái (không có PUT) nên màn hình quản trị không cho sửa tên.
 */
@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/admin/categories */
  listCategories(page: PageQuery): Observable<PagedResult<CategoryDto>> {
    return this.api.get<PagedResult<CategoryDto>>('/v1/admin/categories', {
      params: { pageNumber: page.pageNumber, pageSize: page.pageSize },
    });
  }

  /** POST /api/v1/admin/categories */
  createCategory(request: CreateCategoryRequest): Observable<CategoryDto> {
    return this.api.post<CategoryDto>('/v1/admin/categories', request, { silent: true });
  }

  /** PATCH /api/v1/admin/categories/{id}/status */
  setCategoryStatus(id: number, request: SetCategoryStatusRequest): Observable<void> {
    return this.api.patch<void>(`/v1/admin/categories/${id}/status`, request);
  }

  /** GET /api/v1/admin/brands */
  listBrands(query: BrandListQuery): Observable<PagedResult<BrandDto>> {
    return this.api.get<PagedResult<BrandDto>>('/v1/admin/brands', {
      params: {
        keyword: query.keyword ?? null,
        isActive: query.isActive ?? null,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /** POST /api/v1/admin/brands */
  createBrand(request: CreateBrandRequest): Observable<BrandDto> {
    return this.api.post<BrandDto>('/v1/admin/brands', request, { silent: true });
  }

  /** PUT /api/v1/admin/brands/{id} */
  updateBrand(id: number, request: UpdateBrandRequest): Observable<BrandDto> {
    return this.api.put<BrandDto>(`/v1/admin/brands/${id}`, request, { silent: true });
  }

  /** PATCH /api/v1/admin/brands/{id}/status */
  setBrandStatus(id: number, request: SetBrandStatusRequest): Observable<void> {
    return this.api.patch<void>(`/v1/admin/brands/${id}/status`, request);
  }

  /** GET /api/v1/admin/branches — danh sách chi nhánh (chỉ Admin) để tra cứu mã chi nhánh. */
  listBranches(query: BranchListQuery): Observable<PagedResult<BranchDto>> {
    return this.api.get<PagedResult<BranchDto>>('/v1/admin/branches', {
      params: {
        keyword: query.keyword ?? null,
        isActive: query.isActive ?? null,
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /**
   * POST /api/v1/admin/products/images — upload MỘT ảnh sản phẩm (multipart/form-data: `sku` + `file`).
   *
   * Trả URL **đầy đủ** của ảnh trên API (VD `http://localhost:5270/images/products/...jpg`) để client ghi
   * vào `ProductImages.Url` khi lưu sản phẩm. Nhờ vậy ảnh KHÔNG phải nằm trong mã nguồn web khách hàng —
   * thêm/sửa ảnh không cần build lại FE (Database/README.md mục 3.3).
   */
  uploadProductImage(sku: string, file: File): Observable<{ url: string }> {
    const form = new FormData();
    form.append('sku', sku);
    form.append('file', file);

    return this.api.post<{ url: string }>('/v1/admin/products/images', form);
  }
}
