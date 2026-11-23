import { ChangeDetectionStrategy, Component, input, model, output } from '@angular/core';

/**
 * Hộp thoại xác nhận dạng trình bày (presentational): nhận `open` qua `model()` để có thể hai chiều,
 * phát `confirmed`/`cancelled` cho nơi sử dụng. Dùng trực tiếp trong màn hình hoặc qua
 * `ConfirmService` (xem `confirm-dialog-host.ts`) cho luồng `await confirm.ask(...)`.
 */
@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-40 flex items-center justify-center bg-slate-900/40 p-4">
        <div class="sh-card w-full max-w-md p-5" role="dialog" aria-modal="true">
          <h2 class="sh-page-title">{{ title() }}</h2>
          <p class="mt-2 whitespace-pre-line text-sm text-slate-600">{{ message() }}</p>

          <div class="mt-5 flex justify-end gap-2">
            <button
              type="button"
              class="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
              (click)="reject()"
            >
              {{ cancelLabel() }}
            </button>
            <button
              type="button"
              class="rounded-md px-3 py-1.5 text-sm font-medium text-white"
              [class]="danger() ? 'bg-rose-600 hover:bg-rose-700' : 'bg-brand-600 hover:bg-brand-700'"
              (click)="accept()"
            >
              {{ confirmLabel() }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
})
export class ConfirmDialog {
  readonly open = model(false);
  readonly title = input('Xác nhận thao tác');
  readonly message = input('');
  readonly confirmLabel = input('Đồng ý');
  readonly cancelLabel = input('Huỷ');
  /** true = hành động nguy hiểm (nút đỏ): hủy đơn, xoá dòng, rời form... */
  readonly danger = input(false);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  protected accept(): void {
    this.open.set(false);
    this.confirmed.emit();
  }

  protected reject(): void {
    this.open.set(false);
    this.cancelled.emit();
  }
}
