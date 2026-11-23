import { HttpContextToken } from '@angular/common/http';

/**
 * Cờ điều khiển hành vi của các interceptor, gắn vào từng request qua `HttpContext`
 * (truyền từ `ApiService` — xem `ApiRequestOptions.skipAuth` / `.silent`).
 */

/** Bỏ qua việc gắn Authorization và toàn bộ luồng refresh (dùng cho login/register/refresh). */
export const SKIP_AUTH = new HttpContextToken<boolean>(() => false);

/** Bỏ qua toast lỗi tự động (màn hình tự hiển thị lỗi theo ngữ cảnh, ví dụ kiểm tra voucher). */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);
