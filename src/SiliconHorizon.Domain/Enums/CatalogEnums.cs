namespace SiliconHorizon.Domain.Enums;

/// <summary>Categories.SpecType — quyết định bảng thông số con (Specs_*) sẽ được ghi.</summary>
public enum SpecType : byte
{
    /// <summary>Danh mục không có bảng thông số riêng.</summary>
    Generic = 0,

    Cpu = 1,
    Ram = 2,
    Storage = 3,
    Monitor = 4,
    Mainboard = 5,
    Psu = 6
}

/// <summary>Products.Status.</summary>
public enum ProductStatus : byte
{
    Draft = 0,
    Active = 1,
    Discontinued = 2
}

/// <summary>Promotions.DiscountType và Vouchers.DiscountType.</summary>
public enum DiscountType : byte
{
    Percent = 0,
    Amount = 1
}

/// <summary>Promotions.ScopeType — phạm vi áp dụng khuyến mãi.</summary>
public enum PromotionScopeType : byte
{
    Product = 0,
    Brand = 1,
    Category = 2
}

/// <summary>CompatibilityRules.Severity — mức độ cảnh báo khi kiểm tra tương thích (PRD-11).</summary>
public enum CompatibilitySeverity : byte
{
    Info = 0,
    Warning = 1
}
