namespace MiniErp.Application.Auth;

public static class Permissions
{
    public const string ClaimType = "erp.permission";
    public const string Administrator = "Administrator";
    public const string ManageUsers = "System.Users";
    public const string ManageRoles = "System.Roles";
    public const string ViewAudit = "System.Audit";
    public const string ViewCatalog = "Catalog.View";
    public const string CreateCatalog = "Catalog.Create";
    public const string UpdateCatalog = "Catalog.Update";
    public const string ExportCatalog = "Catalog.Export";
    public const string ViewInventory = "Inventory.View";
    public const string PostInventory = "Inventory.Post";
    public const string ViewPurchasing = "Purchasing.View";
    public const string CreatePurchasing = "Purchasing.Create";
    public const string ApprovePurchasing = "Purchasing.Approve";
    public const string CancelPurchasing = "Purchasing.Cancel";
    public const string ViewSales = "Sales.View";
    public const string CreateSales = "Sales.Create";
    public const string ApproveSales = "Sales.Approve";
    public const string CancelSales = "Sales.Cancel";
    public const string OverrideCreditLimit = "Sales.OverrideCreditLimit";
    public const string SelfApproveOrders = "Orders.SelfApprove";
    public const string ApproveOrdersAboveLimit = "Orders.ApproveAboveLimit";
    public const string ViewFinance = "Finance.View";
    public const string PostFinance = "Finance.Post";
    public const string ReverseFinance = "Finance.Reverse";
    public const string ChangeCreditLimit = "Finance.ChangeCreditLimit";
    public const string ViewReports = "Reports.View";
    public const string ViewSensitiveReports = "Reports.Sensitive";

    public static readonly IReadOnlyList<string> All =
    [
        ManageUsers, ManageRoles, ViewAudit, ViewCatalog, CreateCatalog, UpdateCatalog, ExportCatalog,
        ViewInventory, PostInventory, ViewPurchasing, CreatePurchasing, ApprovePurchasing, CancelPurchasing,
        ViewSales, CreateSales, ApproveSales, CancelSales, SelfApproveOrders, ApproveOrdersAboveLimit,
        OverrideCreditLimit, ViewFinance, PostFinance, ReverseFinance, ChangeCreditLimit, ViewReports, ViewSensitiveReports
    ];

    public static readonly IReadOnlyDictionary<string, string[]> DefaultRoles = new Dictionary<string, string[]>
    {
        [Administrator] = [],
        ["Manager"] = [ViewCatalog, CreateCatalog, UpdateCatalog, ExportCatalog, ViewInventory, PostInventory,
            ViewPurchasing, CreatePurchasing, ApprovePurchasing, CancelPurchasing, ViewSales, CreateSales,
            ApproveSales, CancelSales, ViewFinance, ViewReports, ViewSensitiveReports],
        ["Sales Staff"] = [ViewCatalog, ViewInventory, ViewSales, CreateSales, ViewReports],
        ["Purchasing Staff"] = [ViewCatalog, ViewInventory, ViewPurchasing, CreatePurchasing, ViewReports],
        ["Warehouse Staff"] = [ViewCatalog, ViewInventory, PostInventory, ViewPurchasing, ViewSales],
        ["Accountant"] = [ViewCatalog, ExportCatalog, ViewSales, ViewPurchasing, ViewFinance, PostFinance,
            ViewReports, ViewSensitiveReports],
        ["Viewer"] = [ViewCatalog, ViewInventory, ViewReports]
    };
}
