using System;
using System.Linq;
using Kdbplus.Linq;
using Xunit;
using Kdbplus;

namespace KLinqTests
{
    public class TestExecution : TestQBase,IUseFixture<KdbProcess>, IDisposable
    {
        IConnection connection;

        public TestExecution()
        {
            connection = new Connection("localhost", 18501);
            ktc = new TestKdbContext(connection);
        }

        public void Dispose()
        {
            connection.Dispose();
        }

        [Fact]
        public void Test()
        {
           var flip =  connection.FQuery("select from p");
           foreach (FlipRow fr in flip.GetEnumerator())
           {
               Assert.IsType<string>(fr[0]);
               Assert.IsType<string>(fr["name"]);
               Assert.IsType<string>(fr.GetValue<string>(0));
               Assert.IsType<string>(fr.GetValue<string>("name"));
           }
        }

        [Fact]
        public void GetTable()
        {
            var sdata  = ktc.sRecord.ToArray();
            Assert.Equal(5, sdata.Length);
            Assert.Equal("s1", sdata[0].s);
            Assert.Equal(20, sdata[0].status);
            Assert.Equal("london", sdata[0].city);
            Assert.Equal("smith", sdata[0].name);
        }
        [Fact]
        public void TestRestictandProject()
        {
            var qry = from row in ktc.sRecord
                      where row.status == 20 && row.city == "london"
                      select row.name;
            var data = qry.ToArray();
            Assert.Equal(2, data.Length);
            Assert.Equal(new string[] { "smith", "clark" }, data);
        }
        [Fact]
        public void TestAnonyClass()
        {
           var data =  ktc.sRecord.Select(c => new { c.city, Status = new { c.status } }).ToArray();
           Assert.Equal(20, data[0].Status.status);
        }
        [Fact]
        public void TestSelectWithLocalVar()
        {
            int x=10;
            var data = ktc.sRecord.Select(c => new { X = x, S = c.s }).ToArray();
            Assert.Equal(5, data.Length);
            Assert.NotEqual(data[0].S, data[1].S);
            Assert.True(data.All(d => d.X == x));
        }

        [Fact(Skip = "not implemented")]
        public void TestGroupBy()
        {
            var q = from srec in ktc.sRecord group new { srec.name, srec.status } by srec.city;
//            CheckTranslation("?[s;();(enlist `city)!enlist `city;`name`status!`name`status]", q);
            var data = q.ToArray();
        }



        public void SetFixture(KdbProcess data)
        {
        }
    }
}
