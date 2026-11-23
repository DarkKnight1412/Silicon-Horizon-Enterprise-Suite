import { Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import type { ApiError } from '../models/api-error.model';

export type ToastType = 'success' | 'error' | 'warning' | 'info';

/** Một thông báo (toast) đang hiển thị. */
export interface Toast {
  id: number;
  type: ToastType;
  title: string;
  message: string | null;
  /** Mã tương quan trả về từ backend — hiển thị để người dùng báo lại khi cần tra log. */
  correlationId: string | null;
}

/**
 * Toast dùng chung (state toàn cục): `app-toast-host` đọc danh sách này để render.
 * Không dùng thư viện ngoài — chỉ Tailwind + signal.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly items = signal<readonly Toast[]>([]);
  private sequence = 0;

  /** Danh sách toast hiện tại (chỉ đọc). */
  readonly toasts = this.items.asReadonly();

  success(title: string, message: string | null = null): number {
    return this.show('success', title, message);
  }

  error(title: string, message: string | null = null, correlationId: string | null = null): number {
    return this.show('error', title, message, correlationId);
  }

  warning(title: string, message: string | null = null): number {
    return this.show('warning', title, message);
  }

  info(title: string, message: string | null = null): number {
    return this.show('info', title, message);
  }

  /** Hiện toast lỗi từ `ApiError` — kèm `detail` (mã lỗi) và `correlationId` để tra log. */
  fromApiError(error: ApiError): void {
    const parts: string[] = [];

    if (error.detail !== null) {
      parts.push(`Mã lỗi: ${error.detail}`);
    }

    if (error.fieldErrors !== null) {
      const firstMessages = Object.values(error.fieldErrors)[0];
      const first = firstMessages === undefined ? undefined : firstMessages[0];
      if (first !== undefined) {
        parts.push(first);
      }
    }

    if (error.status >= 500 || error.isNetworkError) {
      parts.push('Vui lòng thử lại hoặc liên hệ quản trị viên.');
    }

    this.error(error.message, parts.length === 0 ? null : parts.join(' · '), error.correlationId);
  }

  show(type: ToastType, title: string, message: string | null = null, correlationId: string | null = null): number {
    const id = ++this.sequence;

    this.items.update((list) => [...list, { id, type, title, message, correlationId }]);

    globalThis.setTimeout(() => this.dismiss(id), environment.toastTimeoutMs);

    return id;
  }

  dismiss(id: number): void {
    this.items.update((list) => list.filter((toast) => toast.id !== id));
  }

  clear(): void {
    this.items.set([]);
  }
}
