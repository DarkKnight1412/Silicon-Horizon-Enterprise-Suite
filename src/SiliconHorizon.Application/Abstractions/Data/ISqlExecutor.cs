namespace SiliconHorizon.Application.Abstractions.Data;

/// <summary>
/// Thực thi stored procedure bằng Dapper. Mọi module giao dịch (Orders, Cart, Inventory, Serial,
/// Warranty, Returns, Reports...) gọi qua interface này thay vì mở kết nối trực tiếp.
/// <para>
/// Implementation tự: mở kết nối, truyền IP/User-Agent vào <c>sp_set_session_context</c> cho nhật ký,
/// chạy proc với <c>CommandType.StoredProcedure</c>, và dịch lỗi 5000x thành exception nghiệp vụ.
/// </para>
/// </summary>
public interface ISqlExecutor
{
    /// <summary>Thực thi proc không trả dữ liệu; trả về số dòng bị ảnh hưởng.</summary>
    Task<int> ExecuteAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>Thực thi proc và ánh xạ tập kết quả đầu tiên sang danh sách DTO.</summary>
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>Thực thi proc và lấy bản ghi đầu tiên (null nếu không có).</summary>
    Task<T?> QuerySingleOrDefaultAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>Thực thi proc và lấy giá trị vô hướng (COUNT, Id vừa tạo...).</summary>
    Task<T?> ExecuteScalarAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thực thi proc trả về nhiều result set, đọc tuần tự theo đúng thứ tự đã ghi trong tài liệu
    /// (ví dụ <c>usp_Cart_Get</c> trả 3 set; <c>usp_Auth_GetUserByUsername</c> trả 5 set).
    /// </summary>
    Task<ISqlMultiResult> QueryMultipleAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Đọc lần lượt các result set của một stored procedure. Không lộ kiểu dữ liệu của Dapper ra tầng Application.
/// </summary>
public interface ISqlMultiResult : IAsyncDisposable
{
    /// <summary>Đọc result set tiếp theo thành danh sách.</summary>
    Task<IReadOnlyList<T>> ReadListAsync<T>(CancellationToken cancellationToken = default);

    /// <summary>Đọc result set tiếp theo và lấy bản ghi đầu tiên.</summary>
    Task<T?> ReadFirstOrDefaultAsync<T>(CancellationToken cancellationToken = default);

    /// <summary>Đọc result set tiếp theo và bắt buộc phải có dữ liệu (ném <c>NotFoundException</c> nếu rỗng).</summary>
    Task<T> ReadSingleAsync<T>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc result set tiếp theo dưới dạng từ điển "tên cột → giá trị".
    /// Dùng cho result set có cột THAY ĐỔI theo dữ liệu — ví dụ thông số kỹ thuật trong
    /// <c>usp_Product_GetById</c> (bảng Specs_CPU, Specs_RAM, Specs_Monitor... khác cột nhau).
    /// </summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadDictionaryListAsync(
        CancellationToken cancellationToken = default);
}
