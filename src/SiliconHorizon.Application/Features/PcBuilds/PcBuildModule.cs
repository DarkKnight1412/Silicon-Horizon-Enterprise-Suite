using Microsoft.Extensions.DependencyInjection;

namespace SiliconHorizon.Application.Features.PcBuilds;

/// <summary>
/// Đăng ký DI của module "PC build sẵn". Mỗi module có đúng một lớp đăng ký như thế này
/// (quy ước: <c>Features/&lt;Module&gt;/&lt;Module&gt;Module.cs</c>) và được <c>AddApplication()</c> gọi lại.
/// </summary>
public static class PcBuildModule
{
    public static IServiceCollection AddPcBuildModule(this IServiceCollection services)
    {
        // Bộ PC dựng sẵn cho khách không có chuyên môn: xem danh sách/chi tiết + thêm CẢ BỘ vào giỏ
        // (usp_PcBuild_Search · usp_PcBuild_GetById · usp_PcBuild_AddToCart — Database/10_pc_builds.sql).
        services.AddScoped<IPcBuildService, PcBuildService>();

        return services;
    }
}
