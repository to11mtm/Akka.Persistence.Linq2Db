using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class SqlServerSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec
    {
        
        public SqlServerSqlCommonSnapshotCompatibilitySpec(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override string OldSnapshot =>
            "akka.persistence.snapshot-store.sql-server";

        protected override string NewSnapshot =>
            "akka.persistence.snapshot-store.linq2db";

        protected override Configuration.Config Config =>
            SqlServerCompatibilitySpecConfig.InitSnapshotConfig("snapshot_compat");
    }
}