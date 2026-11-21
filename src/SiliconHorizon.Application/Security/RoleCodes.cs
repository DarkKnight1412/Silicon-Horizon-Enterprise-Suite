namespace SiliconHorizon.Application.Security;

/// <summary>
/// Mã vai trò — khớp đúng seed <c>dbo.Roles</c> trong Database/table_and_data.sql (mục 13).
/// Dùng cho <c>[Authorize(Roles = RoleCodes.Admin)]</c>.
/// </summary>
public static class RoleCodes
{
    public const string Customer = "Customer";

    public const string Cashier = "Cashier";

    public const string TechAdvisor = "TechAdvisor";

    public const string Storekeeper = "Storekeeper";

    public const string Manager = "Manager";

    public const string Admin = "Admin";

    /// <summary>Thu ngân và Tư vấn kỹ thuật (bán tại quầy / tiếp nhận bảo hành).</summary>
    public const string SalesDesk = Cashier + "," + TechAdvisor;

    /// <summary>Mọi nhân viên — ký hiệu NV trong ma trận BRD 2.2.</summary>
    public const string Staff = Cashier + "," + TechAdvisor + "," + Storekeeper + "," + Manager;

    /// <summary>Quản lý chi nhánh + Admin — nhóm duyệt các thao tác nhạy cảm.</summary>
    public const string Approvers = Manager + "," + Admin;
}
