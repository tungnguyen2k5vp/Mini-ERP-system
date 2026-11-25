using Microsoft.AspNetCore.Identity;

namespace MiniErp.Infrastructure.Auth;

public sealed class ErpUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public bool IsDisabled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class RefreshSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ErpUser User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public string SecurityStamp { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public byte[] Version { get; set; } = [];
}
