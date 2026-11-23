import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { PAGE_SIZE_OPTIONS } from '../../../core/models/paged-result.model';

/**
 * Phân trang dùng chung: hiển thị khoảng dòng đang xem, chọn kích thước trang và dãy số trang.
 * Component chỉ phát sự kiện — màn hình tự quyết định gọi API (giữ quyền kiểm soát luồng dữ liệu).
 */
@Component({
  selector: 'app-pagination',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-3 py-2 text-sm">
      <p class="text-slate-500">
        {{ rangeText() }}
      </p>

      <div class="flex flex-wrap items-center gap-2">
        <label class="flex items-center gap-1 text-slate-600">
          <span class="hidden sm:inline">Số dòng/trang</span>
          <select
            class="rounded-md border border-slate-300 px-2 py-1"
            [value]="pageSize()"
            (change)="onPageSizeChange($event)"
          >
            @for (size of sizeOptions(); track size) {
              <option [value]="size">{{ size }}</option>
            }
          </select>
        </label>

        <button
          type="button"
          class="rounded-md border border-slate-300 px-2.5 py-1 disabled:opacity-40"
          [disabled]="pageNumber() <= 1"
          (click)="go(pageNumber() - 1)"
        >
          ‹ Trước
        </button>

        @for (page of pages(); track page) {
          <button
            type="button"
            class="rounded-md border px-2.5 py-1"
            [class]="page === pageNumber() ? 'border-brand-600 bg-brand-600 text-white' : 'border-slate-300 hover:bg-slate-50'"
            (click)="go(page)"
          >
            {{ page }}
          </button>
        }

        <button
          type="button"
          class="rounded-md border border-slate-300 px-2.5 py-1 disabled:opacity-40"
          [disabled]="pageNumber() >= resolvedTotalPages()"
          (click)="go(pageNumber() + 1)"
        >
          Sau ›
        </button>
      </div>
    </div>
  `,
})
export class Pagination {
  readonly pageNumber = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  /** Nếu không truyền, tự suy ra từ totalCount / pageSize. */
  readonly totalPages = input<number | null>(null);

  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  protected readonly sizeOptions = computed<readonly number[]>(() => PAGE_SIZE_OPTIONS);

  protected readonly resolvedTotalPages = computed(() => {
    const explicit = this.totalPages();
    if (explicit !== null) {
      return explicit;
    }

    const size = this.pageSize();
    return size <= 0 ? 0 : Math.ceil(this.totalCount() / size);
  });

  protected readonly rangeText = computed(() => {
    const total = this.totalCount();
    if (total === 0) {
      return 'Không có dòng nào';
    }

    const size = this.pageSize();
    const from = (this.pageNumber() - 1) * size + 1;
    const to = Math.min(total, from + size - 1);

    return `Hiển thị ${from}–${to} trong ${total} dòng`;
  });

  /** Cửa sổ tối đa 5 số trang quanh trang hiện tại. */
  protected readonly pages = computed<number[]>(() => {
    const total = this.resolvedTotalPages();
    const current = this.pageNumber();

    if (total <= 1) {
      return total === 1 ? [1] : [];
    }

    const totalButtons = 5;
    let start = Math.max(1, current - Math.floor(totalButtons / 2));
    const end = Math.min(total, start + totalButtons - 1);
    start = Math.max(1, end - totalButtons + 1);

    const result: number[] = [];
    for (let page = start; page <= end; page += 1) {
      result.push(page);
    }

    return result;
  });

  protected go(page: number): void {
    const total = this.resolvedTotalPages();

    if (page < 1 || page > total || page === this.pageNumber()) {
      return;
    }

    this.pageChange.emit(page);
  }

  protected onPageSizeChange(event: Event): void {
    const target = event.target;

    if (target instanceof HTMLSelectElement) {
      this.pageSizeChange.emit(Number(target.value));
    }
  }
}
