import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { permissionGuard } from '../../core/auth/permission.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';

/**
 * Route của feature Đổi trả (nạp lazy từ `app.routes.ts`).
 *
 * Phân quyền bám ĐÚNG `ReturnsController` + `ReturnsRoles.cs`:
 * - `Viewers` = mọi nhân viên (TN, TV, TK, QL) + khách + Admin → danh sách/chi tiết.
 * - `Review` = Quản lý + Admin; `Refund` = Quản lý + Admin; `Inspect` = Thủ kho + Admin.
 *
 * Vì một màn chi tiết phục vụ CẢ ba thao tác (duyệt/kiểm tra/hoàn tiền) nên route chỉ chặn theo nhóm
 * xem; từng khối thao tác bên trong tự kiểm tra quyền và chỉ gọi API khi đủ quyền
 * (duyệt & hoàn tiền ⇒ QL/AD; kiểm tra hàng trả ⇒ TK/AD).
 */
const RETURN_VIEWER_ROLES = [
  ROLES.Cashier,
  ROLES.TechAdvisor,
  ROLES.Storekeeper,
  ROLES.Manager,
  ROLES.Admin,
];

export const RETURNS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./return-list/return-list').then((m) => m.ReturnList),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Yêu cầu đổi trả · Apex Circuit Electronics',
    data: {
      title: 'Yêu cầu đổi trả',
      roles: RETURN_VIEWER_ROLES,
      permissions: [PERMISSIONS.ReturnReview],
    },
  },
  {
    path: ':id',
    loadComponent: () => import('./return-detail/return-detail').then((m) => m.ReturnDetail),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Chi tiết yêu cầu đổi trả · Apex Circuit Electronics',
    data: {
      title: 'Chi tiết yêu cầu đổi trả',
      roles: RETURN_VIEWER_ROLES,
      permissions: [PERMISSIONS.ReturnInspect],
    },
  },
];
