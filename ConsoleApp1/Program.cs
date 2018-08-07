using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            TestAsync().Wait();
        }

        public static async Task TestAsync()
        {
            var context = SqlContextProvider.FromConnectionString(@"data source=db4mulpj2b.database.windows.net;initial catalog=domain0;User ID=SyncServer;Password=A$e194!eW");
            var users = context.CreateSimple("select top 10 * from [User]")
                .ExecuteQueryAnonymousAsync(new { ID = 0, Name = "" }).ToArray().Result;


            await context.UsingTransactionAsync(async x =>
            {
                var uu = await x.CreateSimple("select top 10 * from [User]")
                .ExecuteQueryAnonymousAsync(new { ID = 0, Name = "" }).ToArray();
            });


        }
    }
}
