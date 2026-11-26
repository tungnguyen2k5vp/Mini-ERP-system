namespace MiniErp.Application.Reporting;

public sealed record DashboardMetric(string Key, string Label, decimal Value, string Unit, string Href);
public sealed record StockTrend(DateTime Day, decimal Received, decimal Issued);
public sealed record ProfitTrend(DateTime Day, decimal Revenue, decimal Cost, decimal GrossProfit);
public sealed record TopProduct(Guid ProductId, string Sku, string Name, decimal Quantity, decimal Revenue);
public sealed record DashboardAlert(string Label, int Count, string Href);
public sealed record DashboardResponse(DateOnly From, DateOnly To, DashboardMetric[] Metrics, StockTrend[] Trend,
    TopProduct[] TopProducts, DashboardAlert[] Alerts, ProfitTrend[] ProfitTrend);
public sealed record StockReportRow(Guid WarehouseId, string Warehouse, Guid ProductId, string Sku, string Name,
    decimal Opening, decimal Received, decimal Issued, decimal Closing, DateTime? LastMovementAtUtc, decimal MinimumStock);
public sealed record SearchHit(string Kind, string Label, string Detail, string Href);
public sealed record AuditRow(long Id, DateTime TimestampUtc, string Actor, string Action, string Entity,
    string? EntityId, string Result, string CorrelationId);
