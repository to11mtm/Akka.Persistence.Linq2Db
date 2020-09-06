using Xunit;

namespace Akka.Persistence.Linq2Db.BenchmarkTests.Docker
{
    [CollectionDefinition("PostgreSQLSpec")]
    public sealed class PostgreSQLSpecsFixture : ICollectionFixture<PostgreSQLFixture>
    {
    }
}