import { HttpErrorResponse } from '@angular/common/http';

/**
 * Body lỗi của backend — chuẩn ProblemDetails
 * (xem MIDDLEWARE_AND_ERROR_HANDLING.md mục 4 và ApiExceptionHandler.cs).
 * Lỗi validate model của ASP.NET có thêm `errors` dạng { "tênTrường": ["thông điệp"] }.
 */
export interface ProblemDetailsBody {
  type?: string | null;
  title?: string | null;
  status?: number | null;
  detail?: string | null;
  instance?: string | null;
  traceId?: string | null;
  correlationId?: string | null;
  errors?: Record<string, string[]> | null;
}

/**
 * Lỗi API đã CHUẨN HOÁ. Mọi lời gọi đi qua `ApiService` đều ném ra object này
 * (không phải `HttpErrorResponse`) để component chỉ phải xử lý một hình dạng dữ liệu duy nhất.
 */
export interface ApiError {
  /** Mã HTTP; 0 = lỗi mạng/không kết nối được máy chủ. */
  status: number;
  /** Tiêu đề lỗi tiếng Việt từ backend (AppException/SqlErrorTranslator) hoặc thông điệp mặc định theo mã. */
  title: string;
  /** Mã lỗi nội bộ, ví dụ "SQL-50003", "AUTH-INVALID-CREDENTIALS". */
  detail: string | null;
  type: string | null;
  instance: string | null;
  /** Mã trace W3C của backend — dùng khi cần đối chiếu log. */
  traceId: string | null;
  /** Mã tương quan (X-Correlation-Id) — hiển thị kèm toast để người dùng báo lại khi có sự cố. */
  correlationId: string | null;
  /** Lỗi theo từng trường khi backend trả 400 validate. */
  fieldErrors: Record<string, string[]> | null;
  /** true khi request không tới được server (mất mạng, API chưa chạy, bị chặn CORS...). */
  isNetworkError: boolean;
  /** Thông điệp hiển thị cho người dùng (ưu tiên `title`, thêm số trường lỗi nếu là lỗi validate). */
  message: string;
  /** Body gốc — chỉ dùng để tra cứu. */
  raw: unknown;
}

/** Header chứa mã tương quan do `CorrelationIdMiddleware` gắn vào MỌI response. */
export const CORRELATION_ID_HEADER = 'X-Correlation-Id';

const NETWORK_ERROR_MESSAGE =
  'Không kết nối được máy chủ API. Kiểm tra backend đang chạy ở http://localhost:5270 và proxy dev.';

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function asText(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0 ? value : null;
}

/** Thông điệp mặc định theo mã HTTP khi backend không kèm `title` (lỗi 404/403 của UseStatusCodePages). */
export function defaultTitleForStatus(status: number): string {
  switch (status) {
    case 0:
      return NETWORK_ERROR_MESSAGE;
    case 400:
      return 'Dữ liệu gửi lên không hợp lệ.';
    case 401:
      return 'Phiên đăng nhập đã hết hạn hoặc không hợp lệ.';
    case 403:
      return 'Bạn không có quyền thực hiện thao tác này.';
    case 404:
      return 'Không tìm thấy dữ liệu yêu cầu.';
    case 405:
      return 'Phương thức gọi API không được hỗ trợ.';
    case 409:
      return 'Dữ liệu đang ở trạng thái không cho phép thao tác này.';
    case 422:
      return 'Yêu cầu vượt quá hạn mức cho phép.';
    case 499:
      return 'Yêu cầu đã bị huỷ.';
    case 500:
      return 'Lỗi hệ thống, vui lòng liên hệ quản trị viên.';
    case 503:
      return 'Hệ thống đang quá tải hoặc tạm thời không sẵn sàng, vui lòng thử lại.';
    default:
      return 'Đã xảy ra lỗi khi gọi API.';
  }
}

