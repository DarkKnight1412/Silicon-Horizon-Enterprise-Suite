import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Tuỳ chọn kiểm tra giá/tiền VND (backend dùng `decimal(18,0)`, đơn vị đồng). */
export interface PriceValidatorOptions {
  min?: number;
  /** Giá tối đa cho phép (chặn gõ nhầm thêm số 0). */
  max?: number;
  /** Cho phép 0 (ví dụ phí vận chuyển miễn phí). */
  allowZero?: boolean;
  /** Bắt buộc là số nguyên (tiền VND không có phần thập phân). */
  integer?: boolean;
}

/**
 * Validator cho ô nhập tiền VND:
 * - bỏ trống ⇒ null (để `Validators.required` xử lý);
 * - không phải số ⇒ lỗi `priceNotNumber`;
 * - ngoài khoảng ⇒ lỗi `priceRange`;
 * - có phần thập phân khi `integer = true` ⇒ lỗi `priceNotInteger`.
 */
export function priceValidator(options: PriceValidatorOptions = {}): ValidatorFn {
  const { min, max, allowZero = true, integer = true } = options;

  return (control: AbstractControl): ValidationErrors | null => {
    const raw: unknown = control.value;

    if (raw === null || raw === undefined || raw === '') {
      return null;
    }

    const value = typeof raw === 'number' ? raw : Number(String(raw).replace(/[\s.,]/g, ''));

    if (!Number.isFinite(value)) {
      return { priceNotNumber: true };
    }

    if (!allowZero && value === 0) {
      return { priceRange: { min: min ?? 1, max: max ?? null, actual: value } };
    }

    if (integer && !Number.isInteger(value)) {
      return { priceNotInteger: true };
    }

    if (min !== undefined && value < min) {
      return { priceRange: { min, max: max ?? null, actual: value } };
    }

    if (max !== undefined && value > max) {
      return { priceRange: { min: min ?? null, max, actual: value } };
    }

    return null;
  };
}

/** Đọc số tiền từ chuỗi người dùng gõ ('1.250.000' → 1250000); trả null nếu không hợp lệ. */
export function parsePriceInput(value: string): number | null {
  const normalized = value.replace(/[\s.,]/g, '');

  if (normalized.length === 0) {
    return null;
  }

  const parsed = Number(normalized);

  return Number.isFinite(parsed) ? parsed : null;
}
