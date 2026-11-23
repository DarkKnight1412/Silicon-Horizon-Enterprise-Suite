import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type { ApiError } from '../../../core/models/api-error.model';
import { formatDate, formatDateTime } from '../../../core/models/common.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { WarrantyApi } from '../warranty-api.service';
import { WARRANTY_STATUS_CLASSES, WARRANTY_STATUS_LABELS } from '../warranty.model';
import {
  SERIAL_STATUS_CLASSES,
  SERIAL_STATUS_LABELS,
  type SerialLookupDto,
} from '../../inventory/inventory.model';

/**
 * Tra cứu bảo hành theo serial (GET /api/v1/warranty/lookup?serial= — WAR-02).
 *
 * Backend dùng CHÍNH `usp_Serial_Lookup` (cùng proc với `GET /api/v1/serials/{serialNumber}`) nên
 * màn hình chỉ đọc: xem tình trạng bảo hành và các phiếu bảo hành đã lập cho serial đó.
 *
 * Quyền: mọi nhân viên + Admin (khách tra hàng của mình qua `customerId` ép từ token ở backend).
 */
@Component({
  selector: 'app-warranty-lookup',
  imports: [ReactiveFormsModule, RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './warranty-lookup.html',
})
export class WarrantyLookup {
  private readonly warrantyApi = inject(WarrantyApi);

  protected readonly formatMoment = formatDateTime;
  protected readonly formatDay = formatDate;

  protected readonly loading = signal(false);
  protected readonly result = signal<SerialLookupDto | null>(null);
  protected readonly apiError = signal<ApiError | null>(null);

  protected readonly filters = new FormGroup({
    serial: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    productId: new FormControl<number | null>(null),
  });

  protected lookup(): void {
    if (this.filters.controls.serial.invalid) {
      this.filters.controls.serial.markAsTouched();
      return;
    }

    const serial = this.filters.controls.serial.value.trim();

    this.loading.set(true);
    this.apiError.set(null);

    this.warrantyApi.lookupBySerial(serial, this.filters.controls.productId.value).subscribe({
      next: (data) => {
        this.result.set(data);
        this.loading.set(false);
      },
      error: (error: ApiError) => {
        this.result.set(null);
        this.apiError.set(error);
        this.loading.set(false);
      },
    });
  }

  protected serialStatusLabel(status: number): string {
    return SERIAL_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected serialStatusClass(status: number): string {
    return SERIAL_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }

  protected warrantyStatusLabel(status: number): string {
    return WARRANTY_STATUS_LABELS[status] ?? 'Không xác định';
  }

  protected warrantyStatusClass(status: number): string {
    return WARRANTY_STATUS_CLASSES[status] ?? 'bg-slate-200 text-slate-700';
  }
}
