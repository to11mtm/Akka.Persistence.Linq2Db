using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class ReadJournalPluginConfig
    {
        public ReadJournalPluginConfig(Config config)
        {
            TagSeparator = config.GetString("tag-separator", ",");
            Dao = config.GetString("dao",
                "akka.persistence.sql.linq2db.dao.bytea.readjournal.bytearrayreadjournaldao");
            
        }

        public string Dao { get; set; }

        public string TagSeparator { get; set; }
    }
}