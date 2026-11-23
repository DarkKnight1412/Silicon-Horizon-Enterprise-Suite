using SiliconHorizon.Domain.Entities.Common;

namespace SiliconHorizon.Application.Features.Catalog.Dto;

/// <summary>
/// Chi nhánh trả ra cho KHÁCH HÀNG — <c>GET /api/v1/branches</c> (chỉ chi nhánh ĐANG HOẠT ĐỘNG).
/// Cố ý là DTO GỌN, khác <c>BranchDto</c> của khu vực Admin: endpoint này CÔNG KHAI nên không trả về
/// thông tin quản trị (trạng thái, người/thời điểm tạo và cập nhật).
/// </summary>
public class BranchReferenceDto
{
    public int Id { get; init; }

    /// <summary>Mã chi nhánh (varchar(20), duy nhất — <c>UQ_Branches_Code</c>).</summary>
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public static BranchReferenceDto FromEntity(Branch branch) => new()
    {
        Id = branch.Id,
        Code = branch.Code,
        Name = branch.Name,
        Address = branch.Address,
        Phone = branch.Phone
    };
}
