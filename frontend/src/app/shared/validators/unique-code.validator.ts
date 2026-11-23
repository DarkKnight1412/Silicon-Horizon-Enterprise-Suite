import type { AbstractControl, AsyncValidatorFn, ValidationErrors } from '@angular/forms';
import { Observable, of, timer } from 'rxjs';
import { catchError, first, map, switchMap } from 'rxjs/operators';

/** Tuỳ chọn cho validator bất đồng bộ kiểm tra trùng mã. */
export interface UniqueCodeOptions {
  /** Chờ bao lâu sau lần gõ cuối mới gọi API (ms) — tránh spam request. */
  debounceMs?: number;
  /** false = chỉ kiểm tra khi người dùng đã chạm vào ô (tránh gọi API khi form vừa mở). */
  checkPristine?: boolean;
}

/** true = mã còn dùng được (chưa tồn tại). */
export type CodeAvailabilityCheck = (value: string) => Observable<boolean>;

/**
 * FACTORY tạo validator bất đồng bộ kiểm tra trùng mã (SKU, mã danh mục, tên thương hiệu...).
 *
 * Cách dùng trong form sản phẩm — chỉ kiểm tra khi TẠO MỚI vì backend kiểm tra trùng lần cuối
 * (proc báo 50005 → 409) và trả lỗi validate 400 về đúng field:
 * ```ts
 * sku: ['', [Validators.required], [uniqueCodeValidator((value) => this.productApi.isSkuAvailable(value))]]
 * ```
 * Lỗi mạng ⇒ trả null để không chặn người dùng; backend vẫn là nơi quyết định cuối cùng.
 */
export function uniqueCodeValidator(
  check: CodeAvailabilityCheck,
  options: UniqueCodeOptions = {},
): AsyncValidatorFn {
  const { debounceMs = 350, checkPristine = false } = options;

  return (control: AbstractControl): Observable<ValidationErrors | null> => {
    const value = typeof control.value === 'string' ? control.value.trim() : '';

    if (value.length === 0 || (checkPristine && control.pristine)) {
      return of(null);
    }

    return timer(debounceMs).pipe(
      switchMap(() => check(value)),
      map((available): ValidationErrors | null => (available ? null : { codeTaken: true })),
      catchError(() => of(null)),
      first(),
    );
  };
}
