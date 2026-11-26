import type { Routes } from '@angular/router';
import { pcBuildDetailResolver } from './pc-build-detail.resolver';

/**
 * Route của khu vực "PC BUILD SẴN" — nạp lazy từ `landing.routes.ts` nên dùng chung `PublicLayout`.
 * Cả hai màn hình đều là API CÔNG KHAI: khách vãng lai vẫn xem được và thêm cả bộ vào giỏ
 * (giỏ tạm theo mã phiên X-Cart-Session). Chi tiết dùng resolver để có dữ liệu trước khi hiển thị.
 */
export const PC_BUILD_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pc-build-list/pc-build-list').then((m) => m.PcBuildList),
    title: 'PC build sẵn · Apex Circuit Electronics',
    data: { title: 'PC build sẵn' },
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pc-build-detail/pc-build-detail').then((m) => m.PcBuildDetail),
    resolve: { build: pcBuildDetailResolver },
    title: 'Chi tiết bộ PC build sẵn · Apex Circuit Electronics',
    data: { title: 'Chi tiết bộ PC build sẵn' },
  },
];
