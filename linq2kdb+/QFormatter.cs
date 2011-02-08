using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using IQ.Data;

// This source code is made available under the terms of the Microsoft Public License (MS-PL)
namespace Kdbplus.Linq
{
    internal class QFormatter : DbExpressionVisitor
    {
        private bool isNested;
        private bool isTopLevel;
        private StringBuilder sb;
        private Dictionary<TableAlias, string> aliases;

        private QFormatter()
        {
            isTopLevel = true;
            sb = new StringBuilder();
            aliases = new Dictionary<TableAlias, string>();
        }

        public static string Format(Expression expression)
        {
            var formatter = new QFormatter();
            formatter.Visit(expression);
            return formatter.sb.ToString();
        }

        protected override Expression Visit(Expression exp)
        {
            if (exp == null) return null;

            // check for supported node types first 
            // non-supported ones should not be visited (as they would produce bad SQL)
            switch (exp.NodeType)
            {
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.UnaryPlus:
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.Coalesce:
                case ExpressionType.RightShift:
                case ExpressionType.LeftShift:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.Power:
                case ExpressionType.Conditional:
                case ExpressionType.Constant:
                case ExpressionType.MemberAccess:
                case ExpressionType.Call:
                case ExpressionType.New:
                case (ExpressionType)DbExpressionType.Table:
                case (ExpressionType)DbExpressionType.Column:
                case (ExpressionType)DbExpressionType.Select:
                case (ExpressionType)DbExpressionType.Join:
                case (ExpressionType)DbExpressionType.Aggregate:
                case (ExpressionType)DbExpressionType.Scalar:
                case (ExpressionType)DbExpressionType.Exists:
                case (ExpressionType)DbExpressionType.In:
                case (ExpressionType)DbExpressionType.AggregateSubquery:
                case (ExpressionType)DbExpressionType.IsNull:
                case (ExpressionType)DbExpressionType.Between:
                case (ExpressionType)DbExpressionType.RowCount:
                case (ExpressionType)DbExpressionType.Projection:
                case (ExpressionType)DbExpressionType.NamedValue:
                    return base.Visit(exp);

                case ExpressionType.ArrayLength:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                case ExpressionType.ArrayIndex:
                case ExpressionType.TypeIs:
                case ExpressionType.Parameter:
                case ExpressionType.Lambda:
                case ExpressionType.NewArrayInit:
                case ExpressionType.NewArrayBounds:
                case ExpressionType.Invoke:
                case ExpressionType.MemberInit:
                case ExpressionType.ListInit:
                default:
                    throw new NotSupportedException(string.Format("The LINQ expression node of type {0} is not supported", exp.NodeType));
            }
        }
        protected override Expression VisitBinary(BinaryExpression b)
        {
            string op = GetOperator(b);
            Expression left = b.Left;
            Expression right = b.Right;

            switch (b.NodeType)
            {
                case ExpressionType.Power:
                    sb.Append("POWER(");
                    VisitValue(left);
                    sb.Append(", ");
                    VisitValue(right);
                    sb.Append(")");
                    return b;
                case ExpressionType.Coalesce:
                    sb.Append("COALESCE(");
                    VisitValue(left);
                    sb.Append(", ");
                    while (right.NodeType == ExpressionType.Coalesce)
                    {
                        BinaryExpression rb = (BinaryExpression)right;
                        VisitValue(rb.Left);
                        sb.Append(", ");
                        right = rb.Right;
                    }
                    VisitValue(right);
                    sb.Append(")");
                    return b;
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    sb.Append("(");
                    if (IsBoolean(left.Type))
                    {
                        sb.Append(op);
                        sb.Append(";");
                        VisitPredicate(left);
                        sb.Append(";");
                        VisitPredicate(right);
                    }
                    else
                    {
                        sb.Append(op);
                        sb.Append(";");
                        VisitValue(left);
                        sb.Append(";");
                        VisitValue(right);
                    }
                    sb.Append(")");

                    break;
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                    if (right.NodeType == ExpressionType.Constant)
                    {
                        ConstantExpression ce = (ConstantExpression)right;
                        if (ce.Value == null)
                        {
                            Visit(left);
                            sb.Append(" IS NULL");
                            break;
                        }
                    }
                    else if (left.NodeType == ExpressionType.Constant)
                    {
                        ConstantExpression ce = (ConstantExpression)left;
                        if (ce.Value == null)
                        {
                            Visit(right);
                            sb.Append(" IS NULL");
                            break;
                        }
                    }
                    goto case ExpressionType.LessThan;
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                    // check for special x.CompareTo(y) && type.Compare(x,y)
                    if (left.NodeType == ExpressionType.Call && right.NodeType == ExpressionType.Constant)
                    {
                        MethodCallExpression mc = (MethodCallExpression)left;
                        ConstantExpression ce = (ConstantExpression)right;
                        if (ce.Value != null && ce.Value.GetType() == typeof(int) && ((int)ce.Value) == 0)
                        {
                            if (mc.Method.Name == "CompareTo" && !mc.Method.IsStatic && mc.Arguments.Count == 1)
                            {
                                left = mc.Object;
                                right = mc.Arguments[0];
                            }
                            else if (
                                (mc.Method.DeclaringType == typeof(string) || mc.Method.DeclaringType == typeof(decimal))
                                  && mc.Method.Name == "Compare" && mc.Method.IsStatic && mc.Arguments.Count == 2)
                            {
                                left = mc.Arguments[0];
                                right = mc.Arguments[1];
                            }
                        }
                    }
                    goto case ExpressionType.Add;
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.ExclusiveOr:
                    sb.AppendFormat("({0};", op);
                    VisitValue(left);
                    sb.Append(";");
                    VisitValue(right);
                    sb.Append(")");
                    break;
                case ExpressionType.RightShift:
                    VisitValue(left);
                    sb.Append(" / POWER(2, ");
                    VisitValue(right);
                    sb.Append(")");
                    break;
                case ExpressionType.LeftShift:
                    VisitValue(left);
                    sb.Append(" * POWER(2, ");
                    VisitValue(right);
                    sb.Append(")");
                    break;
                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
            }
            return b;
        }

