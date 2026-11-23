/**
 * DTO + nhãn của module NHÂN SỰ (xếp ca · chấm công), khớp
 * `SiliconHorizon.Application.Features.Hr.Dto` (ScheduleDtos.cs, AttendanceDtos.cs,
 * AttendanceStatusText.cs) — Database/README.md mục 8 (HR-04, HR-05, HR-06, UC-19/22/23).
 *
 * Quy ước thời gian & phạm vi:
 * - `workDate` là `date` giờ Việt Nam → JSON `"YYYY-MM-DD"`; `startTime`/`endTime` là `time(0)` →
 *   JSON `"HH:mm:ss"`. Ngược lại `checkInAt`/`checkOutAt` là UTC (ISO 8601 có giờ).
 * - API ép `branchId` và `employeeId` từ token: nhân viên chỉ thấy ca/công của mình, Quản lý chỉ
 *   thấy chi nhánh mình. `employeeId`/`branchId` trong query CHỈ có hiệu lực với Admin.
 * - Chấm công vào/ra không nhận `employeeId` từ client (luôn dùng token).
 */

/** Một ca đã xếp — GET /api/v1/schedules. */
export interface ScheduleItemDto {
  scheduleId: number;
  /** Ngày làm việc theo giờ Việt Nam, định dạng `yyyy-MM-dd`. */
  workDate: string;
  branchId: number;
  employeeId: number;
  employeeName: string;
  /** MORNING, AFTERNOON, FULL. */
  shiftCode: string;
  shiftName: string;
  /** `HH:mm:ss`. */
  startTime: string;
  endTime: string;
}

/** Tham số GET /api/v1/schedules (bỏ trống ⇒ trọn tháng hiện tại theo giờ Việt Nam). */
export interface ScheduleListQuery {
  fromDate?: string | null;
  toDate?: string | null;
  /** Chỉ Admin. */
  employeeId?: number | null;
  /** Chỉ Admin. */
  branchId?: number | null;
}

/** POST /api/v1/schedules — chi nhánh của ca ép từ token (không gửi `branchId`). */
export interface AssignScheduleRequest {
  employeeId: number;
  /** `Shifts.Id` (danh mục ca làm việc). */
  shiftId: number;
  /** `yyyy-MM-dd` theo giờ Việt Nam. */
  workDate: string | null;
}

/** Kết quả xếp ca — chỉ trả Id ca vừa tạo. */
export interface CreatedScheduleDto {
  scheduleId: number;
}

/** Một ngày công trong bảng công tháng. */
export interface AttendanceDayDto {
  employeeId: number;
  employeeName: string;
  /** `yyyy-MM-dd` (giờ Việt Nam). */
  workDate: string;
  /** `null` khi bản ghi công không gắn ca nào (điều chỉnh tay). */
  shiftCode: string | null;
  /** `HH:mm:ss`; `null` khi không gắn ca. */
  startTime: string | null;
  /** UTC. */
  checkInAt: string | null;
  /** UTC. */
  checkOutAt: string | null;
  /** 0 Device, 1 ManualAdjust — xem `ATTENDANCE_SOURCE_LABELS`. */
  source: number;
  sourceName: string;
  /** Chỉ có khi `source = 1`. */
  adjustReason: string | null;
  /** `null` khi thiếu giờ vào hoặc giờ ra. */
  workedMinutes: number | null;
  /** Vào muộn hơn giờ bắt đầu ca trên 5 phút. */
  isLate: boolean;
}

/** Tổng hợp công tháng của một nhân viên. */
export interface AttendanceSummaryDto {
  employeeId: number;
  employeeName: string;
  daysWorked: number;
  totalHours: number;
  scheduledShifts: number;
  /** Số bản ghi công thiếu giờ ra (quên chấm công ra). */
  missingCheckOut: number;
}

/** Bảng công tháng — GET /api/v1/attendance?yearMonth= (2 result set). */
export interface AttendanceMonthDto {
  yearMonth: string;
  days: AttendanceDayDto[];
  employees: AttendanceSummaryDto[];
}

