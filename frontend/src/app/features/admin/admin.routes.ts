import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';
import { pendingChangesGuard } from './pending-changes.guard';

/**
 * Route của feature admin (nạp lazy từ `app.routes.ts`).
 *
 * Mọi màn hình quản trị đều là API Admin-only của backend (`/api/v1/admin/...`) nên bắt buộc
 * đăng nhập + vai trò Admin. Dùng `authGuard` kèm `roleGuard` (đọc `data.roles`) thay vì gộp một
 * guard riêng, đúng quy ước guard/`AuthStore.canAccess` đang dùng ở các feature khác.
 *
 * Thứ tự khai báo quan trọng: `products/new` phải đứng TRƯỚC `products/:id/edit` để không bị
 * route tham số "nuốt" mất.
 */
const ADMIN_ROLES = [ROLES.Admin];
const ADMIN_PERMISSIONS = [PERMISSIONS.CatalogManage];

export const ADMIN_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'products' },
  {
    path: 'products',
    loadComponent: () =>
      import('./products/admin-product-list/admin-product-list').then((m) => m.AdminProductList),
    canActivate: [authGuard, roleGuard],
    title: 'Quản trị sản phẩm · Apex Circuit Electronics',
    data: { title: 'Quản trị sản phẩm', roles: ADMIN_ROLES, permissions: ADMIN_PERMISSIONS },
  },
  {
    path: 'products/new',
    loadComponent: () =>
      import('./products/admin-product-form/admin-product-form').then((m) => m.AdminProductForm),
    canActivate: [authGuard, roleGuard],
    canDeactivate: [pendingChangesGuard],
    title: 'Thêm sản phẩm · Apex Circuit Electronics',
    data: { title: 'Thêm sản phẩm', roles: ADMIN_ROLES, permissions: ADMIN_PERMISSIONS },
  },
  {
    path: 'products/:id/edit',
    loadComponent: () =>
      import('./products/admin-product-form/admin-product-form').then((m) => m.AdminProductForm),
    canActivate: [authGuard, roleGuard],
    canDeactivate: [pendingChangesGuard],
    title: 'Sửa sản phẩm · Apex Circuit Electronics',
    data: { title: 'Sửa sản phẩm', roles: ADMIN_ROLES, permissions: ADMIN_PERMISSIONS },
  },
  {
    path: 'categories',
    loadComponent: () => import('./categories/admin-categories').then((m) => m.AdminCategories),
    canActivate: [authGuard, roleGuard],
    title: 'Quản trị danh mục · Apex Circuit Electronics',
    data: { title: 'Quản trị danh mục', roles: ADMIN_ROLES, permissions: ADMIN_PERMISSIONS },
  },
  {
    path: 'brands',
    loadComponent: () => import('./brands/admin-brands').then((m) => m.AdminBrands),
    canActivate: [authGuard, roleGuard],
    title: 'Quản trị thương hiệu · Apex Circuit Electronics',
    data: { title: 'Quản trị thương hiệu', roles: ADMIN_ROLES, permissions: ADMIN_PERMISSIONS },
  },
];