        private static string GetOperator(BinaryExpression b)
        {
            switch (b.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    return "&";
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return  "|";
                case ExpressionType.Equal:
                    return "=";
/*                case ExpressionType.NotEqual:
                    return "<>";
                case ExpressionType.LessThan:
                    return "<";
                case ExpressionType.LessThanOrEqual:
                    return "<=";
                case ExpressionType.GreaterThan:
                    return ">";
                case ExpressionType.GreaterThanOrEqual:
                    return ">=";
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                    return "+";
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    return "-";
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                    return "*";
                case ExpressionType.Divide:
                    return "/";
                case ExpressionType.Modulo:
                    return "%";
                case ExpressionType.ExclusiveOr:
                    return "^";*/
                default:
                    throw new NotSupportedException(b.NodeType.ToString());
            }
        }

        private static bool IsBoolean(System.Type type)
        {
            return type == typeof(bool) || type == typeof(bool?);
        }

        private static bool IsPredicate(Expression expr)
        {
            switch (expr.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return IsBoolean(((BinaryExpression)expr).Type);
                case ExpressionType.Not:
                    return IsBoolean(((UnaryExpression)expr).Type);
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case (ExpressionType)DbExpressionType.IsNull:
                case (ExpressionType)DbExpressionType.Between:
                case (ExpressionType)DbExpressionType.Exists:
                case (ExpressionType)DbExpressionType.In:
                    return true;
                case ExpressionType.Call:
                    return IsBoolean(((MethodCallExpression)expr).Type);
                default:
                    return false;
            }
        }

        protected virtual Expression VisitPredicate(Expression expr)
        {
            if (expr == null)
            {
                sb.Append("()");
                return expr;
            }
            if (isTopLevel)
            {
                sb.Append("enlist ");
                isTopLevel = false;
            }
            //if (!IsPredicate(expr))
            //{

            //}

            Visit(expr);

            return expr;

        }

