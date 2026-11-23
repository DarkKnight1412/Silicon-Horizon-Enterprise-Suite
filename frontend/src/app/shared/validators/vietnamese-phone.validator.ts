import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Validator dùng chung: số điện thoại Việt Nam.
 * Chấp nhận dạng `0xxxxxxxxx` hoặc `+84xxxxxxxxx` với đầu số di động 3/5/7/8/9 (10 chữ số).
 * Bỏ trống ⇒ trả null để `Validators.required` báo lỗi bắt buộc (mỗi validator một nhiệm vụ).
 */
export const VIETNAMESE_PHONE_PATTERN = /^(?:\+84|0)(?:3|5|7|8|9)\d{8}$/;

/** Bỏ khoảng trắng, dấu chấm, gạch nối trước khi kiểm tra/gửi lên API. */
export function normalizeVietnamesePhone(value: string): string {
  return value.replace(/[\s.\-()]/g, '');
}

export function vietnamesePhoneValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = typeof control.value === 'string' ? normalizeVietnamesePhone(control.value.trim()) : '';

    if (raw.length === 0) {
      return null;
    }

    return VIETNAMESE_PHONE_PATTERN.test(raw) ? null : { phoneInvalid: true };
  };
}
