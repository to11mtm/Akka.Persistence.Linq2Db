namespace Akka.Persistence.Sql.Linq2Db
{
    public class FlowControl
    {
        public class Continue : FlowControl
        {
            public static Continue Instance = new Continue();
        }

        public class ContinueDelayed : FlowControl
        {
            public static ContinueDelayed Instance = new ContinueDelayed();
        }

        public class Stop : FlowControl
        {
            public static Stop Instance = new Stop();
        }
    }
}