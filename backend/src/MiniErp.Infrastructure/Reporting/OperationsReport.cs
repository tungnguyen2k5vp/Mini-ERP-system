using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Reporting;
using MiniErp.Domain;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Reporting;

public sealed class OperationsReport(ErpDbContext db, TimeProvider clock)
{
    public (DateOnly From, DateOnly To) Dates(DateOnly? from, DateOnly? to)
    {
        var end = to ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (end.Year<1753) throw ErpException.Invalid("report_dates", "Ngày báo cáo cần từ năm 1753.");
        var start = from ?? end.AddDays(-29);
        if (start.Year<1753 || end == DateOnly.MaxValue || start > end || end.DayNumber - start.DayNumber > 366)
            throw ErpException.Invalid("report_dates", "Khoảng ngày không hợp lệ hoặc vượt 367 ngày.");
        return (start, end);
    }
    private static object Bounds(DateOnly from, DateOnly to) => new { From = from.ToDateTime(TimeOnly.MinValue), Until = to.AddDays(1).ToDateTime(TimeOnly.MinValue) };

    public async Task<DashboardResponse> DashboardAsync(DateOnly? from, DateOnly? to, IReadOnlySet<string> grants, CancellationToken ct)
    {
        var (start, end) = Dates(from, to);
        var bounds = Bounds(start, end);
        var metrics = new List<DashboardMetric>();
        var alerts = new List<DashboardAlert>();
        StockTrend[] trend = [];
        ProfitTrend[] profit = [];
        TopProduct[] top = [];
        await using var sql = new SqlConnection(db.Database.GetConnectionString());
        async Task<decimal> Scalar(string text) => await sql.ExecuteScalarAsync<decimal>(new CommandDefinition(text, bounds, cancellationToken: ct));
        if (grants.Contains(Permissions.ViewInventory))
        {
            metrics.Add(new("stockQuantity", "Tồn kho cuối kỳ", await Scalar("SELECT COALESCE(SUM(QuantityDelta),0) FROM inventory.Movements WHERE PostedAtUtc<@Until"), "đơn vị", "/ton-kho"));
            trend = (await sql.QueryAsync<StockTrend>(new CommandDefinition("""
                SELECT CAST(PostedAtUtc AS date) Day,SUM(CASE WHEN QuantityDelta>0 THEN QuantityDelta ELSE 0 END) Received,
                SUM(CASE WHEN QuantityDelta<0 THEN -QuantityDelta ELSE 0 END) Issued FROM inventory.Movements
                WHERE PostedAtUtc>=@From AND PostedAtUtc<@Until GROUP BY CAST(PostedAtUtc AS date) ORDER BY Day
                """, bounds, cancellationToken: ct))).ToArray();
            var low = await Scalar("""
                SELECT COUNT(*) FROM (SELECT WarehouseId,ProductId,SUM(QuantityDelta) Qty FROM inventory.Movements
                WHERE PostedAtUtc<@Until GROUP BY WarehouseId,ProductId) b JOIN catalog.Products p ON p.Id=b.ProductId
                WHERE b.Qty<=p.MinimumStock
                """);
            alerts.Add(new("Vị trí tồn thấp", (int)low, "/bao-cao?filter=low"));
        }
        foreach (var kind in new[] { (Grant: Permissions.ViewPurchasing, Kind: 1, Key: "purchaseOrders", Label: "Đơn mua trong kỳ", Href: "/mua-hang"),
            (Grant: Permissions.ViewSales, Kind: 2, Key: "salesOrders", Label: "Đơn bán trong kỳ", Href: "/ban-hang") })
        {
            if (!grants.Contains(kind.Grant)) continue;
            var count = await sql.ExecuteScalarAsync<decimal>(new CommandDefinition("SELECT COUNT(*) FROM orders.Orders WHERE Kind=@Kind AND CreatedAtUtc>=@From AND CreatedAtUtc<@Until",
                new { kind.Kind, From = start.ToDateTime(TimeOnly.MinValue), Until = end.AddDays(1).ToDateTime(TimeOnly.MinValue) }, cancellationToken: ct));
            metrics.Add(new(kind.Key, kind.Label, count, "đơn", kind.Href));
            if(grants.Contains(kind.Kind==1?Permissions.ApprovePurchasing:Permissions.ApproveSales))
            {
                var pending=await sql.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM orders.Orders WHERE Kind=@Kind AND Status=2",new{kind.Kind},cancellationToken:ct));
                alerts.Add(new(kind.Kind==1?"Đơn mua chờ phê duyệt":"Đơn bán chờ phê duyệt",pending,kind.Href+"?status=2"));
            }
        }
        if (grants.Contains(Permissions.ViewFinance))
        {
            foreach (var direction in new[] { (Id: 1, Key: "receivable", Label: "Phải thu cuối kỳ"), (Id: 2, Key: "payable", Label: "Phải trả cuối kỳ") })
            {
                var value = await sql.ExecuteScalarAsync<decimal>(new CommandDefinition("""
                    SELECT COALESCE((SELECT SUM(Amount*CASE WHEN IsCredit=1 THEN -1 ELSE 1 END) FROM finance.Invoices WHERE Direction=@Direction AND IssuedAtUtc<@Until),0)
                    -COALESCE((SELECT SUM(Amount*CASE WHEN IsRefund=1 THEN -1 ELSE 1 END) FROM finance.Payments WHERE Direction=@Direction AND PostedAtUtc<@Until),0)
                    """, new { Direction = direction.Id, Until = end.AddDays(1).ToDateTime(TimeOnly.MinValue) }, cancellationToken: ct));
                metrics.Add(new(direction.Key, direction.Label, value, "VND", "/tai-chinh?direction="+direction.Id));
            }
        }
        if (grants.Contains(Permissions.ViewSensitiveReports) && grants.Contains(Permissions.ViewSales))
        {
            profit = (await sql.QueryAsync<ProfitTrend>(new CommandDefinition("""
                SELECT CAST(IssuedAtUtc AS date) Day,SUM(Amount*CASE WHEN IsCredit=1 THEN -1 ELSE 1 END) Revenue,
                SUM(CostAmount*CASE WHEN IsCredit=1 THEN -1 ELSE 1 END) Cost,
                SUM((Amount-CostAmount)*CASE WHEN IsCredit=1 THEN -1 ELSE 1 END) GrossProfit
                FROM finance.Invoices WHERE Direction=1 AND IssuedAtUtc>=@From AND IssuedAtUtc<@Until
                GROUP BY CAST(IssuedAtUtc AS date) ORDER BY Day
                """, bounds, cancellationToken: ct))).ToArray();
            metrics.Add(new("revenue", "Doanh thu thuần", profit.Sum(x => x.Revenue), "VND", "/tong-quan#financial-report"));
            metrics.Add(new("costOfGoods", "Giá vốn", profit.Sum(x => x.Cost), "VND", "/tong-quan#financial-report"));
            metrics.Add(new("grossProfit", "Lợi nhuận gộp", profit.Sum(x => x.GrossProfit), "VND", "/tong-quan#financial-report"));
            top = (await sql.QueryAsync<TopProduct>(new CommandDefinition("""
                WITH Lines AS (
                SELECT i.Id InvoiceId,m.ProductId,p.Sku,p.Name,-m.QuantityDelta Quantity,
                  ROUND(-m.QuantityDelta*l.UnitPrice,2) LineRevenue,
                  i.Amount*CASE WHEN i.IsCredit=1 THEN -1 ELSE 1 END InvoiceRevenue,
                  ROW_NUMBER() OVER(PARTITION BY i.Id ORDER BY m.ProductId) LineNumber
                FROM inventory.Movements m JOIN orders.Fulfillments f ON f.StockDocumentId=m.DocumentId
                JOIN finance.Invoices i ON i.StockDocumentId=m.DocumentId
                JOIN orders.Orders o ON o.Id=f.OrderId AND o.Kind=2
                JOIN orders.OrderLines l ON l.OrderId=o.Id AND l.ProductId=m.ProductId JOIN catalog.Products p ON p.Id=m.ProductId
                WHERE i.IssuedAtUtc>=@From AND i.IssuedAtUtc<@Until),
                Attributed AS (SELECT *,LineRevenue+CASE WHEN LineNumber=1 THEN InvoiceRevenue-SUM(LineRevenue) OVER(PARTITION BY InvoiceId) ELSE 0 END Revenue FROM Lines)
                SELECT TOP (10) ProductId,Sku,Name,SUM(Quantity) Quantity,SUM(Revenue) Revenue
                FROM Attributed GROUP BY ProductId,Sku,Name
                ORDER BY Revenue DESC,ProductId
                """, bounds, cancellationToken: ct))).ToArray();
        }
        return new(start, end, metrics.ToArray(), trend, top, alerts.ToArray(), profit);
    }

