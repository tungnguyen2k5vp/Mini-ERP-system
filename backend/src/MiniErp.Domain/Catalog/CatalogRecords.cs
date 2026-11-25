namespace MiniErp.Domain.Catalog;

public enum LookupKind : byte { Category = 1, Unit = 2, Warehouse = 3, PaymentMethod = 4 }
public enum PartnerKind : byte { Customer = 1, Supplier = 2 }

public sealed class CatalogLookup
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public LookupKind Kind { get; init; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public byte[] Version { get; set; } = [];
}

public sealed class Product
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public LookupKind CategoryKind { get; private set; } = LookupKind.Category;
    public Guid CategoryId { get; set; }
    public CatalogLookup Category { get; set; } = null!;
    public LookupKind UnitKind { get; private set; } = LookupKind.Unit;
    public Guid UnitId { get; set; }
    public CatalogLookup Unit { get; set; } = null!;
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal MinimumStock { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class BusinessPartner
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public PartnerKind Kind { get; init; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public decimal CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] Version { get; set; } = [];
}

public static class CatalogRules
{
    public static string NormalizeCode(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length is < 2 or > 32 ||
            normalized.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.')))
        {
            throw ErpException.Invalid("invalid_code", "Mã cần từ 2 đến 32 ký tự chữ, số, dấu gạch hoặc dấu chấm.");
        }
        return normalized;
    }

    public static decimal NonNegative(decimal value)
    {
        if (value < 0 || value > 999_999_999_999m || decimal.Round(value, 4) != value)
        {
            throw ErpException.Invalid("invalid_decimal", "Giá trị ngoài phạm vi cho phép hoặc có quá 4 chữ số thập phân.");
        }
        return value;
    }

    public static string? OptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