        protected virtual Expression VisitValue(Expression expr)
        {
            if (IsPredicate(expr))
            {
                sb.Append("CASE WHEN (");
                Visit(expr);
                sb.Append(") THEN 1 ELSE 0 END");
            }
            else
            {
                Visit(expr);
            }
            return expr;
        }


        protected override Expression VisitConstant(ConstantExpression c)
        {
            WriteValue(c.Value);
            return c;
        }

        protected virtual void WriteValue(object value)
        {
            if (value == null)
            {
                sb.Append("NULL");
            }
            else
            {
                switch (System.Type.GetTypeCode(value.GetType()))
                {
                    case TypeCode.Boolean:
                        sb.Append(((bool)value) ? "1b" : "0b");
                        break;
                    case TypeCode.String:
                        sb.Append("enlist `");
                        sb.Append(value);
                        break;
                    case TypeCode.Object:
                        if (value.GetType().IsEnum)
                        {
                            sb.Append(Convert.ChangeType(value, typeof(int)));
                        }
                        else
                        {
                            throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", value));
                        }
                        break;
                    default:
                        sb.Append(value);
                        break;
                }
            }
        }


        protected override Expression VisitColumn(ColumnExpression column)
        {
            sb.Append("`");
            sb.Append(column.Name);
            //if (column.Alias != null)
            //{
            //    sb_old.Append(GetAliasName(column.Alias));
            //    sb_old.Append(".");
            //}
            //sb_old.Append(column.Name);
            return column;
        }


        private void VisitGroupBy(ReadOnlyCollection<Expression> selectGroupBy)
        {
            sb.Append("(");
            if (selectGroupBy != null)
            {
                if (selectGroupBy.Count ==1)
                {
                    ColumnExpression colexpr = (ColumnExpression) selectGroupBy.First();
                    sb.AppendFormat("(enlist `{0})!enlist `{0}", colexpr.Name);
                }
                else
                {
                    throw new  NotImplementedException("");
                }
            }
            //&& selectGroupBy.Count > 0)
            //{
            //    for (int i = 0, n = selectGroupBy.Count; i < n; i++)
            //    {
            //        if (i > 0)
            //        {
            //            sb.Append(", ");
            //        }
            //        VisitValue(selectGroupBy[i]);
            //    }
            //}
            sb.Append(")");
        }
        protected override Expression VisitSelect(SelectExpression select)
        {
            sb.Append("?[");
            VisitSource(select.From);
            sb.Append(";");
            VisitPredicate(select.Where);
            sb.Append(";"); 
            VisitGroupBy(select.GroupBy);
            sb.Append(";");
            //if (select.IsDistinct)
            //{
            //    sb.Append("DISTINCT ");
            //}
            //if (select.Take != null)
            //{
            //    sb.Append("TOP (");
            //    Visit(select.Take);
            //    sb.Append(") ");
            //}
            if (select.Columns.Count == 1)
            {
                ColumnDeclaration col = select.Columns[0];
                sb.AppendFormat("(enlist `{0})!enlist `{1}", col.Name, ((ColumnExpression)col.Expression).Name);
            }
            else if (select.Columns.Count > 1)
            {
                foreach (ColumnDeclaration col in select.Columns)
                {
                    sb.Append("`");
                    sb.Append(col.Name);
                }
                sb.Append("!(");
                foreach (ColumnDeclaration col in select.Columns)
                {
                    sb.Append("`");
                    sb.Append(((ColumnExpression)col.Expression).Name);
                }
                sb.Append(")");
            }
            //else
            //{
            //    sb.Append("NULL ");
            //    if (this.isNested)
            //    {
            //        sb.Append("AS tmp ");
            //    }
            //}

            //if (select.OrderBy != null && select.OrderBy.Count > 0)
            //{
            //    sb.Append("ORDER BY ");
            //    for (int i = 0, n = select.OrderBy.Count; i < n; i++)
            //    {
            //        OrderExpression exp = select.OrderBy[i];
            //        if (i > 0)
            //        {
            //            sb.Append(", ");
            //        }
            //        VisitValue(exp.Expression);
            //        if (exp.OrderType != OrderType.Ascending)
            //        {
            //            sb.Append(" DESC");
            //        }
            //    }
            //}
            sb.Append("]");
            return select;
        }


