import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { routes } from './app.routes';
import { AuthStore } from './core/auth/auth.store';
import { authInterceptor } from './core/http/auth.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';

/**
 * Cấu hình ứng dụng (standalone, KHÔNG có NgModule).
 *
 * Ứng dụng chạy ở chế độ ZONELESS (Angular 22, không có zone.js): change detection được lập lịch theo
 * signal + sự kiện của Angular, nên `signal()/computed()` là nguồn sự thật của mọi state.
 *
 * Thứ tự interceptor CÓ Ý NGHĨA:
 * - `errorInterceptor` đăng ký TRƯỚC nên nằm NGOÀI CÙNG ⇒ chỉ nhận lỗi CUỐI CÙNG sau khi
 *   `authInterceptor` đã thử làm mới token ⇒ không hiện toast trùng cho một lần request hỏng.
 * - `authInterceptor` gắn Bearer token; gặp 401 thì refresh (single-flight) rồi thử lại request cũ.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      // Bind route param / query param / data / kết quả resolver trực tiếp vào input của component.
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'top', anchorScrolling: 'enabled' }),
    ),
    provideHttpClient(withInterceptors([errorInterceptor, authInterceptor])),
    // Khôi phục phiên từ refresh token (localStorage) TRƯỚC khi router chạy guard đầu tiên.
    provideAppInitializer(() => inject(AuthStore).restoreSession()),
  ],
};

