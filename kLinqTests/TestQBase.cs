using System;
using Xunit;
using System.Linq;
using Kdbplus.Linq;

namespace KLinqTests
{
    public abstract class TestQBase
    {
        protected TestKdbContext ktc;
        public TestQBase()
        {
            ktc = new TestKdbContext(null);
        }


        public static void CheckTranslation(string translated, IQueryable qry)
        {
            Assert.Equal(translated, qry.GetQueryText());
        }

    }
}
