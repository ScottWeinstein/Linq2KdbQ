using System;
using System.Linq;
using System.Collections.Generic;
using Kx = Kdbplus;

namespace Kdbplus.Linq
{
    public static class KXExtensions
    {
        public static IEnumerable<FlipRow> GetEnumerator(this Flip source)
        {
            int kk = 0;
            Dictionary<string, int> colmap = source.TheColumnNames.ToDictionary((colName) => colName, (_) => kk++);
            int colCount = source.TheColumnNames.Length;
            for (int ii = 0; ii < Kx.Type.Length(source.TheColumnValues[0]); ii++)
            {
                object[] vals = new object[colCount];
                for (int jj = 0; jj < colCount; jj++)
                {
                    vals[jj] = ((Array)source.TheColumnValues[jj]).GetValue(ii);
                }
                yield return new FlipRow(colmap, vals);
            }
        }

        public static Flip FQuery(this IConnection source, string query, params object[] args)
        {
            object res;
            if (args == null || args.Length==0)
                res = source.Query(query);
            else if (args.Length == 1)
                res = source.Query(query, args[0]);
            else if (args.Length == 2)
                res = source.Query(query, args[0],args[1]);
            else if (args.Length == 3)
                res = source.Query(query, args[0], args[1],args[2]);
            else
                throw new ArgumentException("only 0-3 arguments are supported");
            return Kx.Type.ToFlip(res);
        }

        //public static System.Type GetKdbTypeFromCharCode(char @char)
        //{
        //    switch (@char)
        //    {
        //        case 'x':
        //            return typeof(byte);
        //        case 'h':
        //            return typeof(Int16);
        //        case 'i':
        //            return typeof(Int32);
        //        case 'j':
        //            return typeof(Int64);
        //        case 'e':
        //            return typeof(Single);
        //        case 'f':
        //            return typeof(double);
        //        case 'c':
        //            return typeof(char);
        //        case 's':
        //            return typeof(string);
        //        case 'm':
        //            return typeof(Month);
        //        case 'd':
        //            return typeof(Date);
        //        case 'z':
        //            return typeof(DateTime);
        //        case 'u':
        //            return typeof(Minute);
        //        case 'v':
        //            return typeof(Second);
        //        case 't':
        //            return typeof(TimeSpan);
        //        default:
        //            throw new ArgumentException("unknown or unsupported char type '" + @char + "'");
        //    }
        //}
    }
}
