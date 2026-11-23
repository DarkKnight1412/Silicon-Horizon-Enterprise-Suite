import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { ApiService } from '../http/api.service';
import type { BranchReferenceDto } from '../models/branch.model';

/**
 * STATE TOÀN CỤC của danh sách chi nhánh — `GET /api/v1/branches` (API CÔNG KHAI, chỉ chi nhánh
 * đang hoạt động).
 *
 * Vì sao là STORE (không phải service gọi lẻ): danh sách chi nhánh rất ít thay đổi nhưng được dùng ở
 * nhiều màn hình (đặt hàng, POS, chuyển kho…). Store nạp MỘT LẦN rồi cache trong signal ⇒ các màn hình
 * sau lấy ngay, không gọi lại API. `load()` là single-flight: nhiều nơi gọi cùng lúc chỉ tạo một request.
 *
 * Khi API lỗi, store trả danh sách RỖNG và KHÔNG ném lỗi (interceptor đã hiện toast) để màn hình không
 * bị chặn — nơi gọi vẫn có thể dùng nguồn dự phòng của mình.
 */
@Injectable({ providedIn: 'root' })
export class BranchStore {
  private readonly api = inject(ApiService);

  private readonly _branches = signal<readonly BranchReferenceDto[]>([]);
  private readonly _loading = signal(false);
  private readonly _loaded = signal(false);

  /** Lời gọi đang bay — gộp nhiều màn hình yêu cầu cùng lúc thành một request. */
  private inFlight: Observable<readonly BranchReferenceDto[]> | null = null;

  readonly branches = this._branches.asReadonly();
  readonly loading = this._loading.asReadonly();

  /** true sau lần nạp thành công đầu tiên (dùng để biết có cần gọi API hay lấy cache). */
  readonly loaded = this._loaded.asReadonly();

  /** Tên chi nhánh theo id — tiện hiển thị ở màn hình chỉ có `branchId`. */
  readonly nameById = computed<ReadonlyMap<number, string>>(
    () => new Map(this._branches().map((branch) => [branch.id, branch.name])),
  );

  /** Nạp danh sách chi nhánh; đã có cache thì trả luôn (trừ khi `force = true`). */
  load(force = false): Observable<readonly BranchReferenceDto[]> {
    if (!force && this._loaded()) {
      return of(this._branches());
    }

    if (this.inFlight !== null) {
      return this.inFlight;
    }

    this._loading.set(true);

    this.inFlight = this.api.get<BranchReferenceDto[]>('/v1/branches').pipe(
      map((items): readonly BranchReferenceDto[] => items),
      tap((items) => {
        this._branches.set(items);
        this._loaded.set(true);
      }),
      catchError(() => {
        this._branches.set([]);
        return of<readonly BranchReferenceDto[]>([]);
      }),
      finalize(() => {
        this._loading.set(false);
        this.inFlight = null;
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );

    return this.inFlight;
  }

  /** Xoá cache (dùng khi cần nạp lại chắc chắn ở lần gọi kế tiếp). */
  reset(): void {
    this._branches.set([]);
    this._loaded.set(false);
  }
}
