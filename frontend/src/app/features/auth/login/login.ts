import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { isApiError, type ApiError } from '../../../core/models/api-error.model';
import { CartStore } from '../../../core/services/cart.store';
import { NotificationService } from '../../../core/services/notification.service';

/**
 * Đăng nhập (POST /api/auth/login).
 * `returnUrl` được bind tự động từ query param nhờ `withComponentInputBinding()` trong app.config,
 * nên sau khi đăng nhập người dùng quay lại đúng màn hình đã bị chặn bởi authGuard.
 *
 * Token được lưu bởi `AuthStore`: access token trong bộ nhớ, refresh token ở localStorage.
 * Sau khi đăng nhập còn gộp giỏ hàng vãng lai vào giỏ tài khoản (POST /api/v1/cart/merge).
 */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.html',
})
export class Login {
  private readonly authStore = inject(AuthStore);
  private readonly cartStore = inject(CartStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  /** Đường dẫn cần quay lại sau khi đăng nhập (?returnUrl=...). */
  readonly returnUrl = input('');

  protected readonly submitting = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

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
    const target = this.returnUrl().length > 0 ? this.returnUrl() : '/dashboard';
    void this.router.navigateByUrl(target);
  }
}
