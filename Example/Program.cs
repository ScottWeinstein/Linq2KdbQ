using System;
using System.Linq;
using Kdbplus;
using Kdbplus.Linq;

namespace Example
{
    class Program
    {
        private const int qPortNum = 18502;

        public static void Main(string[] args)
        {
            using (new KLinqTests.KdbProcess(qPortNum,false))
            {
                using (IConnection connection = new Connection("localhost", qPortNum))
                {
                    connection.Run(@"\l sp.q");

                    var tkc = new TestKdbContext(connection);
                    string city = "london";
                    var qry = tkc.sRecord.Where(supRow => supRow.city == city);
                    Console.WriteLine("{0}\n", qry);

                    Console.WriteLine("Query results");
                    Console.WriteLine("{0}\t{1}\t{2}\t{3}", "name", "city", "s", "status");
                    foreach (var item in qry.ToList())
                    {
                        Console.WriteLine("{0}\t{1}\t{2}\t{3}", item.name, item.city, item.s, item.status);
                    }
                    Console.WriteLine("\n");

                    qry = tkc.sRecord.Where(supRow => supRow.status == 10);
                    Console.WriteLine("Query:tkc.sRecord.Where(supRow => supRow.status == 10)");
                    Console.WriteLine("{0}\t{1}\t{2}\t{3}", "name", "city", "s", "status");
                    foreach (var item in qry.ToList())
                    {
                        Console.WriteLine("{0}\t{1}\t{2}\t{3}", item.name, item.city, item.s, item.status);
                    }
                    Console.WriteLine("\n");


                    var qry2 = from row in tkc.sRecord
                               where row.status == 20 && row.city == "london"
                               select row.name;
                    Console.WriteLine(@"from row in tkc.sRecord where row.status == 20 && row.city == ""london"" select row.name;");
                    Console.WriteLine(String.Join(",",qry2.ToArray()));
                    var s = Console.ReadKey();
                }
            }
        }
    }
}

