/**
 * Khớp `SiliconHorizon.Application.Common.Models.PagedResult<T>`:
 * các danh sách đều trả { items, pageNumber, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage }.
 */
export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

/** Tham số phân trang dùng chung (backend: `PageRequest`, pageSize tối đa 200). */
export interface PageQuery {
  pageNumber: number;
  pageSize: number;
}

/** Lựa chọn kích thước trang cho component `app-pagination`. */
export const PAGE_SIZE_OPTIONS: readonly number[] = [10, 20, 50, 100];

/** Kết quả rỗng dùng làm state ban đầu cho các màn hình danh sách. */
export function emptyPagedResult<T>(pageSize: number): PagedResult<T> {
  return {
    items: [],
    pageNumber: 1,
    pageSize,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  };
}
