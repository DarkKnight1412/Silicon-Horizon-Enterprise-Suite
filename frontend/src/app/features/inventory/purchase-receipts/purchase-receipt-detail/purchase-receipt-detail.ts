import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatDateTime, formatVnd } from '../../../../core/models/common.model';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { InventoryApi } from '../../inventory-api.service';
import {
  RECEIPT_STATUS_CLASSES,
  RECEIPT_STATUS_LABELS,
  SERIAL_STATUS_CLASSES,
  SERIAL_STATUS_LABELS,
  type PurchaseReceiptDetailDto,
  type PurchaseReceiptSerialDto,
} from '../../inventory.model';

/**
 * Chi tiết phiếu nhập kho (GET /api/v1/purchase-receipts/{id} — `usp_PurchaseReceipt_Get`,
 * 3 result set: header, dòng hàng, serial đã sinh).
 *
 * Phiếu nhập là chứng từ ĐÃ HOÀN TẤT nên màn hình chỉ đọc; các thao tác hiệu chỉnh tồn phải đi qua
 * kiểm kê (`/inventory/stock-takes`) hoặc đổi trạng thái serial (`/inventory/serials`).
 *
 * `id` nhận từ route param qua `withComponentInputBinding` (xem `app.config.ts`).
 */
@Component({
  selector: 'app-purchase-receipt-detail',
  imports: [RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './purchase-receipt-detail.html',
})
export class PurchaseReceiptDetail implements OnInit {
  private readonly inventoryApi = inject(InventoryApi);

  readonly id = input('');

  protected readonly formatMoment = formatDateTime;
  protected readonly formatPrice = formatVnd;
  protected readonly loading = signal(true);
  protected readonly detail = signal<PurchaseReceiptDetailDto | null>(null);

  ngOnInit(): void {
    const parsed = Number(this.id());

    if (!Number.isFinite(parsed) || parsed <= 0) {
      this.loading.set(false);
      return;
    }

    this.load(parsed);
  }

  /** Lọc serial thuộc một dòng hàng để hiển thị kèm sản phẩm tương ứng. */
  protected serialsOfProduct(productId: number): readonly PurchaseReceiptSerialDto[] {
    const detail = this.detail();
    return detail === null ? [] : detail.serials.filter((serial) => serial.productId === productId);
  }

  protected receiptStatusLabel(status: number): string {
    return RECEIPT_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected receiptStatusClass(status: number): string {
    return RECEIPT_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }

  protected serialStatusLabel(status: number): string {
    return SERIAL_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected serialStatusClass(status: number): string {
    return SERIAL_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }

  private load(id: number): void {
    this.loading.set(true);

    this.inventoryApi.getPurchaseReceipt(id).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.loading.set(false);
      },
      error: () => {
        this.detail.set(null);
        this.loading.set(false);
      },
    });
  }
}
