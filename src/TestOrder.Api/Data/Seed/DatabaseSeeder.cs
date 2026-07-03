using Microsoft.EntityFrameworkCore;
using TestOrder.Api.Data.Entities;

namespace TestOrder.Api.Data.Seed;

public static class DatabaseSeeder
{
    private static readonly DateTime ReferenceDate = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int OrderBatchSize = 500;

    public static async Task SeedAsync(
        TestOrderDbContext context,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (await context.Orders.AnyAsync(cancellationToken))
        {
            return;
        }

        var random = new Random(42);
        var productCount = configuration.GetValue("Seed:ProductCount", 50);
        var orderCount = configuration.GetValue("Seed:OrderCount", 5000);
        var minItemsPerOrder = configuration.GetValue("Seed:MinItemsPerOrder", 2);
        var maxItemsPerOrder = configuration.GetValue("Seed:MaxItemsPerOrder", 5);

        var products = CreateProducts(productCount, random);
        context.Products.AddRange(products);
        await context.SaveChangesAsync(cancellationToken);

        for (var created = 0; created < orderCount; created += OrderBatchSize)
        {
            var batchCount = Math.Min(OrderBatchSize, orderCount - created);
            var orders = new List<Order>(batchCount);

            for (var i = 0; i < batchCount; i++)
            {
                orders.Add(new Order
                {
                    CreatedAt = ReferenceDate.AddDays(-random.Next(0, 365)),
                    Status = random.Next(100) < 10 ? "processed" : "created"
                });
            }

            context.Orders.AddRange(orders);
            await context.SaveChangesAsync(cancellationToken);

            var items = new List<OrderItem>();
            foreach (var order in orders)
            {
                var itemCount = random.Next(minItemsPerOrder, maxItemsPerOrder + 1);
                var productIndexes = PickDistinctProductIndexes(productCount, itemCount, random);

                foreach (var productIndex in productIndexes)
                {
                    var product = products[productIndex];
                    items.Add(new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = product.Id,
                        Quantity = random.Next(1, 4),
                        UnitPrice = product.UnitPrice
                    });
                }
            }

            context.OrderItems.AddRange(items);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static List<Product> CreateProducts(int productCount, Random random)
    {
        var products = new List<Product>(productCount);

        for (var i = 0; i < productCount; i++)
        {
            products.Add(new Product
            {
                Name = $"Produto {i + 1:D2}",
                UnitPrice = Math.Round(10m + (i * 7.3m % 500), 2),
                StockQuantity = random.Next(1000, 10001)
            });
        }

        return products;
    }

    private static List<int> PickDistinctProductIndexes(int productCount, int itemCount, Random random)
    {
        var indexes = Enumerable.Range(0, productCount).OrderBy(_ => random.Next()).Take(itemCount).ToList();
        return indexes;
    }
}
