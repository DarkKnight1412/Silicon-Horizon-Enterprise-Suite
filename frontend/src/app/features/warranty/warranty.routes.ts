import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { permissionGuard } from '../../core/auth/permission.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';
import { pendingChangesGuard } from '../admin/pending-changes.guard';

/**
 * Route của feature Bảo hành (nạp lazy từ `app.routes.ts`).
 *
 * Phân quyền bám ĐÚNG `WarrantyTicketsController` + `WarrantyRoles.cs`:
 * - `Viewers` = mọi nhân viên (TN, TV, TK, QL) + khách + Admin → danh sách/chi tiết/tra cứu.
 * - `Receive` = Thu ngân + Tư vấn kỹ thuật + Admin → tiếp nhận phiếu.
 * - `Process` = Thủ kho + Tư vấn kỹ thuật + Admin → đổi trạng thái phiếu (ẩn form nếu thiếu quyền).
 * - `Attach`  = mọi nhân viên + Admin → đính kèm tài liệu.
 */
const WARRANTY_VIEWER_ROLES = [
  ROLES.Cashier,
  ROLES.TechAdvisor,
  ROLES.Storekeeper,
  ROLES.Manager,
  ROLES.Admin,
];
const WARRANTY_RECEIVE_ROLES = [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Admin];

export const WARRANTY_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tickets' },
  {
    path: 'tickets',
    loadComponent: () => import('./warranty-list/warranty-list').then((m) => m.WarrantyList),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Phiếu bảo hành · Silicon Horizon',
    data: {
      title: 'Phiếu bảo hành',
      roles: WARRANTY_VIEWER_ROLES,
      permissions: [PERMISSIONS.WarrantyReceive],
    },
  },
  {
    path: 'tickets/new',
    loadComponent: () =>
      import('./warranty-receive/warranty-receive').then((m) => m.WarrantyReceive),
    canActivate: [authGuard, roleGuard, permissionGuard],
    canDeactivate: [pendingChangesGuard],
    title: 'Tiếp nhận bảo hành · Silicon Horizon',
    data: {
      title: 'Tiếp nhận bảo hành',
      roles: WARRANTY_RECEIVE_ROLES,
      permissions: [PERMISSIONS.WarrantyReceive],
    },
  },
  {
    path: 'tickets/:id',
    loadComponent: () =>
      import('./warranty-detail/warranty-detail').then((m) => m.WarrantyDetail),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Chi tiết phiếu bảo hành · Silicon Horizon',
    data: {
      title: 'Chi tiết phiếu bảo hành',
      roles: WARRANTY_VIEWER_ROLES,
      permissions: [PERMISSIONS.WarrantyProcess],
    },
  },
  {
    path: 'lookup',
    loadComponent: () =>
      import('./warranty-lookup/warranty-lookup').then((m) => m.WarrantyLookup),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Tra cứu bảo hành · Silicon Horizon',
    data: {
      title: 'Tra cứu bảo hành theo serial',
      roles: WARRANTY_VIEWER_ROLES,
      permissions: [PERMISSIONS.WarrantyReceive],
    },
  },
];