    private const string StockCte = """
        WITH Positions AS (
          SELECT DISTINCT WarehouseId,ProductId FROM inventory.Movements WHERE PostedAtUtc<@Until),
        R AS (SELECT b.WarehouseId,w.Name Warehouse,b.ProductId,p.Sku,p.Name,p.MinimumStock,
          COALESCE(SUM(CASE WHEN m.PostedAtUtc<@From THEN m.QuantityDelta ELSE 0 END),0) Opening,
          COALESCE(SUM(CASE WHEN m.PostedAtUtc>=@From AND m.QuantityDelta>0 THEN m.QuantityDelta ELSE 0 END),0) Received,
          COALESCE(SUM(CASE WHEN m.PostedAtUtc>=@From AND m.QuantityDelta<0 THEN -m.QuantityDelta ELSE 0 END),0) Issued,
          COALESCE(SUM(m.QuantityDelta),0) Closing,MAX(m.PostedAtUtc) LastMovementAtUtc
          FROM Positions b JOIN catalog.Lookups w ON w.Id=b.WarehouseId JOIN catalog.Products p ON p.Id=b.ProductId
          LEFT JOIN inventory.Movements m ON m.WarehouseId=b.WarehouseId AND m.ProductId=b.ProductId AND m.PostedAtUtc<@Until
          WHERE (@WarehouseId IS NULL OR b.WarehouseId=@WarehouseId)
          GROUP BY b.WarehouseId,w.Name,b.ProductId,p.Sku,p.Name,p.MinimumStock),
        F AS (SELECT * FROM R WHERE @Filter='all' OR (@Filter='low' AND Closing<=MinimumStock)
          OR (@Filter='slow' AND Closing>0 AND (LastMovementAtUtc IS NULL OR LastMovementAtUtc<DATEADD(day,-90,@Until))))
        """;

