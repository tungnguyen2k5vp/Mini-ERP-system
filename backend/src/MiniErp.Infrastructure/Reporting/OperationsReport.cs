using System.Data;
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
        var end = to ?? BusinessCalendar.DateAt(clock.GetUtcNow().UtcDateTime);
        if (end.Year<1753) throw ErpException.Invalid("report_dates", "Ngày báo cáo cần từ năm 1753.");
        var start = from ?? end.AddDays(-29);
        if (start.Year<1753 || end == DateOnly.MaxValue || start > end || end.DayNumber - start.DayNumber > 366)
            throw ErpException.Invalid("report_dates", "Khoảng ngày không hợp lệ hoặc vượt 367 ngày.");
        return (start, end);
    }

    private static DynamicParameters Bounds(DateOnly start, DateOnly end, object? filters = null)
    {
        var parameters = new DynamicParameters(filters);
        parameters.Add("From", BusinessCalendar.StartUtc(start), DbType.DateTime2);
        parameters.Add("Until", BusinessCalendar.StartUtc(end.AddDays(1)), DbType.DateTime2);
        return parameters;
    }

    public async Task<DashboardResponse> DashboardAsync(DateOnly? from, DateOnly? to, IReadOnlySet<string> grants, CancellationToken ct)
    {
        var (start, end) = Dates(from, to);
        var today = BusinessCalendar.DateAt(clock.GetUtcNow().UtcDateTime);
        var bounds = Bounds(start, end, new { BusinessCalendar.OffsetHours });
        var metrics = new List<DashboardMetric>();
        var alerts = new List<DashboardAlert>();
        StockTrend[] trend = [];
        ProfitTrend[] profit = [];
        TopProduct[] top = [];
        await using var sql = new SqlConnection(db.Database.GetConnectionString());
        await sql.OpenAsync(ct);
        await using var read = await sql.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        async Task<decimal> Scalar(string text) => await sql.ExecuteScalarAsync<decimal>(new CommandDefinition(text, bounds, read, cancellationToken: ct));
        if (grants.Contains(Permissions.ViewInventory))
        {
            metrics.Add(new("stockSkuCount", "SKU còn tồn cuối kỳ", await Scalar("""
                SELECT COUNT(*) FROM (SELECT ProductId FROM inventory.Movements
                WHERE PostedAtUtc<@Until GROUP BY ProductId HAVING SUM(QuantityDelta)>0) s
                """), "SKU", $"/bao-cao?from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}"));
            trend = (await sql.QueryAsync<StockTrend>(new CommandDefinition("""
                SELECT CAST(DATEADD(hour,@OffsetHours,PostedAtUtc) AS date) Day,
                    COUNT(CASE WHEN Kind=1 THEN 1 END) ReceiptCount,COUNT(CASE WHEN Kind=2 THEN 1 END) IssueCount
                FROM inventory.Documents WHERE Kind IN (1,2) AND PostedAtUtc>=@From AND PostedAtUtc<@Until
                GROUP BY CAST(DATEADD(hour,@OffsetHours,PostedAtUtc) AS date) ORDER BY Day
                """, bounds, read, cancellationToken: ct))).ToArray();
            var low = await Scalar("""
                SELECT COUNT(*) FROM (SELECT WarehouseId,ProductId,SUM(QuantityDelta) Qty FROM inventory.Movements
                WHERE PostedAtUtc<@Until GROUP BY WarehouseId,ProductId) b JOIN catalog.Products p ON p.Id=b.ProductId
                WHERE b.Qty<=p.MinimumStock
                """);
            alerts.Add(new("Vị trí tồn thấp cuối kỳ", (int)low, $"/bao-cao?filter=low&from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}"));
        }
        foreach (var kind in new[] { (Grant: Permissions.ViewPurchasing, Kind: 1, Key: "purchaseOrders", Label: "Đơn mua trong kỳ", Href: "/mua-hang"),
            (Grant: Permissions.ViewSales, Kind: 2, Key: "salesOrders", Label: "Đơn bán trong kỳ", Href: "/ban-hang") })
        {
            if (!grants.Contains(kind.Grant)) continue;
            var count = await sql.ExecuteScalarAsync<decimal>(new CommandDefinition("SELECT COUNT(*) FROM orders.Orders WHERE Kind=@Kind AND CreatedAtUtc>=@From AND CreatedAtUtc<@Until",
                Bounds(start, end, new { kind.Kind }), read, cancellationToken: ct));
            metrics.Add(new(kind.Key, kind.Label, count, "đơn", kind.Href));
            var overdue = await sql.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM orders.Orders
                WHERE Kind=@Kind AND Status IN (3,4) AND ExpectedDate < @Today
                """, new { kind.Kind, Today = today.ToDateTime(TimeOnly.MinValue) }, read, cancellationToken: ct));
            alerts.Add(new(kind.Kind == 1 ? "Đơn mua trễ ngày nhận" : "Đơn bán trễ ngày giao", overdue, kind.Href + "?overdue=true"));
            if(grants.Contains(kind.Kind==1?Permissions.ApprovePurchasing:Permissions.ApproveSales))
            {
                var pending=await sql.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM orders.Orders WHERE Kind=@Kind AND Status=2",new{kind.Kind},read,cancellationToken:ct));
                alerts.Add(new(kind.Kind==1?"Đơn mua chờ phê duyệt":"Đơn bán chờ phê duyệt",pending,kind.Href+"?status=2"));
            }
        }
        if (grants.Contains(Permissions.ViewFinance))
        {
            foreach (var direction in new[] { (Id: 1, Key: "receivable", Label: "Phải thu cuối kỳ"), (Id: 2, Key: "payable", Label: "Phải trả cuối kỳ") })
            {
                var value = await sql.ExecuteScalarAsync<decimal>(new CommandDefinition("""
                    SELECT COALESCE((SELECT CAST(SUM(Amount*CASE WHEN IsCredit=1 THEN -1 ELSE 1 END) AS decimal(28,2)) FROM finance.Invoices WHERE Direction=@Direction AND IssuedAtUtc<@Until),0)
                    -COALESCE((SELECT CAST(SUM(Amount*CASE WHEN IsRefund=1 THEN -1 ELSE 1 END) AS decimal(28,2)) FROM finance.Payments WHERE Direction=@Direction AND PostedAtUtc<@Until),0)
                    """, Bounds(start, end, new { Direction = direction.Id }), read, cancellationToken: ct));
                metrics.Add(new(direction.Key, direction.Label, value, "VND", $"/tai-chinh?direction={direction.Id}&tab=aging&asOfDate={end:yyyy-MM-dd}"));
            }
        }
        if (grants.Contains(Permissions.ViewSensitiveReports) && grants.Contains(Permissions.ViewSales))
        {
            profit = (await sql.QueryAsync<ProfitTrend>(new CommandDefinition("""
                SELECT CAST(DATEADD(hour,@OffsetHours,IssuedAtUtc) AS date) Day,SUM(Amount*CASE WHEN IsCredit=1 THEN -1 ELSE 1 END) Revenue,
                SUM(CostAmount*CASE WHEN IsCredit=1 THEN -1 ELSE 1 END) Cost,
                SUM((Amount-CostAmount)*CASE WHEN IsCredit=1 THEN -1 ELSE 1 END) GrossProfit
                FROM finance.Invoices WHERE Direction=1 AND IssuedAtUtc>=@From AND IssuedAtUtc<@Until
                GROUP BY CAST(DATEADD(hour,@OffsetHours,IssuedAtUtc) AS date) ORDER BY Day
                """, bounds, read, cancellationToken: ct))).ToArray();
            metrics.Add(new("revenue", "Doanh thu thuần", profit.Sum(x => x.Revenue), "VND", "/tong-quan#financial-report"));
            metrics.Add(new("costOfGoods", "Giá vốn", profit.Sum(x => x.Cost), "VND", "/tong-quan#financial-report"));
            metrics.Add(new("grossProfit", "Lợi nhuận gộp", profit.Sum(x => x.GrossProfit), "VND", "/tong-quan#financial-report"));
            // Apply the same cumulative rounding as fulfillment. A reversal copies its source line's cents.
            top = (await sql.QueryAsync<TopProduct>(new CommandDefinition("""
                WITH PeriodInvoices AS (
                    SELECT StockDocumentId,OrderId,IsCredit FROM finance.Invoices
                    WHERE Direction=1 AND IssuedAtUtc>=@From AND IssuedAtUtc<@Until
                ), History AS (
                    SELECT f.Id,f.ReversesId,f.StockDocumentId,d.ProductId,d.Quantity,l.UnitPrice,
                        SUM(CASE WHEN f.ReversesId IS NULL THEN d.Quantity ELSE -d.Quantity END) OVER (
                            PARTITION BY f.OrderId,d.ProductId,COALESCE(original.IsReturn,f.IsReturn)
                            ORDER BY f.Id ROWS UNBOUNDED PRECEDING) Cumulative
                    FROM orders.Fulfillments f
                    JOIN inventory.DocumentLines d ON d.DocumentId=f.StockDocumentId
                    JOIN orders.OrderLines l ON l.OrderId=f.OrderId AND l.ProductId=d.ProductId
                    LEFT JOIN orders.Fulfillments original ON original.Id=f.ReversesId
                    WHERE EXISTS (SELECT 1 FROM PeriodInvoices i WHERE i.OrderId=f.OrderId)
                ), Priced AS (
                    SELECT Id,ProductId,
                        ROUND(CAST(Cumulative AS decimal(18,4))*UnitPrice,2)
                        - ROUND(CAST(CAST(Cumulative AS decimal(19,4))-Quantity AS decimal(18,4))*UnitPrice,2) Amount
                    FROM History WHERE ReversesId IS NULL
                )
                SELECT TOP (10) h.ProductId,p.Sku,p.Name,
                    SUM(CASE WHEN i.IsCredit=1 THEN -h.Quantity ELSE h.Quantity END) Quantity,
                    SUM(CASE WHEN i.IsCredit=1 THEN -s.Amount ELSE s.Amount END) Revenue
                FROM PeriodInvoices i JOIN History h ON h.StockDocumentId=i.StockDocumentId
                JOIN Priced s ON s.Id=COALESCE(h.ReversesId,h.Id) AND s.ProductId=h.ProductId
                JOIN catalog.Products p ON p.Id=h.ProductId
                GROUP BY h.ProductId,p.Sku,p.Name ORDER BY Revenue DESC,h.ProductId
                """, bounds, read, cancellationToken: ct))).ToArray();
        }
        await read.CommitAsync(ct);
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
        await sql.OpenAsync(ct);
        await using var read = await sql.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var command = new CommandDefinition(StockCte + """
            SELECT WarehouseId,Warehouse,ProductId,Sku,Name,Opening,Received,Issued,Closing,LastMovementAtUtc,MinimumStock
            INTO #StockReport FROM F;
            SELECT COUNT(*) FROM #StockReport;
            SELECT * FROM #StockReport ORDER BY Warehouse,Sku,WarehouseId,ProductId OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY;
            """,
            Bounds(start, end, new { WarehouseId = warehouseId, Filter = filter, Offset = (page - 1) * size, Size = size }), read, cancellationToken: ct);
        using var results = await sql.QueryMultipleAsync(command);
        var count = await results.ReadSingleAsync<int>();
        var rows = (await results.ReadAsync<StockReportRow>()).AsList();
        await read.CommitAsync(ct);
        return new(rows, page, size, count);
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
        await sql.OpenAsync(ct);
        await using var read = await sql.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        const string predicate = " FROM audit.AuditLogs a LEFT JOIN auth.Users u ON u.Id=a.UserId WHERE a.TimestampUtc>=@From AND a.TimestampUtc<@Until AND (@Search IS NULL OR CHARINDEX(@Search,a.Action)>0 OR CHARINDEX(@Search,a.Entity)>0 OR CHARINDEX(@Search,a.CorrelationId)>0) ";
        using var result = await sql.QueryMultipleAsync(new CommandDefinition("SELECT COUNT(*)" + predicate + ";SELECT a.Id,a.TimestampUtc,COALESCE(u.DisplayName,N'Hệ thống') Actor,a.Action,a.Entity,a.EntityId,a.Result,a.CorrelationId" + predicate + "ORDER BY a.TimestampUtc DESC,a.Id DESC OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY;",
            Bounds(start,end,new { Search=string.IsNullOrWhiteSpace(search)?null:search.Trim(),Offset=(page-1)*size,Size=size }),read,cancellationToken:ct));
        var total=await result.ReadSingleAsync<int>();
        var rows=(await result.ReadAsync<AuditRow>()).AsList();
        await read.CommitAsync(ct);
        return new(rows,page,size,total);
    }
}
