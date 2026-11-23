import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { APPROVER_ROLES, ROLES } from '../../../core/models/auth.model';
import { isApiError } from '../../../core/models/api-error.model';
import { formatDateTime } from '../../../core/models/common.model';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { HrApi } from '../hr-api.service';
import {
  attendanceSourceLabel,
  formatShiftTime,
  formatWorkedMinutes,
  toIsoDate,
  toYearMonth,
  type AdjustAttendanceRequest,
  type AttendanceDayDto,
  type AttendanceMonthDto,
  type HrEmployeeDto,
} from '../hr.model';

/**
 * Bảng công tháng + chấm công — GET /api/v1/attendance?yearMonth=yyyy-MM và
 * POST /api/v1/attendance/check-in | check-out | adjust
 * (usp_Attendance_GetMonthly / CheckIn / CheckOut / Adjust — Database/README.md mục 8, HR-05, HR-06).
 *
 * Quyền & phạm vi (đọc từ `AttendanceController` + `HrRoles.cs`):
 * - Xem bảng công: NV (công của mình — backend ép `employeeId` từ token), QL (chi nhánh), AD.
 * - Chấm công vào/ra: mọi nhân viên, LUÔN dùng `employeeId` trong token (không nhận từ client).
 * - Điều chỉnh công: QL, AD; bắt buộc lý do; giờ ra phải sau giờ vào; mọi lần điều chỉnh đều ghi nhật ký.
 *
 * `checkInAt`/`checkOutAt` là UTC nên khi hiển thị dùng `formatDateTime`, khi nhập liệu dùng
 * `datetime-local` (giờ địa phương) rồi đổi sang ISO UTC trước khi gửi.
 */
