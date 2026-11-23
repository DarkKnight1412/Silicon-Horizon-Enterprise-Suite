import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import { AuthStore } from './auth.store';

/**
 * Guard cho route yêu cầu đăng nhập.
 * Chưa đăng nhập ⇒ điều hướng `/login?returnUrl=<đường dẫn đang truy cập>` để sau khi đăng nhập
 * quay lại đúng màn hình (component login đọc `returnUrl` qua `withComponentInputBinding`).
 *
 * Lưu ý: phiên từ refresh token trong localStorage đã được khôi phục ở `provideAppInitializer`
 * (AuthStore.restoreSession) TRƯỚC khi router chạy guard đầu tiên, nên ở đây chỉ cần đọc signal.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};
