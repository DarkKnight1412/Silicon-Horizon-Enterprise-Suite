import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { permissionGuard } from '../../core/auth/permission.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';

/**
 * Route của feature BÁO CÁO (nạp lazy từ `app.routes.ts`) — 5 màn theo Database/README.md mục 9.
 *
 * Phân quyền bám ĐÚNG `ReportsController` + `ReportRoles.cs`:
 * - Doanh thu / bảo hành: QL (chi nhánh mình — backend ép), AD.
 * - Tồn kho / serial theo trạng thái: TK, QL, AD.
 * - Công nợ: CHỈ AD (proc không có tham số chi nhánh) ⇒ dùng `accessMode: 'roles'` để siết chặt.
 *
 * Mọi màn đều KHÔNG gửi `branchId` khi người dùng không phải Admin; ô chọn chi nhánh chỉ hiện với
 * Admin vì `GET /api/v1/admin/branches` là endpoint quản trị.
 */
const REVENUE_ROLES = [ROLES.Manager, ROLES.Admin];
const STOCK_REPORT_ROLES = [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin];
const DEBT_ROLES = [ROLES.Admin];

export const REPORTS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'revenue' },
  {
    path: 'revenue',
    loadComponent: () =>
      import('./revenue-report/revenue-report').then((m) => m.RevenueReport),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Báo cáo doanh thu · Apex Circuit Electronics',
    data: {
      title: 'Báo cáo doanh thu',
      roles: REVENUE_ROLES,
      permissions: [PERMISSIONS.ReturnRefund],
    },
  },
  {
    path: 'inventory',
    loadComponent: () =>
      import('./inventory-report/inventory-report').then((m) => m.InventoryReport),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Báo cáo tồn kho · Apex Circuit Electronics',
    data: {
      title: 'Báo cáo tồn kho',
      roles: STOCK_REPORT_ROLES,
      permissions: [PERMISSIONS.StockAdjustApprove],
    },
  },
  {
    path: 'serial-status',
    loadComponent: () =>
      import('./serial-status-report/serial-status-report').then((m) => m.SerialStatusReport),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Báo cáo serial theo trạng thái · Apex Circuit Electronics',
    data: {
      title: 'Báo cáo serial theo trạng thái',
      roles: STOCK_REPORT_ROLES,
      permissions: [PERMISSIONS.SerialManage],
    },
  },
  {
    path: 'debt',
    loadComponent: () => import('./debt-report/debt-report').then((m) => m.DebtReport),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Báo cáo công nợ · Apex Circuit Electronics',
    data: {
      title: 'Báo cáo công nợ',
      roles: DEBT_ROLES,
      permissions: [PERMISSIONS.ReturnRefund],
      accessMode: 'roles',
    },
  },
  {
    path: 'warranty',
    loadComponent: () =>
      import('./warranty-report/warranty-report').then((m) => m.WarrantyReport),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Báo cáo bảo hành · Apex Circuit Electronics',
    data: {
      title: 'Báo cáo bảo hành',
      roles: REVENUE_ROLES,
      permissions: [PERMISSIONS.WarrantyProcess],
    },
  },
];
