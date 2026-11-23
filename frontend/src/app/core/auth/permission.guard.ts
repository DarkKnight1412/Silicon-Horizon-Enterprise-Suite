import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import type { AppRouteData } from '../models/common.model';
import { AuthStore } from './auth.store';

/**
 * Guard theo MÃ QUYỀN — đọc `route.data.permissions`.
 * - Mặc định 'roleOrPermission': khớp vai trò HOẶC đủ quyền (Permissions có thể chưa được seed).
 * - Đặt `accessMode: 'permissions'` khi muốn siết CHẶT chỉ bằng mã quyền.
 * Không khớp ⇒ trang 403.
 */
export const permissionGuard: CanActivateFn = (route) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);
  const data = route.data as AppRouteData;

  if (!authStore.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: router.url } });
  }

  const hasPermissions = data.permissions !== undefined && data.permissions.length > 0;
  if (!hasPermissions) {
    return true;
  }

  const allowed = authStore.canAccess({
    roles: data.roles,
    permissions: data.permissions,
    accessMode: data.accessMode,
  });

  return allowed ? true : router.createUrlTree(['/403']);
};
