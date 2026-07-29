using System.Reflection;
using AqlanDentalPro.API.Controllers;
using Npgsql;
using Xunit;

namespace AqlanDentalPro.UnitTests.InventoryS4;

/// <summary>
/// QA round 3: the schema-compatibility fallback in Suppliers/Inventory/PurchaseOrders
/// never fired in production because it only inspected ex.InnerException — EF Core
/// surfaces read-query PostgresException directly, and the live Suppliers failure
/// (integer "Type" column vs string HasConversion) throws InvalidCastException.
/// These tests pin the hardened detection across all three controllers.
/// </summary>
public class ReadSchemaCompatibilityFailureTests
{
    private static bool Invoke(Type controller, Exception ex)
    {
        var method = controller.GetMethod(
            "IsReadSchemaCompatibilityFailure",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (bool)method!.Invoke(null, [ex])!;
    }

    private static PostgresException UndefinedColumn() =>
        new("column does not exist", "ERROR", "ERROR", "42703");

    public static TheoryData<Type> Controllers => new()
    {
        typeof(SuppliersController),
        typeof(InventoryController),
        typeof(PurchaseOrdersController),
    };

    [Theory]
    [MemberData(nameof(Controllers))]
    public void DirectPostgresException_IsDetected(Type controller)
    {
        // EF Core read queries surface PostgresException unwrapped.
        Assert.True(Invoke(controller, UndefinedColumn()));
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void WrappedPostgresException_IsDetected(Type controller)
    {
        Assert.True(Invoke(controller, new InvalidOperationException("wrap", UndefinedColumn())));
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void DoublyWrappedPostgresException_IsDetected(Type controller)
    {
        Assert.True(Invoke(controller,
            new InvalidOperationException("outer", new Exception("mid", UndefinedColumn()))));
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void InvalidCastException_EnumTypeDrift_IsDetected(Type controller)
    {
        // Live failure mode: Suppliers."Type" stored as integer while the EF model
        // maps HasConversion<string>() — materialization throws InvalidCastException.
        Assert.True(Invoke(controller, new InvalidCastException("Unable to cast object of type 'System.Int32' to type 'System.String'.")));
        Assert.True(Invoke(controller, new InvalidOperationException("wrap", new InvalidCastException())));
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void UnrelatedExceptions_AreNotDetected(Type controller)
    {
        // Genuine faults must still surface as the Arabic 500, not an empty 200.
        Assert.False(Invoke(controller, new InvalidOperationException("bug")));
        Assert.False(Invoke(controller, new NullReferenceException()));
        Assert.False(Invoke(controller,
            new PostgresException("deadlock detected", "ERROR", "ERROR", "40P01")));
    }
}
