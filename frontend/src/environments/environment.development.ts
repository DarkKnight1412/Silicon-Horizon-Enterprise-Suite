import type { Environment } from './environment';

/**
 * Phương án THAY THẾ cho môi trường development khi muốn gọi API trực tiếp (không qua proxy).
 *
 * Cách bật: thêm vào `angular.json` → `projects.silicon-horizon-web.architect.build.configurations.development`:
 *   "fileReplacements": [
 *     { "replace": "src/environments/environment.ts", "with": "src/environments/environment.development.ts" }
 *   ]
 * (file `angular.json` thuộc nhóm đã được cấu hình sẵn nên phần này KHÔNG tự sửa; hãy bật theo cách trên khi cần.)
 *
 * Lưu ý khi dùng cách này: request đi thẳng tới http://localhost:5270 nên phải cấu hình CORS
 * (policy "CustomerWeb" trong SiliconHorizon.Api) cho origin của dev server.
 */
export const environment: Environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5270',
  apiPathPrefix: '/api',
  defaultPageSize: 12,
  toastTimeoutMs: 5000,
};
