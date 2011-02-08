using System;
using System.Linq;

namespace Kdbplus.Linq
{
    public static class IQueryableExtensions
    {
        public static string GetQueryText(this IQueryable query)
        {
            KdbQueryProvider prov = (KdbQueryProvider)query.Provider;
            return prov.GetQueryText(query.Expression);
        }
        public static string GetQueryPlan(this IQueryable query)
        {
            KdbQueryProvider prov = (KdbQueryProvider)query.Provider;
            return prov.GetQueryPlan(query.Expression);
        }
    }
}
