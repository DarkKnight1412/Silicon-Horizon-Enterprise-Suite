import { inject } from '@angular/core';
import { Router, type ResolveFn } from '@angular/router';
import { EMPTY, catchError } from 'rxjs';
import type { OrderDetailDto } from '../../core/models/order.model';
import { OrderApi } from './order-api.service';

/**
 * Resolver cho chi tiết đơn (GET /api/v1/orders/{id}).
 * Không tìm thấy / không thuộc quyền của người dùng (403, 404) ⇒ điều hướng sang /404 rồi trả EMPTY
 * để huỷ điều hướng hiện tại (quy ước resolver của Angular).
 */
export const orderDetailResolver: ResolveFn<OrderDetailDto> = (route) => {
  const orderApi = inject(OrderApi);
  const router = inject(Router);
  const id = Number(route.paramMap.get('id'));

  if (!Number.isFinite(id) || id <= 0) {
    void router.navigate(['/404'], { replaceUrl: true });
    return EMPTY;
  }

  return orderApi.getOrder(id).pipe(
    catchError(() => {
      void router.navigate(['/404'], { replaceUrl: true });
      return EMPTY;
    }),
  );
};
