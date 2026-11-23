import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { ApiService } from '../../core/http/api.service';
import { isApiError } from '../../core/models/api-error.model';
import { ROLE_LABELS, EMPLOYEE_POSITION_LABELS } from '../../core/models/auth.model';
import { formatDateTime } from '../../core/models/common.model';

/**
 * Trang tổng quan: hồ sơ tài khoản (vai trò, quyền, phạm vi chi nhánh), lối tắt theo vai trò và
 * tình trạng kết nối API (`GET /api/v1/health`).
 *
 * Đây cũng là màn hình minh hoạ rõ nhất hai cờ của AuthStore: `isCustomer` và `isStaff`.
 */
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.html',
})
export class Dashboard {
  private readonly authStore = inject(AuthStore);
  private readonly api = inject(ApiService);

  protected readonly account = this.authStore.account;
  protected readonly permissions = this.authStore.permissions;
  protected readonly isCustomer = this.authStore.isCustomer;
  protected readonly isStaff = this.authStore.isStaff;
  protected readonly apiBaseUrl = this.api.baseUrl;

  protected readonly healthState = signal<'checking' | 'ok' | 'error'>('checking');
  protected readonly healthMessage = signal('Đang kiểm tra kết nối API…');

  protected readonly roleText = computed(() => {
    const roles = this.account()?.roles ?? [];

    return roles.length === 0 ? '—' : roles.map((role) => ROLE_LABELS[role] ?? role).join(', ');
  });

  protected readonly positionText = computed(() => {
    const position = this.account()?.employeePosition ?? null;

    return position === null ? '—' : (EMPLOYEE_POSITION_LABELS[position] ?? '—');
  });

  protected readonly branchText = computed(() => {
    const branchId = this.account()?.branchId ?? null;

    return branchId === null ? 'Không giới hạn (khách hàng / Admin)' : `Chi nhánh #${branchId}`;
  });

  constructor() {
    this.checkHealth();
  }

  /** Gọi endpoint công khai /api/v1/health để xác nhận frontend đã nối đúng API. */
  protected checkHealth(): void {
    this.healthState.set('checking');
    this.healthMessage.set('Đang kiểm tra kết nối API…');

    this.api.get<{ status: string; utc: string }>('/v1/health', { silent: true }).subscribe({
      next: (response) => {
        this.healthState.set('ok');
        this.healthMessage.set(
          `API hoạt động (${response.status}) — thời điểm máy chủ ${formatDateTime(response.utc)}`,
        );
      },
      error: (error: unknown) => {
        this.healthState.set('error');
        this.healthMessage.set(
          isApiError(error) ? error.message : 'Không kết nối được máy chủ API.',
        );
      },
    });
  }
}
