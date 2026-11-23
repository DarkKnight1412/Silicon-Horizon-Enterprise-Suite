import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { formatDateTime } from '../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { WarrantyApi } from '../warranty-api.service';
import {
  WARRANTY_STATUS_CLASSES,
  WARRANTY_STATUS_LABELS,
  WARRANTY_STATUS_OPTIONS,
  type WarrantyTicketListItemDto,
} from '../warranty.model';

/**
 * Danh sách phiếu bảo hành (GET /api/v1/warranty-tickets — `usp_Warranty_List`).
 *
 * Phạm vi do backend ép: nhân viên thấy phiếu của chi nhánh mình, khách chỉ thấy phiếu của mình
 * ⇒ màn hình KHÔNG gửi `branchId`/`customerId`.
 */
@Component({
  selector: 'app-warranty-list',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './warranty-list.html',
})
export class WarrantyList {
  private readonly warrantyApi = inject(WarrantyApi);

  protected readonly formatMoment = formatDateTime;
  protected readonly statusOptions = WARRANTY_STATUS_OPTIONS;
  protected readonly statusLabels = WARRANTY_STATUS_LABELS;
  protected readonly statusClasses = WARRANTY_STATUS_CLASSES;

  protected readonly loading = signal(false);
  protected readonly result = signal<PagedResult<WarrantyTicketListItemDto>>(
    emptyPagedResult<WarrantyTicketListItemDto>(20),
  );

  protected readonly filters = new FormGroup({
    status: new FormControl<number | null>(null),
    keyword: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    this.load(1, this.result().pageSize);
  }

  protected load(pageNumber: number, pageSize: number): void {
    const value = this.filters.getRawValue();
    const keyword = value.keyword.trim();

    this.loading.set(true);

    this.warrantyApi
      .listTickets({
        status: value.status,
        keyword: keyword.length > 0 ? keyword : null,
        pageNumber,
        pageSize,
      })
      .subscribe({
        next: (page) => {
          this.result.set(page);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  protected resetFilters(): void {
    this.filters.reset({ status: null, keyword: '' });
    this.load(1, this.result().pageSize);
  }

  protected statusLabel(status: number): string {
    return this.statusLabels[status] ?? 'Không xác định';
  }

  protected statusClass(status: number): string {
    return this.statusClasses[status] ?? 'bg-slate-200 text-slate-700';
  }
}
