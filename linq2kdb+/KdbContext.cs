using System.IO;
using System.Linq;
using IQ.Data;
using Kdbplus;

namespace Kdbplus.Linq
{
    public class KdbContext
    {
        public IQueryProvider Provider { get; protected set; }
        internal static QueryPolicy StandardPolicy = new QueryPolicy(new QMapping(new QKdbLanguage()));

        public KdbContext(IConnection connection) : this(connection, null, StandardPolicy) { }
        public KdbContext(IConnection connection, TextWriter log):this(connection, log, StandardPolicy) {}
        internal KdbContext(IConnection connection, TextWriter log, QueryPolicy policy)
        {
            Provider = new KdbQueryProvider(connection, policy, log);
        }
    }
}
