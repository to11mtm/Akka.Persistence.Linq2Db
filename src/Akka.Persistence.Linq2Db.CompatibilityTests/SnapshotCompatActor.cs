using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class SnapshotCompatActor : ReceivePersistentActor
    {
        private List<SomeEvent> events = new List<SomeEvent>();
        public SnapshotCompatActor(string snapshot, string persistenceId)
        {
            JournalPluginId = "akka.persistence.journal.inmem";
            SnapshotPluginId = snapshot;
            PersistenceId = persistenceId;
            Command<SomeEvent>(se=>Persist(se, p =>
            {
                events.Add(p);
                SaveSnapshot(events);
            }));
            Command<ContainsEvent>(ce=>Context.Sender.Tell(events.Any(e=>e.Guid==ce.Guid)));
            
            Recover<SnapshotOffer>(se =>
            {
                if (se.Snapshot is List<SomeEvent> sel)
                    events = sel;
            });
        }
        public override string PersistenceId { get; }
    }
}