using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace AqlanDentalPro.UnitTests.Lab;

/// <summary>
/// LABINV-REQ-011 — materials consumed for a case are deducted through the owner API and
/// linked to the lab order.
///
/// <para>
/// The behaviours pinned here are the ones that would cost the clinic real money or real
/// stock if they regressed: that the order's own cost columns are never touched, that a
/// request cannot drive a balance negative, and that the link is a queryable column rather
/// than text buried in <c>Reason</c>.
/// </para>
/// </summary>
public class LabOrderConsumablesTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static InventoryController Controller(AppDbContext db) =>
        new(db, NullLogger<InventoryController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                    ], "Test"))
                }
            }
        };

    private static async Task<(LabOrder Order, Inventory Item)> SeedAsync(
        AppDbContext db, int quantity = 10, decimal? costPerUnit = 250m)
    {
        var patient = new Patient
        {
            FirstName = "TEST",
            LastName = "PATIENT",
            PatientNumber = "P-LABINV-001",
            Phone = "770000000",
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var order = new LabOrder
        {
            PatientId = patient.Id,
            OrderNumber = "LAB-2026-011",
            ApplianceType = "crown",
            Cost = 40_000m,
            TotalCost = 40_000m,
            Currency = "YER",
        };
        var item = new Inventory
        {
            Name = "Zirconia block",
            Quantity = quantity,
            MinQuantity = 2,
            Unit = "block",
            CostPerUnit = costPerUnit,
        };
        db.LabOrders.Add(order);
        db.Inventory.Add(item);
        await db.SaveChangesAsync();

        return (order, item);
    }

    private static ConsumeLabOrderInventoryRequest Request(Guid orderId, Guid itemId, int qty) =>
        new()
        {
            LabOrderId = orderId,
            Items = [new ConsumeLabOrderLine { InventoryItemId = itemId, Quantity = qty }],
        };

    [Fact]
    public async Task Consuming_deducts_stock_and_links_the_adjustment_to_the_order()
    {
        using var db = CreateDb();
        var (order, item) = await SeedAsync(db);

        var result = await Controller(db).ConsumeLabOrderInventory(Request(order.Id, item.Id, 3));

        result.Should().BeOfType<OkObjectResult>();

        var adjustment = await db.InventoryAdjustments.SingleAsync();
        adjustment.LabOrderId.Should().Be(order.Id);
        adjustment.AdjustmentType.Should().Be("consumption");
        adjustment.Delta.Should().Be(-3);
        adjustment.PreviousQuantity.Should().Be(10);
        adjustment.NewQuantity.Should().Be(7);

        (await db.Inventory.SingleAsync()).Quantity.Should().Be(7);
    }

    /// <summary>
    /// The requirement that this slice exists to protect. Materials the clinic consumes are
    /// its own cost, not part of what it owes the lab. Adding them to the order would inflate
    /// the supplier bill and the lab-cost deduction inside the doctor's commission at once.
    /// </summary>
    [Fact]
    public async Task Consuming_never_writes_material_cost_into_the_order()
    {
        using var db = CreateDb();
        var (order, item) = await SeedAsync(db);

        await Controller(db).ConsumeLabOrderInventory(Request(order.Id, item.Id, 4));

        var stored = await db.LabOrders.AsNoTracking().SingleAsync();
        stored.TotalCost.Should().Be(40_000m, "material cost is reported beside the order, never inside it");
        stored.Cost.Should().Be(40_000m);
    }

    /// <summary>
    /// The link must be a column. Encoding the order id inside <c>Reason</c> breaks the first
    /// time the wording is edited and cannot be queried — it was explicitly ruled out.
    /// </summary>
    [Fact]
    public async Task The_link_is_a_queryable_column_not_text_in_the_reason()
    {
        using var db = CreateDb();
        var (order, item) = await SeedAsync(db);

        await Controller(db).ConsumeLabOrderInventory(Request(order.Id, item.Id, 1));

        var found = await db.InventoryAdjustments.Where(a => a.LabOrderId == order.Id).ToListAsync();
        found.Should().HaveCount(1);
        found[0].Reason.Should().NotContain(order.Id.ToString());
    }

    [Fact]
    public async Task Consuming_more_than_is_in_stock_is_refused_and_changes_nothing()
    {
        using var db = CreateDb();
        var (order, item) = await SeedAsync(db, quantity: 2);

        var result = await Controller(db).ConsumeLabOrderInventory(Request(order.Id, item.Id, 5));

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Inventory.SingleAsync()).Quantity.Should().Be(2);
        (await db.InventoryAdjustments.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Two lines for the same item are each checked against the full opening stock, so
    /// 6 + 6 against a balance of 8 would pass the sufficiency check and then go negative.
    /// Merging them silently would consume a quantity the user never asked for.
    /// </summary>
    [Fact]
    public async Task The_same_item_twice_in_one_request_is_refused_rather_than_merged()
    {
        using var db = CreateDb();
        var (order, item) = await SeedAsync(db, quantity: 8);

        var result = await Controller(db).ConsumeLabOrderInventory(new ConsumeLabOrderInventoryRequest
        {
            LabOrderId = order.Id,
            Items =
            [
                new ConsumeLabOrderLine { InventoryItemId = item.Id, Quantity = 6 },
                new ConsumeLabOrderLine { InventoryItemId = item.Id, Quantity = 6 },
            ],
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Inventory.SingleAsync()).Quantity.Should().Be(8);
        (await db.InventoryAdjustments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task An_unknown_order_is_refused_in_Arabic_and_consumes_nothing()
    {
        using var db = CreateDb();
        var (_, item) = await SeedAsync(db);

        var result = await Controller(db).ConsumeLabOrderInventory(Request(Guid.NewGuid(), item.Id, 1));

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value!.GetType().GetProperty("message")!.GetValue(notFound.Value)!
            .ToString().Should().Be("أمر المختبر غير موجود");
        (await db.Inventory.SingleAsync()).Quantity.Should().Be(10);
    }

    /// <summary>
    /// A partial deduction is worse than none: stock would be wrong with no record of why.
    /// The second line is unsatisfiable, so the first must not be applied either.
    /// </summary>
    [Fact]
    public async Task One_unsatisfiable_line_prevents_every_line_in_the_request()
    {
        using var db = CreateDb();
        var (order, plenty) = await SeedAsync(db, quantity: 10);

        var scarce = new Inventory { Name = "Porcelain", Quantity = 1, MinQuantity = 0, Unit = "g" };
        db.Inventory.Add(scarce);
        await db.SaveChangesAsync();

        var result = await Controller(db).ConsumeLabOrderInventory(new ConsumeLabOrderInventoryRequest
        {
            LabOrderId = order.Id,
            Items =
            [
                new ConsumeLabOrderLine { InventoryItemId = plenty.Id, Quantity = 2 },
                new ConsumeLabOrderLine { InventoryItemId = scarce.Id, Quantity = 9 },
            ],
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Inventory.SingleAsync(i => i.Id == plenty.Id)).Quantity.Should().Be(10);
        (await db.InventoryAdjustments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task An_item_with_no_unit_cost_still_consumes_and_is_counted_as_unpriced()
    {
        using var db = CreateDb();
        var (order, item) = await SeedAsync(db, costPerUnit: null);

        var result = await Controller(db).ConsumeLabOrderInventory(Request(order.Id, item.Id, 2));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value!.GetType().GetProperty("materialCost")!.GetValue(ok.Value).Should().Be(0m);
        (await db.Inventory.SingleAsync()).Quantity.Should().Be(8);
    }

    [Fact]
    public async Task Material_cost_multiplies_unit_cost_by_the_quantity_consumed()
    {
        using var db = CreateDb();
        var (order, item) = await SeedAsync(db, costPerUnit: 250m);

        var result = await Controller(db).ConsumeLabOrderInventory(Request(order.Id, item.Id, 3));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value!.GetType().GetProperty("materialCost")!.GetValue(ok.Value).Should().Be(750m);
    }

    /// <summary>
    /// Consumption written for one order must not appear against another. Without this the
    /// cost shown beside a case would silently include another case's materials.
    /// </summary>
    [Fact]
    public async Task Consumption_is_scoped_to_its_own_order()
    {
        using var db = CreateDb();
        var (first, item) = await SeedAsync(db, quantity: 10);

        var second = new LabOrder
        {
            PatientId = first.PatientId,
            OrderNumber = "LAB-2026-012",
            ApplianceType = "retainer",
        };
        db.LabOrders.Add(second);
        await db.SaveChangesAsync();

        await Controller(db).ConsumeLabOrderInventory(Request(first.Id, item.Id, 2));

        (await db.InventoryAdjustments.CountAsync(a => a.LabOrderId == first.Id)).Should().Be(1);
        (await db.InventoryAdjustments.CountAsync(a => a.LabOrderId == second.Id)).Should().Be(0);
    }

    /// <summary>
    /// Every adjustment written before this column existed has a null link, and none of them
    /// is wrong for it. The column must stay nullable and unenforced.
    /// </summary>
    [Fact]
    public async Task Adjustments_made_outside_a_lab_order_keep_a_null_link()
    {
        using var db = CreateDb();
        var (_, item) = await SeedAsync(db);

        await Controller(db).AdjustQuantity(item.Id, new AdjustQuantityRequest { Delta = -1 });

        var adjustment = await db.InventoryAdjustments.SingleAsync();
        adjustment.LabOrderId.Should().BeNull();
    }
}
