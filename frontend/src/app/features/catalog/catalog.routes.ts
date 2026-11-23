import type { Routes } from '@angular/router';
import { productDetailResolver } from './product-detail.resolver';

/**
 * Route của feature catalog (nạp lazy từ `app.routes.ts`).
 * Danh mục sản phẩm và kiểm tra tương thích là API CÔNG KHAI nên không cần authGuard;
 * chi tiết sản phẩm dùng resolver để có dữ liệu trước khi hiển thị.
 */
export const CATALOG_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'products' },
  {
    // Danh sách sản phẩm CŨ đã được thay bằng trang bán hàng thống nhất `/shop`
    // (trang cũ có giao diện khác) ⇒ chuyển hướng để khách hàng luôn thấy MỘT giao diện duy nhất.
    path: 'products',
    pathMatch: 'full',
    redirectTo: '/shop',
  },
  {
    path: 'products/:id',
    loadComponent: () => import('./product-detail/product-detail').then((m) => m.ProductDetail),
    resolve: { product: productDetailResolver },
    title: 'Chi tiết sản phẩm · Apex Circuit Electronics',
    data: { title: 'Chi tiết sản phẩm' },
  },
  {
    path: 'compatibility',
    loadComponent: () => import('./compatibility/compatibility').then((m) => m.Compatibility),
    title: 'Kiểm tra tương thích · Apex Circuit Electronics',
    data: { title: 'Kiểm tra tương thích linh kiện' },
  },
];
