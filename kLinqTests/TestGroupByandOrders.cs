using System;
using System.Linq;
using Xunit;
using System.Text.RegularExpressions;

namespace KLinqTests
{
    public class TestGroupByandOrders : TestQBase
    {
        public TestGroupByandOrders()
        {
            ktc = new TestKdbContext(null);
        }

        [Fact]
        public void TSort1()
        {
            var q = ktc.sRecord.OrderBy(s => s.city);
            CheckTranslation("?[s;();();(enlist `city)!enlist `city]", q);
        }

        [Fact(Skip="not implemented")]
        public void TestGB1()
		{
            var q = from srec in ktc.sRecord group new { srec.name, srec.status } by srec.city;
            CheckTranslation("?[s;();(enlist `city)!enlist `city;`name`status!`name`status]", q);
        }

        [Fact]
        public void OrderBy1()
        {
            var q = ktc.pRecord.OrderBy(p => p.city).Select(p=>p.weight);
            CheckTranslation("?[p;();();(enlist `weight)!enlist `weight]", q);

        }
    }
}
