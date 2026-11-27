namespace MiniErp.Application.Orders;

public sealed record CreditExposure(decimal CreditLimit, decimal Outstanding, decimal Unbilled,
    decimal Used, decimal? Available, decimal OrderAmount, decimal Projected, bool RequiresOverride);
