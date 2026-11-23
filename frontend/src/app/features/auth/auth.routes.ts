import type { Routes } from '@angular/router';
import { guestGuard } from '../../core/auth/guest.guard';

/**
 * Route của feature auth (nạp lazy từ `app.routes.ts` qua loadChildren).
 * Cả hai route đều nằm trong `AuthLayout` và bị `guestGuard` chặn khi đã đăng nhập
 * (chuyển về `data.redirectTo`).
 */
export const AUTH_ROUTES: Routes = [
  {
    // Vào `/` (chưa đăng nhập) → hiển thị ngay trang đăng nhập.
    // Nếu thiếu route này, layout sẽ render với outlet rỗng (màn hình trắng).
    path: '',
    pathMatch: 'full',
    redirectTo: 'login',
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./login/login').then((m) => m.Login),
    title: 'Đăng nhập · Silicon Horizon',
    data: { title: 'Đăng nhập', redirectTo: '/dashboard' },
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./register/register').then((m) => m.Register),
    title: 'Đăng ký · Silicon Horizon',
    data: { title: 'Đăng ký tài khoản', redirectTo: '/dashboard' },
  },
];