        protected override Expression VisitSource(Expression source)
        {
            bool saveIsNested = isNested;
            isNested = true;
            switch ((DbExpressionType)source.NodeType)
            {
                case DbExpressionType.Table:
                    TableExpression table = (TableExpression)source;
                    sb.Append(table.Name);
                    //sb_old.Append(table.Name);
                    //sb_old.Append(" AS ");
                    //sb_old.Append(GetAliasName(table.Alias));
                    break;
                case DbExpressionType.Select:
                    SelectExpression select = (SelectExpression)source;
                    sb.Append("(");
                    Visit(select);
                    sb.Append(")");
                    sb.Append(" AS ");
                    sb.Append(GetAliasName(select.Alias));
                    break;
                case DbExpressionType.Join:
                    VisitJoin((JoinExpression)source);
                    break;
                default:
                    throw new InvalidOperationException("Select source is not valid type");
            }
            isNested = saveIsNested;
            return source;
        }


        protected override Expression VisitNamedValue(NamedValueExpression value)
        {
            if (value.Type == typeof(string))
                sb.Append("enlist ");

            switch (value.Name)
            {
                case "p0":
                    sb.Append("x");
                    break;
                case "p1":
                    sb.Append("y");
                    break;
                case "p2":
                    sb.Append("z");
                    break;
                default:
                    break;
            //        throw new NotSupportedException();
            }
            return value;
        }

        private string GetAliasName(TableAlias alias)
        {
            string name;
            if (aliases.TryGetValue(alias, out name))
            {
                return name;
            }

            name = "t" + aliases.Count;
            aliases.Add(alias, name);
            return name;
        }


#if Supported

                protected override Expression VisitProjection(ProjectionExpression proj)
        {
            // treat these like scalar subqueries
            if (proj.Projector is ColumnExpression)
            {
                sb.Append("(");
                Visit(proj.Source);
                sb.Append(")");
            }
            else
            {
                throw new NotSupportedException("Non-scalar projections cannot be translated to SQL.");
            }
            return proj;
        }

        protected override Expression VisitJoin(JoinExpression join)
        {
            VisitSource(join.Left);
            switch (join.Join)
            {
                case JoinType.CrossJoin:
                    sb.Append("CROSS JOIN ");
                    break;
                case JoinType.InnerJoin:
                    sb.Append("INNER JOIN ");
                    break;
                case JoinType.CrossApply:
                    sb.Append("CROSS APPLY ");
                    break;
                case JoinType.OuterApply:
                    sb.Append("OUTER APPLY ");
                    break;
                case JoinType.LeftOuter:
                    sb.Append("LEFT OUTER JOIN ");
                    break;
            }
            VisitSource(join.Right);
            if (join.Condition == null)
            {
                return join;
            }

            sb.Append("ON ");
            VisitPredicate(join.Condition);
            return join;
        }

        private static string GetAggregateName(AggregateType aggregateType)
        {
            switch (aggregateType)
            {
                case AggregateType.Count: return "COUNT";
                case AggregateType.Min: return "MIN";
                case AggregateType.Max: return "MAX";
                case AggregateType.Sum: return "SUM";
                case AggregateType.Average: return "AVG";
                default: throw new NotImplementedException(string.Format("Unknown aggregate type: {0}", aggregateType));
            }
        }


                private static bool RequiresAsteriskWhenNoArgument(AggregateType aggregateType)
        {
            return aggregateType == AggregateType.Count;
        }

        protected override Expression VisitAggregate(AggregateExpression aggregate)
        {
            sb.Append(GetAggregateName(aggregate.AggregateType));
            sb.Append("(");
            if (aggregate.IsDistinct)
            {
                sb.Append("DISTINCT ");
            }
            if (aggregate.Argument != null)
            {
                VisitValue(aggregate.Argument);
            }
            else if (RequiresAsteriskWhenNoArgument(aggregate.AggregateType))
            {
                sb.Append("*");
            }
            sb.Append(")");
            return aggregate;
        }

