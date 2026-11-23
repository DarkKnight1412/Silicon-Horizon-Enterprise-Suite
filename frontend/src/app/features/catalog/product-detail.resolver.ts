import { inject } from '@angular/core';
import { Router, type ResolveFn } from '@angular/router';
import { EMPTY, catchError } from 'rxjs';
import type { ProductDetailDto } from '../../core/models/product.model';
import { CatalogApi } from './catalog-api.service';

/**
 * Resolver cho trang chi tiết sản phẩm (GET /api/v1/products/{id}).
 * Dữ liệu về TRƯỚC khi kích hoạt route nên trang không cần trạng thái "đang tải";
 * nhờ `withComponentInputBinding()` kết quả được bind thẳng vào input `product` của component.
 *
 * Không tìm thấy (404) hoặc id sai ⇒ điều hướng sang /404 rồi trả EMPTY để HUỶ điều hướng hiện tại
 * (quy ước của Angular: resolver trả Observable hoàn tất mà không phát giá trị thì navigation bị huỷ).
 */
export const productDetailResolver: ResolveFn<ProductDetailDto> = (route) => {
  const catalog = inject(CatalogApi);
  const router = inject(Router);
  const id = Number(route.paramMap.get('id'));

  if (!Number.isFinite(id) || id <= 0) {
    void router.navigate(['/404'], { replaceUrl: true });
    return EMPTY;
  }

  return catalog.getProduct(id).pipe(
    catchError(() => {
      void router.navigate(['/404'], { replaceUrl: true });
      return EMPTY;
    }),
  );
};
