using SiliconHorizon.Application.Common.Models;
using SiliconHorizon.Application.Features.PcBuilds.Dto;

namespace SiliconHorizon.Application.Features.PcBuilds;

/// <summary>
/// Use case "PC BUILD SẴN" (combo linh kiện) — dành cho khách không có chuyên môn: thay vì tự chọn
/// từng món, khách chọn một bộ dựng sẵn của cửa hàng rồi thêm MỘT LẦN toàn bộ linh kiện vào giỏ.
/// <para>Toàn bộ đi qua stored procedure trong <c>Database/10_pc_builds.sql</c> — không truy vấn bảng trực tiếp.</para>
/// </summary>
public interface IPcBuildService
{
    /// <summary>
    /// GET /api/v1/pc-builds — danh sách bộ (lọc theo hạng/khoảng giá/còn hàng, sắp xếp, phân trang)
    /// — <c>usp_PcBuild_Search</c>.
    /// </summary>
    Task<PagedResult<PcBuildListItemDto>> SearchAsync(
        PcBuildSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/pc-builds/{id} — chi tiết bộ kèm danh sách linh kiện (giá và tồn hiện tại)
    /// — <c>usp_PcBuild_GetById</c>. Không tìm thấy ⇒ 404.
    /// </summary>
    Task<PcBuildDetailDto> GetByIdAsync(
        long id,
        int? customerGroupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/pc-builds/{id}/add-to-cart — thêm TẤT CẢ linh kiện của bộ vào giỏ trong một giao dịch
    /// — <c>usp_PcBuild_AddToCart</c>. Thiếu tồn ở bất kỳ linh kiện nào ⇒ 409 và không thêm gì cả.
    /// Chủ giỏ: khách đã đăng nhập (customerId từ token) hoặc khách vãng lai (header X-Cart-Session).
    /// </summary>
    Task<AddPcBuildToCartResultDto> AddToCartAsync(
        long id,
        AddPcBuildToCartRequest request,
        string? sessionToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /api/v1/pc-builds/builder/catalog — danh mục linh kiện cho tab "TỰ BUILD PC"
    /// (<c>usp_PcBuild_BuilderCatalog</c>): 6 nhóm theo đúng thứ tự chọn của khách, kèm giá, tồn kho và
    /// thông số kỹ thuật để giao diện tự gợi ý linh kiện PHÙ HỢP với các lựa chọn trước đó.
    /// </summary>
    Task<PcBuilderCatalogDto> GetBuilderCatalogAsync(
        int? customerGroupId = null,
        CancellationToken cancellationToken = default);
}
