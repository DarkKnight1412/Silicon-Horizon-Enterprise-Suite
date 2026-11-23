import { HttpErrorResponse, type HttpInterceptorFn, type HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthStore } from '../auth/auth.store';
import { NotificationService } from '../services/notification.service';
import { SKIP_AUTH } from './http-context';

/**
 * Luồng xác thực của mọi request (trừ request có cờ `skipAuth` như login/register/refresh):
 *
 * 1. Gắn `Authorization: Bearer <accessToken đang giữ trong bộ nhớ>` nếu đã đăng nhập.
 * 2. Nếu server trả 401 ⇒ gọi `AuthStore.refreshOnce()` — SINGLE-FLIGHT nên dù 5 request cùng hỏng
 *    thì backend chỉ nhận ĐÚNG MỘT lời gọi /api/auth/refresh.
 * 3. Refresh thành công ⇒ **thử lại chính request cũ** với token mới (không cần người dùng làm gì).
 * 4. Refresh thất bại (hoặc không có refresh token) ⇒ xoá state, thông báo và điều hướng
 *    `/login?returnUrl=<url hiện tại>` để quay lại đúng màn hình sau khi đăng nhập lại.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);
  const notifications = inject(NotificationService);

  if (req.context.get(SKIP_AUTH)) {
    return next(req);
  }

  const token = authStore.accessToken();
  const authorizedRequest =
    token === null ? req : req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      if (!authStore.hasRefreshToken()) {
        authStore.expireSession();
        return throwError(() => error);
      }

      return authStore.refreshOnce().pipe(
        switchMap(() => next(withFreshToken(req, authStore.accessToken()))),
        catchError((refreshError: unknown) => {
          authStore.expireSession();
          notifications.warning(
            'Phiên đăng nhập đã hết hạn',
            'Vui lòng đăng nhập lại để tiếp tục thao tác.',
          );
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};

/** Gắn lại access token mới vào request cần thử lại. */
function withFreshToken(request: HttpRequest<unknown>, accessToken: string | null): HttpRequest<unknown> {
  return accessToken === null
    ? request
    : request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } });
}
