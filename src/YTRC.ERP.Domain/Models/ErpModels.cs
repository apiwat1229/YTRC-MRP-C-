using System.Globalization;

namespace YTRC.ERP.Domain.Models;

public enum DepartmentId
{
    Home, Purchase, Receiving, Production, QA, Warehouse, Maintenance, IT,
    RRC, Shipping,
    Settings, UserManagement, RolesPermissions, StaffDirectory, HardwareScales
}

public enum UserRole { Guest, Operator, Supervisor, Manager, SystemAdmin }
public enum PermissionLevel { View, Create, Edit, Approve, Delete, Admin }

public sealed record DepartmentInfo(
    DepartmentId Id, string Code, string Name, string Description, string Accent,
    IReadOnlyList<string> Workflows, IReadOnlyList<string> ApiEndpoints);

public static class DepartmentCatalog
{
    public static readonly IReadOnlyList<DepartmentInfo> All =
    [
        new(DepartmentId.Purchase, "PO", "Purchasing", "Requisitions, purchase orders and suppliers", "#1D8270",
            ["Requisitions", "Purchase orders", "Vendor management"], ["/api/orders", "/api/suppliers", "/api/contracts"]),
        new(DepartmentId.Receiving, "RCV", "Receiving", "Inbound deliveries, inspections and receiving records", "#2F6FAE",
            ["Inbound deliveries", "Receiving records", "Receipt inspection"], ["/api/receive-records"]),
        new(DepartmentId.Production, "PR", "Production", "Work orders, batch tracking and yield analysis", "#458452",
            ["Work orders", "Batch tracking", "Yield analysis"], ["/api/production-plans", "/api/job-orders", "/api/kanban"]),
        new(DepartmentId.QA, "QA", "Quality Assurance", "Lab testing, inspections and non-conformance", "#297691",
            ["Lab testing", "Inspection", "Non-conformance"], ["/api/cpk-analyses", "/api/pools", "/api/cpk-grades"]),
        new(DepartmentId.Warehouse, "WH", "Warehouse & Inventory", "Stock control, receiving and transfers", "#BE812B",
            ["Stock control", "Finished goods storage", "Dispatch & shipping"], ["/api/uss-warehouse", "/api/pallet-types"]),
        new(DepartmentId.Maintenance, "MNT", "Maintenance", "Equipment service requests and maintenance coordination", "#805BA8",
            ["Maintenance requests", "Machine coordination", "Service history"], ["/api/maintenance", "/api/mymachine"]),
        new(DepartmentId.IT, "IT", "IT", "IT service requests and support coordination", "#526F91",
            ["IT tickets", "Support requests", "Service status"], ["/api/it-tickets"])
    ];

    public static DepartmentInfo Get(DepartmentId id) => All.FirstOrDefault(d => d.Id == id) ?? id switch
    {
        DepartmentId.RRC => new(id, "RRC", "Review & Coordination", "Legacy review and coordination workspace", "#805BA8",
            ["Review queue", "Quality reviews", "Coordination"], ["/api/maintenance", "/api/mymachine", "/api/it-tickets"]),
        DepartmentId.Shipping => new(id, "SH", "Shipping", "Dispatch planning and delivery tracking", "#2F6FAE",
            ["Shipping plans", "Delivery bookings", "Pallet availability"], ["/api/shipping-plans", "/api/bookings"]),
        _ => new(id, "SET", "Settings & System", "System utilities and maintenance", "#64748B",
            ["User accounts", "Roles & permissions", "Staff directory"], ["/api/users", "/api/roles", "/api/staff"])
    };
}

public sealed class UserProfile
{
    public string UserId { get; init; } = "";
    public string Username { get; init; } = "guest";
    public string FullName { get; init; } = "Guest User";
    public string Email { get; init; } = "";
    public string DepartmentName { get; init; } = "";
    public DepartmentId PrimaryDepartment { get; init; } = DepartmentId.Home;
    public UserRole Role { get; init; } = UserRole.Guest;
    public IReadOnlyList<string> RawPermissions { get; init; } = [];
    public IReadOnlyList<DepartmentGrant> DepartmentPermissions { get; init; } = [];
    public string RoleName => Role switch
    {
        UserRole.SystemAdmin => "System Administrator",
        UserRole.Manager => "Department Manager",
        UserRole.Supervisor => "Supervisor",
        UserRole.Operator => "Operator",
        _ => "Guest"
    };

    public bool HasDepartmentAccess(DepartmentId department, PermissionLevel level = PermissionLevel.View)
    {
        if (Role == UserRole.SystemAdmin || department is DepartmentId.Home or DepartmentId.Settings)
            return true;
        if (PrimaryDepartment == department)
            return true;
        if (DepartmentPermissions.Any(g => g.Department == department && g.Level >= level))
            return true;

        var keys = department switch
        {
            DepartmentId.QA => new[] { "cpk", "pools", "qa" },
            DepartmentId.Purchase => new[] { "orders", "suppliers", "purchase", "contracts" },
            DepartmentId.Receiving => new[] { "receive_records", "receiving" },
            DepartmentId.Warehouse => new[] { "warehouse", "pallet" },
            DepartmentId.Production => new[] { "production", "job_orders", "kanban" },
            DepartmentId.Shipping => new[] { "shipping", "bookings" },
            DepartmentId.Maintenance => new[] { "maintenance", "mymachine" },
            DepartmentId.IT => new[] { "it_", "it-ticket", "information technology" },
            DepartmentId.RRC => new[] { "maintenance", "it_", "mymachine" },
            _ => Array.Empty<string>()
        };
        return RawPermissions.Any(p => p == "*" || keys.Any(k => p.Contains(k, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed record DepartmentGrant(DepartmentId Department, PermissionLevel Level);
public sealed record ScaleReading(double Weight, int DecimalPlaces, bool IsStable, string Unit, string RawData)
{
    public string Formatted => Weight.ToString($"F{Math.Clamp(DecimalPlaces, 0, 6)}", CultureInfo.InvariantCulture);
}
