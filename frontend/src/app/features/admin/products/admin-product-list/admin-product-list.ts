import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { formatVnd } from '../../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../../core/models/paged-result.model';
import {
  PRODUCT_STATUS_LABELS,
  type ProductListItemDto,
} from '../../../../core/models/product.model';
import { ConfirmService } from '../../../../core/services/confirm.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../../shared/components/pagination/pagination';
import { CatalogApi } from '../../../catalog/catalog-api.service';

/**
 * Quản trị sản phẩm — danh sách + đổi trạng thái + lối vào form thêm/sửa.
 *
 * Ghi chú kỹ thuật: backend KHÔNG có endpoint danh sách riêng cho quản trị (chỉ có tạo/sửa/đổi trạng
 * thái) nên màn hình dùng chung `GET /api/v1/products` với `sortBy=newest`; `ProductListItemDto`
 * cũng không có trường `status` nên trạng thái hiện tại chỉ xem/sửa được trong màn hình chi tiết
 * `/admin/products/{id}/edit`. Cách đổi trạng thái ở đây là chọn trạng thái MỚI cần đặt.
 */
@Component({
  selector: 'app-admin-product-list',
  imports: [ReactiveFormsModule, RouterLink, Pagination, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-product-list.html',
})
export class AdminProductList {
  private readonly catalog = inject(CatalogApi);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected readonly formatPrice = formatVnd;
  protected readonly loading = signal(false);
  protected readonly busyProductId = signal<number | null>(null);
  protected readonly result = signal<PagedResult<ProductListItemDto>>(
    emptyPagedResult<ProductListItemDto>(20),
  );

  protected readonly filters = new FormGroup({
    keyword: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    this.search(1, this.result().pageSize);
  }

  protected search(pageNumber: number, pageSize: number): void {
    const keyword = this.filters.controls.keyword.value.trim();

    this.loading.set(true);

    this.catalog
      .searchProducts({
        keyword: keyword.length > 0 ? keyword : null,
        sortBy: 'newest',
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

  protected onPageChange(pageNumber: number): void {
    this.search(pageNumber, this.result().pageSize);
  }

  protected onPageSizeChange(pageSize: number): void {
    this.search(1, pageSize);
  }

  protected goToCreate(): void {
    void this.router.navigate(['/admin/products/new']);
  }

  /** Đổi trạng thái sản phẩm (0 Nháp, 1 Đang bán, 2 Ngừng kinh doanh). */
  protected async changeStatus(product: ProductListItemDto, rawStatus: string): Promise<void> {
    if (rawStatus.length === 0) {
      return;
    }

    const status = Number(rawStatus);

    const accepted = await this.confirm.ask({
      title: 'Đổi trạng thái sản phẩm',
      message: `${product.name} → "${PRODUCT_STATUS_LABELS[status] ?? status}". Tiếp tục?`,
      confirmLabel: 'Đổi trạng thái',
    });

    if (!accepted) {
      return;
    }

    this.busyProductId.set(product.id);

    this.catalog.setProductStatus(product.id, { status }).subscribe({
      next: () => {
        this.busyProductId.set(null);
        this.notifications.success('Đã đổi trạng thái', product.name);
        this.search(this.result().pageNumber, this.result().pageSize);
      },
      error: () => this.busyProductId.set(null),
    });
  }
}