    public async Task<Page<StockReportRow>> StockAsync(DateOnly? from, DateOnly? to, Guid? warehouseId, string? filter, int page, int size, CancellationToken ct)
    {
        var (start, end) = Dates(from, to);
        filter ??= "all";
        if (filter is not ("all" or "slow" or "low")) throw ErpException.Invalid("stock_filter", "Bộ lọc tồn kho không hợp lệ.");
        await using var sql = new SqlConnection(db.Database.GetConnectionString());
        var command = new CommandDefinition(StockCte + "SELECT COUNT(*) FROM F;" + StockCte + "SELECT WarehouseId,Warehouse,ProductId,Sku,Name,Opening,Received,Issued,Closing,LastMovementAtUtc,MinimumStock FROM F ORDER BY Warehouse,Sku,WarehouseId,ProductId OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY;",
            new { From = start.ToDateTime(TimeOnly.MinValue), Until = end.AddDays(1).ToDateTime(TimeOnly.MinValue), WarehouseId = warehouseId, Filter = filter, Offset = (page - 1) * size, Size = size }, cancellationToken: ct);
        using var results = await sql.QueryMultipleAsync(command);
        var count = await results.ReadSingleAsync<int>();
        return new((await results.ReadAsync<StockReportRow>()).AsList(), page, size, count);
    }

