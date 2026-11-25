using MiniErp.Application.Auth;

namespace MiniErp.UnitTests;

public sealed class PermissionDefaultsTests
{
    [Fact]
    public void Default_roles_only_contain_registered_permissions()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct().Count());
        foreach (var grants in Permissions.DefaultRoles.Values)
        {
            Assert.All(grants, permission => Assert.Contains(permission, Permissions.All));
        }

        Assert.DoesNotContain(Permissions.ApproveSales, Permissions.DefaultRoles["Sales Staff"]);
        Assert.DoesNotContain(Permissions.ApprovePurchasing, Permissions.DefaultRoles["Purchasing Staff"]);
        Assert.DoesNotContain(Permissions.PostInventory, Permissions.DefaultRoles["Viewer"]);
    }
}
