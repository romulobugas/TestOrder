using Microsoft.Extensions.Configuration;
using Testcontainers.MySql;

namespace TestOrder.Api.Tests.Integration;

public sealed class MySqlContainerFixture : IAsyncLifetime
{
    private MySqlContainer? _container;
    public CustomWebApplicationFactory Factory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!await DockerHelper.IsAvailableAsync())
        {
            throw new InvalidOperationException(
                "Docker is not running or not installed. Integration tests require Docker for Testcontainers.");
        }

        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        var testSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Test.json");
        var testSettings = new ConfigurationBuilder()
            .AddJsonFile(testSettingsPath, optional: false)
            .Build();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");

        foreach (var child in testSettings.GetSection("Seed").GetChildren())
        {
            Environment.SetEnvironmentVariable($"Seed__{child.Key}", child.Value);
        }

        Factory = new CustomWebApplicationFactory(ConnectionString);
        _ = Factory.Server;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