/** Đọc body lỗi: backend trả JSON, nhưng lỗi hạ tầng có thể chỉ là chuỗi thô. */
function readBody(error: HttpErrorResponse): unknown {
  const body: unknown = error.error;

  if (typeof body === 'string' && body.trim().length > 0) {
    try {
      return JSON.parse(body) as unknown;
    } catch {
      return { title: body } satisfies ProblemDetailsBody;
    }
  }

  return body;
}

/** Chuẩn hoá `errors` của ValidationProblemDetails (chỉ giữ mảng chuỗi không rỗng). */
function readFieldErrors(body: unknown): Record<string, string[]> | null {
  if (!isObject(body) || !isObject(body['errors'])) {
    return null;
  }

  const source = body['errors'];
  const result: Record<string, string[]> = {};

  for (const [field, messages] of Object.entries(source)) {
    const list = Array.isArray(messages)
      ? messages.filter((item): item is string => typeof item === 'string' && item.length > 0)
      : [];

    if (list.length > 0) {
      result[field] = list;
    }
  }

  return Object.keys(result).length > 0 ? result : null;
}

/** Chuyển `HttpErrorResponse` → `ApiError` (dùng ở ApiService và errorInterceptor). */
export function mapHttpErrorToApiError(error: HttpErrorResponse): ApiError {
  const body = readBody(error);
  const fieldErrors = readFieldErrors(body);
  const isNetworkError = error.status === 0;

  const title =
    asText(isObject(body) ? body['title'] : null) ??
    (isNetworkError ? NETWORK_ERROR_MESSAGE : defaultTitleForStatus(error.status));

  const correlationId =
    asText(isObject(body) ? body['correlationId'] : null) ??
    asText(error.headers.get(CORRELATION_ID_HEADER));

  const fieldCount = fieldErrors === null ? 0 : Object.keys(fieldErrors).length;

  return {
    status: error.status,
    title,
    detail: asText(isObject(body) ? body['detail'] : null),
    type: asText(isObject(body) ? body['type'] : null),
    instance: asText(isObject(body) ? body['instance'] : null),
    traceId: asText(isObject(body) ? body['traceId'] : null),
    correlationId,
    fieldErrors,
    isNetworkError,
    message: fieldCount > 0 ? `${title} (${fieldCount} trường cần kiểm tra lại)` : title,
    raw: body,
  };
}

/** Chuyển exception bất kỳ (lỗi runtime, lỗi mạng do interceptor ném ra...) → `ApiError`. */
export function toApiError(reason: unknown, status = 0): ApiError {
  if (reason instanceof HttpErrorResponse) {
    return mapHttpErrorToApiError(reason);
  }

  return {
    status,
    title: NETWORK_ERROR_MESSAGE,
    detail: reason instanceof Error ? reason.message : null,
    type: null,
    instance: null,
    traceId: null,
    correlationId: null,
    fieldErrors: null,
    isNetworkError: true,
    message: NETWORK_ERROR_MESSAGE,
    raw: reason,
  };
}

/** true nếu giá trị là `ApiError` (dùng để phân nhánh trong catchError của component). */
export function isApiError(value: unknown): value is ApiError {
  return isObject(value) && typeof value['status'] === 'number' && typeof value['message'] === 'string';
}

/**
 * Tra thông điệp lỗi của một trường, KHÔNG phân biệt hoa/thường và bỏ tiền tố đường dẫn
 * ("specs.socket" / "Specs.Socket" đều khớp khoá "socket"), dùng để map lỗi 400 về đúng field trong form.
 */
export function firstFieldError(
  error: ApiError | null,
  field: string,
  fallback: string | null = null,
): string | null {
  const errors = error === null ? null : error.fieldErrors;
  if (errors === null) {
    return fallback;
  }

  const wanted = field.toLowerCase();

  for (const [key, messages] of Object.entries(errors)) {
    const shortKey = key.split('.').pop()?.toLowerCase() ?? key.toLowerCase();

    if (shortKey === wanted && messages.length > 0) {
      return messages[0] ?? fallback;
    }
  }

  return fallback;
}
