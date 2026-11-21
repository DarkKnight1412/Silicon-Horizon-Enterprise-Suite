using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using SiliconHorizon.Application.Abstractions.Data;
using SiliconHorizon.Application.Abstractions.Security;
using SiliconHorizon.Application.Common.Exceptions;

namespace SiliconHorizon.Infrastructure.Sql;

/// <summary>
/// Hiện thực <see cref="ISqlExecutor"/> bằng Dapper.
/// Trước mỗi proc, IP/User-Agent/UserId được đưa vào <c>sp_set_session_context</c> để nhật ký (AUD-02) lấy được thiết bị.
/// Lỗi 50001..50006 / 1205 được dịch thành exception nghiệp vụ (xem <see cref="SqlErrorTranslator"/>).
/// </summary>
public sealed class DapperSqlExecutor : ISqlExecutor
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICurrentUser _currentUser;

    public DapperSqlExecutor(ISqlConnectionFactory connectionFactory, ICurrentUser currentUser)
    {
        _connectionFactory = connectionFactory;
        _currentUser = currentUser;
    }

    public async Task<int> ExecuteAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);

        try
        {
            return await connection.ExecuteAsync(CreateCommand(procedureName, parameters, cancellationToken));
        }
        catch (SqlException exception) when (SqlErrorTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);

        try
        {
            var items = await connection.QueryAsync<T>(CreateCommand(procedureName, parameters, cancellationToken));
            return items.AsList();
        }
        catch (SqlException exception) when (SqlErrorTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);

        try
        {
            return await connection.QueryFirstOrDefaultAsync<T>(
                CreateCommand(procedureName, parameters, cancellationToken));
        }
        catch (SqlException exception) when (SqlErrorTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<T?> ExecuteScalarAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenAsync(cancellationToken);

        try
        {
            return await connection.ExecuteScalarAsync<T>(
                CreateCommand(procedureName, parameters, cancellationToken));
        }
        catch (SqlException exception) when (SqlErrorTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public async Task<ISqlMultiResult> QueryMultipleAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await OpenAsync(cancellationToken);

        try
        {
            var grid = await connection.QueryMultipleAsync(
                CreateCommand(procedureName, parameters, cancellationToken));

            return new DapperMultiResult(connection, grid);
        }
        catch (SqlException exception) when (SqlErrorTranslator.Translate(exception) is { } translated)
        {
            connection.Dispose();
            throw translated;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private async Task<IDbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.SetAuditContextAsync(_currentUser, cancellationToken);

        return connection;
    }

    private static CommandDefinition CreateCommand(
        string procedureName,
        object? parameters,
        CancellationToken cancellationToken)
        => new(
            procedureName,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

    /// <summary>Giữ kết nối mở cho tới khi đọc xong toàn bộ result set của proc.</summary>
    private sealed class DapperMultiResult : ISqlMultiResult
    {
        private readonly IDbConnection _connection;
        private readonly SqlMapper.GridReader _grid;

        public DapperMultiResult(IDbConnection connection, SqlMapper.GridReader grid)
        {
            _connection = connection;
            _grid = grid;
        }

        public async Task<IReadOnlyList<T>> ReadListAsync<T>(CancellationToken cancellationToken = default)
        {
            try
            {
                var items = await _grid.ReadAsync<T>();
                return items.AsList();
            }
            catch (SqlException exception) when (SqlErrorTranslator.Translate(exception) is { } translated)
            {
                throw translated;
            }
        }

        public async Task<T?> ReadFirstOrDefaultAsync<T>(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _grid.ReadFirstOrDefaultAsync<T>();
            }
            catch (SqlException exception) when (SqlErrorTranslator.Translate(exception) is { } translated)
            {
                throw translated;
            }
        }

        public async Task<T> ReadSingleAsync<T>(CancellationToken cancellationToken = default)
        {
            var item = await ReadFirstOrDefaultAsync<T>(cancellationToken);

            if (item is null)
            {
                throw new NotFoundException("Không tìm thấy dữ liệu theo yêu cầu.");
            }

            return item;
        }

        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadDictionaryListAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Dapper trả mỗi dòng dưới dạng DapperRow (IDictionary<string, object>) khi dùng dynamic.
                var rows = await _grid.ReadAsync<dynamic>();

                return rows
                    .Cast<IDictionary<string, object>>()
                    .Select(row => (IReadOnlyDictionary<string, object?>)row.ToDictionary(
                        pair => pair.Key,
                        pair => (object?)pair.Value))
                    .ToList();
            }
            catch (SqlException exception) when (SqlErrorTranslator.Translate(exception) is { } translated)
            {
                throw translated;
            }
            catch (InvalidCastException)
            {
                return Array.Empty<IReadOnlyDictionary<string, object?>>();
            }
        }

        public ValueTask DisposeAsync()
        {
            _grid.Dispose();
            _connection.Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
