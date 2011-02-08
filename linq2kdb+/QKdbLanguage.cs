using System;
using System.Linq.Expressions;
using IQ.Data;

namespace Kdbplus.Linq
{
    internal class QKdbLanguage : QueryLanguage
    {
        public override string Format(Expression expression)
        {
            return QFormatter.Format(expression);
        }
    }
}
