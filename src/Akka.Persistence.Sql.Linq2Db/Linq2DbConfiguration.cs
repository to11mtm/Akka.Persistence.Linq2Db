using Akka.Configuration;
using Akka.Streams.Extra;
using Type = Google.Protobuf.WellKnownTypes.Type;

namespace Akka.Persistence.Sql.Linq2Db
{
    
    public class Linq2DbConfiguration
    {
        public Linq2DbConfiguration(Config config)
        {
            ProviderName = config.GetString("providername");
            ConnectionString = config.GetString("connectionstring");
        }

        public string ConnectionString { get; }

        public string ProviderName { get; }
    }
}