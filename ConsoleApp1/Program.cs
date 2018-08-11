using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;
using System;
using System.Linq;
using System.Threading;
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
            var context2 = SqlContextProvider.FromConnectionString(@"Data Source=devsrv;Initial Catalog=Test;Integrated Security=True");

            context.CreateSimple("select top 10 * from [User]").WriteToServer(context2, "Users");


            await context2.UsingTransactionAsync(y => context
                    .UsingTransactionAsync(x => x.CreateSimple("select top 5 * from [User] where ID > 100")
                    .WriteToServerAsync(y, new BulkOptions(FieldsSelector.Common), "Person")));

            var qq = await context2.CreateProcedure("TestProc", new { maxID = 3 }).ExecuteQueryAnonymousAsync(new { Name = "" }).ToArray();
        }
    }
}
