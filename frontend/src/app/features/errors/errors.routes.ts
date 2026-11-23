import type { Routes } from '@angular/router';

/**
 * Route của feature errors. Đặt trong khung chính (main layout) để người dùng vẫn có menu
 * và nút đăng nhập; đường dẫn ngắn `/403`, `/404` khớp với đích điều hướng của các guard/resolver.
 */
export const ERROR_ROUTES: Routes = [
  {
    path: '403',
    loadComponent: () => import('./forbidden/forbidden').then((m) => m.Forbidden),
    title: 'Không đủ quyền · Apex Circuit Electronics',
    data: { title: 'Không đủ quyền' },
  },
  {
    path: '404',
    loadComponent: () => import('./not-found/not-found').then((m) => m.NotFound),
    title: 'Không tìm thấy trang · Apex Circuit Electronics',
    data: { title: 'Không tìm thấy trang' },
  },
];
