using System;
using System.Diagnostics;

namespace KLinqTests
{
    public class KdbProcess : IDisposable
    {
        private Process _kproc;

        public KdbProcess():this(18501,true)
        {
        }
        public KdbProcess(int port,bool showWindow)
        {
            var psi = new ProcessStartInfo(@"c:\q\q.exe", "sp.q -p " +port) { CreateNoWindow = showWindow , WindowStyle = (showWindow)? ProcessWindowStyle.Normal: ProcessWindowStyle.Hidden};
            _kproc = Process.Start(psi);
        }

        public void Dispose()
        {
            _kproc.Kill();
            _kproc.Dispose();
        }
    }
}