@Component({
  selector: 'app-attendance-month',
  imports: [ReactiveFormsModule, RouterLink, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './attendance-month.html',
})
export class AttendanceMonth {
  private readonly hrApi = inject(HrApi);
  private readonly authStore = inject(AuthStore);
  private readonly notifications = inject(NotificationService);

  protected readonly formatMoment = formatDateTime;
  protected readonly formatShiftTime = formatShiftTime;
  protected readonly formatWorkedMinutes = formatWorkedMinutes;
  protected readonly sourceLabel = attendanceSourceLabel;

  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly month = signal<AttendanceMonthDto | null>(null);

  protected readonly adjustOpen = signal(false);

  /** Chỉ Admin mới lọc được theo nhân viên (endpoint danh sách nhân viên cũng chỉ Admin). */
  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));

  /** Mọi nhân viên đều chấm công được cho chính mình. */
  protected readonly canCheckInOut = computed(() => this.authStore.isStaff());

  /** Điều chỉnh công chỉ dành cho QL, AD. */
  protected readonly canAdjust = computed(() => this.authStore.hasAnyRole(APPROVER_ROLES));

  protected readonly filters = new FormGroup({
    yearMonth: new FormControl(toYearMonth(new Date()), {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\d{4}-(0[1-9]|1[0-2])$/)],
    }),
    employeeId: new FormControl<number | null>(null),
  });

  /** Lọc cục bộ bảng chi tiết theo một nhân viên (không gọi lại API). */
  protected readonly detailEmployeeId = signal<number | null>(null);

  protected readonly employees = signal<readonly HrEmployeeDto[]>([]);

  protected readonly adjustForm = new FormGroup({
    employeeId: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    workDate: new FormControl(toIsoDate(new Date()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
    checkIn: new FormControl('', { nonNullable: true }),
    checkOut: new FormControl('', { nonNullable: true }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  /** Chi tiết ngày công sau khi lọc theo nhân viên (nếu chọn). */
  protected readonly dayRows = computed<readonly AttendanceDayDto[]>(() => {
    const days = this.month()?.days ?? [];
    const employeeId = this.detailEmployeeId();

    return employeeId === null ? days : days.filter((day) => day.employeeId === employeeId);
  });

  constructor() {
    this.load();

    if (this.isAdmin()) {
      this.loadEmployees();
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
      .getMonthlyAttendance({
        yearMonth: value.yearMonth,
        employeeId: this.isAdmin() ? value.employeeId : null,
      })
      .subscribe({
        next: (month) => {
          this.month.set(month);
          this.loading.set(false);
        },
        error: () => {
          this.month.set(null);
          this.loading.set(false);
        },
      });
  }

  /** Chấm công vào ca hôm nay chưa chấm (ca bắt đầu sớm nhất). */
  protected checkIn(): void {
    this.busy.set(true);

    this.hrApi.checkIn().subscribe({
      next: (result) => {
        this.busy.set(false);
        this.notifications.success(
          'Đã chấm công vào',
          `Lúc ${formatDateTime(result.checkInAt)} · ca #${result.scheduleId}`,
        );
        this.load();
      },
      error: (error: unknown) => this.reportError(error),
    });
  }

  /** Chấm công ra ca đang mở của hôm nay. */
  protected checkOut(): void {
    this.busy.set(true);

    this.hrApi.checkOut().subscribe({
      next: (result) => {
        this.busy.set(false);
        this.notifications.success('Đã chấm công ra', `Lúc ${formatDateTime(result.checkOutAt)}`);
        this.load();
      },
      error: (error: unknown) => this.reportError(error),
    });
  }

  /** Mở hộp thoại điều chỉnh công, điền sẵn dữ liệu nếu bấm từ một dòng ngày công. */
  protected openAdjust(day?: AttendanceDayDto): void {
    this.adjustForm.reset({
      employeeId: day?.employeeId ?? null,
      workDate: day?.workDate ?? toIsoDate(new Date()),
      checkIn: toLocalDateTimeInput(day?.checkInAt ?? null),
      checkOut: toLocalDateTimeInput(day?.checkOutAt ?? null),
      reason: '',
    });
    this.adjustOpen.set(true);
  }

  protected closeAdjust(): void {
    this.adjustOpen.set(false);
  }

  /** Điều chỉnh công (QL/AD) — bắt buộc lý do và ít nhất một mốc giờ. */
  protected submitAdjust(): void {
    const value = this.adjustForm.getRawValue();

    if (this.adjustForm.invalid || value.employeeId === null) {
      this.adjustForm.markAllAsTouched();
      return;
    }

    const checkInAt = toIsoUtc(value.checkIn);
    const checkOutAt = toIsoUtc(value.checkOut);

    if (checkInAt === null && checkOutAt === null) {
      this.notifications.warning(
        'Thiếu giờ chấm công',
        'Cần nhập ít nhất giờ vào hoặc giờ ra sau khi điều chỉnh.',
      );
      return;
    }

    if (checkInAt !== null && checkOutAt !== null && new Date(checkOutAt) <= new Date(checkInAt)) {
      this.notifications.warning('Giờ ra không hợp lệ', 'Giờ ra phải sau giờ vào.');
      return;
    }

    const request: AdjustAttendanceRequest = {
      employeeId: value.employeeId,
      workDate: value.workDate,
      scheduleId: null,
      checkInAt,
      checkOutAt,
      reason: value.reason.trim(),
    };

    this.busy.set(true);

    this.hrApi.adjustAttendance(request).subscribe({
      next: (result) => {
        this.busy.set(false);
        this.adjustOpen.set(false);
        this.notifications.success('Đã điều chỉnh công', `Bản ghi #${result.attendanceId}`);
        this.load();
      },
      error: (error: unknown) => this.reportError(error),
    });
  }

  protected employeeLabel(employee: HrEmployeeDto): string {
    return `${employee.employeeCode} · ${employee.fullName}`;
  }

  /** Lọc cục bộ bảng chi tiết theo nhân viên được chọn (không gọi lại API). */
  protected onDetailEmployeeChange(event: Event): void {
    const target = event.target;

    if (!(target instanceof HTMLSelectElement)) {
      return;
    }

    const parsed = Number(target.value);

    this.detailEmployeeId.set(target.value.length === 0 || !Number.isFinite(parsed) ? null : parsed);
  }

  private reportError(error: unknown): void {
    this.busy.set(false);

    if (isApiError(error)) {
      this.notifications.fromApiError(error);
    }
  }

  private loadEmployees(): void {
    this.hrApi.listEmployees(this.authStore.account()?.branchId ?? null).subscribe({
      next: (page) => this.employees.set(page.items),
      error: () => this.employees.set([]),
    });
  }
}

/** ISO UTC → giá trị cho `input[type=datetime-local]` (giờ địa phương); null → chuỗi rỗng. */
function toLocalDateTimeInput(isoUtc: string | null): string {
  if (isoUtc === null) {
    return '';
  }

  const date = new Date(isoUtc);

  if (Number.isNaN(date.getTime())) {
    return '';
  }

  const pad = (value: number): string => String(value).padStart(2, '0');

  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** `datetime-local` (giờ địa phương) → ISO UTC; chuỗi rỗng → null. */
function toIsoUtc(value: string): string | null {
  if (value.length === 0) {
    return null;
  }

  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}
