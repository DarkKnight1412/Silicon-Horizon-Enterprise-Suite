import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  ViewChild,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { AuthStore } from '../../../core/auth/auth.store';
import { isApiError, type ApiError } from '../../../core/models/api-error.model';
import { CartStore } from '../../../core/services/cart.store';
import { NotificationService } from '../../../core/services/notification.service';

/** Khai báo TỐI THIỂU cho Google Identity Services — tránh phải thêm package `@types/google.accounts`. */
interface GoogleIdentityServices {
  accounts: {
    id: {
      initialize(options: {
        client_id: string;
        callback: (response: { credential: string }) => void;
      }): void;

      renderButton(parent: HTMLElement, options: Record<string, unknown>): void;
    };
  };
}

/** Script GIS — chỉ nạp MỘT lần cho cả phiên (các lần mở lại trang đăng nhập dùng lại). */
const GOOGLE_GIS_SCRIPT_URL = 'https://accounts.google.com/gsi/client';

let googleIdentityScript$: Promise<void> | null = null;

/**
 * Đăng nhập bằng MẬT KHẨU (POST /api/auth/login) và bằng GOOGLE (POST /api/auth/google).
 *
 * `returnUrl` được bind tự động từ query param nhờ `withComponentInputBinding()` trong app.config,
 * nên sau khi đăng nhập người dùng quay lại đúng màn hình đã bị chặn bởi authGuard.
 *
 * Token được lưu bởi `AuthStore`: access token trong bộ nhớ, refresh token ở localStorage.
 * Sau khi đăng nhập còn gộp giỏ hàng vãng lai vào giỏ tài khoản (POST /api/v1/cart/merge).
 *
 * Google: nút do Google Identity Services render; callback trả ID token ⇒ gửi lên API để xác minh và
 * đổi thành JWT của hệ thống (email chưa có tài khoản sẽ được tạo tài khoản KHÁCH HÀNG ở backend).
 */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.html',
})
export class Login implements AfterViewInit {
  private readonly authStore = inject(AuthStore);
  private readonly cartStore = inject(CartStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  /** Đường dẫn cần quay lại sau khi đăng nhập (?returnUrl=...). */
  readonly returnUrl = input('');

  protected readonly submitting = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  /** Client ID Google (rỗng ⇒ ẩn nút Google — môi trường chưa cấu hình). */
  protected readonly googleClientId = environment.googleClientId ?? '';

  /** true sau khi Google Identity Services đã render nút (để hiện/ẩn dòng "đang tải"). */
  protected readonly googleReady = signal(false);

  /** Phần tử để Google render nút vào (xem `#googleButton` trong template). */
  @ViewChild('googleButton') private googleButton?: ElementRef<HTMLElement>;

  /** Typed reactive form — chỉ hai trường nên không cần FormBuilder. */
  protected readonly form = new FormGroup({
    username: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(6)],
    }),
  });

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.apiError.set(null);

    const value = this.form.getRawValue();

    this.authStore.login({ username: value.username.trim(), password: value.password }).subscribe({
      next: (account) => {
        this.submitting.set(false);
        this.notifications.success('Đăng nhập thành công', `Xin chào ${account.username}!`);

        // Gộp giỏ vãng lai (nếu có) rồi mới điều hướng; lỗi gộp giỏ không chặn đăng nhập.
        this.cartStore.merge().subscribe({
          next: () => this.redirect(),
          error: () => this.redirect(),
        });
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.apiError.set(isApiError(error) ? error : null);
      },
    });
  }

  /** Sau khi view sẵn sàng: nạp Google Identity Services và render nút (nếu đã cấu hình client id). */
  ngAfterViewInit(): void {
    if (this.googleClientId.length === 0) {
      return;
    }

    void this.renderGoogleButton();
  }

  /**
   * Render nút "Đăng nhập bằng Google" vào `#googleButton`.
   * Nạp script thất bại (mất mạng) ⇒ chỉ cảnh báo nhẹ; người dùng vẫn đăng nhập bằng mật khẩu bình thường.
   */
  private async renderGoogleButton(): Promise<void> {
    await this.loadGoogleIdentityScript();

    const gsi = (globalThis as { google?: GoogleIdentityServices }).google;
    const container = this.googleButton?.nativeElement;

    if (gsi === undefined || container === undefined) {
      this.notifications.warning(
        'Không tải được Google Sign-In',
        'Kiểm tra kết nối mạng rồi tải lại trang, hoặc đăng nhập bằng mật khẩu.',
      );
      return;
    }

    gsi.accounts.id.initialize({
      client_id: this.googleClientId,
      callback: (response) => this.signInWithGoogle(response.credential),
    });

    gsi.accounts.id.renderButton(container, {
      type: 'standard',
      theme: 'outline',
      size: 'large',
      text: 'signin_with',
      shape: 'rectangular',
      logo_alignment: 'left',
      width: 320,
    });

    this.googleReady.set(true);
  }

  /** Nạp script GIS (chỉ MỘT lần cho cả phiên) — resolve khi script sẵn sàng hoặc khi tải lỗi. */
  private loadGoogleIdentityScript(): Promise<void> {
    googleIdentityScript$ ??= new Promise<void>((resolve) => {
      const existing = document.querySelector(`script[src="${GOOGLE_GIS_SCRIPT_URL}"]`);

      if (existing !== null) {
        resolve();
        return;
      }

      const script = document.createElement('script');
      script.src = GOOGLE_GIS_SCRIPT_URL;
      script.async = true;
      script.onload = () => resolve();
      script.onerror = () => resolve();

      document.head.appendChild(script);
    });

    return googleIdentityScript$;
  }

  /**
   * Nhận ID token từ Google ⇒ đổi lấy JWT của hệ thống, rồi xử lý GIỐNG đăng nhập mật khẩu
   * (gộp giỏ vãng lai ⇒ điều hướng theo `returnUrl` hoặc về khu vực đúng vai trò).
   */
  private signInWithGoogle(credential: string): void {
    if (this.submitting()) {
      return;
    }

    this.submitting.set(true);
    this.apiError.set(null);

    this.authStore.loginWithGoogle(credential).subscribe({
      next: (account) => {
        this.submitting.set(false);
        this.notifications.success('Đăng nhập Google thành công', `Xin chào ${account.username}!`);

        this.cartStore.merge().subscribe({
          next: () => this.redirect(),
          error: () => this.redirect(),
        });
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.apiError.set(isApiError(error) ? error : null);
      },
    });
  }

  /** Lấy thông điệp lỗi 400 của backend cho một trường (nếu backend trả ValidationProblemDetails). */
  protected fieldError(field: string): string | null {
    const errors = this.apiError()?.fieldErrors ?? null;
    if (errors === null) {
      return null;
    }

    for (const [key, messages] of Object.entries(errors)) {
      if (key.toLowerCase() === field.toLowerCase()) {
        return messages[0] ?? null;
      }
    }

    return null;
  }

  private redirect(): void {
    // Không có returnUrl ⇒ về ĐÚNG KHU VỰC theo vai trò (khách hàng ⇒ /shop, nhân viên ⇒ /dashboard).
    const target = this.returnUrl().length > 0 ? this.returnUrl() : this.authStore.homePath();
    void this.router.navigateByUrl(target);
  }
}
