import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/http/api.service';
import type { PagedResult } from '../../core/models/paged-result.model';
import type {
  AdjustAttendanceRequest,
  AssignScheduleRequest,
  AttendanceAdjustResultDto,
  AttendanceCheckInDto,
  AttendanceCheckOutDto,
  AttendanceMonthDto,
  AttendanceMonthlyQuery,
  CreatedScheduleDto,
  HrEmployeeDto,
  HrShiftDto,
  ScheduleItemDto,
  ScheduleListQuery,
} from './hr.model';

/**
 * Lời gọi HTTP của module NHÂN SỰ (Database/README.md mục 8; HR-04, HR-05, HR-06).
 *
 * Quyền (đọc từ `SchedulesController` / `AttendanceController` + `HrRoles.cs`):
 * - `getSchedules`: NV (ca của mình), QL (ca chi nhánh), AD.
 * - `assignSchedule` / `removeSchedule`: QL, AD.
 * - `checkIn` / `checkOut`: NV (mọi nhân viên), luôn dùng `employeeId` trong token.
 * - `adjustAttendance`: QL, AD.
 * - `getMonthlyAttendance`: NV (công của mình), QL (chi nhánh), AD.
 * - `listShifts` / `listEmployees`: CHỈ Admin (endpoint `/api/v1/admin/...`) — dùng cho dropdown
 *   khi Admin xếp ca; vai trò khác nhập trực tiếp Id.
 */
@Injectable({ providedIn: 'root' })
export class HrApi {
  private readonly api = inject(ApiService);

  /** GET /api/v1/schedules — bảng xếp ca trong khoảng ngày (bao gồm cả hai đầu). */
  getSchedules(query: ScheduleListQuery): Observable<ScheduleItemDto[]> {
    return this.api.get<ScheduleItemDto[]>('/v1/schedules', {
      params: {
        fromDate: query.fromDate ?? null,
        toDate: query.toDate ?? null,
        employeeId: query.employeeId ?? null,
        branchId: query.branchId ?? null,
      },
    });
  }

  /** POST /api/v1/schedules — xếp ca (QL/AD); trùng (nhân viên, ngày, ca) → 409. */
  assignSchedule(request: AssignScheduleRequest): Observable<CreatedScheduleDto> {
    return this.api.post<CreatedScheduleDto>('/v1/schedules', request, { silent: true });
  }

  /** DELETE /api/v1/schedules/{id} — chỉ xóa được ca chưa có chấm công (đã có → 409). */
  removeSchedule(id: number): Observable<void> {
    return this.api.delete<void>(`/v1/schedules/${id}`, { silent: true });
  }

  /** POST /api/v1/attendance/check-in — chấm công vào ca hôm nay chưa chấm (sớm nhất). */
  checkIn(): Observable<AttendanceCheckInDto> {
    return this.api.post<AttendanceCheckInDto>('/v1/attendance/check-in', null, { silent: true });
  }

  /** POST /api/v1/attendance/check-out — chấm công ra ca đang mở (không có ca mở → 409). */
  checkOut(): Observable<AttendanceCheckOutDto> {
    return this.api.post<AttendanceCheckOutDto>('/v1/attendance/check-out', null, { silent: true });
  }

  /** POST /api/v1/attendance/adjust — điều chỉnh công kèm lý do (QL/AD). */
  adjustAttendance(request: AdjustAttendanceRequest): Observable<AttendanceAdjustResultDto> {
    return this.api.post<AttendanceAdjustResultDto>('/v1/attendance/adjust', request, {
      silent: true,
    });
  }

  /** GET /api/v1/attendance?yearMonth=yyyy-MM — bảng công tháng (chi tiết + tổng hợp). */
  getMonthlyAttendance(query: AttendanceMonthlyQuery): Observable<AttendanceMonthDto> {
    return this.api.get<AttendanceMonthDto>('/v1/attendance', {
      params: {
        yearMonth: query.yearMonth,
        employeeId: query.employeeId ?? null,
        branchId: query.branchId ?? null,
      },
      silent: true,
    });
  }

  /** GET /api/v1/admin/shifts — CHỈ Admin (danh mục ca làm việc để xếp ca). */
  listShifts(): Observable<PagedResult<HrShiftDto>> {
    return this.api.get<PagedResult<HrShiftDto>>('/v1/admin/shifts', {
      params: { pageNumber: 1, pageSize: 100 },
      silent: true,
    });
  }

  /** GET /api/v1/admin/employees — CHỈ Admin (chọn nhân viên được xếp ca). */
  listEmployees(branchId: number | null): Observable<PagedResult<HrEmployeeDto>> {
    return this.api.get<PagedResult<HrEmployeeDto>>('/v1/admin/employees', {
      params: { branchId: branchId ?? null, status: 0, pageNumber: 1, pageSize: 200 },
      silent: true,
    });
  }
}
