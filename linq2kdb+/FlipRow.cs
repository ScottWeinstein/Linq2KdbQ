using System.Collections.Generic;

namespace Kdbplus.Linq
{
    public class FlipRow
    {
        public FlipRow(Dictionary<string, int> columnMap, object[] values)
        {
            Colmap = columnMap;
            Values = values;
        }
        public object this[string columnName]
        {
            get
            {
                return Values[Colmap[columnName]];
            }            
        }

        public object this[int columnIndex]
        {
            get
            {
                return Values[columnIndex];
            }
        }

        public T GetValue<T>(string columnName)
        {
            return (T)this[columnName];
        }
        public T GetValue<T>(int columnIndex)
        {
            return (T)Values[columnIndex];
        }
        internal Dictionary<string, int> Colmap { get; set; }
        internal object[] Values { get; private set; }

    }
}