    public async Task<SearchHit[]> SearchAsync(string? query, IReadOnlySet<string> grants, CancellationToken ct)
    {
        var text = query?.Trim();
        if (text is null || text.Length < 2 || text.Length > 100) throw ErpException.Invalid("search_query", "Nhập từ 2 đến 100 ký tự.");
        var result = new List<SearchHit>();
        await using var sql = new SqlConnection(db.Database.GetConnectionString());
        var term = "%" + text.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        async Task Add(string statement) => result.AddRange(await sql.QueryAsync<SearchHit>(new CommandDefinition(statement, new { Term = term,OrderId = int.TryParse(text.StartsWith("SO-",StringComparison.OrdinalIgnoreCase) || text.StartsWith("PO-",StringComparison.OrdinalIgnoreCase) ? text[3..] : text,out var orderId) ? orderId : (int?)null }, cancellationToken: ct)));
        if (grants.Contains(Permissions.ViewCatalog)) await Add("SELECT TOP(10) N'product' Kind,Sku+N' · '+Name Label,Name Detail,N'/san-pham/'+LOWER(CONVERT(nvarchar(36),Id)) Href FROM catalog.Products WHERE Sku LIKE @Term OR Name LIKE @Term ORDER BY Sku,Id");
        if (grants.Contains(Permissions.ViewCatalog)) await Add("SELECT TOP(10) CASE WHEN Kind=1 THEN N'customer' ELSE N'supplier' END Kind,Code+N' · '+Name Label,CASE WHEN Kind=1 THEN N'Khách hàng' ELSE N'Nhà cung cấp' END Detail,CASE WHEN Kind=1 THEN N'/khach-hang/' ELSE N'/nha-cung-cap/' END+LOWER(CONVERT(nvarchar(36),Id)) Href FROM catalog.BusinessPartners WHERE Code LIKE @Term OR Name LIKE @Term ORDER BY Name,Id");
        if (grants.Contains(Permissions.ViewSales)) await Add("SELECT TOP(10) N'sales' Kind,N'SO-'+CONVERT(nvarchar(20),o.Id) Label,p.Name Detail,N'/ban-hang/'+CONVERT(nvarchar(20),o.Id) Href FROM orders.Orders o JOIN catalog.BusinessPartners p ON p.Id=o.PartnerId WHERE o.Kind=2 AND (p.Name LIKE @Term OR o.Id=@OrderId OR CONVERT(nvarchar(20),o.Id) LIKE @Term) ORDER BY o.Id DESC");
        if (grants.Contains(Permissions.ViewPurchasing)) await Add("SELECT TOP(10) N'purchase' Kind,N'PO-'+CONVERT(nvarchar(20),o.Id) Label,p.Name Detail,N'/mua-hang/'+CONVERT(nvarchar(20),o.Id) Href FROM orders.Orders o JOIN catalog.BusinessPartners p ON p.Id=o.PartnerId WHERE o.Kind=1 AND (p.Name LIKE @Term OR o.Id=@OrderId OR CONVERT(nvarchar(20),o.Id) LIKE @Term) ORDER BY o.Id DESC");
        if (grants.Contains(Permissions.ViewFinance)) await Add("SELECT TOP(10) N'invoice' Kind,N'Hóa đơn '+CONVERT(nvarchar(20),i.Id)+N' · '+i.OrderNumber Label,p.Name Detail,N'/tai-chinh?direction='+CONVERT(nvarchar(2),i.Direction)+N'&partnerId='+LOWER(CONVERT(nvarchar(36),i.PartnerId))+N'&invoiceId='+CONVERT(nvarchar(20),i.Id) Href FROM finance.Invoices i JOIN catalog.BusinessPartners p ON p.Id=i.PartnerId WHERE p.Name LIKE @Term OR i.OrderNumber LIKE @Term OR CONVERT(nvarchar(20),i.Id) LIKE @Term ORDER BY i.Id DESC");
        if (grants.Contains(Permissions.ViewInventory)) await Add("SELECT TOP(10) N'stock' Kind,N'Phiếu kho '+CONVERT(nvarchar(20),Id) Label,COALESCE(Note,N'') Detail,N'/ton-kho/chung-tu/'+CONVERT(nvarchar(20),Id) Href FROM inventory.Documents WHERE Note LIKE @Term OR CONVERT(nvarchar(20),Id) LIKE @Term ORDER BY Id DESC");
        return result.Take(50).ToArray();
    }

    public async Task<Page<AuditRow>> AuditAsync(DateOnly? from, DateOnly? to, string? search, int page, int size, CancellationToken ct)
    {
        var (start,end) = Dates(from,to);
        await using var sql = new SqlConnection(db.Database.GetConnectionString());
        const string predicate = " FROM audit.AuditLogs a LEFT JOIN auth.Users u ON u.Id=a.UserId WHERE a.TimestampUtc>=@From AND a.TimestampUtc<@Until AND (@Search IS NULL OR CHARINDEX(@Search,a.Action)>0 OR CHARINDEX(@Search,a.Entity)>0 OR CHARINDEX(@Search,a.CorrelationId)>0) ";
        using var result = await sql.QueryMultipleAsync(new CommandDefinition("SELECT COUNT(*)" + predicate + ";SELECT a.Id,a.TimestampUtc,COALESCE(u.DisplayName,N'Hệ thống') Actor,a.Action,a.Entity,a.EntityId,a.Result,a.CorrelationId" + predicate + "ORDER BY a.TimestampUtc DESC,a.Id DESC OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY;",
            new { From=start.ToDateTime(TimeOnly.MinValue), Until=end.AddDays(1).ToDateTime(TimeOnly.MinValue), Search=string.IsNullOrWhiteSpace(search)?null:search.Trim(),Offset=(page-1)*size,Size=size },cancellationToken:ct));
        var total=await result.ReadSingleAsync<int>();
        return new((await result.ReadAsync<AuditRow>()).AsList(),page,size,total);
    }
}
