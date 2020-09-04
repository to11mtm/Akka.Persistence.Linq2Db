using Xunit;

namespace Akka.Persistence.Linq2Db.BenchmarkTests.Docker
{
    [CollectionDefinition("SqlServerSpec")]
    public sealed class SqlServerSpecsFixture : ICollectionFixture<SqlServerFixture>
    {
    }
}