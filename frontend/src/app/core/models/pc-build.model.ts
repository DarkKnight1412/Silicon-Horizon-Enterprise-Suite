import type { SelectOption } from './common.model';

/**
 * DTO "PC BUILD SẴN" (combo linh kiện) — khớp `Features/PcBuilds/Dto/PcBuildDtos.cs`
 * và các proc trong `Database/10_pc_builds.sql`.
 *
 * Quy ước quan trọng: bộ KHÔNG có giá riêng — `partsPrice` (tổng giá linh kiện theo giá hiện hành)
 * CHÍNH LÀ giá bộ, và khi thêm vào giỏ thì giỏ cũng tính đúng bằng tổng giá từng linh kiện.
 */

/** Hạng của bộ — cột `PcBuilds.Tier`. */
export type PcBuildTier = 0 | 1 | 2 | 3;

/** Lựa chọn hạng cho bộ lọc (kèm mô tả + biểu tượng hiển thị trên tab). */
export interface PcBuildTierOption extends SelectOption<PcBuildTier> {
  description: string;
  icon: string;
}

export const PC_BUILD_TIERS: readonly PcBuildTierOption[] = [
  { value: 0, label: 'Văn phòng', description: 'Học tập · kế toán · bán hàng', icon: '💼' },
  { value: 1, label: 'Gaming', description: 'Chơi game · giải trí', icon: '🎮' },
  { value: 2, label: 'Đồ hoạ - Render', description: 'Thiết kế · dựng hình', icon: '🎨' },
  { value: 3, label: 'Cao cấp', description: 'Ảo hoá · dựng phim 4K', icon: '🚀' },
];

/** Nhãn hạng theo giá trị (tra nhanh cho template). */
export const PC_BUILD_TIER_LABELS: Readonly<Record<number, string>> = {
  0: 'Văn phòng',
  1: 'Gaming',
  2: 'Đồ hoạ - Render',
  3: 'Cao cấp',
};

/** Lớp màu cho nhãn hạng trên nền tối (đồng bộ với cách làm của `/shop`). */
export const PC_BUILD_TIER_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-sky-500/15 text-sky-300',
  1: 'bg-emerald-500/15 text-emerald-300',
  2: 'bg-violet-500/15 text-violet-300',
  3: 'bg-amber-500/15 text-amber-300',
};

/** Giá trị sắp xếp hợp lệ của `sortBy` (usp_PcBuild_Search). */
export type PcBuildSortBy = 'order' | 'price_asc' | 'price_desc' | 'name';

export const PC_BUILD_SORT_OPTIONS: readonly SelectOption<PcBuildSortBy>[] = [
  { value: 'order', label: 'Đề xuất' },
  { value: 'price_asc', label: 'Giá tăng dần' },
  { value: 'price_desc', label: 'Giá giảm dần' },
  { value: 'name', label: 'Tên A → Z' },
];

/** Tham số GET /api/v1/pc-builds — khớp `PcBuildSearchRequest`. */
export interface PcBuildSearchQuery {
  keyword?: string | null;
  tier?: number | null;
  customerGroupId?: number | null;
  minPrice?: number | null;
  maxPrice?: number | null;
  inStockOnly?: boolean;
  featuredOnly?: boolean;
  sortBy?: PcBuildSortBy;
  pageNumber: number;
  pageSize: number;
}

/** Một bộ trong danh sách (`PcBuildListItemDto`). */
export interface PcBuildListItemDto {
  id: number;
  code: string;
  name: string;
  slug: string;
  tier: number;
  summary: string | null;
  imageUrl: string | null;
  isFeatured: boolean;
  componentCount: number;
  /** Tổng giá linh kiện — CHÍNH LÀ giá bộ. */
  partsPrice: number;
  /** Số bộ còn lắp được theo tồn kho hiện tại. */
  availableBuilds: number;
  /** Tên các linh kiện nối bằng " · " để xem nhanh trên thẻ. */
  componentPreview: string | null;
}

/** Một linh kiện trong bộ (`PcBuildComponentDto`). */
export interface PcBuildComponentDto {
  productId: number;
  sku: string;
  name: string;
  slug: string;
  categoryCode: string;
  categoryName: string;
  /** 0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU. */
  specType: number;
  brandName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  availableQty: number;
  imageUrl: string | null;
  warrantyMonths: number;
  requiresSerial: boolean;
  /** 0 Draft, 1 Active, 2 Discontinued. */
  status: number;
  note: string | null;
  sortOrder: number;
}

