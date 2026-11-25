using System.ComponentModel.DataAnnotations;

namespace MiniErp.Application.Catalog;

/// <summary>Search, filtering, and an allowlisted sort for catalog pages.</summary>
public sealed record CatalogPageQuery(int Page, int PageSize, string? Search, bool? Active, string Sort, bool Descending);

/// <summary>Editable product fields. Include the current version when updating.</summary>
public sealed record ProductInput(
    [property: Required, StringLength(32)] string Sku,
    [property: Required, StringLength(160)] string Name,
    Guid CategoryId, Guid UnitId, decimal PurchasePrice, decimal SalePrice, decimal MinimumStock,
    [property: StringLength(500)] string? Description,
    [property: StringLength(500)] string? ImageUrl,
    bool IsActive, byte[]? Version);

/// <summary>Product detail and references translated to their display names.</summary>
public sealed record ProductResponse(Guid Id, string Sku, string Name, Guid CategoryId, string CategoryName,
    Guid UnitId, string UnitName, decimal PurchasePrice, decimal SalePrice, decimal MinimumStock,
    string? Description, string? ImageUrl, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, byte[] Version);

/// <summary>Editable code, name and active status for catalog reference records.</summary>
public sealed record LookupInput(
    [property: Required, StringLength(32)] string Code,
    [property: Required, StringLength(160)] string Name,
    bool IsActive, byte[]? Version);

public sealed record LookupResponse(Guid Id, string Code, string Name, bool IsActive, byte[] Version);
public sealed record LookupOption(Guid Id, string Name, bool IsActive);

/// <summary>Supplier or customer master data. The URL determines the partner type.</summary>
public sealed record PartnerInput(
    [property: Required, StringLength(32)] string Code,
    [property: Required, StringLength(160)] string Name,
    [property: StringLength(32)] string? Phone,
    [property: EmailAddress, StringLength(254)] string? Email,
    [property: StringLength(250)] string? Address,
    [property: StringLength(32)] string? TaxCode,
    decimal CreditLimit,
    [property: Range(0, 365)] int PaymentTermsDays,
    bool IsActive, byte[]? Version);

public sealed record PartnerResponse(Guid Id, string Code, string Name, string? Phone,
    string? Email, string? Address, string? TaxCode, decimal CreditLimit, int PaymentTermsDays, bool IsActive, byte[] Version);
