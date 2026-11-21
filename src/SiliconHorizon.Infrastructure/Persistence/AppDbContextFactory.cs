using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SiliconHorizon.Infrastructure.Persistence;

/// <summary>
/// Factory cho thời điểm thiết kế (design-time) để chạy:
/// <code>
/// dotnet ef migrations add Initial --project src/SiliconHorizon.Infrastructure --startup-project src/SiliconHorizon.Api
/// dotnet ef dbcontext scaffold ... (nếu muốn sinh entity từ database có sẵn)
/// </code>
/// Chuỗi kết nối lấy theo thứ tự: biến môi trường <c>ConnectionStrings__DefaultConnection</c>,
/// rồi <c>appsettings.Development.json</c>, rồi <c>appsettings.json</c> của project API.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveConfigurationDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Thiếu ConnectionStrings:DefaultConnection. Hãy khai báo trong src/SiliconHorizon.Api/appsettings.Development.json " +
                "hoặc đặt biến môi trường ConnectionStrings__DefaultConnection.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql
                .MigrationsAssembly(typeof(AppDbContextFactory).Assembly.FullName)
                .EnableRetryOnFailure(3))
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>Tìm thư mục chứa appsettings.json (thường là src/SiliconHorizon.Api) từ thư mục hiện tại đi lên.</summary>
    private static string ResolveConfigurationDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var apiDirectory = Path.Combine(directory.FullName, "SiliconHorizon.Api");

            if (File.Exists(Path.Combine(apiDirectory, "appsettings.json")))
            {
                return apiDirectory;
            }

            if (File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
