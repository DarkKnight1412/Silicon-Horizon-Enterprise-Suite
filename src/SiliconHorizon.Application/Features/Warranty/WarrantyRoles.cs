using SiliconHorizon.Application.Security;

namespace SiliconHorizon.Application.Features.Warranty;

/// <summary>
/// Nhóm vai trò dùng cho <c>[Authorize(Roles = ...)]</c> của module Bảo hành, đối chiếu cột "Quyền" ở
/// Database/README.md mục 7.1 (TN = thu ngân, TV = tư vấn kỹ thuật, TK = thủ kho, NV = mọi nhân viên,
/// KH = khách hàng, AD = admin) và ma trận BRD mục 2.2 (Admin có toàn quyền).
/// </summary>
/// <remarks>
/// ASP.NET Core ghép nhiều <c>[Authorize]</c> theo kiểu AND, nên controller đặt quyền xem rộng ở cấp class
/// rồi siết hẹp ở từng action (ví dụ class dùng <see cref="Viewers"/>, action tiếp nhận dùng <see cref="Receive"/>).
/// </remarks>
public static class WarrantyRoles
{
    /// <summary>TN, TV, AD — tiếp nhận bảo hành tại quầy/chi nhánh (WAR-04).</summary>
    public const string Receive = RoleCodes.SalesDesk + "," + RoleCodes.Admin;

    /// <summary>TK, TV, AD — chẩn đoán, gửi hãng, sửa xong, đổi mới, từ chối, trả khách (WAR-06, WAR-07).</summary>
    public const string Process = RoleCodes.Storekeeper + "," + RoleCodes.TechAdvisor + "," + RoleCodes.Admin;

    /// <summary>NV, AD — đính kèm ảnh/tài liệu cho phiếu (mọi nhân viên đang xử lý phiếu).</summary>
    public const string Attach = RoleCodes.Staff + "," + RoleCodes.Admin;

    /// <summary>NV, KH, AD — xem danh sách/chi tiết phiếu; khách chỉ thấy phiếu của mình (proc ép CustomerId).</summary>
    public const string Viewers = RoleCodes.Staff + "," + RoleCodes.Customer + "," + RoleCodes.Admin;
}