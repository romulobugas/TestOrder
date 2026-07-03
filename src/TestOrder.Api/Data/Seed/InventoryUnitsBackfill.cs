using Microsoft.EntityFrameworkCore;
using TestOrder.Api.Data.Entities;

namespace TestOrder.Api.Data.Seed;

public static class InventoryUnitsBackfill
{
    private const int BatchSize = 5000;
    public const string AvailableStatus = "available";

    public static async Task RunAsync(
        TestOrderDbContext db,
        CancellationToken cancellationToken = default)
    {
        if (await db.InventoryUnits.AnyAsync(cancellationToken))
        {
            return;
        }

        var products = await db.Products
            .AsNoTracking()
            .Select(p => new { p.Id, p.StockQuantity })
            .ToListAsync(cancellationToken);

        var batch = new List<InventoryUnit>(BatchSize);

        foreach (var product in products)
        {
            for (var i = 0; i < product.StockQuantity; i++)
            {
                batch.Add(new InventoryUnit
                {
                    ProductId = product.Id,
                    Status = AvailableStatus
                });

                if (batch.Count >= BatchSize)
                {
                    await FlushBatchAsync(db, batch, cancellationToken);
                }
            }
        }

        if (batch.Count > 0)
        {
            await FlushBatchAsync(db, batch, cancellationToken);
        }
    }

    private static async Task FlushBatchAsync(
        TestOrderDbContext db,
        List<InventoryUnit> batch,
        CancellationToken cancellationToken)
    {
        db.InventoryUnits.AddRange(batch);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        batch.Clear();
    }
}
