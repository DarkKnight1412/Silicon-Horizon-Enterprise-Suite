import type { Routes } from '@angular/router';
import { guestGuard } from './core/auth/guest.guard';

/**
 * Bảng route gốc.
 *
 * Cấu trúc:
 * 1. Khung ĐĂNG NHẬP (AuthLayout) — chỉ chứa /login, /register, bị `guestGuard` chặn khi đã đăng nhập.
 * 2. Khung CHÍNH (MainLayout) — mọi màn hình còn lại: mỗi feature nạp lazy bằng `loadChildren`,
 *    từng trang lẻ nạp bằng `loadComponent` (kể cả 2 layout ở đây).
 * 3. Trang 403/404 (`features/errors`) + route `**` bắt mọi đường dẫn sai.
 *
 * Lưu ý: khung đăng nhập đứng TRƯỚC để `/login` không bị route `**` của khung chính "nuốt".
 */
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./layout/auth-layout/auth-layout').then((m) => m.AuthLayout),
    canActivate: [guestGuard],
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: '',
    loadComponent: () => import('./layout/main-layout/main-layout').then((m) => m.MainLayout),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'catalog',
        loadChildren: () =>
          import('./features/catalog/catalog.routes').then((m) => m.CATALOG_ROUTES),
      },
      {
        path: 'cart',
        loadChildren: () => import('./features/cart/cart.routes').then((m) => m.CART_ROUTES),
      },
      {
        path: 'orders',
        loadChildren: () => import('./features/orders/orders.routes').then((m) => m.ORDERS_ROUTES),
      },
      {
        path: 'admin',
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      // Nhóm vận hành kho: tồn kho · nhập/chuyển kho · kiểm kê · serial.
      {
        path: 'inventory',
        loadChildren: () =>
          import('./features/inventory/inventory.routes').then((m) => m.INVENTORY_ROUTES),
      },
      // Nhà cung cấp: danh mục, công nợ phải trả, trả hàng nhà cung cấp.
      {
        path: 'suppliers',
        loadChildren: () =>
          import('./features/suppliers/suppliers.routes').then((m) => m.SUPPLIERS_ROUTES),
      },
      // Bảo hành: tiếp nhận, xử lý phiếu, tra cứu theo serial.
      {
        path: 'warranty',
        loadChildren: () =>
          import('./features/warranty/warranty.routes').then((m) => m.WARRANTY_ROUTES),
      },
      // Đổi trả: duyệt, kiểm tra hàng trả, hoàn tiền.
      {
        path: 'returns',
        loadChildren: () => import('./features/returns/returns.routes').then((m) => m.RETURNS_ROUTES),
      },
      // Thanh toán: tra cứu công nợ khách (khối ghi nhận thanh toán/hoàn tiền nằm trong chi tiết đơn).
      {
        path: 'payments',
        loadChildren: () =>
          import('./features/payments/payments.routes').then((m) => m.PAYMENTS_ROUTES),
      },
      // Đơn hàng nhân viên: danh sách đơn chi nhánh + POS tạo đơn tại quầy.
      {
        path: 'staff-orders',
        loadChildren: () =>
          import('./features/staff-orders/staff-orders.routes').then((m) => m.STAFF_ORDERS_ROUTES),
      },
      // Nhân sự: xếp ca làm việc + chấm công / bảng công tháng.
      {
        path: 'hr',
        loadChildren: () => import('./features/hr/hr.routes').then((m) => m.HR_ROUTES),
      },
      // Hoa hồng theo kỳ (tổng hợp, điều chỉnh, chốt kỳ).
      {
        path: 'commission',
        loadChildren: () =>
          import('./features/commission/commission.routes').then((m) => m.COMMISSION_ROUTES),
      },
      // Nhật ký hệ thống (sổ append-only, chỉ đọc).
      {
        path: 'audit',
        loadChildren: () => import('./features/audit/audit.routes').then((m) => m.AUDIT_ROUTES),
      },
      // Báo cáo: doanh thu · tồn kho · serial · công nợ · bảo hành.
      {
        path: 'reports',
        loadChildren: () =>
          import('./features/reports/reports.routes').then((m) => m.REPORTS_ROUTES),
      },
      // Trang 403/404 dùng đường dẫn ngắn đúng như đích điều hướng của guard/resolver.
      {
        path: '',
        loadChildren: () => import('./features/errors/errors.routes').then((m) => m.ERROR_ROUTES),
      },
      {
        path: '**',
        loadComponent: () =>
          import('./features/errors/not-found/not-found').then((m) => m.NotFound),
        title: 'Không tìm thấy trang · Silicon Horizon',
      },
    ],
  },
];

