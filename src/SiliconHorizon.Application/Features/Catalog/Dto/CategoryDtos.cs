using SiliconHorizon.Domain.Entities.Catalog;
using SiliconHorizon.Domain.Enums;

namespace SiliconHorizon.Application.Features.Catalog.Dto;

/// <summary>Dữ liệu danh mục trả ra API (không lộ entity trực tiếp ra ngoài).</summary>
public class CategoryDto
{
    public int Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU.</summary>
    public SpecType SpecType { get; init; }

    public int? ParentId { get; init; }

    public bool IsActive { get; init; }

    public static CategoryDto FromEntity(Category category) => new()
    {
        Id = category.Id,
        Code = category.Code,
        Name = category.Name,
        SpecType = category.SpecType,
        ParentId = category.ParentId,
        IsActive = category.IsActive
    };
}

/// <summary>Yêu cầu tạo danh mục: POST /api/v1/admin/categories</summary>
public record CreateCategoryRequest(string Code, string Name, SpecType SpecType, int? ParentId);

/// <summary>Yêu cầu đổi trạng thái danh mục (không xóa cứng bản ghi đã phát sinh).</summary>
public record SetCategoryStatusRequest(bool IsActive);
