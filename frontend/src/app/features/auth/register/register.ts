import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { isApiError, type ApiError } from '../../../core/models/api-error.model';
import { CartStore } from '../../../core/services/cart.store';
import { NotificationService } from '../../../core/services/notification.service';
import {
  estimatePasswordStrength,
  passwordStrengthValidator,
  passwordsMatchValidator,
} from '../../../shared/validators/password-strength.validator';
import { vietnamesePhoneValidator } from '../../../shared/validators/vietnamese-phone.validator';

/**
 * Đăng ký tài khoản khách hàng (POST /api/auth/register).
 * Form dùng typed reactive forms + validator dùng chung:
 * - `vietnamesePhoneValidator`: số điện thoại VN (0/+84);
 * - `passwordStrengthValidator`: trả danh sách yêu cầu còn thiếu để hiển thị trực tiếp;
 * - `passwordsMatchValidator`: validator cấp group cho cặp mật khẩu / xác nhận.
 * Đăng ký xong tự động đăng nhập để giữ trải nghiệm liền mạch.
 */
@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.html',
})
export class Register {
  private readonly authStore = inject(AuthStore);
  private readonly cartStore = inject(CartStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);
  /** Giá trị mật khẩu để tính thanh đo độ mạnh (chỉ dùng cho hiển thị). */
  private readonly passwordValue = signal('');

  protected readonly form = new FormGroup(
    {
      username: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.minLength(4), Validators.maxLength(50)],
      }),
      fullName: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(150)],
      }),
      phone: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, vietnamesePhoneValidator()],
      }),
      email: new FormControl('', { nonNullable: true, validators: [Validators.email] }),
      password: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, passwordStrengthValidator()],
      }),
      confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    },
    { validators: [passwordsMatchValidator()] },
  );

  protected readonly strength = computed(() => estimatePasswordStrength(this.passwordValue()));

  constructor() {
    // Cập nhật thanh đo độ mạnh mỗi khi mật khẩu đổi (signal cục bộ của màn hình).
    this.form.controls.password.valueChanges.subscribe((value) => this.passwordValue.set(value));
  }

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
    const email = value.email.trim();

    this.authStore
      .register({
        username: value.username.trim(),
        password: value.password,
        phone: value.phone.trim(),
        fullName: value.fullName.trim(),
        email: email.length === 0 ? null : email,
      })
      .subscribe({
        next: () => {
          this.notifications.success('Đăng ký thành công', 'Hệ thống đang đăng nhập giúp bạn…');
          this.loginAfterRegister(value.username.trim(), value.password);
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          this.apiError.set(isApiError(error) ? error : null);
        },
      });
  }

  /** Bộ yêu cầu mật khẩu còn thiếu (validator trả về trong errors.passwordStrength.missing). */
  protected missingPasswordRules(): string[] {
    const errors = this.form.controls.password.errors;
    const strength: unknown = errors === null ? null : errors['passwordStrength'];

    if (strength === null || typeof strength !== 'object') {
      return [];
    }

    const missing: unknown = (strength as { missing?: unknown }).missing;

    return Array.isArray(missing) ? missing.map((item) => String(item)) : [];
  }

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

  private loginAfterRegister(username: string, password: string): void {
    this.authStore.login({ username, password }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.cartStore.merge().subscribe({
          next: () => void this.router.navigate(['/dashboard']),
          error: () => void this.router.navigate(['/dashboard']),
        });
      },
      error: () => {
        // Đăng ký đã thành công nhưng tự đăng nhập lỗi ⇒ yêu cầu đăng nhập thủ công.
        this.submitting.set(false);
        this.notifications.info('Đăng ký thành công', 'Vui lòng đăng nhập bằng tài khoản vừa tạo.');
        void this.router.navigate(['/login']);
      },
    });
  }
}