        protected override Expression VisitIsNull(IsNullExpression isnull)
        {
            VisitValue(isnull.Expression);
            sb.Append(" IS NULL");
            return isnull;
        }

        protected override Expression VisitBetween(BetweenExpression between)
        {
            VisitValue(between.Expression);
            sb.Append(" BETWEEN ");
            VisitValue(between.Lower);
            sb.Append(" AND ");
            VisitValue(between.Upper);
            return between;
        }

        protected override Expression VisitRowNumber(RowNumberExpression rowNumber)
        {
            sb.Append("ROW_NUMBER() OVER(");
            if (rowNumber.OrderBy != null && rowNumber.OrderBy.Count > 0)
            {
                sb.Append("ORDER BY ");
                for (int i = 0, n = rowNumber.OrderBy.Count; i < n; i++)
                {
                    OrderExpression exp = rowNumber.OrderBy[i];
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }
                    VisitValue(exp.Expression);
                    if (exp.OrderType != OrderType.Ascending)
                    {
                        sb.Append(" DESC");
                    }
                }
            }
            sb.Append(")");
            return rowNumber;
        }

        protected override Expression VisitScalar(ScalarExpression subquery)
        {
            sb.Append("(");
            Visit(subquery.Select);
            sb.Append(")");
            return subquery;
        }

        protected override Expression VisitExists(ExistsExpression exists)
        {
            sb.Append("EXISTS(");
            Visit(exists.Select);
            sb.Append(")");
            return exists;
        }

        protected override Expression VisitIn(InExpression @in)
        {
            VisitValue(@in.Expression);
            sb.Append(" IN (");
            if (@in.Select != null)
            {
                Visit(@in.Select);
                sb.Append(")");
            }
            else if (@in.Values == null)
            {
                return @in;
            }
            for (int i = 0, n = @in.Values.Count; i < n; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                VisitValue(@in.Values[i]);
            }
            sb.Append(")");
            return @in;
        }

