import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationService, type ToastType } from '../../../core/services/notification.service';

/**
 * Nơi hiển thị toast (đặt một lần trong App). Đọc state toàn cục từ `NotificationService`
 * nên bất kỳ service/component nào cũng có thể phát thông báo mà không cần truyền sự kiện qua nhiều tầng.
 */
@Component({
  selector: 'app-toast-host',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pointer-events-none fixed right-4 top-4 z-50 flex w-full max-w-sm flex-col gap-2">
      @for (toast of toasts(); track toast.id) {
        <div
          class="pointer-events-auto flex items-start gap-3 rounded-lg border p-3 shadow-lg"
          [class]="containerClass(toast.type)"
          role="status"
        >
          <span aria-hidden="true" class="text-lg leading-none">{{ icon(toast.type) }}</span>
          <div class="min-w-0 flex-1">
            <p class="text-sm font-semibold">{{ toast.title }}</p>
            @if (toast.message) {
              <p class="mt-0.5 text-xs opacity-90">{{ toast.message }}</p>
            }
            @if (toast.correlationId) {
              <p class="mt-1 break-all font-mono text-[11px] opacity-70">
                correlationId: {{ toast.correlationId }}
              </p>
            }
          </div>
          <button
            type="button"
            class="rounded p-1 text-xs opacity-70 hover:opacity-100"
            aria-label="Đóng thông báo"
            (click)="dismiss(toast.id)"
          >
            ✕
          </button>
        </div>
      }
    </div>
  `,
})
export class ToastHost {
  private readonly notifications = inject(NotificationService);

  protected readonly toasts = this.notifications.toasts;

  protected dismiss(id: number): void {
    this.notifications.dismiss(id);
  }

  protected icon(type: ToastType): string {
    switch (type) {
      case 'success':
        return '✅';
      case 'error':
        return '⛔';
      case 'warning':
        return '⚠️';
      default:
        return 'ℹ️';
    }
  }

  protected containerClass(type: ToastType): string {
    switch (type) {
      case 'success':
        return 'border-emerald-200 bg-emerald-50 text-emerald-900';
      case 'error':
        return 'border-rose-200 bg-rose-50 text-rose-900';
      case 'warning':
        return 'border-amber-200 bg-amber-50 text-amber-900';
      default:
        return 'border-sky-200 bg-sky-50 text-sky-900';
    }
  }
}
