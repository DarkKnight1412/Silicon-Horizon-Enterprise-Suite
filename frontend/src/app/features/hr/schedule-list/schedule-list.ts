import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { APPROVER_ROLES, ROLES } from '../../../core/models/auth.model';
import { isApiError } from '../../../core/models/api-error.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { dateRangeValidator } from '../../../shared/validators/date-range.validator';
import { HrApi } from '../hr-api.service';
import {
  formatShiftTime,
  toIsoDate,
  type AssignScheduleRequest,
  type HrEmployeeDto,
  type HrShiftDto,
  type ScheduleItemDto,
} from '../hr.model';

/**
 * Bảng xếp ca làm việc — GET/POST /api/v1/schedules, DELETE /api/v1/schedules/{id}
 * (usp_Schedule_List/Assign/Remove — Database/README.md mục 8, HR-04/UC-22).
 *
 * Phạm vi & quyền (đọc từ `SchedulesController` + `HrRoles.cs`):
 * - Xem: NV (chỉ ca của mình), QL (chi nhánh mình), AD — backend ép từ token.
 * - Xếp ca / xoá ca: QL, AD. Ca luôn thuộc chi nhánh của người gọi; nhân viên được xếp phải thuộc
 *   chi nhánh đó (khác → 403), trùng (nhân viên, ngày, ca) → 409, ca đã có chấm công → 409.
 * - ⚠️ Danh mục ca (`/api/v1/admin/shifts`) và danh sách nhân viên (`/api/v1/admin/employees`) CHỈ
 *   Admin gọi được ⇒ Admin chọn từ dropdown, Quản lý nhập trực tiếp Id (đúng hợp đồng backend).
 */
@Component({
  selector: 'app-schedule-list',
  imports: [ReactiveFormsModule, RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './schedule-list.html',
})
export class ScheduleList {
  private readonly hrApi = inject(HrApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly formatShiftTime = formatShiftTime;

  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly schedules = signal<readonly ScheduleItemDto[]>([]);

  protected readonly shifts = signal<readonly HrShiftDto[]>([]);
  protected readonly employees = signal<readonly HrEmployeeDto[]>([]);

  protected readonly assignOpen = signal(false);

  /** Chỉ Admin mới gọi được endpoint quản trị để đổ dropdown. */
  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));

  /** Xếp ca / xoá ca chỉ dành cho QL, AD. */
  protected readonly canManage = computed(() => this.authStore.hasAnyRole(APPROVER_ROLES));

  protected readonly filters = new FormGroup(
    {
      fromDate: new FormControl(this.firstDayOfMonth(), { nonNullable: true }),
      toDate: new FormControl(this.lastDayOfMonth(), { nonNullable: true }),
      employeeId: new FormControl<number | null>(null),
    },
    { validators: [dateRangeValidator('fromDate', 'toDate')] },
  );

  protected readonly assignForm = new FormGroup({
    employeeId: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    shiftId: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    workDate: new FormControl(toIsoDate(new Date()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  constructor() {
    this.load();

    if (this.isAdmin()) {
      this.loadAdminLookups();
    }
  }

  protected load(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }

    const value = this.filters.getRawValue();

    this.loading.set(true);

    this.hrApi
      .getSchedules({
        fromDate: value.fromDate.length > 0 ? value.fromDate : null,
        toDate: value.toDate.length > 0 ? value.toDate : null,
        employeeId: this.isAdmin() ? value.employeeId : null,
      })
      .subscribe({
        next: (items) => {
          this.schedules.set(items);
          this.loading.set(false);
        },
        error: () => {
          this.schedules.set([]);
          this.loading.set(false);
        },
      });
  }

  /** Về mặc định: trọn tháng hiện tại (giờ Việt Nam) — đúng mặc định của proc. */
  protected resetFilters(): void {
    this.filters.reset({
      fromDate: this.firstDayOfMonth(),
      toDate: this.lastDayOfMonth(),
      employeeId: null,
    });
    this.load();
  }

  protected openAssign(): void {
    this.assignForm.reset({
      employeeId: null,
      shiftId: null,
      workDate: toIsoDate(new Date()),
    });
    this.assignOpen.set(true);
  }

  protected closeAssign(): void {
    this.assignOpen.set(false);
  }

  protected submitAssign(): void {
    if (this.assignForm.invalid) {
      this.assignForm.markAllAsTouched();
      return;
    }

    const value = this.assignForm.getRawValue();

    if (value.employeeId === null || value.shiftId === null) {
      return;
    }

    const request: AssignScheduleRequest = {
      employeeId: value.employeeId,
      shiftId: value.shiftId,
      workDate: value.workDate,
    };

    this.busy.set(true);

    this.hrApi.assignSchedule(request).subscribe({
      next: (created) => {
        this.busy.set(false);
        this.assignOpen.set(false);
        this.notifications.success('Đã xếp ca', `Mã ca #${created.scheduleId}`);
        this.load();
      },
      error: (error: unknown) => {
        this.busy.set(false);

        if (isApiError(error)) {
          this.notifications.fromApiError(error);
        }
      },
    });
  }

  /** Xoá ca chưa có chấm công — thao tác nguy hiểm nên xác nhận trước. */
  protected async removeSchedule(item: ScheduleItemDto): Promise<void> {
    const accepted = await this.confirm.ask({
      title: 'Xoá ca làm việc',
      message: `Xoá ca ${item.shiftName} (${item.shiftCode}) của ${item.employeeName} ngày ${item.workDate}?`,
      confirmLabel: 'Xoá ca',
      danger: true,
    });

    if (!accepted) {
      return;
    }

    this.busy.set(true);

    this.hrApi.removeSchedule(item.scheduleId).subscribe({
      next: () => {
        this.busy.set(false);
        this.notifications.success('Đã xoá ca', `${item.shiftCode} · ${item.workDate}`);
        this.load();
      },
      error: (error: unknown) => {
        this.busy.set(false);

        if (isApiError(error)) {
          this.notifications.fromApiError(error);
        }
      },
    });
  }

  protected employeeLabel(employee: HrEmployeeDto): string {
    return `${employee.employeeCode} · ${employee.fullName}`;
  }

  protected shiftLabel(shift: HrShiftDto): string {
    return `${shift.name} (${shift.code} · ${formatShiftTime(shift.startTime)}–${formatShiftTime(shift.endTime)})`;
  }

  /** Đổ dropdown ca làm + nhân viên — chỉ gọi được khi là Admin. */
  private loadAdminLookups(): void {
    this.hrApi.listShifts().subscribe({
      next: (page) => this.shifts.set(page.items),
      error: () => this.shifts.set([]),
    });

    this.hrApi.listEmployees(this.authStore.account()?.branchId ?? null).subscribe({
      next: (page) => this.employees.set(page.items),
      error: () => this.employees.set([]),
    });
  }

  /** Ngày đầu tháng hiện tại (giờ địa phương) — `yyyy-MM-dd`. */
  private firstDayOfMonth(): string {
    const now = new Date();

    return toIsoDate(new Date(now.getFullYear(), now.getMonth(), 1));
  }

  /** Ngày cuối tháng hiện tại (giờ địa phương) — `yyyy-MM-dd`. */
  private lastDayOfMonth(): string {
    const now = new Date();

    return toIsoDate(new Date(now.getFullYear(), now.getMonth() + 1, 0));
  }
}
