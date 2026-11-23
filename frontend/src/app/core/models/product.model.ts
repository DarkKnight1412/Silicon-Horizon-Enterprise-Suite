import type { SelectOption } from './common.model';

/**
 * DTO danh mục sản phẩm — khớp `Features/Catalog/Dto/ProductDtos.cs`, `ProductDetailDtos.cs`,
 * `Features/Catalog/Dto/CategoryDtos.cs` và các bảng dbo.Products / ProductImages / Specs_*.
 */

/** Danh mục hoặc thương hiệu bản công khai (GET /api/v1/categories, GET /api/v1/brands). */
export interface ReferenceItemDto {
  id: number;
  /** Danh mục có Code (CPU, RAM...); thương hiệu trả null. */
  code: string | null;
  name: string;
}

/** Giá trị sắp xếp hợp lệ của `sortBy` (usp_Product_Search). */
export type ProductSortBy = 'newest' | 'price_asc' | 'price_desc' | 'name' | 'discount';

export const PRODUCT_SORT_OPTIONS: readonly SelectOption<ProductSortBy>[] = [
  { value: 'newest', label: 'Mới nhất' },
  { value: 'price_asc', label: 'Giá tăng dần' },
  { value: 'price_desc', label: 'Giá giảm dần' },
  { value: 'name', label: 'Tên A → Z' },
  { value: 'discount', label: 'Giảm giá nhiều nhất' },
];

/** Tham số GET /api/v1/products — khớp `ProductSearchRequest`. */
export interface ProductSearchQuery {
  keyword?: string | null;
  categoryCode?: string | null;
  brandId?: number | null;
  customerGroupId?: number | null;
  minPrice?: number | null;
  maxPrice?: number | null;
  inStockOnly?: boolean;
  onPromotion?: boolean;
  /** Dạng rút gọn của backend: 'memoryType:DDR5,capacityGbPerKit:32'. */
  specFilters?: string | null;
  sortBy?: ProductSortBy;
  pageNumber: number;
  pageSize: number;
}

/** Một dòng kết quả tìm kiếm (`ProductListItemDto`). */
export interface ProductListItemDto {
  id: number;
  sku: string;
  name: string;
  slug: string;
  brandId: number;
  brandName: string;
  categoryId: number;
  requiresSerial: boolean;
  warrantyMonths: number;
  /** Giá niêm yết. */
  basePrice: number;
  /** Giá sau khuyến mãi/bảng giá theo nhóm khách. */
  price: number;
  promotionId: number | null;
  availableQty: number;
  imageUrl: string | null;
  createdAt: string;
}

/** Ảnh sản phẩm (`ProductImageDto`). */
export interface ProductImageDto {
  id: number;
  url: string;
  sortOrder: number;
  isPrimary: boolean;
}

/** Tồn khả dụng tại một chi nhánh (`ProductStockByBranchDto`). */
export interface ProductStockByBranchDto {
  branchId: number;
  branchName: string;
  availableQty: number;
}

/**
 * Chi tiết sản phẩm (`ProductDetailDto`): `specifications` là "tên cột → giá trị" của bảng Specs_*
 * tương ứng SpecType; `monitorPorts` chỉ có khi SpecType = 4 (Monitor).
 */
export interface ProductDetailDto {
  id: number;
  sku: string;
  name: string;
  slug: string;
  brandId: number;
  brandName: string;
  categoryId: number;
  categoryCode: string;
  categoryName: string;
  /** 0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU. */
  specType: number;
  shortDescription: string | null;
  description: string | null;
  requiresSerial: boolean;
  warrantyMonths: number;
  warrantyTerms: string | null;
  status: number;
  listPrice: number;
  basePrice: number;
  price: number;
  promotionId: number | null;
  availableQty: number;
  images: ProductImageDto[];
  specifications: Record<string, unknown> | null;
  monitorPorts: Record<string, unknown>[];
  stockByBranch: ProductStockByBranchDto[];
}

