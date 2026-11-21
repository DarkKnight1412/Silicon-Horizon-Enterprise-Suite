namespace SiliconHorizon.Application.Common.Errors;

/// <summary>
/// Mã lỗi do stored procedure ném ra và ánh xạ sang HTTP (Database/README.md mục 1.2).
/// </summary>
public static class SqlErrorCode
{
    public const int NotFound = 50001;
    public const int InvalidState = 50002;
    public const int InsufficientStock = 50003;
    public const int InvalidData = 50004;
    public const int Duplicate = 50005;
    public const int BusinessRuleViolation = 50006;
    public const int Deadlock = 1205;

    /// <summary>Trả về mã HTTP tương ứng; 0 nghĩa là không phải lỗi nghiệp vụ đã biết.</summary>
    public static int ToHttpStatusCode(int sqlErrorNumber) => sqlErrorNumber switch
    {
        NotFound => 404,
        InvalidState => 409,
        InsufficientStock => 409,
        Duplicate => 409,
        InvalidData => 400,
        BusinessRuleViolation => 422,
        Deadlock => 503,
        _ => 0
    };

    public static bool IsBusinessError(int sqlErrorNumber)
        => sqlErrorNumber is >= NotFound and <= BusinessRuleViolation;
}
