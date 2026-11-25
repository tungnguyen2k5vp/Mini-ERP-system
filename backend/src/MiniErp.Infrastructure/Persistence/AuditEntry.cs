using MiniErp.Application;

namespace MiniErp.Infrastructure.Persistence;

public sealed class AuditEntry
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string CorrelationId { get; set; } = "";
    public string Result { get; set; } = "";
    public string? Before { get; set; }
    public string? After { get; set; }

    public static AuditEntry Create(RequestActor actor, DateTime utc, string action, string entity,
        string? entityId, string result = "Succeeded", string? before = null, string? after = null) =>
        new()
        {
            UserId = actor.UserId, TimestampUtc = utc, Action = action, Entity = entity, EntityId = entityId,
            CorrelationId = actor.CorrelationId, Result = result, Before = before, After = after
        };
}
