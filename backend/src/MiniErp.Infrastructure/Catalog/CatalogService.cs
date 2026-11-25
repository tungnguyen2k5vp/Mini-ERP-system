using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Catalog;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Catalog;

public sealed class CatalogService(ErpDbContext db, TimeProvider clock)
{
    public Task<Page<ProductResponse>> ListProductsAsync(CatalogPageQuery filter, Guid? categoryId, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (filter.Active is { } active) query = query.Where(x => x.IsActive == active);
        if (categoryId is { } category) query = query.Where(x => x.CategoryId == category);
        if (filter.Search is { Length: > 0 } search)
        {
            query = query.Where(x => x.Sku.StartsWith(search) || x.Name.Contains(search));
        }
        var ordered = filter.Sort switch
        {
            "sku" => Sort(query, x => x.Sku, filter.Descending),
            "salePrice" => Sort(query, x => x.SalePrice, filter.Descending),
            "minimumStock" => Sort(query, x => x.MinimumStock, filter.Descending),
            _ => Sort(query, x => x.Name, filter.Descending)
        };
        return PageAsync(ordered.ThenBy(x => x.Id).Select(ProductProjection), filter, ct);
    }

    public async Task<ProductResponse> GetProductAsync(Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(x => x.Id == id).Select(ProductProjection).SingleOrDefaultAsync(ct)
        ?? throw ErpException.Missing("product_missing", "Sản phẩm không tồn tại.");

    public async Task<ProductResponse> SaveProductAsync(Guid? id, ProductInput input, RequestActor actor, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var product = id is null ? new Product { CreatedAtUtc = now } : await db.Products.FindAsync([id.Value], ct)
            ?? throw ErpException.Missing("product_missing", "Sản phẩm không tồn tại.");
        if (id is not null) RequireVersion(input.Version, product.Version);

        var references = await db.Lookups.AsNoTracking()
            .Where(x => x.Id == input.CategoryId || x.Id == input.UnitId)
            .Select(x => new { x.Id, x.Kind, x.IsActive }).ToListAsync(ct);
        if (!references.Any(x => x.Id == input.CategoryId && x.Kind == LookupKind.Category &&
                (x.IsActive || product.CategoryId == input.CategoryId)) ||
            !references.Any(x => x.Id == input.UnitId && x.Kind == LookupKind.Unit &&
                (x.IsActive || product.UnitId == input.UnitId)))
        {
            throw ErpException.Invalid("invalid_lookup", "Chọn nhóm hàng và đơn vị tính hợp lệ.");
        }
        var image = CatalogRules.OptionalText(input.ImageUrl);
        if (image is not null && (!Uri.TryCreate(image, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0))
        {
            throw ErpException.Invalid("invalid_image_url", "Đường dẫn ảnh phải dùng HTTPS.");
        }

        var before = id.HasValue ? Snapshot(product.Sku, product.Name, product.IsActive) : null;
        product.Sku = CatalogRules.NormalizeCode(input.Sku);
        product.Name = input.Name.Trim();
        product.CategoryId = input.CategoryId;
        product.UnitId = input.UnitId;
        product.PurchasePrice = CatalogRules.NonNegative(input.PurchasePrice);
        product.SalePrice = CatalogRules.NonNegative(input.SalePrice);
        product.MinimumStock = CatalogRules.NonNegative(input.MinimumStock);
        product.Description = CatalogRules.OptionalText(input.Description);
        product.ImageUrl = image;
        product.IsActive = input.IsActive;
        product.UpdatedAtUtc = now;
        if (id is null) db.Products.Add(product);

        Audit(actor, id is null ? "CreateProduct" : "UpdateProduct", "Product", product.Id, before,
            Snapshot(product.Sku, product.Name, product.IsActive));
        await db.SaveChangesAsync(ct);
        return await GetProductAsync(product.Id, ct);
    }

    public Task<Page<LookupResponse>> ListLookupsAsync(LookupKind kind, CatalogPageQuery filter, CancellationToken ct)
    {
        var query = db.Lookups.AsNoTracking().Where(x => x.Kind == kind);
        if (filter.Active is { } active) query = query.Where(x => x.IsActive == active);
        if (filter.Search is { Length: > 0 } search) query = query.Where(x => x.Code.StartsWith(search) || x.Name.Contains(search));

        var ordered = filter.Sort == "code" ? Sort(query, x => x.Code, filter.Descending) : Sort(query, x => x.Name, filter.Descending);
        return PageAsync(ordered.ThenBy(x => x.Id).Select(x => new LookupResponse(x.Id, x.Code, x.Name, x.IsActive, x.Version)), filter, ct);
    }

    public Task<LookupOption[]> LookupOptionsAsync(LookupKind kind, Guid? includedId, CancellationToken ct) =>
        db.Lookups.Where(x => x.Kind == kind && (x.IsActive || x.Id == includedId))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new LookupOption(x.Id, x.Name, x.IsActive)).ToArrayAsync(ct);

    public async Task<LookupResponse> GetLookupAsync(LookupKind kind, Guid id, CancellationToken ct) =>
        await db.Lookups.Where(x => x.Kind == kind && x.Id == id)
            .Select(x => new LookupResponse(x.Id, x.Code, x.Name, x.IsActive, x.Version)).SingleOrDefaultAsync(ct)
            ?? throw ErpException.Missing("lookup_missing", "Bản ghi danh mục không tồn tại.");

    public async Task<LookupResponse> SaveLookupAsync(LookupKind kind, Guid? id, LookupInput input, RequestActor actor, CancellationToken ct)
    {
        var row = id is null ? new CatalogLookup { Kind = kind } : await db.Lookups
            .SingleOrDefaultAsync(x => x.Kind == kind && x.Id == id, ct)
            ?? throw ErpException.Missing("lookup_missing", "Bản ghi danh mục không tồn tại.");
        if (id is not null) RequireVersion(input.Version, row.Version);

        var before = id.HasValue ? Snapshot(row.Code, row.Name, row.IsActive) : null;
        row.Code = CatalogRules.NormalizeCode(input.Code);
        row.Name = input.Name.Trim();
        row.IsActive = input.IsActive;
        if (id is null) db.Lookups.Add(row);

        Audit(actor, id is null ? "CreateLookup" : "UpdateLookup", kind.ToString(), row.Id, before,
            Snapshot(row.Code, row.Name, row.IsActive));
        await db.SaveChangesAsync(ct);
        return new LookupResponse(row.Id, row.Code, row.Name, row.IsActive, row.Version);
    }

    public Task<Page<PartnerResponse>> ListPartnersAsync(PartnerKind kind, CatalogPageQuery filter, CancellationToken ct)
    {
        var query = db.Partners.AsNoTracking().Where(x => x.Kind == kind);
        if (filter.Active is { } active) query = query.Where(x => x.IsActive == active);
        if (filter.Search is { Length: > 0 } search) query = query.Where(x => x.Code.StartsWith(search) || x.Name.Contains(search));
        var ordered = filter.Sort == "code" ? Sort(query, x => x.Code, filter.Descending) : Sort(query, x => x.Name, filter.Descending);
        return PageAsync(ordered.ThenBy(x => x.Id).Select(PartnerProjection), filter, ct);
    }

    public async Task<PartnerResponse> GetPartnerAsync(PartnerKind kind, Guid id, CancellationToken ct) =>
        await db.Partners.Where(x => x.Kind == kind && x.Id == id).Select(PartnerProjection).SingleOrDefaultAsync(ct)
        ?? throw ErpException.Missing("partner_missing", "Đối tác không tồn tại.");

    public async Task<PartnerResponse> SavePartnerAsync(PartnerKind kind, Guid? id, PartnerInput input, RequestActor actor, CancellationToken ct)
    {
        var row = id is null ? new BusinessPartner { Kind = kind } : await db.Partners
            .SingleOrDefaultAsync(x => x.Kind == kind && x.Id == id, ct)
            ?? throw ErpException.Missing("partner_missing", "Đối tác không tồn tại.");
        if (id is not null) RequireVersion(input.Version, row.Version);

        var before = id.HasValue ? Snapshot(row.Code, row.Name, row.IsActive) : null;
        row.Code = CatalogRules.NormalizeCode(input.Code);
        row.Name = input.Name.Trim();
        row.Phone = CatalogRules.OptionalText(input.Phone);
        row.Email = CatalogRules.OptionalText(input.Email);
        row.Address = CatalogRules.OptionalText(input.Address);
        row.TaxCode = CatalogRules.OptionalText(input.TaxCode);
        if (kind == PartnerKind.Supplier && input.CreditLimit != 0)
        {
            throw ErpException.Invalid("supplier_credit_limit", "Hạn mức chỉ áp dụng cho khách hàng.");
        }
        row.CreditLimit = CatalogRules.NonNegative(input.CreditLimit);
        row.PaymentTermsDays = input.PaymentTermsDays;
        row.IsActive = input.IsActive;
        if (id is null) db.Partners.Add(row);

        Audit(actor, id is null ? "CreatePartner" : "UpdatePartner", kind.ToString(), row.Id, before,
            Snapshot(row.Code, row.Name, row.IsActive));
        await db.SaveChangesAsync(ct);
        return await GetPartnerAsync(kind, row.Id, ct);
    }

    private void Audit(RequestActor actor, string action, string entity, Guid id, string? before, string after) =>
        db.AuditEntries.Add(AuditEntry.Create(actor, clock.GetUtcNow().UtcDateTime, action, entity, id.ToString(),
            before: before, after: after));

    private static string Snapshot(string code, string name, bool active) => $"{code} / {name} / Active={active}";

    private static void RequireVersion(byte[]? requested, byte[] stored)
    {
        if (requested?.Length != 8) throw ErpException.Invalid("missing_version", "Cần gửi phiên bản của bản ghi hiện tại.");
        if (!requested.AsSpan().SequenceEqual(stored))
        {
            throw ErpException.Conflict("stale_version", "Bản ghi đã thay đổi. Tải lại để lấy phiên bản mới.");
        }
    }

    private static IOrderedQueryable<T> Sort<T, TKey>(IQueryable<T> query, Expression<Func<T, TKey>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    private static async Task<Page<T>> PageAsync<T>(IQueryable<T> query, CatalogPageQuery filter, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var page = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return new Page<T>(page, filter.Page, filter.PageSize, total);
    }

    private static readonly Expression<Func<Product, ProductResponse>> ProductProjection = x =>
        new ProductResponse(x.Id, x.Sku, x.Name, x.CategoryId, x.Category.Name, x.UnitId, x.Unit.Name, x.PurchasePrice,
            x.SalePrice, x.MinimumStock, x.Description, x.ImageUrl, x.IsActive, new DateTimeOffset(x.CreatedAtUtc, TimeSpan.Zero),
            new DateTimeOffset(x.UpdatedAtUtc, TimeSpan.Zero), x.Version);

    private static readonly Expression<Func<BusinessPartner, PartnerResponse>> PartnerProjection = x =>
        new PartnerResponse(x.Id, x.Code, x.Name, x.Phone, x.Email, x.Address, x.TaxCode, x.CreditLimit, x.PaymentTermsDays,
            x.IsActive, x.Version);
}
