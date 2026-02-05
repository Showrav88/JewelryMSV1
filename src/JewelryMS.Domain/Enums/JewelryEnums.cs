using NpgsqlTypes; // This requires the NuGet package above

namespace JewelryMS.Domain.Enums;

public enum MetalPurity
{
    [PgName("14K")] P14K,
    [PgName("18K")] P18K,
    [PgName("21K")] P21K,
    [PgName("22K")] P22K,
    [PgName("24K")] P24K,
    [PgName("999")] P999,
    [PgName("925")] P925
}

public enum JewelryCategory
{
    Ring,
    Chain,
    Bracelet,
    Necklace,
    Earrings,
    Pendant,
    Bangle,
    Nosepin,
    Other
}

public enum UserRole
{
    SUPER_ADMIN,
    SHOP_OWNER,
    STAFF
}
public enum MaterialType
{
    Gold,
    Silver,
    Platinum,
    Diamond
}
public enum StockStatus
{
    Available,
    Sold,
    Reserved,
    Out_at_Workshop
}
public enum Payment_type
{
    Cash,           // Must match 'Cash' in Postgres
    Card,           // Must match 'Card'
    bKash,          // Must match 'bKash'
    Bank_Transfer   // Must match 'Bank_Transfer'
}
