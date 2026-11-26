import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { PagedResult } from '../../core/models/paged-result.model';
import type {
  PcBuildDetailDto,
  PcBuildListItemDto,
  PcBuildSearchQuery,
  PcBuilderCatalogDto,
} from '../../core/models/pc-build.model';

/**
 * Module "PC BUILD SẴN" (combo linh kiện) — khớp `PcBuildsController` của backend.
 * Chỉ chứa lời gọi HTTP + kiểu dữ liệu; state nằm ở signal cục bộ của từng màn hình
 * (danh sách/chi tiết chỉ phục vụ một trang ⇒ không cần store toàn cục).
 *
 * Thao tác GHI VÀO GIỎ nằm ở `CartStore.addBuild()` chứ không ở đây, để state giỏ hàng
 * (badge trên header, trang giỏ) chỉ được cập nhật tại MỘT nơi.
 */
@Injectable({ providedIn: 'root' })
export class PcBuildApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/pc-builds — danh sách bộ (lọc hạng/khoảng giá/còn hàng, sắp xếp, phân trang). */
  searchBuilds(query: PcBuildSearchQuery): Observable<PagedResult<PcBuildListItemDto>> {
    return this.api.get<PagedResult<PcBuildListItemDto>>('/v1/pc-builds', {
      params: {
        keyword: query.keyword ?? null,
        tier: query.tier ?? null,
        customerGroupId: query.customerGroupId ?? null,
        minPrice: query.minPrice ?? null,
        maxPrice: query.maxPrice ?? null,
        inStockOnly: query.inStockOnly ?? false,
        featuredOnly: query.featuredOnly ?? false,
        sortBy: query.sortBy ?? 'order',
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
      },
    });
  }

  /**
   * GET /api/v1/pc-builds/builder/catalog — danh mục linh kiện cho tab "TỰ BUILD PC":
   * 6 nhóm theo ĐÚNG THỨ TỰ CHỌN của khách (bo mạch chủ → CPU → RAM → ổ cứng → nguồn → màn hình),
   * kèm giá, tồn kho và thông số để giao diện tự lọc ra linh kiện PHÙ HỢP với lựa chọn trước đó.
   */
  getBuilderCatalog(customerGroupId?: number | null): Observable<PcBuilderCatalogDto> {
    return this.api.get<PcBuilderCatalogDto>('/v1/pc-builds/builder/catalog', {
      params: { customerGroupId: customerGroupId ?? null },
    });
  }

  /**
   * GET /api/v1/pc-builds/{id} — chi tiết bộ + danh sách linh kiện (giá và tồn hiện tại).
   * Trang chi tiết dùng resolver (dữ liệu về trước khi hiển thị) nên gọi hàm này từ resolver.
   */
  getBuild(id: number, customerGroupId?: number | null): Observable<PcBuildDetailDto> {
    return this.api.get<PcBuildDetailDto>(`/v1/pc-builds/${id}`, {
      params: { customerGroupId: customerGroupId ?? null },
    });
  }
}
