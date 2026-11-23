import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import type { ProductListItemDto } from '../../../core/models/product.model';

/**
 * Khối "tìm & chọn sản phẩm" dùng chung cho các form kho/NCC (nhập kho, chuyển kho, trả NCC).
 *
 * Component chỉ TRÌNH BÀY: state tìm kiếm nằm ở component cha (`keyword`, `results`, `searching`)
 * nên mỗi màn hình tự quyết định gọi `GET /api/v1/products` thế nào và giữ luồng dữ liệu tập trung
 * một chỗ — cùng quy ước với `app-pagination` / `app-empty-state`.
 *
 * `requiresSerial` của sản phẩm được trả kèm để cha biết có phải bắt nhập serial hay không.
 */
@Component({
  selector: 'app-product-picker',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-end gap-2">
      <div class="min-w-56 flex-1">
        <label class="block text-sm font-medium text-slate-700" for="productKeyword">
          Tìm sản phẩm (tên hoặc đầu mã SKU)
        </label>
        <input
          id="productKeyword"
          type="search"
          [formControl]="keyword()"
          placeholder="Ví dụ: RAM DDR5 hoặc SSD-"
          class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          (keyup.enter)="search.emit()"
        />
      </div>
      <button
        type="button"
        class="rounded-md border border-slate-300 px-4 py-2 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
        [disabled]="searching()"
        (click)="search.emit()"
      >
        {{ searching() ? 'Đang tìm…' : 'Tìm' }}
      </button>
    </div>

    @if (results().length > 0) {
      <ul class="mt-3 max-h-64 divide-y divide-slate-100 overflow-y-auto rounded-md border border-slate-200">
        @for (product of results(); track product.id) {
          <li class="flex items-center justify-between gap-3 px-3 py-2 text-sm">
            <div class="min-w-0">
              <p class="truncate font-medium text-slate-800">{{ product.name }}</p>
              <p class="text-xs text-slate-500">
                {{ product.sku }} · {{ product.brandName }}
                @if (product.requiresSerial) {
                  <span class="ml-1 rounded bg-amber-100 px-1.5 py-0.5 text-amber-800">
                    quản lý serial
                  </span>
                }
              </p>
            </div>
            <button
              type="button"
              class="shrink-0 rounded-md bg-brand-600 px-3 py-1 text-xs font-medium text-white hover:bg-brand-700"
              (click)="picked.emit(product)"
            >
              Chọn
            </button>
          </li>
        }
      </ul>
    } @else if (searched()) {
      <p class="mt-3 text-sm text-slate-500">Không tìm thấy sản phẩm phù hợp.</p>
    }
  `,
})
export class ProductPicker {
  /** Ô nhập từ khoá do cha sở hữu để cha gọi API và biết giá trị hiện tại. */
  readonly keyword = input.required<FormControl<string>>();
  readonly results = input.required<readonly ProductListItemDto[]>();
  readonly searching = input(false);
  /** Cha bật true sau lần tìm đầu tiên để hiện thông báo "không tìm thấy". */
  readonly searched = input(false);

  readonly search = output<void>();
  readonly picked = output<ProductListItemDto>();
}
