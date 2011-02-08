using System.IO;
using IQ;
using Kdbplus;
using Kdbplus.Linq;
namespace KLinqTests
{
	public class TestKdbContext:KdbContext
	{
        public Query<pRecord> pRecord {get;private set;}
        public Query<sRecord> sRecord {get;private set;}
        public Query<spRecord> spRecord {get;private set;}
         
		public TestKdbContext(IConnection connection):this(connection,null) {}
        public TestKdbContext(IConnection connection, TextWriter log):base(connection,log)
        {
        pRecord = new Query<pRecord>(Provider);
        sRecord = new Query<sRecord>(Provider);
        spRecord = new Query<spRecord>(Provider);
         
		}
	}
}
