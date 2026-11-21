using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Hr.Dto;

/// <summary>
/// Nhãn tiếng Việt cho dữ liệu chấm công (HR-05, HR-06) — dùng chung cho module Nhân sự.
/// </summary>
/// <remarks>
/// Enum <c>AttendanceSource</c> đã có sẵn ở tầng Domain (<c>SiliconHorizon.Domain/Enums/HrEnums.cs</c>),
/// khớp quy ước cột <c>Attendances.Source</c>: 0 = Device (máy chấm công),
/// 1 = ManualAdjust (quản lý điều chỉnh — luôn kèm <c>AdjustedBy</c> + <c>AdjustReason</c>).
/// Hai enum còn lại của nhóm nhân sự (<c>EmployeePosition</c>, <c>EmployeeStatus</c>) thuộc bảng
/// <c>Employees</c> — các proc chấm công/xếp ca không trả về chúng nên module này không dùng.
/// </remarks>
public static class AttendanceStatusText
{
    /// <summary>Tên nguồn chấm công (Attendances.Source).</summary>
    public static string SourceText(byte source) => (AttendanceSource)source switch
    {
        AttendanceSource.Device => "Chấm công bằng thiết bị",
        AttendanceSource.ManualAdjust => "Quản lý điều chỉnh",
        _ => "Không xác định"
    };
}
