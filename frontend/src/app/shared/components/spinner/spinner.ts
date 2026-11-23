import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Vòng xoay báo đang tải — dùng chung cho nút bấm và khối nội dung. */
@Component({
  selector: 'app-spinner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="inline-flex items-center gap-2 text-slate-500">
      <span
        class="animate-spin rounded-full border-2 border-slate-300 border-t-brand-600"
        [class]="dimensionClass()"
        role="progressbar"
        aria-label="Đang tải"
      ></span>
      @if (label()) {
        <span class="text-sm">{{ label() }}</span>
      }
    </span>
  `,
})
export class Spinner {
  /** Nhãn kèm theo (bỏ trống chỉ hiện vòng xoay). */
  readonly label = input('');

  readonly size = input<'sm' | 'md' | 'lg'>('md');

  protected readonly dimensionClass = computed(() => {
    switch (this.size()) {
      case 'sm':
        return 'h-4 w-4';
      case 'lg':
        return 'h-8 w-8';
      default:
        return 'h-5 w-5';
    }
  });
}
