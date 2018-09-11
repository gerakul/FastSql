using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Examples
{
    class Program
    {
        static string connString = @"{your connection string}";

        static void Main(string[] args)
        {
            // Examples below have been written for Sql Server, but examples for PostgreSQL would look same
            // NOTE: first execute script InitQuery.sql on your test database before executing examples

            // get context
            var context = SqlContextProvider.DefaultInstance.CreateContext(connString);

            // create and execute command
            var employees = context.CreateSimple("select * from Employee").ExecuteQuery<Employee>().ToArray();

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
            // all these types of context implement the same interface ICommandCreator

            // context from connection string
            var context1 = provider.CreateContext(connString);

            // context from connection
            var connection = GetOpenConnection();
            var context2 = provider.CreateContext(connection);
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
            var context3 = provider.CreateContext(transaction);
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

        static async Task WorkingWithContext(DbContext context)
        {
            // Most of functionality of specific context is derived from interface ICommandCreator
            // regardless of context type (connection string, connection or transaction).
            // All methods of the interface ICommandCreator and its extensions return IWrappedCommand.
            // Interface IWrappedCommand provides all methods for command execution,
            // so a combination of these two interfaces creates a powerful flexibility

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
            var data6 = await context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 }).ExecuteQueryAsync<Employee>().ToArray();

            // NOTE: all commands can be executed asynchronously but examples below only show synchronous execution in order not to complicate them

            // using options 
            // NOTE: options can be set for context or ContextProvider as well as for certain command
            var data7 = context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 },
                new QueryOptions(1000, true))
                .ExecuteQuery<Employee>(new ReadOptions(FieldsSelector.Destination)).ToArray();

            // retrieving anonymous entities
            var data8 = context.CreateSimple("select * from Employee").ExecuteQueryAnonymous(new { ID = 0, Name = ""}).ToArray();

            // retrieving list of values
            var data9 = context.CreateSimple("select ID from Employee").ExecuteQueryFirstColumn<int>().ToArray();

            // execute without retrieving data
            context.CreateSimple("update Company set Name = @p1 where ID = @p0", 2, "Updated Co").ExecuteNonQuery();

            // insert
            var emp1 = new { CompanyID = 2, Name = "New inserted", Phone = "111" };
            context.CreateInsert("Employee", emp1).ExecuteNonQuery();
            // or return new id
            var id = context.CreateInsert("Employee", emp1, true).ExecuteScalar();

            // update
            var emp2 = new { ID = 2, CompanyID = 2, Name = "Updated", Phone = "111" };
            context.CreateUpdate("Employee", emp2, "ID").ExecuteNonQuery();
            // or merge
            var company = new { ID = 3, Name = "New name", DateOfFoundation = DateTime.Now };
            context.CreateMerge("Company", company, "ID").ExecuteNonQuery();

            // delete
            context.CreateDelete("Employee", emp2, "ID").ExecuteNonQuery();

            // bulk copy
            Employee[] newEmployees = new Employee[] {
                new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
                new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
                new Employee() { CompanyID = 2, Name = "New Employee1" }
            };

            newEmployees.WriteToServer(context, "Employee");

            // stored procedures
            var data10 = context.CreateProcedure("TestProc", new { CompanyID = 1, Age = 40 }).ExecuteQuery<Employee>().ToArray();
            // passing custom DbParameters
            var data11 = context.CreateProcedureSimple("TestProc", new SqlParameter("CompanyID", 1), new SqlParameter("Age", 40)).ExecuteQuery<Employee>().ToArray();

            // data import
            // NOTE: Here are only couple of data import examples, but WriteToServer functionality can be applied for any IWrappedCommand
            context.CreateSimple("select * from Employee").WriteToServer(context, "Employee");
            context.CreateProcedure("TestProc", new { CompanyID = 1, Age = 40 }).WriteToServer(context, "Employee");
        }

        static DbConnection GetOpenConnection()
        {
            var conn = new SqlConnection(connString);
            // var conn = NpgsqlConnection(connString);
            conn.Open();
            return conn;
        }
    }
}
