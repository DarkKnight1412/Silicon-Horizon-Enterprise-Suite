/**
 * Cấu hình môi trường MẶC ĐỊNH (file này luôn được dùng vì angular.json chưa khai báo `fileReplacements`).
 *
 * Cách chọn `apiBaseUrl`:
 * - Dev (khuyến nghị, có proxy): để RỖNG '' → request đi tới `/api/...` trên chính origin của dev server
 *   (http://localhost:4200) rồi `proxy.conf.json` chuyển tiếp sang http://localhost:5270 ⇒ tránh CORS.
 * - Dev gọi trực tiếp (không proxy): 'http://localhost:5270' — xem `environment.development.ts`,
 *   khi đó API phải cho phép origin http://localhost:4200 qua CORS policy "CustomerWeb".
 * - Production: domain API thật, ví dụ 'https://api.apexcircuitelectronics.vn' (KHÔNG có '/api' ở cuối,
 *   vì `apiPathPrefix` đã được ghép thêm).
 */
export interface Environment {
  /** true khi build production (dùng để tắt log chi tiết, bật cảnh báo...). */
  production: boolean;
  /** Origin của API, không có '/' ở cuối. Rỗng = dùng đường dẫn tương đối + proxy của dev server. */
  apiBaseUrl: string;
  /** Tiền tố đường dẫn API (đặt ở một chỗ để đổi nhanh khi backend nâng version: '/api'). */
  apiPathPrefix: string;
  /** Số dòng mặc định mỗi trang danh sách. */
  defaultPageSize: number;
  /** Thời gian tự ẩn của toast (ms). */
  toastTimeoutMs: number;
}

export const environment: Environment = {
  production: false,
  apiBaseUrl: '',
  apiPathPrefix: '/api',
  defaultPageSize: 12,
  toastTimeoutMs: 5000,
};
