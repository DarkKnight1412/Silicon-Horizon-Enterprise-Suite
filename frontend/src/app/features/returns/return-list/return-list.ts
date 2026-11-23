import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { formatDateTime, formatVnd } from '../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { ReturnApi } from '../return-api.service';
import {
  RETURN_STATUS_CLASSES,
  RETURN_STATUS_LABELS,
  RETURN_STATUS_OPTIONS,
  type ReturnListItemDto,
} from '../return.model';

/**
 * Danh sách yêu cầu đổi trả (GET /api/v1/returns — `usp_Return_List`).
 *
 * Phạm vi do backend ép: nhân viên thấy yêu cầu của chi nhánh mình, khách chỉ thấy yêu cầu của mình
 * ⇒ màn hình không gửi `branchId`/`customerId`.
 */
@Component({
  selector: 'app-return-list',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './return-list.html',
})
export class ReturnList {
  private readonly returnApi = inject(ReturnApi);

  protected readonly formatMoment = formatDateTime;
  protected readonly formatPrice = formatVnd;
  protected readonly statusOptions = RETURN_STATUS_OPTIONS;
  protected readonly statusLabels = RETURN_STATUS_LABELS;
  protected readonly statusClasses = RETURN_STATUS_CLASSES;

  protected readonly loading = signal(false);
  protected readonly result = signal<PagedResult<ReturnListItemDto>>(
    emptyPagedResult<ReturnListItemDto>(20),
  );

  protected readonly filters = new FormGroup({
    status: new FormControl<number | null>(null),
  });

  constructor() {
    this.load(1, this.result().pageSize);
  }

  protected load(pageNumber: number, pageSize: number): void {
    this.loading.set(true);

    this.returnApi.listReturns({ status: this.filters.controls.status.value, pageNumber, pageSize }).subscribe({
      next: (page) => {
        this.result.set(page);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected resetFilters(): void {
    this.filters.reset({ status: null });
    this.load(1, this.result().pageSize);
  }

  protected statusLabel(status: number): string {
    return this.statusLabels[status] ?? 'Không xác định';
  }

  protected statusClass(status: number): string {
    return this.statusClasses[status] ?? 'bg-slate-200 text-slate-700';
  }
}
