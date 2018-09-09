using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using Npgsql;
using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Examples
{
    class Program
    {
        static string connString = @"<your connection string>";

        static void Main(string[] args)
        {
            // get context
            var context = SqlContextProvider.DefaultInstance.CreateConnectionStringContext(connString);

            // create and execute command
            var employees1 = context.CreateSimple("select * from Employee").ExecuteQuery<Employee>().ToArray();

            // more examples about getting ContextProvider in the GettingContextProvider method
            // about context creation in the ContextCreation method
            // about creating and executing commands in the WorkingWithContext method
        }

        static void GettingContextProvider()
        {
            // default provider (for most cases)
            var sqlProvider1 = SqlContextProvider.DefaultInstance;
            var postgreProvider1 = NpgsqlContextProvider.DefaultInstance;

            // specific provider
            var sqlProvider2 = new SqlContextProvider();
            var postgreProvider2 = new NpgsqlContextProvider();
        }

        static async Task ContextCreation(ContextProvider provider)
        {
            // context can be created from connection string, connection or transaction
            // all these types of context have the same functionality (except few specific methods)
            // derived from interface ICommandCreator

            // context from connection string
            var context1 = provider.CreateConnectionStringContext(connString);

            // context from connection
            var connection = GetConnection();
            var context2 = provider.CreateConnectionContext(connection);
            // or
            context1.UsingConnection(context =>
            {
                // doing something with context (attached to opened connection)
                // for example using transaction
                context.UsingTransaction(cntxt =>
                {
                    // doing something with cntxt (attached to active transaction)
                });
            });
            // or asynchronously
            await context1.UsingConnectionAsync(async context =>
            {
                // doing something with context (attached to opened connection)
                // for example using transaction
                await context.UsingTransactionAsync(async cntxt =>
                {
                    // doing something with cntxt (attached to active transaction)
                });
            });

            // context from transaction
            var transaction = connection.BeginTransaction();
            var context3 = provider.CreateTransactionContext(transaction);
            // or
            context1.UsingTransaction(context =>
            {
                // doing something with context (attached to active transaction)
            });
            // or asynchronously
            await context1.UsingTransactionAsync(async context =>
            {
                // doing something with context (attached to active transaction)
            });
        }

        static async Task WorkingWithContext(ICommandCreator context)
        {
            // the almost entire functionality of specific context is derived from interface ICommandCreator
            // regardless of context type (connection string, connection or transaction)
            // so all examples are based on this interface and can by applied to any context

            // simple retrieving data
            var data1 = context.CreateSimple("select * from Employee").ExecuteQuery<Employee>().ToArray();
            // or async
            var data2 = await context.CreateSimple("select * from Employee").ExecuteQueryAsync<Employee>().ToArray();

            // using parameters
            // simple
            var data3 = context.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ExecuteQuery<Employee>().ToArray();
            // or async
            var data4 = await context.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ExecuteQueryAsync<Employee>().ToArray();
            // parameters mapped to object
            var data5 = context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 }).ExecuteQuery<Employee>().ToArray();
            // or async
            var data6 = context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 }).ExecuteQueryAsync<Employee>().ToArray();

            // note: all commands can be executed asynchronously but examples below show only synchronous execution in order to be simple

            // using options 
            // note: options can be set for context or provider as well as for certain command
            var data7 = context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 },
                new QueryOptions(1000, true))
                .ExecuteQuery<Employee>(new ReadOptions(FieldsSelector.Destination)).ToArray();

            // retrieving anonimous entities
            var data8 = context.CreateSimple("select * from Employee").ExecuteQueryAnonymous(new { ID = 0, Name = ""}).ToArray();

            // retrieving list of values
            var data9 = context.CreateSimple("select ID from Employee").ExecuteQueryFirstColumn<int>().ToArray();

            // execute without retrieving data
            context.CreateSimple("update Company set Name = @p1 where ID = @p0", 2, "Updated Co").ExecuteNonQuery();

            // insert
            var emp1 = new Employee() { CompanyID = 2, Name = "New inserted", Phone = "111" };
            context.CreateInsert("Employee", emp1).ExecuteNonQuery();
            // or return new id
            var id = context.CreateInsert("Employee", emp1, true).ExecuteScalar();

            // update
            var emp2 = new Employee() { ID = 2, CompanyID = 2, Name = "Updated", Phone = "111" };
            context.CreateUpdate("Employee", emp2, "ID").ExecuteNonQuery();
            // or merge
            context.CreateMerge("Employee", emp2, "ID").ExecuteNonQuery();

            // delete
            context.CreateDelete("Employee", emp2, "ID").ExecuteNonQuery();

            // bulk insert
            Employee[] newEmployees = new Employee[] {
                new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
                new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
                new Employee() { CompanyID = 2, Name = "New Employee1" }
            };

            newEmployees.WriteToServer<Employee>(context, "Employee");
        }

        static DbConnection GetConnection()
        {
            return new SqlConnection(connString);
            //return new NpgsqlConnection(connString);
        }
    }
}
