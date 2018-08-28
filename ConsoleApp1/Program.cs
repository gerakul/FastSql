using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using Npgsql;
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
            //TestAsyncPg().Wait();
        }

        public static async Task TestAsync()
        {
            var provider = SqlContextProvider.DefaultInstance;

            var context = provider.CreateConnectionStringContext(@"data source=db4mulpj2b.database.windows.net;initial catalog=domain0;User ID=SyncServer;Password=A$e194!eW");
            var context2 = provider.CreateConnectionStringContext(@"Data Source=devsrv;Initial Catalog=Test;Integrated Security=True");
            var contextTest = provider.CreateConnectionStringContext(@"Data Source=gerasimov2;Initial Catalog=TestDB;Integrated Security=True");

            //context.CreateSimple("select top 10 * from [User]").WriteToServer(context2, "Users");

            await context2.UsingTransactionAsync(y => context
                    .UsingTransactionAsync(x => x.CreateSimple("select top 5 * from [User] where ID > 100")
                    .WriteToServerAsync(y, "Person", new BulkOptions(FieldsSelector.Common))));

            //var qq = await context2.CreateProcedure("TestProc", new { maxID = 3 }).ExecuteQueryAnonymousAsync(new { ID = 1, Name = "" }, new ReadOptions(FieldsSelector.Common)).ToArray();

            //var id = await contextTest.CreateUpdate("Test", qq.First(), new QueryOptions(10), "ID").ExecuteNonQueryAsync();

            //await contextTest.CreateDelete("Test", new { ID = 3 }).ExecuteNonQueryAsync();
        }

        public static async Task TestAsyncPg()
        {
            var provider = NpgsqlContextProvider.DefaultInstance;

            provider.DefaultReadOptions.CaseSensitive = true;
            var context = provider.CreateConnectionStringContext(@"Server=luvpgtest.postgres.database.azure.com;Database=testdb;Port=5432;User Id=dba@luvpgtest;Password=w8rE_j36ag$Q;SSL Mode=Prefer; Trust Server Certificate=true");

            var q = await context.CreateSimple("select * from test1").ExecuteQueryAnonymousAsync(new { ID = 1L, Name = "", name = default(int?) }).ToArray();

            //await context.CreateInsert("test1", new { Name = "qqq1", name = 1 }).ExecuteNonQueryAsync();

            //var qq = await context.CreateProcedure("reffunc", new { }).ExecuteQueryAsync().ToArray();
            var www = await context.CreateProcedureSimple("tabfunc").ExecuteQueryAnonymousAsync(new { ID1 = 1L, Name1 = "", name1 = default(int?) }).ToArray();

            await context.UsingTransactionAsync(async tc =>
            {
                await tc.CreateProcedureSimple("reffunc", new NpgsqlParameter("ppp", NpgsqlTypes.NpgsqlDbType.Refcursor)
                    {
                        Value = "v"
                    }).ExecuteNonQueryAsync();

                var qqq = await tc.CreateSimple("fetch all in v").ExecuteQueryAnonymousAsync(new { ID = 1L, Name = "", Num = default(int?) }).ToArray();
            });
        }
    }
}
