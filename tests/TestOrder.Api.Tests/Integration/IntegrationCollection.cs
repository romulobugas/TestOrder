namespace TestOrder.Api.Tests.Integration;

[CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<MySqlContainerFixture>
{
    public const string Name = "Integration";
}
