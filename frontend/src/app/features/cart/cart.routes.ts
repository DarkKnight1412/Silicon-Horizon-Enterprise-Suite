import type { Routes } from '@angular/router';

/**
 * Route của feature cart. Giỏ hàng hoạt động cho cả khách vãng lai (mã phiên ở header X-Cart-Session)
 * nên KHÔNG gắn authGuard; riêng bước đặt hàng (feature orders) mới bắt buộc đăng nhập.
 */
export const CART_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./cart-page/cart-page').then((m) => m.CartPage),
    title: 'Giỏ hàng · Apex Circuit Electronics',
    data: { title: 'Giỏ hàng' },
  },
];
