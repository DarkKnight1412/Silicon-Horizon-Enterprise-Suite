/**
 * DTO danh mục & thương hiệu cho màn hình quản trị — khớp
 * `Features/Catalog/Dto/CategoryDtos.cs` và `Features/Admin/Dto/BrandDtos.cs`.
 * Lưu ý: `/api/v1/categories` (công khai) KHÔNG trả SpecType, nên form sản phẩm động dùng
 * `/api/v1/admin/categories` để biết bộ thông số cần hiển thị.
 */

/** Danh mục quản trị (CategoryDto) — có SpecType để quyết định bộ trường thông số. */
export interface CategoryDto {
  id: number;
  code: string;
  name: string;
  /** 0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU. */
  specType: number;
  parentId: number | null;
  isActive: boolean;
}

/** POST /api/v1/admin/categories */
export interface CreateCategoryRequest {
  code: string;
  name: string;
  specType: number;
  parentId: number | null;
}

/** PATCH /api/v1/admin/categories/{id}/status */
export interface SetCategoryStatusRequest {
  isActive: boolean;
}

/** Thương hiệu quản trị (BrandDto). */
export interface BrandDto {
  id: number;
  name: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

/** POST /api/v1/admin/brands */
export interface CreateBrandRequest {
  name: string;
}

/** PUT /api/v1/admin/brands/{id} */
export interface UpdateBrandRequest {
  name: string;
}

/** PATCH /api/v1/admin/brands/{id}/status */
export interface SetBrandStatusRequest {
  isActive: boolean;
}

/** Tham số lọc danh sách thương hiệu (BrandListQuery). */
export interface BrandListQuery {
  keyword?: string | null;
  isActive?: boolean | null;
  pageNumber: number;
  pageSize: number;
}

/** Tham số lọc chi nhánh (BranchListQuery / BranchDto — dùng cho màn hình quản trị chi nhánh). */
export interface BranchListQuery {
  keyword?: string | null;
  isActive?: boolean | null;
  pageNumber: number;
  pageSize: number;
}

/** Chi nhánh (BranchDto). */
export interface BranchDto {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
}
