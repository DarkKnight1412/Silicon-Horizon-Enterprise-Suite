import { Injectable, computed, signal } from '@angular/core';
import { Observable, defer, finalize } from 'rxjs';

/**
 * Cờ BẬN TOÀN CỤC (state toàn cục theo quy ước ở `core/models/common.model.ts`).
 * Dùng cho overlay spinner khi có request dài; các màn hình nhỏ vẫn nên tự quản lý `isLoading` cục bộ
 * để spinner không nhấp nháy toàn trang.
 */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly counter = signal(0);

  /** true khi còn ít nhất một tác vụ đang chạy. */
  readonly isBusy = computed(() => this.counter() > 0);

  /** Số tác vụ đang chạy (hiển thị khi cần debug). */
  readonly pendingCount = computed(() => this.counter());

  start(): void {
    this.counter.update((value) => value + 1);
  }

  stop(): void {
    this.counter.update((value) => (value > 0 ? value - 1 : 0));
  }

  reset(): void {
    this.counter.set(0);
  }

  /** Bọc một Observable: bật cờ khi subscribe, tắt khi hoàn tất / lỗi / bị huỷ. */
  track<T>(source: Observable<T>): Observable<T> {
    return defer(() => {
      this.start();

      return source.pipe(finalize(() => this.stop()));
    });
  }
}
