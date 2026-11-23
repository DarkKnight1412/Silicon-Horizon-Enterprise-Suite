import type { Routes } from '@angular/router';
import { guestGuard } from './core/auth/guest.guard';
import { staffGuard } from './core/auth/staff.guard';

/**
 * Bảng route gốc — TÁCH 2 KHU VỰC THEO VAI TRÒ.
 *
 * 1. KHUNG KHÁCH HÀNG (PublicLayout) — đặt ĐẦU TIÊN nên mở đường dẫn gốc `/` luôn vào trang chủ giới thiệu.
 *    Chứa toàn bộ LUỒNG MUA HÀNG: `/shop`, `/cart`, `/catalog` (danh mục & chi tiết sản phẩm) và `/orders`
 *    (đặt hàng + đơn của tôi), cùng `/about`, `/development`. Khách vãng lai vẫn xem và mua được.
 * 2. KHUNG XÁC THỰC (AuthLayout) — chỉ `/login`, `/register`, bị `guestGuard` chặn khi đã đăng nhập
 *    (chuyển về đúng khu vực theo vai trò — `AuthStore.homePath`).
 * 3. KHUNG NỘI BỘ (MainLayout) — CHỈ dành cho NHÂN VIÊN & QUẢN LÝ/ADMIN (`staffGuard`): tổng quan, quản trị
 *    sản phẩm, kho, nhà cung cấp, bảo hành, đổi trả, thanh toán, POS, nhân sự, hoa hồng, nhật ký, báo cáo.
 *    Khách hàng vào đây sẽ bị đẩy về khu vực khách hàng nên KHÔNG bao giờ thấy khung nội bộ.
 * 4. Trang 403/404 (`features/errors`) + route `**` bắt mọi đường dẫn sai.
 *
 * Lưu ý: khung xác thực đứng TRƯỚC để `/login` không bị route `**` của khung nội bộ "nuốt".
 */
export const routes: Routes = [
  // 1. Khung công khai (landing): mở đường dẫn gốc ⇒ luôn vào TRANG CHỦ. Không cần đăng nhập.
  {
    path: '',
    loadComponent: () => import('./layout/public-layout/public-layout').then((m) => m.PublicLayout),
    loadChildren: () => import('./features/landing/landing.routes').then((m) => m.LANDING_ROUTES),
  },
  {
    // 2. Khung đăng nhập (AuthLayout) — /login, /register.
    path: '',
    loadComponent: () => import('./layout/auth-layout/auth-layout').then((m) => m.AuthLayout),
    canActivate: [guestGuard],
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    // KHU VỰC NỘI BỘ — CHỈ dành cho NHÂN VIÊN & QUẢN LÝ/ADMIN (staffGuard chặn ở đây).
    // Khách hàng bị đẩy về giao diện mua sắm công khai, nên họ không bao giờ thấy khung này.
    path: '',
    loadComponent: () => import('./layout/main-layout/main-layout').then((m) => m.MainLayout),
    canActivate: [staffGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      // `catalog` (danh mục & chi tiết sản phẩm), `cart` và `orders` (đặt hàng, đơn của tôi)
      // đã chuyển sang KHUNG KHÁCH HÀNG — xem `features/landing/landing.routes.ts`.
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
        title: 'Không tìm thấy trang · Apex Circuit Electronics',
      },
    ],
  },
];

