import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Validator slug: chữ thường, số và dấu gạch nối đơn (ví dụ `cpu-amd-ryzen-7-7800x3d`).
 * Khớp ràng buộc cột Products.Slug varchar(300) UNIQUE của backend.
 */
export const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export const SLUG_MAX_LENGTH = 300;

export function slugValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = typeof control.value === 'string' ? control.value.trim() : '';

    if (value.length === 0) {
      return null;
    }

    if (value.length > SLUG_MAX_LENGTH) {
      return { slugTooLong: { maxLength: SLUG_MAX_LENGTH, actual: value.length } };
    }

    return SLUG_PATTERN.test(value) ? null : { slugInvalid: true };
  };
}