                protected override Expression VisitConditional(ConditionalExpression c)
        {
            if (IsPredicate(c.Test))
            {
                sb.Append("(CASE WHEN ");
                VisitPredicate(c.Test);
                sb.Append(" THEN ");
                VisitValue(c.IfTrue);
                Expression ifFalse = c.IfFalse;
                while (ifFalse != null && ifFalse.NodeType == ExpressionType.Conditional)
                {
                    ConditionalExpression fc = (ConditionalExpression)ifFalse;
                    sb.Append(" WHEN ");
                    VisitPredicate(fc.Test);
                    sb.Append(" THEN ");
                    VisitValue(fc.IfTrue);
                    ifFalse = fc.IfFalse;
                }
                if (ifFalse != null)
                {
                    sb.Append(" ELSE ");
                    VisitValue(ifFalse);
                }
                sb.Append(" END)");
            }
            else
            {
                sb.Append("(CASE ");
                VisitValue(c.Test);
                sb.Append(" WHEN 0 THEN ");
                VisitValue(c.IfFalse);
                sb.Append(" ELSE ");
                VisitValue(c.IfTrue);
                sb.Append(" END)");
            }
            return c;
        }


        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            if (m.Member.DeclaringType == typeof(string))
            {
                switch (m.Member.Name)
                {
                    case "Length":
                        sb.Append("LEN(");
                        Visit(m.Expression);
                        sb.Append(")");
                        return m;
                    default:
                        throw new NotSupportedException("m.Member.Name");
                }
            }
            else if (m.Member.DeclaringType == typeof(DateTime) || m.Member.DeclaringType == typeof(DateTimeOffset))
            {
                switch (m.Member.Name)
                {
                    case "Day":
                        sb.Append("DAY(");
                        Visit(m.Expression);
                        sb.Append(")");
                        return m;
                    case "Month":
                        sb.Append("MONTH(");
                        Visit(m.Expression);
                        sb.Append(")");
                        return m;
                    case "Year":
                        sb.Append("YEAR(");
                        Visit(m.Expression);
                        sb.Append(")");
                        return m;
                    case "Hour":
                        sb.Append("DATEPART(hour, ");
                        Visit(m.Expression);
                        sb.Append(")");
                        return m;
                    case "Minute":
                        sb.Append("DATEPART(minute, ");
                        Visit(m.Expression);
                        sb.Append(")");
                        return m;
                    case "Second":
                        sb.Append("DATEPART(second, ");
                        Visit(m.Expression);
                        sb.Append(")");
                        return m;
                    case "Millisecond":
                        sb.Append("DATEPART(millisecond, ");
                        Visit(m.Expression);
                        sb.Append(")");
                        return m;
                    case "DayOfWeek":
                        sb.Append("(DATEPART(weekday, ");
                        Visit(m.Expression);
                        sb.Append(") - 1)");
                        return m;
                    case "DayOfYear":
                        sb.Append("(DATEPART(dayofyear, ");
                        Visit(m.Expression);
                        sb.Append(") - 1)");
                        return m;
                    default:
                        throw new NotSupportedException(m.Member.Name);
                }
            }
            throw new NotSupportedException(string.Format("The member '{0}' is not supported", m.Member.Name));
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.DeclaringType == typeof(string))
            {
                switch (m.Method.Name)
                {
                    case "StartsWith":
                        sb.Append("(");
                        Visit(m.Object);
                        sb.Append(" LIKE ");
                        Visit(m.Arguments[0]);
                        sb.Append(" + '%')");
                        return m;
                    case "EndsWith":
                        sb.Append("(");
                        Visit(m.Object);
                        sb.Append(" LIKE '%' + ");
                        Visit(m.Arguments[0]);
                        sb.Append(")");
                        return m;
                    case "Contains":
                        sb.Append("(");
                        Visit(m.Object);
                        sb.Append(" LIKE '%' + ");
                        Visit(m.Arguments[0]);
                        sb.Append(" + '%')");
                        return m;
                    case "Concat":
                        IList<Expression> args = m.Arguments;
                        if (args.Count == 1 && args[0].NodeType == ExpressionType.NewArrayInit)
                        {
                            args = ((NewArrayExpression)args[0]).Expressions;
                        }
                        for (int i = 0, n = args.Count; i < n; i++)
                        {
                            if (i > 0) sb.Append(" + ");
                            Visit(args[i]);
                        }
                        return m;
                    case "IsNullOrEmpty":
                        sb.Append("(");
                        Visit(m.Arguments[0]);
                        sb.Append(" IS NULL OR ");
                        Visit(m.Arguments[0]);
                        sb.Append(" = '')");
                        return m;
                    case "ToUpper":
                        sb.Append("UPPER(");
                        Visit(m.Object);
                        sb.Append(")");
                        return m;
                    case "ToLower":
                        sb.Append("LOWER(");
                        Visit(m.Object);
                        sb.Append(")");
                        return m;
                    case "Replace":
                        sb.Append("REPLACE(");
                        Visit(m.Object);
                        sb.Append(", ");
                        Visit(m.Arguments[0]);
                        sb.Append(", ");
                        Visit(m.Arguments[1]);
                        sb.Append(")");
                        return m;
                    case "Substring":
                        sb.Append("SUBSTRING(");
                        Visit(m.Object);
                        sb.Append(", ");
                        Visit(m.Arguments[0]);
                        sb.Append(" + 1, ");
                        if (m.Arguments.Count == 2)
                        {
                            Visit(m.Arguments[1]);
                        }
                        else
                        {
                            sb.Append("8000");
                        }
                        sb.Append(")");
                        return m;
                    case "Remove":
                        sb.Append("STUFF(");
                        Visit(m.Object);
                        sb.Append(", ");
                        Visit(m.Arguments[0]);
                        sb.Append(" + 1, ");
                        if (m.Arguments.Count == 2)
                        {
                            Visit(m.Arguments[1]);
                        }
                        else
                        {
                            sb.Append("8000");
                        }
                        sb.Append(", '')");
                        return m;
                    case "IndexOf":
                        sb.Append("(CHARINDEX(");
                        Visit(m.Object);
                        sb.Append(", ");
                        Visit(m.Arguments[0]);
                        if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int))
                        {
                            sb.Append(", ");
                            Visit(m.Arguments[1]);
                        }
                        sb.Append(") - 1)");
                        return m;
                    case "Trim":
                        sb.Append("RTRIM(LTRIM(");
                        Visit(m.Object);
                        sb.Append("))");
                        return m;
                    default:
                        throw new NotSupportedException(m.Method.Name);

                }
            }
            else if (m.Method.DeclaringType == typeof(DateTime))
            {
                switch (m.Method.Name)
                {
                    case "op_Subtract":
                        if (m.Arguments[1].Type == typeof(DateTime))
                        {
                            sb.Append("DATEDIFF(");
                            Visit(m.Arguments[0]);
                            sb.Append(", ");
                            Visit(m.Arguments[1]);
                            sb.Append(")");
                            return m;
                        }
                        break;
                    default:
                        throw new NotSupportedException(m.Method.Name);
                }
            }
            else if (m.Method.DeclaringType == typeof(Decimal))
            {
                switch (m.Method.Name)
                {
                    case "Add":
                    case "Subtract":
                    case "Multiply":
                    case "Divide":
                    case "Remainder":
                        sb.Append("(");
                        VisitValue(m.Arguments[0]);
                        sb.Append(" ");
                        sb.Append(GetOperator(m.Method.Name));
                        sb.Append(" ");
                        VisitValue(m.Arguments[1]);
                        sb.Append(")");
                        return m;
                    case "Negate":
                        sb.Append("-");
                        Visit(m.Arguments[0]);
                        sb.Append("");
                        return m;
                    case "Ceiling":
                    case "Floor":
                        sb.Append(m.Method.Name.ToUpper());
                        sb.Append("(");
                        Visit(m.Arguments[0]);
                        sb.Append(")");
                        return m;
                    case "Round":
                        if (m.Arguments.Count == 1)
                        {
                            sb.Append("ROUND(");
                            Visit(m.Arguments[0]);
                            sb.Append(", 0)");
                            return m;
                        }
                        else if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int))
                        {
                            sb.Append("ROUND(");
                            Visit(m.Arguments[0]);
                            sb.Append(", ");
                            Visit(m.Arguments[1]);
                            sb.Append(")");
                            return m;
                        }
                        break;
                    case "Truncate":
                        sb.Append("ROUND(");
                        Visit(m.Arguments[0]);
                        sb.Append(", 0, 1)");
                        return m;
                    default:
                        throw new NotSupportedException(m.Method.Name);
                }
            }
            else if (m.Method.DeclaringType == typeof(Math))
            {
                switch (m.Method.Name)
                {
                    case "Abs":
                    case "Acos":
                    case "Asin":
                    case "Atan":
                    case "Cos":
                    case "Exp":
                    case "Log10":
                    case "Sin":
                    case "Tan":
                    case "Sqrt":
                    case "Sign":
                    case "Ceiling":
                    case "Floor":
                        sb.Append(m.Method.Name.ToUpper());
                        sb.Append("(");
                        Visit(m.Arguments[0]);
                        sb.Append(")");
                        return m;
                    case "Atan2":
                        sb.Append("ATN2(");
                        Visit(m.Arguments[0]);
                        sb.Append(", ");
                        Visit(m.Arguments[1]);
                        sb.Append(")");
                        return m;
                    case "Log":
                        if (m.Arguments.Count == 1)
                        {
                            goto case "Log10";
                        }
                        break;
                    case "Pow":
                        sb.Append("POWER(");
                        Visit(m.Arguments[0]);
                        sb.Append(", ");
                        Visit(m.Arguments[1]);
                        sb.Append(")");
                        return m;
                    case "Round":
                        if (m.Arguments.Count == 1)
                        {
                            sb.Append("ROUND(");
                            Visit(m.Arguments[0]);
                            sb.Append(", 0)");
                            return m;
                        }
                        else if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int))
                        {
                            sb.Append("ROUND(");
                            Visit(m.Arguments[0]);
                            sb.Append(", ");
                            Visit(m.Arguments[1]);
                            sb.Append(")");
                            return m;
                        }
                        break;
                    case "Truncate":
                        sb.Append("ROUND(");
                        Visit(m.Arguments[0]);
                        sb.Append(", 0, 1)");
                        return m;
                    default:
                        throw new NotSupportedException(m.Method.Name);
                }
            }
            if (m.Method.Name == "ToString")
            {
                if (m.Object.Type == typeof(string))
                {
                    Visit(m.Object);  // no op
                }
                else
                {
                    sb.Append("CONVERT(VARCHAR, ");
                    Visit(m.Object);
                    sb.Append(")");
                }
                return m;
            }
            else if (m.Method.Name == "Equals")
            {
                if (m.Method.IsStatic && m.Method.DeclaringType == typeof(object))
                {
                    sb.Append("(");
                    Visit(m.Arguments[0]);
                    sb.Append(" = ");
                    Visit(m.Arguments[1]);
                    sb.Append(")");
                    return m;
                }
                else if (!m.Method.IsStatic && m.Arguments.Count == 1 && m.Arguments[0].Type == m.Object.Type)
                {
                    sb.Append("(");
                    Visit(m.Object);
                    sb.Append(" = ");
                    Visit(m.Arguments[0]);
                    sb.Append(")");
                    return m;
                }
            }

            throw new NotSupportedException(string.Format("The method '{0}' is not supported", m.Method.Name));
        }

        protected override NewExpression VisitNew(NewExpression nex)
        {
            if (nex.Constructor.DeclaringType == typeof(DateTime))
            {
                if (nex.Arguments.Count == 3)
                {
                    sb.Append("DATEADD(year, ");
                    Visit(nex.Arguments[0]);
                    sb.Append(", DATEADD(month, ");
                    Visit(nex.Arguments[1]);
                    sb.Append(", DATEADD(day, ");
                    Visit(nex.Arguments[2]);
                    sb.Append(", 0)))");
                    return nex;
                }
                else if (nex.Arguments.Count == 6)
                {
                    sb.Append("DATEADD(year, ");
                    Visit(nex.Arguments[0]);
                    sb.Append(", DATEADD(month, ");
                    Visit(nex.Arguments[1]);
                    sb.Append(", DATEADD(day, ");
                    Visit(nex.Arguments[2]);
                    sb.Append(", DATEADD(hour, ");
                    Visit(nex.Arguments[3]);
                    sb.Append(", DATEADD(minute, ");
                    Visit(nex.Arguments[4]);
                    sb.Append(", DATEADD(second, ");
                    Visit(nex.Arguments[5]);
                    sb.Append(", 0))))))");
                    return nex;
                }
            }
            throw new NotSupportedException(string.Format("The construtor '{0}' is not supported", nex.Constructor));
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            string op = GetOperator(u);
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    if (IsBoolean(u.Operand.Type))
                    {
                        sb.Append(op);
                        sb.Append(" ");
                        VisitPredicate(u.Operand);
                    }
                    else
                    {
                        sb.Append(op);
                        VisitValue(u.Operand);
                    }
                    break;
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                    sb.Append(op);
                    VisitValue(u.Operand);
                    break;
                case ExpressionType.UnaryPlus:
                    VisitValue(u.Operand);
                    break;
                case ExpressionType.Convert:
                    // ignore conversions for now
                    Visit(u.Operand);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
            }
            return u;
        }
        private static string GetOperator(string methodName)
        {
            switch (methodName)
            {
                case "Add": return "+";
                case "Subtract": return "-";
                case "Multiply": return "*";
                case "Divide": return "/";
                case "Negate": return "-";
                case "Remainder": return "%";
                default:
                    throw new NotSupportedException(methodName);
            }
        }

        private static string GetOperator(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                    return "-";
                case ExpressionType.UnaryPlus:
                    return "+";
                case ExpressionType.Not:
                    return IsBoolean(u.Operand.Type) ? "NOT" : "~";
                default:
                    throw new NotSupportedException(u.NodeType.ToString());
            }
        }
#endif


    }
}
