import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Tuỳ chọn kiểm tra khoảng ngày. */
export interface DateRangeOptions {
  /** Bắt buộc có cả hai mốc (mặc định false: cho phép lọc chỉ theo một đầu). */
  requireBoth?: boolean;
  /** Cho phép fromDate === toDate (mặc định true vì API coi toDate là chặn trên không bao gồm). */
  allowSameDay?: boolean;
}

/**
 * Validator cấp GROUP cho cặp FromDate/ToDate (bộ lọc danh sách đơn, báo cáo).
 * `toDate` của backend là CHẶN TRÊN không bao gồm, nên mặc định vẫn cho phép hai mốc bằng nhau.
 */
export function dateRangeValidator(
  startKey = 'fromDate',
  endKey = 'toDate',
  options: DateRangeOptions = {},
): ValidatorFn {
  const { requireBoth = false, allowSameDay = true } = options;

  return (group: AbstractControl): ValidationErrors | null => {
    const startControl = group.get(startKey);
    const endControl = group.get(endKey);

    if (startControl === null || endControl === null) {
      return null;
    }

    const start = toTime(startControl.value);
    const end = toTime(endControl.value);

    if (requireBoth && (start === null || end === null)) {
      return { dateRange: { message: 'Vui lòng chọn đủ ngày bắt đầu và ngày kết thúc.' } };
    }

    if (start === null || end === null) {
      return null;
    }

    const invalid = allowSameDay ? end < start : end <= start;

    return invalid
      ? {
          dateRange: {
            message: allowSameDay
              ? 'Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.'
              : 'Ngày kết thúc phải sau ngày bắt đầu.',
          },
        }
      : null;
  };
}

/** Đổi giá trị của input date/number/Date về mốc thời gian; trả null khi rỗng hoặc không hợp lệ. */
function toTime(value: unknown): number | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }

  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? null : value.getTime();
  }

  const date = new Date(String(value));

  return Number.isNaN(date.getTime()) ? null : date.getTime();
}
