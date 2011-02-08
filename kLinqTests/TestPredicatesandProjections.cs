using System;
using System.Diagnostics;
using System.Linq;
using Kdbplus.Linq;
using Xunit;

namespace KLinqTests
{
    public class TestPredicatesandProjections : TestQBase
    {

        
        [Fact]
        public void WhereTrue()
        {
            CheckTranslation(
                "?[s;enlist 1b;();`city`name`s`status!(`city`name`s`status)]",
                ktc.sRecord.Where(supRow => true));
        }
        [Fact]
        public void WhereFalse()
        {
            CheckTranslation(
                "?[s;enlist 0b;();`city`name`s`status!(`city`name`s`status)]",
                ktc.sRecord.Where(s => false));
        }

        [Fact]
        public void TestSelectScalar()
        {
            CheckTranslation("?[s;();();(enlist `city)!enlist `city]", ktc.sRecord.Select(c => c.city));
        }

        [Fact]
        public void TestSelectAnonymousOne()
        {
            CheckTranslation("?[s;();();(enlist `city)!enlist `city]", ktc.sRecord.Select(c => new { c.city }));
        }

        [Fact]
        public void TestSelectAnonymousTwo()
        {
            CheckTranslation("?[s;();();`city`status!(`city`status)]",
                ktc.sRecord.Select(c => new { c.city, c.status }));
        }

        [Fact]
        public void TestSelectAnonymousThree()
        {
            CheckTranslation("?[s;();();`city`status`name!(`city`status`name)]",
                ktc.sRecord.Select(c => new { c.city, c.status, c.name }));
        }

        [Fact]
        public void TestSelectTable()
        {
            CheckTranslation("?[s;();();`city`name`s`status!(`city`name`s`status)]", ktc.sRecord);
        }

        [Fact]
        public void TestSelectCustomerIdentity()
        {
            CheckTranslation("?[s;();();`city`name`s`status!(`city`name`s`status)]", ktc.sRecord.Select(c => c));
        }

        [Fact(Skip = "na")]
        public void TestSelectAnonymousWithObject()
        {
            CheckTranslation("", ktc.sRecord.Select(c => new { c.city, c }));
        }

        [Fact]
        public void TestSelectAnonymousNested()
        {
            CheckTranslation("?[s;();();`city`status!(`city`status)]",
                ktc.sRecord.Select(c => new { c.city, Status = new { c.status } }));
        }

        [Fact(Skip = "Not Supported")]
        public void TestSelectAnonymousEmpty()
        {
            CheckTranslation("?[s;();0b;(enlist `s)!enlist `s]", ktc.sRecord.Select(c => new { }));
        }

        [Fact]
        public void TestSelectAnonymousLiteral()
        {
            int x = 10;
            CheckTranslation("?[s;();();(enlist `s)!enlist `s]", ktc.sRecord.Select(c => new { X = x, S = c.s }));
        }


        [Fact]
        public void WhereStrColEq()
        {
            var qry = ktc.sRecord.Where(s => s.city == "london");
            CheckTranslation("?[s;enlist (=;`city;enlist `london);();`city`name`s`status!(`city`name`s`status)]", qry);
        }
        [Fact]
        public void WhereIntColEq()
        {
            var qry = ktc.sRecord.Where(supRow => supRow.status == 20).Select(r => r.name);
            CheckTranslation("?[s;enlist (=;`status;20);();(enlist `name)!enlist `name]", qry);
        }
        [Fact]
        public void WhereAnd()
        {
            var qry = from row in ktc.sRecord
                      where row.status == 20 && row.city == GetCity()
                      select row.name;
            CheckTranslation("?[s;enlist (&;(=;`status;20);(=;`city;enlist `london));();(enlist `name)!enlist `name]", qry);
        }

        [Fact]
        public void WhereOr()
        {
            var qry = from row in ktc.sRecord
                      where row.status == 20 || row.city == GetCity()
                      select row.name;
            CheckTranslation("?[s;enlist (|;(=;`status;20);(=;`city;enlist `london));();(enlist `name)!enlist `name]", qry);
        }
        private string GetCity()
        {
            return "london";
        }

    }
}
