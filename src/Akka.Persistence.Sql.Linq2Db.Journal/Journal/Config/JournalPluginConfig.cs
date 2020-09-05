namespace Akka.Persistence.Sql.Linq2Db.Journal.Journal.Config
{
    public class JournalPluginConfig
    {
        public JournalPluginConfig(Configuration.Config config)
        {
            TagSeparator = config.GetString("tag-separator", ",");
            //TODO: FILL IN SANELY
            Dao = config.GetString("dao", "akka.persistence.sql.linq2db.dao");
        }
        public string TagSeparator { get; protected set; }
        public string Dao { get; protected set; }
    }
}