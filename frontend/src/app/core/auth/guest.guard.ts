import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import type { AppRouteData } from '../models/common.model';
import { AuthStore } from './auth.store';

/**
 * Guard cho trang dành cho NGƯỜI CHƯA đăng nhập (login/register):
 * đã đăng nhập thì chuyển hướng về `route.data.redirectTo` (mặc định /dashboard).
 */
export const guestGuard: CanActivateFn = (route) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (!authStore.isAuthenticated()) {
    return true;
  }

  const data = route.data as AppRouteData;

  return router.createUrlTree([data.redirectTo ?? '/dashboard']);
};
