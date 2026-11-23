import { Injectable, computed, signal } from '@angular/core';

/** Nội dung hộp thoại xác nhận. */
export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** true = hành động nguy hiểm (nút đỏ), ví dụ hủy đơn. */
  danger?: boolean;
}

interface ConfirmState extends ConfirmOptions {
  open: boolean;
}

/**
 * Hộp thoại xác nhận dạng "gọi và chờ" (`await confirm.ask(...)`) — dùng cho luồng bắt buộc phải
 * dừng lại chờ người dùng quyết định, ví dụ `canDeactivate` chống mất dữ liệu form sản phẩm.
 * Component hiển thị: `shared/components/confirm-dialog/confirm-dialog-host.ts`.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly state = signal<ConfirmState | null>(null);
  private resolver: ((confirmed: boolean) => void) | null = null;

  /** Trạng thái hiện tại cho host component đọc. */
  readonly current = this.state.asReadonly();

  readonly isOpen = computed(() => this.state()?.open === true);

  /** Mở hộp thoại và trả về Promise<boolean> = người dùng có xác nhận hay không. */
  ask(options: ConfirmOptions): Promise<boolean> {
    // Nếu còn hộp thoại cũ chưa xử lý thì coi như người dùng huỷ để không treo luồng trước.
    this.resolver?.(false);

    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
      this.state.set({ ...options, open: true });
    });
  }

  /** Người dùng bấm nút xác nhận. */
  accept(): void {
    this.close(true);
  }

  /** Người dùng bấm huỷ / đóng. */
  dismiss(): void {
    this.close(false);
  }

  private close(confirmed: boolean): void {
    const resolver = this.resolver;
    this.resolver = null;
    this.state.set(null);
    resolver?.(confirmed);
  }
}
