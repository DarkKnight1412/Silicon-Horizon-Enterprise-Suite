import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';

/** Route của feature dashboard (trang tổng quan sau khi đăng nhập). */
export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./dashboard').then((m) => m.Dashboard),
    canActivate: [authGuard],
    title: 'Tổng quan · Silicon Horizon',
    data: { title: 'Tổng quan' },
  },
];
