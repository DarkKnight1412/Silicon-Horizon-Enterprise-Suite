import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { formatDateTime } from '../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { dateRangeValidator } from '../../../shared/validators/date-range.validator';
import { AuditApi } from '../audit-api.service';
import {
  AUDIT_ENTITY_TYPE_SUGGESTIONS,
  prettyJson,
  type AuditLogDto,
  type AuditLogQuery,
} from '../audit.model';

/**
 * Tra cứu nhật ký hệ thống — GET /api/v1/audit-logs (usp_Audit_Search, UC-29/AUD-04).
 *
 * Quyền: QL (chỉ nhật ký của chi nhánh mình — backend tự ép `branchId` từ token, client KHÔNG gửi
 * `branchId`), AD (toàn hệ thống). Bảng `AuditLogs` là sổ append-only nên màn hình chỉ đọc.
 *
 * `fromDate`/`toDate` là UTC và `toDate` là chặn trên KHÔNG bao gồm ⇒ ngày người dùng chọn được đổi
 * thành 00:00:00 của ngày đầu và 23:59:59 của ngày cuối (giờ địa phương quy về UTC).
 */
@Component({
  selector: 'app-audit-log-list',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './audit-log-list.html',
})
export class AuditLogList {
  private readonly auditApi = inject(AuditApi);

  protected readonly formatMoment = formatDateTime;
  protected readonly entityTypeSuggestions = AUDIT_ENTITY_TYPE_SUGGESTIONS;

  protected readonly loading = signal(false);
  protected readonly result = signal<PagedResult<AuditLogDto>>(
    emptyPagedResult<AuditLogDto>(20),
  );

  /** Dòng nhật ký đang xem chi tiết (null = đóng hộp thoại). */
  protected readonly detail = signal<AuditLogDto | null>(null);

  protected readonly filters = new FormGroup(
    {
      entityType: new FormControl('', { nonNullable: true }),
      entityId: new FormControl<number | null>(null),
      userId: new FormControl<number | null>(null),
      action: new FormControl('', { nonNullable: true }),
      fromDate: new FormControl('', { nonNullable: true }),
      toDate: new FormControl('', { nonNullable: true }),
    },
    { validators: [dateRangeValidator('fromDate', 'toDate')] },
  );

  constructor() {
    this.load(1, this.result().pageSize);
  }

  protected load(pageNumber: number, pageSize: number): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }

    const value = this.filters.getRawValue();
    const entityType = value.entityType.trim();
    const action = value.action.trim();

    const query: AuditLogQuery = {
      entityType: entityType.length > 0 ? entityType : null,
      entityId: value.entityId,
      userId: value.userId,
      action: action.length > 0 ? action : null,
      fromDate: toIsoStart(value.fromDate),
      toDate: toIsoEnd(value.toDate),
      pageNumber,
      pageSize,
    };

    this.loading.set(true);

    this.auditApi.search(query).subscribe({
      next: (page) => {
        this.result.set(page);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected resetFilters(): void {
    this.filters.reset({
      entityType: '',
      entityId: null,
      userId: null,
      action: '',
      fromDate: '',
      toDate: '',
    });
    this.load(1, this.result().pageSize);
  }

  protected openDetail(entry: AuditLogDto): void {
    this.detail.set(entry);
  }

  protected closeDetail(): void {
    this.detail.set(null);
  }

  /** Người thực hiện: ưu tiên tên đăng nhập, fallback Id, `null` = hệ thống. */
  protected actorLabel(entry: AuditLogDto): string {
    if (entry.username !== null && entry.username.length > 0) {
      return entry.username;
    }

    return entry.userId === null ? 'Hệ thống' : `Người dùng #${entry.userId}`;
  }

  protected oldValuesText(entry: AuditLogDto): string {
    return prettyJson(entry.oldValues);
  }

  protected newValuesText(entry: AuditLogDto): string {
    return prettyJson(entry.newValues);
  }
}

/** 'YYYY-MM-DD' → ISO mốc đầu ngày UTC; rỗng → null. */
function toIsoStart(value: string): string | null {
  return value.length === 0 ? null : new Date(`${value}T00:00:00.000Z`).toISOString();
}

/** 'YYYY-MM-DD' → ISO 23:59:59 UTC vì backend coi `toDate` là chặn trên KHÔNG bao gồm. */
function toIsoEnd(value: string): string | null {
  return value.length === 0 ? null : new Date(`${value}T23:59:59.000Z`).toISOString();
}
