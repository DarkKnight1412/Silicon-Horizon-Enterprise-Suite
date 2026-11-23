import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { formatVnd } from '../../../core/models/common.model';
import {
  COMPATIBILITY_CODE_LABELS,
  type CompatibilityCheckResultDto,
  type CompatibilityItem,
  type ProductListItemDto,
} from '../../../core/models/product.model';
import { CatalogApi } from '../catalog-api.service';

/** Cấu hình một vị trí trong dàn máy — `categoryCode` khớp seed dbo.Categories. */
interface BuilderSlot {
  key: 'cpuId' | 'ramId' | 'mainboardId' | 'psuId';
  categoryCode: string;
  label: string;
  hint: string;
}

/**
 * Kiểm tra tương thích linh kiện (POST /api/v1/compatibility/check — coupler socket, chuẩn RAM,
 * số khe RAM, công suất nguồn theo TDP CPU).
 *
 * Lưu ý nghiệp vụ: API chỉ trả CẢNH BÁO, không chặn đặt hàng (PRD-10) nên màn hình chỉ hiển thị
 * kết quả, không tự khoá nút mua.
 */
@Component({
  selector: 'app-compatibility',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './compatibility.html',
})
export class Compatibility {
  private readonly catalog = inject(CatalogApi);

  protected readonly formatPrice = formatVnd;

  protected readonly slots: readonly BuilderSlot[] = [
    { key: 'cpuId', categoryCode: 'CPU', label: 'CPU', hint: 'Ảnh hưởng socket và TDP' },
    { key: 'mainboardId', categoryCode: 'MAINBOARD', label: 'Bo mạch chủ', hint: 'Socket, chuẩn RAM, số khe RAM' },
    { key: 'ramId', categoryCode: 'RAM', label: 'RAM', hint: 'Chuẩn DDR và số thanh mỗi kit' },
    { key: 'psuId', categoryCode: 'PSU', label: 'Nguồn (PSU)', hint: 'Công suất so với tổng TDP CPU' },
  ];

  /** Sản phẩm gợi ý cho từng vị trí (nạp một lần, tối đa 50 sản phẩm mỗi danh mục). */
  protected readonly options = signal<Record<string, ProductListItemDto[]>>({});
  protected readonly loadingOptions = signal(false);
  protected readonly checking = signal(false);
  protected readonly result = signal<CompatibilityCheckResultDto | null>(null);

  protected readonly form = new FormGroup({
    cpuId: new FormControl<number | null>(null),
    mainboardId: new FormControl<number | null>(null),
    ramId: new FormControl<number | null>(null),
    psuId: new FormControl<number | null>(null),
    ramQuantity: new FormControl(1, { nonNullable: true }),
  });

  constructor() {
    this.loadOptions();
  }

  protected optionsFor(categoryCode: string): ProductListItemDto[] {
    return this.options()[categoryCode] ?? [];
  }

  /** Gửi các linh kiện đã chọn lên API kiểm tra tương thích. */
  protected check(): void {
    const value = this.form.getRawValue();
    const items: CompatibilityItem[] = [];

    if (value.cpuId !== null) {
      items.push({ productId: value.cpuId, quantity: 1 });
    }
    if (value.mainboardId !== null) {
      items.push({ productId: value.mainboardId, quantity: 1 });
    }
    if (value.ramId !== null) {
      items.push({ productId: value.ramId, quantity: value.ramQuantity });
    }
    if (value.psuId !== null) {
      items.push({ productId: value.psuId, quantity: 1 });
    }

    if (items.length < 2) {
      this.result.set({ isCompatible: true, warnings: [] });
      return;
    }

    this.checking.set(true);

    this.catalog.checkCompatibility(items).subscribe({
      next: (response) => {
        this.result.set(response);
        this.checking.set(false);
      },
      error: () => this.checking.set(false),
    });
  }

  protected reset(): void {
    this.form.reset({ cpuId: null, mainboardId: null, ramId: null, psuId: null, ramQuantity: 1 });
    this.result.set(null);
  }

  protected warningLabel(code: string): string {
    return COMPATIBILITY_CODE_LABELS[code] ?? code;
  }

  private loadOptions(): void {
    this.loadingOptions.set(true);

    for (const slot of this.slots) {
      this.catalog
        .searchProducts({
          categoryCode: slot.categoryCode,
          sortBy: 'price_desc',
          inStockOnly: true,
          pageNumber: 1,
          pageSize: 50,
        })
        .subscribe({
          next: (page) => {
            this.options.update((current) => ({ ...current, [slot.categoryCode]: page.items }));
          },
          error: () => {
            this.options.update((current) => ({ ...current, [slot.categoryCode]: [] }));
          },
        });
    }

    this.loadingOptions.set(false);
  }
}
