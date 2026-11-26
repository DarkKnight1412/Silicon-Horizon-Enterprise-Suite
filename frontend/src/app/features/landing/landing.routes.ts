import type { Routes } from '@angular/router';

/**
 * Route của KHU VỰC KHÁCH HÀNG (không cần đăng nhập), nạp lazy từ `app.routes.ts`.
 *
 * Tất cả cùng nằm trong `PublicLayout` — khung có thanh điều hướng ngang
 * (Trang chủ · Mua sắm · Về chúng tôi · Định hướng phát triển) + giỏ hàng & nút "Đăng nhập" ở góc phải.
 *
 * TOÀN BỘ LUỒNG MUA HÀNG nằm ở đây (KHÔNG dùng khung nội bộ của nhân viên):
 *   `/shop` (danh mục sản phẩm) → `/catalog` (chi tiết sản phẩm) → `/cart` (giỏ hàng)
 *   → `/orders` (đặt hàng + đơn của tôi + chi tiết đơn).
 * Trang `/about`, `/development` cũng thuộc khung này.
 *
 * `path: ''` (pathMatch 'full') chính là TRANG CHỦ: màn hình hiển thị NGAY khi mở đường dẫn gốc
 * của ứng dụng (`/`). Các đường dẫn khác không khớp ở đây sẽ tự "rơi" xuống khung xác thực / khung nội bộ.
 */
export const LANDING_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home').then((m) => m.Home),
    title: 'Trang chủ · Apex Circuit Electronics',
    data: { title: 'Trang chủ' },
  },
  {
    path: 'shop',
    loadComponent: () => import('./shop/shop').then((m) => m.Shop),
    title: 'Mua sắm · Apex Circuit Electronics',
    data: { title: 'Mua sắm' },
  },
  {
    // PC BUILD SẴN đã được gộp vào /shop dưới dạng tab chế độ ("Mua linh kiện" | "Mua theo PC build sẵn")
    // nên KHÔNG còn trang riêng. Giữ chuyển hướng để link/bookmark cũ không bị 404.
    path: 'pc-builds',
    redirectTo: '/shop',
    pathMatch: 'prefix',
  },
  {
    // Giỏ hàng: khách VÃNG LAI vẫn dùng được (biên lai giỏ giữ theo mã phiên X-Cart-Session).
    path: 'cart',
    loadChildren: () => import('../cart/cart.routes').then((m) => m.CART_ROUTES),
  },
  {
    // Danh mục & CHI TIẾT SẢN PHẨM (API công khai) — nơi nút "Chi tiết" của /shop trỏ tới.
    path: 'catalog',
    loadChildren: () => import('../catalog/catalog.routes').then((m) => m.CATALOG_ROUTES),
  },
  {
    // LUỒNG ĐẶT HÀNG: đặt hàng (checkout) + đơn hàng của tôi + chi tiết đơn.
    // `checkout` và danh sách đơn yêu cầu đăng nhập + vai trò Customer (authGuard/roleGuard trong ORDERS_ROUTES).
    path: 'orders',
    loadChildren: () => import('../orders/orders.routes').then((m) => m.ORDERS_ROUTES),
  },
  {
    path: 'about',
    loadComponent: () => import('./about/about').then((m) => m.About),
    title: 'Về chúng tôi · Apex Circuit Electronics',
    data: { title: 'Về chúng tôi' },
  },
  {
    path: 'development',
    loadComponent: () => import('./development/development').then((m) => m.Development),
    title: 'Định hướng phát triển · Apex Circuit Electronics',
    data: { title: 'Định hướng phát triển' },
  },
];
