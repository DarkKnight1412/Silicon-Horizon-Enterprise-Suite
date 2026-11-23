import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import type { AppRouteData } from '../models/common.model';
import { AuthStore } from './auth.store';

/**
 * Guard theo VAI TRÒ — đọc `route.data.roles`.
 *
 * Cách kết hợp với quyền do `route.data.accessMode` quyết định (mặc định 'roleOrPermission'):
 * - 'roles'            : chỉ xét vai trò.
 * - 'roleOrPermission' : khớp vai trò HOẶC đủ quyền ⇒ tránh chặn oan khi bảng Permissions chưa seed đủ.
 * Không khớp ⇒ chuyển sang trang 403 (không phải 404, để người dùng biết là do quyền).
 */
export const roleGuard: CanActivateFn = (route) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);
  const data = route.data as AppRouteData;

  if (!authStore.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: router.url } });
  }

  const hasRoles = data.roles !== undefined && data.roles.length > 0;
  if (!hasRoles) {
    return true;
  }

  const allowed = authStore.canAccess({
    roles: data.roles,
    permissions: data.permissions,
    accessMode: data.accessMode,
  });

  return allowed ? true : router.createUrlTree(['/403']);
};
