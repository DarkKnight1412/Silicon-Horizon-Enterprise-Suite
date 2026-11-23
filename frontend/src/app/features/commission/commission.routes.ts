import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { permissionGuard } from '../../core/auth/permission.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';

/**
 * Route của feature HOA HỒNG (nạp lazy từ `app.routes.ts`) — một màn tổng hợp theo kỳ.
 *
 * Phân quyền bám ĐÚNG `CommissionRoles.cs` (Database/README.md mục 8):
 * - `Viewers` = NV (hoa hồng của mình), QL (chi nhánh mình), AD.
 * - Điều chỉnh thủ công và chốt kỳ = AD — hai nút này nằm trong màn và tự ẩn khi không phải Admin.
 */
const COMMISSION_VIEWER_ROLES = [
  ROLES.Cashier,
  ROLES.TechAdvisor,
  ROLES.Storekeeper,
  ROLES.Manager,
  ROLES.Admin,
];

export const COMMISSION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./commission-summary/commission-summary').then((m) => m.CommissionSummary),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Hoa hồng theo kỳ · Apex Circuit Electronics',
    data: {
      title: 'Hoa hồng theo kỳ',
      roles: COMMISSION_VIEWER_ROLES,
      permissions: [PERMISSIONS.CommissionManage],
    },
  },
];
