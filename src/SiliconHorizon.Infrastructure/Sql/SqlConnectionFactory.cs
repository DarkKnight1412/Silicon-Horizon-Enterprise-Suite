using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SiliconHorizon.Application.Abstractions.Data;

namespace SiliconHorizon.Infrastructure.Sql;

/// <summary>
/// Cấp kết nối SQL Server cho Dapper (gọi stored procedure).
/// Đọc chuỗi kết nối <c>ConnectionStrings:DefaultConnection</c>.
/// </summary>
public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection trong cấu hình.");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
