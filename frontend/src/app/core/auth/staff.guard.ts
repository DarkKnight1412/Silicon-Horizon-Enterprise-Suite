import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import { AuthStore } from './auth.store';

/**
 * Guard cho KHU VỰC NỘI BỘ (chỉ dành cho NHÂN VIÊN & QUẢN LÝ/ADMIN).
 *
 * Quy tắc:
 * - Chưa đăng nhập ⇒ `/login?returnUrl=...` (nhân viên mở deep link vẫn quay lại đúng màn hình).
 * - Đã đăng nhập nhưng KHÔNG phải nhân viên (khách hàng) ⇒ đẩy về giao diện MUA SẮM CÔNG KHAI (`/`).
 *
 * Nhờ vậy khách hàng KHÔNG BAO GIỜ nhìn thấy khung nội bộ (sidebar, kho, báo cáo, nhật ký…),
 * dù họ gõ thẳng đường dẫn nội bộ.
 */
export const staffGuard: CanActivateFn = (_route, state) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (!authStore.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  return authStore.isStaff() ? true : router.createUrlTree(['/']);
};