/** Tham số GET /api/v1/attendance — `yearMonth` bắt buộc theo `yyyy-MM`. */
export interface AttendanceMonthlyQuery {
  yearMonth: string;
  /** Chỉ Admin. */
  employeeId?: number | null;
  /** Chỉ Admin. */
  branchId?: number | null;
}

/** Kết quả chấm công vào (`usp_Attendance_CheckIn`). */
export interface AttendanceCheckInDto {
  attendanceId: number;
  scheduleId: number;
  checkInAt: string;
}

/** Kết quả chấm công ra (`usp_Attendance_CheckOut`). */
export interface AttendanceCheckOutDto {
  attendanceId: number;
  checkOutAt: string;
}

/** Kết quả điều chỉnh công (`usp_Attendance_Adjust`). */
export interface AttendanceAdjustResultDto {
  attendanceId: number;
}

/**
 * POST /api/v1/attendance/adjust — điều chỉnh công (QL/AD).
 * Bắt buộc `reason` và ít nhất một trong `checkInAt`/`checkOutAt`; giờ ra phải sau giờ vào.
 */
export interface AdjustAttendanceRequest {
  employeeId: number;
  /** `yyyy-MM-dd` theo giờ Việt Nam. */
  workDate: string | null;
  /** Tuỳ chọn — nếu truyền, proc kiểm tra ca thuộc đúng nhân viên + ngày. */
  scheduleId: number | null;
  /** ISO UTC. */
  checkInAt: string | null;
  /** ISO UTC. */
  checkOutAt: string | null;
  reason: string | null;
}

/** Nguồn chấm công (Attendances.Source) — khớp `AttendanceStatusText.SourceText`. */
export const ATTENDANCE_SOURCE_LABELS: Readonly<Record<number, string>> = {
  0: 'Chấm công bằng thiết bị',
  1: 'Quản lý điều chỉnh',
};

/**
 * Ca làm việc trong danh mục `Shifts` (rút gọn `ShiftDto`).
 * ⚠️ `GET /api/v1/admin/shifts` chỉ Admin gọi được — vai trò khác nhập trực tiếp `shiftId` khi xếp ca.
 */
export interface HrShiftDto {
  id: number;
  /** MORNING, AFTERNOON, FULL. */
  code: string;
  name: string;
  /** `HH:mm:ss`. */
  startTime: string;
  endTime: string;
}

/**
 * Nhân viên dùng cho bộ lọc/xếp ca của module Nhân sự (rút gọn `EmployeeDto`).
 * ⚠️ `GET /api/v1/admin/employees` chỉ Admin gọi được — Quản lý nhập trực tiếp `employeeId`.
 */
export interface HrEmployeeDto {
  id: number;
  employeeCode: string;
  fullName: string;
  branchId: number;
  position: number;
  positionName: string;
  status: number;
  statusName: string;
}

/** Nhãn nguồn chấm công; trả nhãn dự phòng khi mã lạ. */
export function attendanceSourceLabel(source: number): string {
  return ATTENDANCE_SOURCE_LABELS[source] ?? 'Không xác định';
}

/** `Date` → `yyyy-MM` giờ địa phương (giá trị mặc định cho bảng công tháng). */
export function toYearMonth(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');

  return `${date.getFullYear()}-${month}`;
}

/** `Date` → `yyyy-MM-dd` giờ địa phương (input type="date"). */
export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${date.getFullYear()}-${month}-${day}`;
}

/** `"08:00:00"` → `"08:00"`; giữ nguyên giá trị rỗng/null thành `—`. */
export function formatShiftTime(value: string | null | undefined): string {
  if (value === null || value === undefined || value.length === 0) {
    return '—';
  }

  return value.slice(0, 5);
}

/** Số phút → `7h30` (dùng cho cột "Giờ làm" của bảng công). */
export function formatWorkedMinutes(minutes: number | null | undefined): string {
  if (minutes === null || minutes === undefined) {
    return '—';
  }

  const hours = Math.floor(minutes / 60);
  const remainder = minutes % 60;

  return remainder === 0 ? `${hours}h` : `${hours}h${String(remainder).padStart(2, '0')}`;
}
