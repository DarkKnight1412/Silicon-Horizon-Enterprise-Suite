import { inject } from '@angular/core';
import { Router, type ResolveFn } from '@angular/router';
import { EMPTY, catchError } from 'rxjs';
import type { PcBuildDetailDto } from '../../core/models/pc-build.model';
import { PcBuildApi } from './pc-build-api.service';

/**
 * Resolver cho trang chi tiết bộ PC build sẵn (GET /api/v1/pc-builds/{id}).
 * Dữ liệu về TRƯỚC khi kích hoạt route nên trang không cần trạng thái "đang tải";
 * nhờ `withComponentInputBinding()` kết quả được bind thẳng vào input `build` của component.
 *
 * Không tìm thấy (404) hoặc id sai ⇒ điều hướng sang /404 rồi trả EMPTY để HUỶ điều hướng hiện tại
 * (quy ước giống `productDetailResolver`).
 */
export const pcBuildDetailResolver: ResolveFn<PcBuildDetailDto> = (route) => {
  const api = inject(PcBuildApi);
  const router = inject(Router);
  const id = Number(route.paramMap.get('id'));

  if (!Number.isFinite(id) || id <= 0) {
    void router.navigate(['/404'], { replaceUrl: true });
    return EMPTY;
  }

  return api.getBuild(id).pipe(
    catchError(() => {
      void router.navigate(['/404'], { replaceUrl: true });
      return EMPTY;
    }),
  );
};
