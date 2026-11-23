import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { permissionGuard } from '../../core/auth/permission.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';
import { pendingChangesGuard } from '../admin/pending-changes.guard';

/**
 * Route của feature ĐƠN HÀNG NHÂN VIÊN + POS (nạp lazy từ `app.routes.ts`).
 *
 * Phân quyền bám ĐÚNG backend:
 * - `GET /api/v1/staff/orders` = NV (mọi nhân viên: TN/TV/TK/QL) + AD ⇒ danh sách đơn chi nhánh.
 * - `POST /api/v1/pos/orders` = TN/TV/QL/AD (`OrderRoleGroups.SalesCounter`) ⇒ màn bán tại quầy.
 *
 * Các bước chuyển trạng thái đơn nằm ngay trên danh sách (không cần route riêng) và từng nút tự
 * kiểm tra vai trò + trạng thái như `order-detail`; chi tiết đơn đầy đủ dùng lại route `/orders/:id`
 * của feature orders (chỉ yêu cầu đăng nhập, backend tự kiểm tra phạm vi).
 */
const BRANCH_ORDER_ROLES = [
  ROLES.Cashier,
  ROLES.TechAdvisor,
  ROLES.Storekeeper,
  ROLES.Manager,
  ROLES.Admin,
];
const POS_ROLES = [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Manager, ROLES.Admin];

export const STAFF_ORDERS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    loadComponent: () =>
      import('./staff-order-list/staff-order-list').then((m) => m.StaffOrderList),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Đơn của chi nhánh · Apex Circuit Electronics',
    data: {
      title: 'Đơn của chi nhánh',
      roles: BRANCH_ORDER_ROLES,
      permissions: [PERMISSIONS.OrderConfirm],
    },
  },
  {
    path: 'pos',
    loadComponent: () => import('./pos-create/pos-create').then((m) => m.PosCreate),
    canActivate: [authGuard, roleGuard, permissionGuard],
    canDeactivate: [pendingChangesGuard],
    title: 'Bán tại quầy (POS) · Apex Circuit Electronics',
    data: {
      title: 'Bán tại quầy (POS)',
      roles: POS_ROLES,
      permissions: [PERMISSIONS.OrderCreate],
    },
  },
];
