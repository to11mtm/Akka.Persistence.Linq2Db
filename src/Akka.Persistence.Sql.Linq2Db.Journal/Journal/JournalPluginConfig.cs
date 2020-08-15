using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class JournalPluginConfig
    {
        public JournalPluginConfig(Config config)
        {
            TagSeparator = config.GetString("tag-separator", ",");
            //TODO: FILL IN SANELY
            Dao = config.GetString("dao", "akka.persistence.sql.linq2db.dao");
        }
        public string TagSeparator { get; protected set; }
        public string Dao { get; protected set; }
    }
}