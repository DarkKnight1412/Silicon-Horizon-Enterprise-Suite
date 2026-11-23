import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { ROLES } from '../../core/models/auth.model';

/**
 * Route của feature THANH TOÁN / CÔNG NỢ (nạp lazy từ `app.routes.ts`).
 *
 * Phần "ghi nhận thanh toán / hoàn tiền" KHÔNG có route riêng: nó là khối nhúng
 * (`OrderPaymentPanel`) trong chi tiết đơn — đúng luồng nghiệp vụ vì thanh toán luôn gắn với một đơn
 * (`POST /api/v1/orders/{id}/payments`, `POST /api/v1/orders/{id}/refunds`, Database/README.md mục 5.3).
 *
 * Màn duy nhất có route là tra cứu công nợ khách — backend `CustomerLedgerController` dùng
 * `OrderRoleGroups.LedgerViewers` = KH (sao kê của mình), QL, AD. Màn tra cứu theo ID dành cho nhân
 * viên nên dùng `accessMode: 'roles'` để siết đúng QL/AD (không cho quyền thay thế vai trò).
 */
const LEDGER_ROLES = [ROLES.Manager, ROLES.Admin];

export const PAYMENTS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'ledger' },
  {
    path: 'ledger',
    loadComponent: () =>
      import('./customer-ledger/customer-ledger').then((m) => m.CustomerLedger),
    canActivate: [authGuard, roleGuard],
    title: 'Công nợ khách hàng · Silicon Horizon',
    data: { title: 'Công nợ khách hàng', roles: LEDGER_ROLES, accessMode: 'roles' },
  },
];
