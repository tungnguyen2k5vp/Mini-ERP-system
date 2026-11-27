namespace MiniErp.Application;

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int Total);

public sealed record RequestActor(Guid? UserId, string CorrelationId);

public sealed record DocumentEvent(string Action, DateTime AtUtc, Guid? ActorId, string? ActorName, string? Reason);
