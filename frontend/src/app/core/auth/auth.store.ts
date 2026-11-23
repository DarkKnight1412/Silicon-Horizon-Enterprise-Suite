import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, of, shareReplay, switchMap, tap, throwError } from 'rxjs';
import { ApiService } from '../http/api.service';
import { toApiError } from '../models/api-error.model';
import type { AccessMode } from '../models/common.model';
import {
  ROLES,
  isStaffRoles,
  type AuthTokensDto,
  type CurrentAccountDto,
  type LoginRequest,
  type RefreshTokenRequest,
  type RegisteredCustomerDto,
  type RegisterCustomerRequest,
} from '../models/auth.model';
import { NotificationService } from '../services/notification.service';

/** Khoá localStorage giữ REFRESH TOKEN (access token KHÔNG bao giờ được lưu ngoài bộ nhớ). */
export const REFRESH_TOKEN_STORAGE_KEY = 'sh.refresh-token';

/** Tuỳ chọn kiểm tra quyền — dùng chung cho guard, menu sidebar và nút thao tác trên màn hình. */
export interface AccessQuery {
  roles?: readonly string[] | undefined;
  permissions?: readonly string[] | undefined;
  accessMode?: AccessMode | undefined;
}

/**
 * STATE TOÀN CỤC của phiên đăng nhập (quy ước ở `core/models/common.model.ts`).
 *
 * Nguyên tắc bảo mật & vòng đời token:
 * - **Access token chỉ nằm TRONG BỘ NHỚ** (signal), không localStorage ⇒ giảm rủi ro XSS đánh cắp token.
 * - **Refresh token ở localStorage** để khôi phục phiên khi F5; xoá ngay khi đăng xuất / refresh thất bại.
 * - `refreshOnce()` là SINGLE-FLIGHT: nhiều request cùng bị 401 chỉ tạo DUY NHẤT một lời gọi /auth/refresh.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  private readonly _accessToken = signal<string | null>(null);
  private readonly _refreshToken = signal<string | null>(readStoredRefreshToken());
  private readonly _account = signal<CurrentAccountDto | null>(null);
  private readonly _ready = signal(false);

  /** Lời gọi refresh đang bay — gộp nhiều request 401 vào một lần refresh duy nhất. */
  private refreshInFlight: Observable<AuthTokensDto> | null = null;

  /** Access token (chỉ trong bộ nhớ). */
  readonly accessToken = this._accessToken.asReadonly();

  /** Hồ sơ tài khoản hiện tại (GET /api/me). */
  readonly account = this._account.asReadonly();

  /** true khi đã khôi phục xong phiên lúc khởi động — authGuard chờ cờ này. */
  readonly ready = this._ready.asReadonly();

  readonly isAuthenticated = computed(() => this._accessToken() !== null);

  readonly roles = computed<readonly string[]>(() => this._account()?.roles ?? []);

  readonly permissions = computed<readonly string[]>(() => this._account()?.permissions ?? []);

  readonly isCustomer = computed(() => this.roles().includes(ROLES.Customer));

  /** Nhân viên (Thu ngân/Tư vấn/Thủ kho/Quản lý/Admin) — dùng để hiện các nút xử lý đơn. */
  readonly isStaff = computed(() => isStaffRoles(this.roles()));

  readonly displayName = computed(() => this._account()?.username ?? 'Khách');

  /** Refresh token hiện có (null nếu chưa đăng nhập). */
  refreshToken(): string | null {
    return this._refreshToken();
  }

  hasRefreshToken(): boolean {
    return this._refreshToken() !== null;
  }

  /** Lưu cặp token mới: access token vào bộ nhớ, refresh token vào localStorage. */
  applyTokens(tokens: AuthTokensDto): void {
    this._accessToken.set(tokens.accessToken);
    this._refreshToken.set(tokens.refreshToken);
    storeRefreshToken(tokens.refreshToken);
  }

  /** Xoá sạch state phiên (đăng xuất hoặc refresh thất bại). */
  clearSession(): void {
    this._accessToken.set(null);
    this._refreshToken.set(null);
    this._account.set(null);
    this.refreshInFlight = null;
    removeStoredRefreshToken();
  }

  /** POST /api/auth/login — nhận token rồi nạp ngay hồ sơ /api/me. */
  login(request: LoginRequest): Observable<CurrentAccountDto> {
    return this.api
      .post<AuthTokensDto>('/auth/login', request, { skipAuth: true, silent: true })
      .pipe(
        tap((tokens) => this.applyTokens(tokens)),
        switchMap(() => this.loadAccount()),
      );
  }

  /** POST /api/auth/register — khách tự đăng ký, trả về userId/customerId. */
  register(request: RegisterCustomerRequest): Observable<RegisteredCustomerDto> {
    return this.api.post<RegisteredCustomerDto>('/auth/register', request, {
      skipAuth: true,
      silent: true,
    });
  }

  /**
   * POST /api/auth/refresh — SINGLE-FLIGHT.
   * Nhiều request 401 xảy ra cùng lúc dùng chung một Observable (shareReplay) nên backend chỉ nhận
   * đúng MỘT lời gọi refresh; refresh xong mọi request đang chờ đều dùng token mới.
   */
  refreshOnce(): Observable<AuthTokensDto> {
    if (this.refreshInFlight !== null) {
      return this.refreshInFlight;
    }

    const refreshToken = this._refreshToken();
    if (refreshToken === null) {
      return throwError(() => toApiError(new Error('Không có refresh token để làm mới phiên.'), 401));
    }

    const body: RefreshTokenRequest = { refreshToken };

    const request$ = this.api
      .post<AuthTokensDto>('/auth/refresh', body, { skipAuth: true, silent: true })
      .pipe(
        tap((tokens) => this.applyTokens(tokens)),
        finalize(() => {
          this.refreshInFlight = null;
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    this.refreshInFlight = request$;

    return request$;
  }

  /** GET /api/me — nạp hồ sơ, vai trò, quyền và phạm vi chi nhánh. */
  loadAccount(): Observable<CurrentAccountDto> {
    return this.api.get<CurrentAccountDto>('/me').pipe(tap((account) => this._account.set(account)));
  }

  /**
   * Khôi phục phiên khi mở lại ứng dụng (chạy trong `provideAppInitializer`):
   * có refresh token ⇒ đổi lấy access token mới rồi nạp hồ sơ; lỗi bất kỳ ⇒ xoá phiên, tiếp tục ở chế độ khách.
   */
  restoreSession(): Promise<void> {
    if (this._refreshToken() === null) {
      this._ready.set(true);
      return Promise.resolve();
    }

    return new Promise<void>((resolve) => {
      this.refreshOnce()
        .pipe(
          switchMap(() => this.loadAccount()),
          catchError(() => {
            this.clearSession();
            return of(null);
          }),
          finalize(() => {
            this._ready.set(true);
            resolve();
          }),
        )
        .subscribe();
    });
  }

  /** Đăng xuất do người dùng bấm nút. */
  logout(): void {
    this.clearSession();
    this.notifications.info('Đã đăng xuất khỏi hệ thống.');
    void this.router.navigate(['/login']);
  }

  /** Phiên hết hạn (refresh thất bại): xoá state và về login kèm returnUrl để quay lại sau khi đăng nhập. */
  expireSession(): void {
    const returnUrl = this.router.url;
    this.clearSession();
    void this.router.navigate(['/login'], { queryParams: { returnUrl } });
  }

  // ───────────────────────────────────────────────────────────────────────────
  // Kiểm tra quyền — dùng cho roleGuard/permissionGuard, menu sidebar và nút thao tác
  // ───────────────────────────────────────────────────────────────────────────

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  /** Không truyền danh sách ⇒ coi như không ràng buộc (trả true). */
  hasAnyRole(roles: readonly string[] | undefined): boolean {
    if (roles === undefined || roles.length === 0) {
      return true;
    }
    return roles.some((role) => this.hasRole(role));
  }

  hasPermission(code: string): boolean {
    return this.permissions().includes(code);
  }

  hasAnyPermission(codes: readonly string[] | undefined): boolean {
    if (codes === undefined || codes.length === 0) {
      return true;
    }
    return codes.some((code) => this.hasPermission(code));
  }

  hasAllPermissions(codes: readonly string[] | undefined): boolean {
    if (codes === undefined || codes.length === 0) {
      return true;
    }
    return codes.every((code) => this.hasPermission(code));
  }

  /**
   * Quyết định DUY NHẤT cho việc vào route và hiện menu.
   * - 'roles'      : phải khớp một vai trò trong `roles`.
   * - 'permissions': phải có ĐỦ các mã trong `permissions`.
   * - 'roleOrPermission' (mặc định): khớp vai trò HOẶC đủ quyền — vì bảng Permissions có thể chưa
   *   được seed đủ (xem PermissionCodes.cs), nếu bắt buộc phải có mã quyền thì Admin sẽ bị chặn oan.
   */
  canAccess(query: AccessQuery): boolean {
    const roles = query.roles ?? [];
    const permissions = query.permissions ?? [];
    const mode: AccessMode = query.accessMode ?? 'roleOrPermission';

    if (roles.length === 0 && permissions.length === 0) {
      return true;
    }

    switch (mode) {
      case 'roles':
        return roles.length === 0 ? true : this.hasAnyRole(roles);
      case 'permissions':
        return permissions.length === 0 ? true : this.hasAllPermissions(permissions);
      default:
        return (
          (roles.length > 0 && this.hasAnyRole(roles)) ||
          (permissions.length > 0 && this.hasAllPermissions(permissions))
        );
    }
  }
}

/** Đọc refresh token đã lưu (bọc try/catch vì localStorage có thể bị chặn ở chế độ riêng tư). */
function readStoredRefreshToken(): string | null {
  try {
    const value = globalThis.localStorage?.getItem(REFRESH_TOKEN_STORAGE_KEY);
    return value !== undefined && value !== null && value.length > 0 ? value : null;
  } catch {
    return null;
  }
}

function storeRefreshToken(token: string): void {
  try {
    globalThis.localStorage?.setItem(REFRESH_TOKEN_STORAGE_KEY, token);
  } catch {
    // Không lưu được thì phiên chỉ sống trong tab hiện tại — không chặn luồng đăng nhập.
  }
}

function removeStoredRefreshToken(): void {
  try {
    globalThis.localStorage?.removeItem(REFRESH_TOKEN_STORAGE_KEY);
  } catch {
    // bỏ qua
  }
}
