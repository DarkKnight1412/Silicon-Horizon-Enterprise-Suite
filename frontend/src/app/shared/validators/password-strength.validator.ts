import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Cấu hình độ mạnh mật khẩu (mặc định khớp quy ước tối thiểu của dự án). */
export interface PasswordStrengthOptions {
  minLength: number;
  requireUpper: boolean;
  requireLower: boolean;
  requireDigit: boolean;
  requireSpecial: boolean;
}

export const DEFAULT_PASSWORD_STRENGTH: PasswordStrengthOptions = {
  minLength: 8,
  requireUpper: true,
  requireLower: true,
  requireDigit: true,
  requireSpecial: false,
};

/**
 * Validator độ mạnh mật khẩu — trả lỗi kèm danh sách yêu cầu còn thiếu để template hiển thị trực tiếp:
 * `errors?.['passwordStrength'].missing`.
 */
export function passwordStrengthValidator(
  options: Partial<PasswordStrengthOptions> = {},
): ValidatorFn {
  const config: PasswordStrengthOptions = { ...DEFAULT_PASSWORD_STRENGTH, ...options };

  return (control: AbstractControl): ValidationErrors | null => {
    const value = typeof control.value === 'string' ? control.value : '';

    if (value.length === 0) {
      return null;
    }

    const missing = collectMissing(value, config);

    return missing.length === 0 ? null : { passwordStrength: { missing } };
  };
}

/** Danh sách yêu cầu chưa đạt của một mật khẩu (dùng cho cả validator và thanh đo độ mạnh). */
export function collectMissing(value: string, config: PasswordStrengthOptions = DEFAULT_PASSWORD_STRENGTH): string[] {
  const missing: string[] = [];

  if (value.length < config.minLength) {
    missing.push(`ít nhất ${config.minLength} ký tự`);
  }
  if (config.requireUpper && !/[A-Z]/.test(value)) {
    missing.push('một chữ hoa');
  }
  if (config.requireLower && !/[a-z]/.test(value)) {
    missing.push('một chữ thường');
  }
  if (config.requireDigit && !/\d/.test(value)) {
    missing.push('một chữ số');
  }
  if (config.requireSpecial && !/[^A-Za-z0-9]/.test(value)) {
    missing.push('một ký tự đặc biệt');
  }

  return missing;
}

/** Điểm độ mạnh 0..4 để vẽ thanh đo (chỉ là gợi ý trực quan, không chặn submit). */
export interface PasswordScore {
  score: 0 | 1 | 2 | 3 | 4;
  label: string;
  /** Lớp Tailwind cho thanh đo. */
  barClass: string;
}

export function estimatePasswordStrength(value: string): PasswordScore {
  if (value.length === 0) {
    return { score: 0, label: 'Chưa nhập', barClass: 'bg-slate-300' };
  }

  let points = 0;
  if (value.length >= 8) points += 1;
  if (value.length >= 12) points += 1;
  if (/[A-Z]/.test(value) && /[a-z]/.test(value)) points += 1;
  if (/\d/.test(value)) points += 1;
  if (/[^A-Za-z0-9]/.test(value)) points += 1;

  if (points <= 1) {
    return { score: 1, label: 'Rất yếu', barClass: 'bg-rose-500' };
  }
  if (points === 2) {
    return { score: 2, label: 'Yếu', barClass: 'bg-amber-500' };
  }
  if (points === 3) {
    return { score: 3, label: 'Trung bình', barClass: 'bg-sky-500' };
  }
  return { score: 4, label: 'Mạnh', barClass: 'bg-emerald-500' };
}

/** Validator cấp GROUP: giá trị hai control phải trùng nhau (mật khẩu / xác nhận mật khẩu). */
export function passwordsMatchValidator(passwordKey = 'password', confirmKey = 'confirmPassword'): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const password = group.get(passwordKey);
    const confirm = group.get(confirmKey);

    if (password === null || confirm === null) {
      return null;
    }

    if (password.value !== confirm.value) {
      confirm.setErrors({ ...(confirm.errors ?? {}), passwordMismatch: true });
      return { passwordMismatch: true };
    }

    confirm.setErrors(withoutErrorKey(confirm.errors, 'passwordMismatch'));
    return null;
  };
}

/** Bỏ một khoá lỗi khỏi ValidationErrors (trả null nếu không còn lỗi nào). */
function withoutErrorKey(errors: ValidationErrors | null, key: string): ValidationErrors | null {
  if (errors === null) {
    return null;
  }

  const copy: ValidationErrors = {};
  for (const [name, value] of Object.entries(errors)) {
    if (name !== key) {
      copy[name] = value;
    }
  }

  return Object.keys(copy).length > 0 ? copy : null;
}
