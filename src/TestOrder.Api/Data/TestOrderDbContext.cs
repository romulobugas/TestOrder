using Microsoft.EntityFrameworkCore;
using TestOrder.Api.Data.Entities;

namespace TestOrder.Api.Data;

public class TestOrderDbContext : DbContext
{
    public TestOrderDbContext(DbContextOptions<TestOrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<InventoryUnit> InventoryUnits => Set<InventoryUnit>();

    public DbSet<OrderReservationUnit> OrderReservationUnits => Set<OrderReservationUnit>();

    public DbSet<OrderProcessingEvent> OrderProcessingEvents => Set<OrderProcessingEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
            entity.Property(e => e.StockQuantity).HasColumnName("stock_quantity").HasDefaultValue(0);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(e => e.CustomerName).HasColumnName("customer_name").HasMaxLength(200);

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_orders_created_at")
                .IsDescending();
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);

            entity.HasIndex(e => e.OrderId).HasDatabaseName("IX_order_items_order_id");
            entity.HasIndex(e => e.ProductId).HasDatabaseName("IX_order_items_product_id");

            entity.HasOne<Order>()
                .WithMany()
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryUnit>(entity =>
        {
            entity.ToTable("inventory_units");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(16).IsRequired();

            entity.HasIndex(e => new { e.ProductId, e.Status, e.Id })
                .HasDatabaseName("IX_inventory_units_product_status_id");

            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderReservationUnit>(entity =>
        {
            entity.ToTable("order_reservation_units");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.InventoryUnitId).HasColumnName("inventory_unit_id");

            entity.HasIndex(e => e.OrderId).HasDatabaseName("IX_order_reservation_units_order_id");
            entity.HasIndex(e => e.InventoryUnitId)
                .HasDatabaseName("UQ_order_reservation_units_inventory_unit_id")
                .IsUnique();

            entity.HasOne<Order>()
                .WithMany()
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<InventoryUnit>()
                .WithMany()
                .HasForeignKey(e => e.InventoryUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderProcessingEvent>(entity =>
        {
            entity.ToTable("order_processing_events");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(e => e.Payload).HasColumnName("payload").HasColumnType("json").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");

            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("IX_order_processing_events_status_created");

            entity.HasIndex(e => e.OrderId).HasDatabaseName("IX_order_processing_events_order_id");

            entity.HasOne<Order>()
                .WithMany()
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
