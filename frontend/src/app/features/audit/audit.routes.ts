import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { permissionGuard } from '../../core/auth/permission.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';

/**
 * Route của feature NHẬT KÝ (nạp lazy từ `app.routes.ts`).
 *
 * Phân quyền bám ĐÚNG `AuditRoles.cs` (Database/README.md mục 8): `AuditRoles.Viewers` = QL, AD.
 * Dùng `accessMode: 'roles'` để siết chặt đúng hai vai trò này — nhật ký là dữ liệu nhạy cảm, không
 * cho phép "có mã quyền là vào" (bảng Permissions có thể chưa seed đủ).
 * Phạm vi chi nhánh do backend tự ép (`branchId` từ token của Quản lý chi nhánh).
 */
const AUDIT_ROLES = [ROLES.Manager, ROLES.Admin];

export const AUDIT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./audit-log-list/audit-log-list').then((m) => m.AuditLogList),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Nhật ký hệ thống · Silicon Horizon',
    data: {
      title: 'Nhật ký hệ thống',
      roles: AUDIT_ROLES,
      permissions: [PERMISSIONS.AuditView],
      accessMode: 'roles',
    },
  },
];