/** Trạng thái sản phẩm — cột Products.Status (0 Draft, 1 Active, 2 Discontinued). */
export const PRODUCT_STATUS_LABELS: Readonly<Record<number, string>> = {
  0: 'Nháp',
  1: 'Đang bán',
  2: 'Ngừng kinh doanh',
};

export const PRODUCT_STATUS_OPTIONS: readonly SelectOption<number>[] = [
  { value: 0, label: PRODUCT_STATUS_LABELS[0] ?? 'Nháp' },
  { value: 1, label: PRODUCT_STATUS_LABELS[1] ?? 'Đang bán' },
  { value: 2, label: PRODUCT_STATUS_LABELS[2] ?? 'Ngừng kinh doanh' },
];

/** Ảnh gửi lên khi tạo/sửa sản phẩm — `ProductImageInput` (camelCase: url, sortOrder, isPrimary). */
export interface ProductImageInput {
  url: string;
  sortOrder: number | null;
  isPrimary: boolean | null;
}

/** POST /api/v1/admin/products — khớp `CreateProductRequest`. */
export interface CreateProductRequest {
  sku: string;
  name: string;
  slug: string;
  brandId: number;
  categoryId: number;
  shortDescription: string | null;
  description: string | null;
  listPrice: number;
  lastImportPrice: number | null;
  requiresSerial: boolean;
  minStockDefault: number;
  warrantyMonths: number;
  warrantyTerms: string | null;
  status: number;
  /** Thông số theo SpecType — API chuyển nguyên văn vào @SpecsJson (camelCase). */
  specs: Record<string, unknown> | null;
  images: ProductImageInput[];
}

/** PUT /api/v1/admin/products/{id} — khớp `UpdateProductRequest` (không đổi Category/Sku). */
export interface UpdateProductRequest {
  name: string;
  slug: string;
  brandId: number;
  shortDescription: string | null;
  description: string | null;
  listPrice: number;
  requiresSerial: boolean;
  minStockDefault: number;
  warrantyMonths: number;
  warrantyTerms: string | null;
  specs: Record<string, unknown> | null;
  images: ProductImageInput[];
}

/** PATCH /api/v1/admin/products/{id}/status. */
export interface SetProductStatusRequest {
  status: number;
}

/** PUT /api/v1/admin/products/{id}/prices/{groupId}. */
export interface UpsertProductPriceRequest {
  price: number;
  approvedBy: number | null;
}

/** Một dòng hàng cần kiểm tra tương thích (POST /api/v1/compatibility/check). */
export interface CompatibilityItem {
  productId: number;
  quantity: number;
}

export interface CompatibilityCheckRequest {
  items: CompatibilityItem[];
}

/** Cảnh báo tương thích (usp_Compat_Check): Severity 0 Info, 1 Warning. */
export interface CompatibilityWarningDto {
  severity: number;
  /** SOCKET_MISMATCH, MEMORY_TYPE_MISMATCH, RAM_SLOTS_EXCEEDED, PSU_POWER_LOW. */
  code: string;
  message: string;
}

/** Kết quả kiểm tra tương thích — cảnh báo KHÔNG chặn đặt hàng (PRD-10). */
export interface CompatibilityCheckResultDto {
  isCompatible: boolean;
  warnings: CompatibilityWarningDto[];
}

/** Nhãn tiếng Việt của mã cảnh báo tương thích. */
export const COMPATIBILITY_CODE_LABELS: Readonly<Record<string, string>> = {
  SOCKET_MISMATCH: 'Socket CPU và bo mạch chủ không khớp',
  MEMORY_TYPE_MISMATCH: 'Chuẩn RAM (DDR4/DDR5) không khớp bo mạch chủ',
  RAM_SLOTS_EXCEEDED: 'Số thanh RAM vượt số khe của bo mạch chủ',
  PSU_POWER_LOW: 'Công suất nguồn thấp hơn TDP của CPU',
};
