import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { ROLES } from '../../core/models/auth.model';
import { orderDetailResolver } from './order-detail.resolver';

/**
 * Route của feature orders.
 *
 * Phân quyền bám đúng backend:
 * - `GET /api/v1/orders` (danh sách) và `POST /api/v1/orders` (đặt hàng) chỉ cho vai trò **Customer**
 *   ⇒ dùng `accessMode: 'roles'` để siết chặt đúng vai trò (không dùng quyền thay thế).
 * - `GET /api/v1/orders/{id}` cho khách (đơn của mình), nhân viên (đơn chi nhánh mình) và Admin
 *   ⇒ chỉ cần đăng nhập; backend tự kiểm tra phạm vi và trả 403/404 (route hiển thị trang 404).
 *
 * Thứ tự khai báo quan trọng: `checkout` phải đứng trước `:id`.
 */
export const ORDERS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./order-list/order-list').then((m) => m.OrderListComponent),
    canActivate: [authGuard, roleGuard],
    title: 'Đơn hàng của tôi · Apex Circuit Electronics',
    data: { title: 'Đơn hàng của tôi', roles: [ROLES.Customer], accessMode: 'roles' },
  },
  {
    path: 'checkout',
    loadComponent: () => import('./checkout/checkout').then((m) => m.Checkout),
    canActivate: [authGuard, roleGuard],
    title: 'Đặt hàng · Apex Circuit Electronics',
    data: { title: 'Đặt hàng', roles: [ROLES.Customer], accessMode: 'roles' },
  },
  {
    path: ':id',
    loadComponent: () => import('./order-detail/order-detail').then((m) => m.OrderDetailComponent),
    canActivate: [authGuard],
    resolve: { order: orderDetailResolver },
    title: 'Chi tiết đơn hàng · Apex Circuit Electronics',
    data: { title: 'Chi tiết đơn hàng' },
  },
];
