using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using IQ;
using IQ.Data;
using Kdbplus;
using System.Globalization;

namespace Kdbplus.Linq
{
    public class KdbQueryProvider : QueryProvider
    {
        public KdbQueryProvider(IConnection connection, QueryPolicy policy, TextWriter log)
        {
            Connection = connection;
            Policy = policy;
            Mapping = policy.Mapping;
            Language = Mapping.Language;
            Log = log;
        }
        
         

        public IConnection Connection { get;private set;}
        public TextWriter Log { get; private set; }
        public QueryPolicy Policy { get; private set; }
        public QueryMapping Mapping { get; private set; }
        public QueryLanguage Language { get; private set; }

        public override string GetQueryText(Expression expression)
        {
            Expression translated = Translate(expression);
            var selects = SelectGatherer.Gather(translated).Select(s => Language.Format(s));
            return string.Join("\n\n", selects.ToArray());
        }

        public string GetQueryPlan(Expression expression)
        {
            Expression plan = GetExecutionPlan(expression);
            return DbExpressionWriter.WriteToString(plan);
        }

        public override object Execute(Expression expression)
        {
            Expression plan = GetExecutionPlan(expression);

            LambdaExpression lambda = expression as LambdaExpression;
            if (lambda != null)
            {
                // compile & return the execution plan so it can be used multiple times
                LambdaExpression fn = Expression.Lambda(lambda.Type, plan, lambda.Parameters);
                return fn.Compile();
            }
            else
            {
                // compile the execution plan and invoke it
                Expression<Func<object>> efn = Expression.Lambda<Func<object>>(Expression.Convert(plan, typeof(object)));
                Func<object> fn = efn.Compile();
                return fn();
            }
        }
        protected virtual Expression GetExecutionPlan(Expression expression)
        {
            // strip off lambda for now
            LambdaExpression lambda = expression as LambdaExpression;
            if (lambda != null)
                expression = lambda.Body;

            // translate query into client & server parts
            ProjectionExpression projection = Translate(expression);

            Expression rootQueryable = RootQueryableFinder.Find(expression);
            Expression provider = Expression.Convert(
                Expression.Property(rootQueryable, typeof(IQueryable).GetProperty("Provider")),
                typeof(KdbQueryProvider)
                );

            return QExecutionBuilder.Build(Policy,projection, provider);
        }
        protected virtual ProjectionExpression Translate(Expression expression)
        {
            // pre-evaluate local sub-trees
            expression = PartialEvaluator.Eval(expression, CanBeEvaluatedLocally);

            // apply mapping (binds LINQ operators too)
            expression = Mapping.Translate(expression);

            // any policy specific translations or validations
            expression = Policy.Translate(expression);

            // any language specific translations or validations
            expression = Language.Translate(expression);

            // do final reduction
            expression = UnusedColumnRemover.Remove(expression);
            expression = RedundantSubqueryRemover.Remove(expression);
            expression = RedundantJoinRemover.Remove(expression);
            expression = RedundantColumnRemover.Remove(expression);

            return (ProjectionExpression)expression;
        }
        protected virtual bool CanBeEvaluatedLocally(Expression expression)
        {
            // any operation on a query can't be done locally
            ConstantExpression cex = expression as ConstantExpression;
            if (cex != null)
            {
                IQueryable query = cex.Value as IQueryable;
                if (query != null && query.Provider == this)
                    return false;
            }
            MethodCallExpression mc = expression as MethodCallExpression;
            if (mc != null &&
                (mc.Method.DeclaringType == typeof(Enumerable) ||
                 mc.Method.DeclaringType == typeof(Queryable)))
            {
                return false;
            }
            if (expression.NodeType == ExpressionType.Convert &&
                expression.Type == typeof(object))
                return true;
            return expression.NodeType != ExpressionType.Parameter &&
                   expression.NodeType != ExpressionType.Lambda;
        }

        internal virtual IEnumerable<T> Execute<T>(QCommand<T> query, object[] parameterValues)
        {
            LogCommand(query.CommandText);
            string format = parameterValues != null && parameterValues.Length > 0 ? "{{flip[{0}]}}" : "flip[{0}]";
            string cmdtext = string.Format(CultureInfo.InvariantCulture, format, query.CommandText); 
            Flip flip = Connection.FQuery(cmdtext, parameterValues);
            return Project(flip, query.Projector);
        }

        public virtual IEnumerable<T> Project<T>(Flip reader, Func<FlipRow, T> fnProjector)
        {
            foreach(FlipRow flipRow in reader.GetEnumerator())
            {
            	yield return fnProjector(flipRow);
            }
        }
        public void LogCommand(string cmd)
        {
            if (Log != null)
            {
                Log.WriteLine(cmd);
            }
        }
    }
    

}
