import { HttpClient, HttpContext, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiError, mapHttpErrorToApiError, toApiError } from '../models/api-error.model';
import { LoadingService } from '../services/loading.service';
import { SKIP_AUTH, SKIP_ERROR_TOAST } from './http-context';

/** Giá trị hợp lệ của một tham số query (mảng sẽ được lặp thành nhiều tham số cùng tên). */
export type QueryValue =
  | string
  | number
  | boolean
  | Date
  | null
  | undefined
  | readonly (string | number)[];

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

/** Tuỳ chọn dùng chung cho mọi lời gọi API. */
export interface ApiRequestOptions {
  /** Query string; giá trị null/undefined/'' bị bỏ qua tự động. */
  params?: Record<string, QueryValue>;
  /** Header bổ sung, ví dụ { 'X-Cart-Session': token } cho giỏ hàng của khách vãng lai. */
  headers?: Record<string, string>;
  /** Không gắn Authorization (endpoint public / auth). */
  skipAuth?: boolean;
  /** Không hiện toast lỗi tự động — nơi gọi tự xử lý `ApiError`. */
  silent?: boolean;
  /** Bật cờ bận toàn cục (LoadingService) trong lúc request đang chạy. */
  trackLoading?: boolean;
}

/**
 * Lớp bọc DUY NHẤT quanh HttpClient.
 *
 * Quy ước quan trọng:
 * - Ghép sẵn base URL: `environment.apiBaseUrl + environment.apiPathPrefix`
 *   (dev để rỗng ⇒ request đi qua proxy `/api` ⇒ không vướng CORS).
 * - Mọi lỗi (kể cả lỗi mạng status 0) được map thành `ApiError` trước khi tới component,
 *   nên component không bao giờ phải xử lý `HttpErrorResponse` thô.
 * - Việc hiện toast lỗi nằm ở `errorInterceptor` (chạy phía ngoài ApiService), tránh hiện trùng.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly loading = inject(LoadingService);

  /** Tiền tố đầy đủ của API, ví dụ '/api' khi dùng proxy hoặc 'https://api.../api'. */
  readonly baseUrl = `${environment.apiBaseUrl}${environment.apiPathPrefix}`;

  get<T>(path: string, options: ApiRequestOptions = {}): Observable<T> {
    return this.request<T>('GET', path, null, options);
  }

  post<T>(path: string, body: unknown = null, options: ApiRequestOptions = {}): Observable<T> {
    return this.request<T>('POST', path, body, options);
  }

  put<T>(path: string, body: unknown = null, options: ApiRequestOptions = {}): Observable<T> {
    return this.request<T>('PUT', path, body, options);
  }

  patch<T>(path: string, body: unknown = null, options: ApiRequestOptions = {}): Observable<T> {
    return this.request<T>('PATCH', path, body, options);
  }

  delete<T>(path: string, options: ApiRequestOptions = {}): Observable<T> {
    return this.request<T>('DELETE', path, null, options);
  }

  /** Đường dẫn tuyệt đối tới API (dùng khi cần hiển thị hoặc dựng URL thủ công). */
  url(path: string): string {
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path;
    }

    return `${this.baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
  }

  private request<T>(method: HttpMethod, path: string, body: unknown, options: ApiRequestOptions): Observable<T> {
    const request$ = this.http
      .request<T>(method, this.url(path), {
        body: body === null ? undefined : body,
        params: buildParams(options.params),
        headers: options.headers ?? {},
        context: buildContext(options),
      })
      .pipe(catchError((error: unknown) => throwError(() => normalizeError(error))));

    return options.trackLoading === true ? this.loading.track(request$) : request$;
  }
}

/** Dựng HttpParams, bỏ qua giá trị rỗng để query string gọn và đúng ý nghĩa "không lọc". */
function buildParams(values: Record<string, QueryValue> | undefined): HttpParams {
  let params = new HttpParams();

  if (values === undefined) {
    return params;
  }

  for (const [key, value] of Object.entries(values)) {
    if (value === null || value === undefined || value === '') {
      continue;
    }

    if (Array.isArray(value)) {
      for (const item of value as readonly (string | number)[]) {
        params = params.append(key, String(item));
      }
      continue;
    }

    params = params.set(key, value instanceof Date ? value.toISOString() : String(value));
  }

  return params;
}

function buildContext(options: ApiRequestOptions): HttpContext {
  let context = new HttpContext();

  if (options.skipAuth === true) {
    context = context.set(SKIP_AUTH, true);
  }

  if (options.silent === true) {
    context = context.set(SKIP_ERROR_TOAST, true);
  }

  return context;
}

/** Chuẩn hoá mọi lỗi (kể cả lỗi runtime trong luồng RxJS) về `ApiError`. */
function normalizeError(error: unknown): ApiError {
  return error instanceof HttpErrorResponse ? mapHttpErrorToApiError(error) : toApiError(error);
}
