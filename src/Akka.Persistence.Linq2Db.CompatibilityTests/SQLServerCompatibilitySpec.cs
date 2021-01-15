﻿using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class SQLServerJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec
    {
        public SQLServerJournalCompatibilitySpec(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override string OldJournal =>
            "akka.persistence.journal.sql-server";

        protected override string NewJournal =>
            "akka.persistence.journal.testspec";

        protected override Configuration.Config Config =>
            SqlServerCompatibilitySpecConfig.InitJournalConfig("journal_compat",
                "journal_metadata_compat");
    }
}