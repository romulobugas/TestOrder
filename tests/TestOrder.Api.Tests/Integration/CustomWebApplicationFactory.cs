using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TestOrder.Api.Tests.Integration;

public sealed class CustomWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        var testSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Test.json");
        var testSettings = new ConfigurationBuilder()
            .AddJsonFile(testSettingsPath, optional: false)
            .Build();

        builder.UseSetting("ConnectionStrings:Default", connectionString);

        foreach (var child in testSettings.GetSection("Seed").GetChildren())
        {
            builder.UseSetting($"Seed:{child.Key}", child.Value);
        }
    }
}
