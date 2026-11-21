using SiliconHorizon.Application.Common.Exceptions;

namespace SiliconHorizon.Application.Common.Validation;

/// <summary>
/// Kiểm tra đầu vào dùng chung cho các use case CRUD bằng EF Core (module Pricing/Admin/Customers).
/// Mục đích: chặn dữ liệu sai <b>trước khi</b> ghi để trả 400 kèm thông điệp tiếng Việt,
/// thay vì để SQL Server ném lỗi CHECK/UNIQUE thô (Database/README.md mục 1.2).
/// </summary>
public static class InputGuard
{
    /// <summary>Bắt buộc có nội dung; cắt khoảng trắng và chặn vượt độ dài cột.</summary>
    public static string RequireText(string? value, int maxLength, string fieldName)
        => OptionalText(value, maxLength, fieldName)
           ?? throw new BadRequestException($"{fieldName} không được để trống.");

    /// <summary>Chuỗi rỗng/khoảng trắng → <c>null</c>; vượt độ dài cột → 400.</summary>
    public static string? OptionalText(string? value, int maxLength, string fieldName)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length > maxLength)
        {
            throw new BadRequestException($"{fieldName} tối đa {maxLength} ký tự.");
        }

        return text;
    }

    /// <summary>Mã (code/sku) — bắt buộc, viết hoa, không chứa khoảng trắng.</summary>
    public static string RequireCode(string? value, int maxLength, string fieldName)
    {
        var code = RequireText(value, maxLength, fieldName).ToUpperInvariant();

        if (code.Any(char.IsWhiteSpace))
        {
            throw new BadRequestException($"{fieldName} không được chứa khoảng trắng.");
        }

        return code;
    }

    /// <summary>Số nguyên phải &gt; 0.</summary>
    public static void EnsurePositive(int value, string fieldName)
    {
        if (value <= 0)
        {
            throw new BadRequestException($"{fieldName} phải là số nguyên dương.");
        }
    }

    /// <summary>Số nguyên dài phải &gt; 0.</summary>
    public static void EnsurePositive(long value, string fieldName)
    {
        if (value <= 0)
        {
            throw new BadRequestException($"{fieldName} phải là số nguyên dương.");
        }
    }

    /// <summary>Số ngày/giờ công việc không được âm.</summary>
    public static void EnsureNotNegative(int value, string fieldName)
    {
        if (value < 0)
        {
            throw new BadRequestException($"{fieldName} không được âm.");
        }
    }

    /// <summary>Số tiền không được âm (khớp CHECK <c>&gt;= 0</c> của các cột tiền).</summary>
    public static void EnsureNotNegative(decimal value, string fieldName)
    {
        if (value < 0)
        {
            throw new BadRequestException($"{fieldName} không được âm.");
        }
    }

    /// <summary>Số tiền phải &gt; 0 (khớp CHECK <c>&gt; 0</c> của DiscountValue).</summary>
    public static void EnsurePositiveAmount(decimal value, string fieldName)
    {
        if (value <= 0)
        {
            throw new BadRequestException($"{fieldName} phải lớn hơn 0.");
        }
    }

    /// <summary>Giá trị nằm trong đoạn [min, max] — dùng cho phần trăm hoa hồng/giảm giá.</summary>
    public static void EnsureWithin(decimal value, decimal min, decimal max, string fieldName)
    {
        if (value < min || value > max)
        {
            throw new BadRequestException($"{fieldName} phải nằm trong khoảng {min} đến {max}.");
        }
    }

    /// <summary>Giá trị enum phải nằm trong tập đã định nghĩa (khớp CHECK của cột tinyint).</summary>
    public static void EnsureDefined<TEnum>(TEnum value, string fieldName)
        where TEnum : struct, Enum
    {
        if (Enum.IsDefined(value))
        {
            return;
        }

        var allowed = Enum.GetValues<TEnum>().Select(item => $"{Convert.ToByte(item)} ({item})");

        throw new BadRequestException(
            $"{fieldName} không hợp lệ. Giá trị cho phép: {string.Join(", ", allowed)}.");
    }

    /// <summary>Khoảng thời gian phải có <paramref name="end"/> sau <paramref name="start"/> (khớp CHECK <c>EndAt &gt; StartAt</c>).</summary>
    public static void EnsureDateRange(DateTime start, DateTime end, string startFieldName, string endFieldName)
    {
        if (start == default || end == default)
        {
            throw new BadRequestException($"{startFieldName} và {endFieldName} là bắt buộc.");
        }

        if (end <= start)
        {
            throw new BadRequestException($"{endFieldName} phải sau {startFieldName}.");
        }
    }

    /// <summary>Khoảng ngày (date) — dùng cho <c>CommissionRules.EffectiveFrom/EffectiveTo</c>.</summary>
    public static void EnsureOptionalDateRange(DateOnly start, DateOnly? end, string startFieldName, string endFieldName)
    {
        if (end.HasValue && end.Value <= start)
        {
            throw new BadRequestException($"{endFieldName} phải sau {startFieldName}.");
        }
    }
}
