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
    path: 'products',
    loadComponent: () => import('./product-list/product-list').then((m) => m.ProductList),
    title: 'Sản phẩm · Silicon Horizon',
    data: { title: 'Danh mục sản phẩm' },
  },
  {
    path: 'products/:id',
    loadComponent: () => import('./product-detail/product-detail').then((m) => m.ProductDetail),
    resolve: { product: productDetailResolver },
    title: 'Chi tiết sản phẩm · Silicon Horizon',
    data: { title: 'Chi tiết sản phẩm' },
  },
  {
    path: 'compatibility',
    loadComponent: () => import('./compatibility/compatibility').then((m) => m.Compatibility),
    title: 'Kiểm tra tương thích · Silicon Horizon',
    data: { title: 'Kiểm tra tương thích linh kiện' },
  },
];
