import type { PermissionCode, RoleCode } from './auth.model';

/**
 * Quy ước state (mục 4 của yêu cầu):
 * - ĐƯA LÊN STATE TOÀN CỤC (service `providedIn: 'root'` + signals) khi dữ liệu được chia sẻ giữa
 *   nhiều màn hình hoặc sống lâu hơn một component: phiên đăng nhập (`AuthStore`), giỏ hàng (`CartStore`),
 *   toast (`NotificationService`), cờ bận toàn cục (`LoadingService`), hộp thoại xác nhận (`ConfirmService`).
 * - GIỮ STATE CỤC BỘ (signal trong chính component) với dữ liệu chỉ phục vụ một màn hình: bộ lọc danh sách,
 *   kết quả tìm kiếm, form đang nhập, trạng thái mở/đóng của panel... — tránh biến store toàn cục thành
 *   "nơi chứa mọi thứ" và tránh rò rỉ state giữa các lần điều hướng.
 */

/** Chế độ kiểm tra quyền của route/menu (xem AuthStore.canAccess). */
export type AccessMode = 'roles' | 'permissions' | 'roleOrPermission';

/** Dữ liệu khai báo trên `route.data` để guard + tiêu đề trang đọc lại. */
export interface AppRouteData {
  /** Tiêu đề trang (dùng cho breadcrumb). */
  title?: string;
  /** Vai trò được phép vào route. */
  roles?: RoleCode[];
  /** Mã quyền được phép vào route. */
  permissions?: PermissionCode[];
  /**
   * Cách kết hợp `roles` và `permissions`. Mặc định 'roleOrPermission': khớp vai trò HOẶC đủ quyền.
   * Lý do mặc định này: bảng Permissions chưa được seed đầy đủ (xem PermissionCodes.cs) nên nếu bắt buộc
   * phải có mã quyền thì Admin/Quản lý sẽ bị chặn oan.
   */
  accessMode?: AccessMode;
  /** Đích chuyển hướng thay cho mặc định (guestGuard dùng để về trang chủ sau khi đăng nhập). */
  redirectTo?: string;
}

/** Một mục trên sidebar — ẩn/hiện theo `roles`/`permissions` bằng AuthStore.canAccess. */
export interface NavItem {
  label: string;
  /** Đường dẫn tuyệt đối dùng cho routerLink. */
  route: string;
  /** Icon dạng emoji/ký tự để không phải kéo thêm thư viện icon. */
  icon: string;
  roles?: RoleCode[];
  permissions?: PermissionCode[];
  accessMode?: AccessMode;
}

/** Lựa chọn cho thẻ <select> trong form. */
export interface SelectOption<TValue extends string | number = number> {
  value: TValue;
  label: string;
}

/** Tra nhãn tiếng Việt của một giá trị enum số; trả về `fallback` khi không có trong bảng. */
export function labelOf(
  labels: Readonly<Record<number, string>>,
  value: number | null | undefined,
  fallback = 'Không xác định',
): string {
  if (value === null || value === undefined) {
    return fallback;
  }
  return labels[value] ?? fallback;
}

const VND_FORMATTER = new Intl.NumberFormat('vi-VN', {
  style: 'currency',
  currency: 'VND',
  maximumFractionDigits: 0,
});

const NUMBER_FORMATTER = new Intl.NumberFormat('vi-VN');

const DATE_TIME_FORMATTER = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

const DATE_FORMATTER = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

/** Định dạng tiền VND (backend dùng số nguyên VND). */
export function formatVnd(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : VND_FORMATTER.format(value);
}

/** Định dạng số theo kiểu Việt Nam (1.234). */
export function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : NUMBER_FORMATTER.format(value);
}

/** Định dạng thời điểm ISO (UTC) của backend sang giờ địa phương. */
export function formatDateTime(value: string | Date | null | undefined): string {
  if (value === null || value === undefined) {
    return '—';
  }

  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : DATE_TIME_FORMATTER.format(date);
}

/** Định dạng ngày (không giờ). */
export function formatDate(value: string | Date | null | undefined): string {
  if (value === null || value === undefined) {
    return '—';
  }

  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : DATE_FORMATTER.format(date);
}

/** Tạo slug từ chuỗi tiếng Việt (bỏ dấu) để gợi ý sẵn trong form sản phẩm/danh mục. */
export function slugify(input: string): string {
  return input
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 300);
}
