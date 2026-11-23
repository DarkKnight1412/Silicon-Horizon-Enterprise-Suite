import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Trạng thái rỗng của danh sách/bảng: icon + tiêu đề + mô tả + nút hành động tuỳ chọn. */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center justify-center gap-2 px-6 py-10 text-center">
      <span aria-hidden="true" class="text-3xl">{{ icon() }}</span>
      <p class="text-sm font-semibold text-slate-700">{{ title() }}</p>
      @if (description()) {
        <p class="max-w-md text-sm text-slate-500">{{ description() }}</p>
      }
      @if (actionLabel()) {
        <button
          type="button"
          class="mt-2 rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700"
          (click)="action.emit()"
        >
          {{ actionLabel() }}
        </button>
      }
    </div>
  `,
})
export class EmptyState {
  readonly icon = input('📭');
  readonly title = input('Chưa có dữ liệu');
  readonly description = input('');
  readonly actionLabel = input('');

  readonly action = output<void>();
}
