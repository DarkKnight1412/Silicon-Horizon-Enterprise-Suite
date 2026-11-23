import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { permissionGuard } from '../../core/auth/permission.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';
import { pendingChangesGuard } from '../admin/pending-changes.guard';

/**
 * Route của feature Kho · Serial (nạp lazy từ `app.routes.ts`).
 *
 * Phân quyền bám ĐÚNG backend (`InventoryRoles.cs`):
 * - `StockDesk`        = Thủ kho + Quản lý + Admin → xem tồn kho, thẻ kho, phiếu nhập/chuyển/kiểm kê.
 * - `StockOperations`  = Thủ kho + Admin → tạo phiếu nhập, tạo/xuất/nhận/hủy phiếu chuyển,
 *                                          mở đợt kiểm kê và nhập số đếm.
 * - `Approvers`        = Quản lý + Admin → đặt tồn tối thiểu, duyệt kiểm kê, đổi trạng thái serial.
 *
 * `accessMode` để mặc định `roleOrPermission` (xem `AppRouteData`): khớp vai trò HOẶC đủ mã quyền —
 * đúng quy ước đang dùng ở feature admin/orders nên không chặn oan khi bảng Permissions chưa seed đủ.
 *
 * Thứ tự khai báo quan trọng: `receipts/new` phải đứng TRƯỚC `receipts/:id`.
 */
const STOCK_DESK_ROLES = [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin];
const STOCK_OPERATION_ROLES = [ROLES.Storekeeper, ROLES.Admin];
const APPROVER_ROLES = [ROLES.Manager, ROLES.Admin];

export const INVENTORY_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'stock' },

  // ── Tồn kho (GET /api/v1/stock, PUT /api/v1/stock/min) ───────────────────
  {
    path: 'stock',
    loadComponent: () => import('./stock-levels/stock-levels').then((m) => m.StockLevels),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Tồn kho · Apex Circuit Electronics',
    data: {
      title: 'Tồn kho',
      roles: STOCK_DESK_ROLES,
      permissions: [PERMISSIONS.StockAdjustApprove],
    },
  },

  // ── Thẻ kho (GET /api/v1/stock/movements) ────────────────────────────────
  {
    path: 'movements',
    loadComponent: () => import('./stock-movements/stock-movements').then((m) => m.StockMovements),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Thẻ kho · Apex Circuit Electronics',
    data: { title: 'Thẻ kho', roles: STOCK_DESK_ROLES, permissions: [PERMISSIONS.StockReceipt] },
  },

  // ── Nhập kho ─────────────────────────────────────────────────────────────
  {
    path: 'receipts',
    loadComponent: () =>
      import('./purchase-receipts/purchase-receipt-list/purchase-receipt-list').then(
        (m) => m.PurchaseReceiptList,
      ),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Phiếu nhập kho · Apex Circuit Electronics',
    data: {
      title: 'Phiếu nhập kho',
      roles: STOCK_DESK_ROLES,
      permissions: [PERMISSIONS.StockReceipt],
    },
  },
  {
    path: 'receipts/new',
    loadComponent: () =>
      import('./purchase-receipts/purchase-receipt-form/purchase-receipt-form').then(
        (m) => m.PurchaseReceiptForm,
      ),
    canActivate: [authGuard, roleGuard, permissionGuard],
    canDeactivate: [pendingChangesGuard],
    title: 'Tạo phiếu nhập kho · Apex Circuit Electronics',
    data: {
      title: 'Tạo phiếu nhập kho',
      roles: STOCK_OPERATION_ROLES,
      permissions: [PERMISSIONS.StockReceipt],
    },
  },
  {
    path: 'receipts/:id',
    loadComponent: () =>
      import('./purchase-receipts/purchase-receipt-detail/purchase-receipt-detail').then(
        (m) => m.PurchaseReceiptDetail,
      ),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Chi tiết phiếu nhập kho · Apex Circuit Electronics',
    data: {
      title: 'Chi tiết phiếu nhập kho',
      roles: STOCK_DESK_ROLES,
      permissions: [PERMISSIONS.StockReceipt],
    },
  },

  // ── Chuyển kho (backend KHÔNG có endpoint danh sách ⇒ vào bằng ID phiếu) ─
  {
    path: 'transfers',
    loadComponent: () =>
      import('./stock-transfers/stock-transfer-create/stock-transfer-create').then(
        (m) => m.StockTransferCreate,
      ),
    canActivate: [authGuard, roleGuard, permissionGuard],
    canDeactivate: [pendingChangesGuard],
    title: 'Tạo phiếu chuyển kho · Apex Circuit Electronics',
    data: {
      title: 'Tạo phiếu chuyển kho',
      roles: STOCK_OPERATION_ROLES,
      permissions: [PERMISSIONS.StockTransfer],
    },
  },
  {
    path: 'transfers/:id',
    loadComponent: () =>
      import('./stock-transfers/stock-transfer-detail/stock-transfer-detail').then(
        (m) => m.StockTransferDetail,
      ),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Chi tiết phiếu chuyển kho · Apex Circuit Electronics',
    data: {
      title: 'Chi tiết phiếu chuyển kho',
      roles: STOCK_DESK_ROLES,
      permissions: [PERMISSIONS.StockTransfer],
    },
  },

  // ── Kiểm kê (backend KHÔNG có endpoint danh sách ⇒ vào bằng ID đợt) ──────
  {
    path: 'stock-takes',
    loadComponent: () => import('./stock-takes/stock-take-create').then((m) => m.StockTakeCreate),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Kiểm kê kho · Apex Circuit Electronics',
    data: {
      title: 'Kiểm kê kho',
      roles: STOCK_OPERATION_ROLES,
      permissions: [PERMISSIONS.StockTake],
    },
  },
  {
    path: 'stock-takes/:id',
    loadComponent: () => import('./stock-takes/stock-take-detail').then((m) => m.StockTakeDetail),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Chi tiết kiểm kê · Apex Circuit Electronics',
    data: {
      title: 'Chi tiết kiểm kê',
      roles: STOCK_DESK_ROLES,
      permissions: [PERMISSIONS.StockTake],
    },
  },

  // ── Serial (GET /api/v1/serials/{serialNumber}, POST /{id}/status) ───────
  {
    path: 'serials',
    loadComponent: () =>
      import('./serials/serial-lookup/serial-lookup').then((m) => m.SerialLookup),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Tra cứu serial · Apex Circuit Electronics',
    data: {
      title: 'Tra cứu serial',
      // Backend `InventoryRoles.SerialLookup` = mọi nhân viên + khách + Admin. Màn quản trị này
      // giới hạn cho nhân viên; khách xem serial của mình ngay trong chi tiết đơn hàng.
      roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
      permissions: [PERMISSIONS.SerialManage],
    },
  },
];

/** Vai trò được ĐỔI TRẠNG THÁI serial thủ công (QL, AD — `InventoryRoles.Approvers`). */
export const SERIAL_STATUS_ROLES = APPROVER_ROLES;

