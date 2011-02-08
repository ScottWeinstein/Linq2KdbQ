using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Kdbplus.Linq
{
    internal class QCommand<T>
    {
        public QCommand(string commandText, IEnumerable<string> paramNames, Func<FlipRow, T> projector)
        {
            CommandText = commandText;
            ParameterNames = new List<string>(paramNames).AsReadOnly();
            Projector = projector;
        }
        public string CommandText { get; private set; }
        public ReadOnlyCollection<string> ParameterNames { get; private set; }
        public Func<FlipRow, T> Projector { get; private set; }
    }
}
