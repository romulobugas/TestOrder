using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TestOrder.Api.Data;
using TestOrder.Api.Data.Seed;
using TestOrder.Api.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    });

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

builder.Services.AddDbContext<TestOrderDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0))));

builder.Services.AddTransient(_ => new MySqlConnection(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TestOrderDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(db, builder.Configuration);
    await InventoryUnitsBackfill.RunAsync(db);
}

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
