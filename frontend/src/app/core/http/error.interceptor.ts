import { HttpErrorResponse, type HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { mapHttpErrorToApiError, type ApiError } from '../models/api-error.model';
import { NotificationService, type ToastType } from '../services/notification.service';
import { SKIP_ERROR_TOAST } from './http-context';

/**
 * Bắt lỗi HTTP và hiện toast — TRỪ các mã đã có luồng xử lý riêng:
 * - 401: do `authInterceptor` làm mới token rồi thử lại; chỉ khi refresh thất bại mới có thông báo riêng.
 * - 499: người dùng tự huỷ request (đổi trang, timeout) — chỉ ghi log ở mức debug của backend.
 */
const SKIP_TOAST_STATUSES: readonly number[] = [401, 499];

/**
 * Interceptor NGOÀI CÙNG (đăng ký trước `authInterceptor`) nên chỉ nhận lỗi CUỐI CÙNG sau khi
 * authInterceptor đã thử refresh ⇒ không hiện toast trùng cho một lần hỏng.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);

  if (req.context.get(SKIP_ERROR_TOAST)) {
    return next(req);
  }

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        report(error, notifications, req.method, req.urlWithParams);
      }

      return throwError(() => error);
    }),
  );
};

/** Phân nhánh thông báo theo mã lỗi và đính kèm correlationId để người dùng báo lại khi cần tra log. */
function report(
  error: HttpErrorResponse,
  notifications: NotificationService,
  method: string,
  url: string,
): void {
  const apiError = mapHttpErrorToApiError(error);

  if (!environment.production) {
    console.error(`[API] ${method} ${url} → ${apiError.status}`, apiError);
  }

  if (SKIP_TOAST_STATUSES.includes(apiError.status)) {
    return;
  }

  const { type, title } = describe(apiError);
  const context = [apiError.detail, firstFieldMessage(apiError)]
    .filter((part): part is string => part !== null)
    .join(' · ');

  const message = context.length === 0 ? null : context;

  if (type === 'warning') {
    notifications.warning(title, message);
    return;
  }

  notifications.error(title, message, apiError.correlationId);
}

function describe(error: ApiError): { type: ToastType; title: string } {
  switch (error.status) {
    case 0:
      return { type: 'error', title: 'Không kết nối được API' };
    case 400:
      return { type: 'error', title: 'Dữ liệu không hợp lệ' };
    case 403:
      return { type: 'error', title: 'Không đủ quyền' };
    case 404:
      return { type: 'error', title: 'Không tìm thấy dữ liệu' };
    case 409:
      return { type: 'error', title: 'Xung đột dữ liệu' };
    case 422:
      return { type: 'warning', title: 'Vượt hạn mức cho phép' };
    case 503:
      return { type: 'error', title: 'Hệ thống tạm thời không sẵn sàng' };
    default:
      return { type: 'error', title: error.status >= 500 ? 'Lỗi hệ thống' : error.message };
  }
}

/** Lấy thông điệp lỗi trường đầu tiên (backend trả ValidationProblemDetails.errors). */
function firstFieldMessage(error: ApiError): string | null {
  if (error.fieldErrors === null) {
    return null;
  }

  for (const messages of Object.values(error.fieldErrors)) {
    const first = messages[0];
    if (first !== undefined) {
      return first;
    }
  }

  return null;
}