/** Chi tiết bộ (`PcBuildDetailDto`). */
export interface PcBuildDetailDto {
  id: number;
  code: string;
  name: string;
  slug: string;
  tier: number;
  summary: string | null;
  description: string | null;
  imageUrl: string | null;
  isFeatured: boolean;
  componentCount: number;
  partsPrice: number;
  availableBuilds: number;
  components: PcBuildComponentDto[];
}

/** POST /api/v1/pc-builds/{id}/add-to-cart — số bộ cần thêm. */
export interface AddPcBuildToCartRequest {
  quantity: number;
}

/** Kết quả thêm bộ vào giỏ (`AddPcBuildToCartResultDto`). */
export interface AddPcBuildToCartResultDto {
  pcBuildId: number;
  quantity: number;
  itemCount: number;
  items: AddedPcBuildComponentDto[];
}

/** Một linh kiện vừa được thêm vào giỏ theo bộ. */
export interface AddedPcBuildComponentDto {
  productId: number;
  name: string;
  /** Số lượng vừa thêm (số linh kiện trong bộ × số bộ). */
  addedQuantity: number;
  /** Số lượng hiện có trong giỏ sau khi thêm. */
  cartQuantity: number;
}

// ─────────────────────────────────────────────────────────────────────────────
// TAB "TỰ BUILD PC" — danh mục linh kiện + thông số để gợi ý tương thích
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Thông số cần cho việc gợi ý tương thích — khoá KHÁC NHAU theo nhóm linh kiện
 * (xem `usp_PcBuild_BuilderCatalog`). Dùng các interface dưới đây để đọc cho đúng nhóm.
 */
export type PcBuilderSpecs = Record<string, unknown>;

/** Thông số bo mạch chủ — quyết định socket CPU, chuẩn RAM, số khe RAM và số cổng lưu trữ. */
export interface MainboardSpecs {
  socket: string;
  memoryType: string;
  ramSlots: number;
  m2Slots: number;
  sataPorts: number;
  chipset: string;
  formFactor: string;
}

/** Thông số CPU — socket phải khớp bo mạch; TDP dùng để tính công suất nguồn. */
export interface CpuSpecs {
  socket: string;
  tdpW: number;
  cores: number;
  hasIntegratedGpu: boolean;
}

/** Thông số RAM — chuẩn bộ nhớ phải khớp bo mạch; `modulesPerKit` để đếm số khe bị chiếm. */
export interface RamSpecs {
  memoryType: string;
  modulesPerKit: number;
  capacityGbPerKit: number;
  busMhz: number;
  formFactor: string;
}

/** Thông số ổ cứng (gợi ý theo số khe M.2/SATA của bo mạch). */
export interface StorageSpecs {
  storageType: string;
  interface: string;
  formFactor: string;
  capacityGb: number;
}

/** Thông số nguồn — `powerW` so với công suất khuyến nghị của cả cấu hình. */
export interface PsuSpecs {
  powerW: number;
  efficiencyRating: string;
  modularType: string;
}

/** Thông số màn hình. */
export interface MonitorSpecs {
  sizeInch: number;
  resolution: string;
  refreshRateHz: number;
  panelType: string;
}

/** Một linh kiện khách có thể chọn trong tab "Tự build PC" (`PcBuilderProductDto`). */
export interface PcBuilderProductDto {
  productId: number;
  sku: string;
  name: string;
  slug: string;
  brandName: string;
  basePrice: number;
  price: number;
  availableQty: number;
  imageUrl: string | null;
  warrantyMonths: number;
  requiresSerial: boolean;
  /** Thông số theo nhóm — đọc bằng hằng số khoá ở trên (null nếu thiếu dòng specs). */
  specs: PcBuilderSpecs | null;
}

/** Một nhóm linh kiện trong danh mục tự build (`PcBuilderCategoryDto`). */
export interface PcBuilderCategoryDto {
  /** 1 bo mạch chủ · 2 CPU · 3 RAM · 4 ổ cứng · 5 nguồn · 6 màn hình. */
  stepOrder: number;
  categoryId: number;
  categoryCode: string;
  categoryName: string;
  specType: number;
  products: PcBuilderProductDto[];
}

/** GET /api/v1/pc-builds/builder/catalog (`PcBuilderCatalogDto`). */
export interface PcBuilderCatalogDto {
  categories: PcBuilderCategoryDto[];
}

/** Đọc một khoá thông số với kiểu mong muốn (BE trả `specs` là object JSON dùng chung cho 6 nhóm). */
export function specValue<T>(specs: PcBuilderSpecs | null, key: string): T | null {
  const value = specs?.[key];

  return value === undefined || value === null ? null : (value as T);
}

