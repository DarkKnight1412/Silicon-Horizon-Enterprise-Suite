import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { permissionGuard } from '../../core/auth/permission.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';

/**
 * Route của feature NHÂN SỰ — xếp ca & chấm công (nạp lazy từ `app.routes.ts`).
 *
 * Phân quyền bám ĐÚNG `HrRoles.cs` (Database/README.md mục 8):
 * - `ScheduleViewers`   = NV (ca của mình), QL (chi nhánh), AD ⇒ `/hr/schedules`.
 * - `AttendanceViewers` = NV (công của mình), QL (chi nhánh), AD ⇒ `/hr/attendance`.
 *
 * Từng thao tác nguy hiểm tự kiểm tra quyền bên trong màn hình nên route chỉ chặn theo nhóm xem:
 * - Xếp ca / xoá ca: QL, AD (`HrRoles.ScheduleApprovers`) — nút ẩn nếu thiếu quyền.
 * - Điều chỉnh công: QL, AD (`HrRoles.AttendanceAdjusters`) — hộp thoại ẩn nếu thiếu quyền.
 * - Chấm công vào/ra: mọi nhân viên (`HrRoles.AttendanceStaff`).
 */
const HR_VIEWER_ROLES = [
  ROLES.Cashier,
  ROLES.TechAdvisor,
  ROLES.Storekeeper,
  ROLES.Manager,
  ROLES.Admin,
];

export const HR_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'schedules' },
  {
    path: 'schedules',
    loadComponent: () => import('./schedule-list/schedule-list').then((m) => m.ScheduleList),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Xếp ca làm việc · Apex Circuit Electronics',
    data: {
      title: 'Xếp ca làm việc',
      roles: HR_VIEWER_ROLES,
      permissions: [PERMISSIONS.ScheduleManage],
    },
  },
  {
    path: 'attendance',
    loadComponent: () =>
      import('./attendance-month/attendance-month').then((m) => m.AttendanceMonth),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Chấm công · Apex Circuit Electronics',
    data: {
      title: 'Chấm công & bảng công tháng',
      roles: HR_VIEWER_ROLES,
      permissions: [PERMISSIONS.AttendanceAdjust],
    },
  },
];
