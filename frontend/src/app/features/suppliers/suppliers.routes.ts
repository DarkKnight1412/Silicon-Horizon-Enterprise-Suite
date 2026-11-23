import type { Routes } from '@angular/router';
import { authGuard } from '../../core/auth/auth.guard';
import { permissionGuard } from '../../core/auth/permission.guard';
import { roleGuard } from '../../core/auth/role.guard';
import { PERMISSIONS, ROLES } from '../../core/models/auth.model';
import { pendingChangesGuard } from '../admin/pending-changes.guard';

/**
 * Route của feature Nhà cung cấp (nạp lazy từ `app.routes.ts`).
 *
 * Phân quyền bám ĐÚNG `SuppliersController` / `SupplierReturnsController`:
 * - Đọc danh mục (GET `/suppliers`, `/{id}`): Thủ kho + Quản lý + Admin (`InventoryRoles.StockDesk`).
 * - Tạo/sửa nhà cung cấp (POST/PUT): CHỈ Admin — màn hình ẩn form với các vai trò khác.
 * - Công nợ & thanh toán (`/{id}/ledger`, `/{id}/payments`): Quản lý + Admin (ẩn khối nếu thiếu quyền).
 * - Trả hàng NCC (POST `/supplier-returns`): Thủ kho + Quản lý + Admin.
 *
 * Thứ tự khai báo: `:id/return` phải đứng TRƯỚC `:id` để không bị route tham số "nuốt" mất.
 */
const SUPPLIER_READ_ROLES = [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin];
const SUPPLIER_RETURN_ROLES = [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin];
const SUPPLIER_DEBT_ROLES = [ROLES.Manager, ROLES.Admin];

export const SUPPLIERS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'list' },
  {
    path: 'list',
    loadComponent: () =>
      import('./supplier-list/supplier-list').then((m) => m.SupplierList),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Nhà cung cấp · Apex Circuit Electronics',
    data: {
      title: 'Nhà cung cấp',
      roles: SUPPLIER_READ_ROLES,
      permissions: [PERMISSIONS.StockReceipt],
    },
  },
  {
    path: ':id/return',
    loadComponent: () =>
      import('./supplier-return/supplier-return').then((m) => m.SupplierReturn),
    canActivate: [authGuard, roleGuard, permissionGuard],
    canDeactivate: [pendingChangesGuard],
    title: 'Trả hàng nhà cung cấp · Apex Circuit Electronics',
    data: {
      title: 'Trả hàng nhà cung cấp',
      roles: SUPPLIER_RETURN_ROLES,
      permissions: [PERMISSIONS.StockReceipt],
    },
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./supplier-detail/supplier-detail').then((m) => m.SupplierDetail),
    canActivate: [authGuard, roleGuard, permissionGuard],
    title: 'Chi tiết nhà cung cấp · Apex Circuit Electronics',
    data: {
      title: 'Chi tiết nhà cung cấp',
      roles: SUPPLIER_READ_ROLES,
      permissions: [PERMISSIONS.StockReceipt],
    },
  },
];

/** Vai trò xem được công nợ & ghi nhận thanh toán NCC (`InventoryRoles.Approvers`). */
export const SUPPLIER_LEDGER_ROLES = SUPPLIER_DEBT_ROLES;
