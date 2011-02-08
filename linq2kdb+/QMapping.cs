using System;
using IQ.Data;
using System.Linq.Expressions;

namespace Kdbplus.Linq
{


    internal class QMapping : ImplicitMapping
    {
        public QMapping(QueryLanguage language) : base(language) { }
        public override string GetTableName(System.Type rowType)
        {
            return rowType.Name.Replace("Record", "");
        }
    }
}

