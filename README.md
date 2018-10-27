_**NOTE:** The old version of FastSql has been moved to old\_version branch_
# FastSql
Light-weight, flexible and easy to use C# library to retrieve and manipulate (including bulk copy) data in SQL databases. Now it has implemented for Sql Server and PostgreSQL. Examples below have been written for Sql Server, but examples for PostgreSQL would look same.

**NOTE:** Put "using Gerakul.FastSql.Common;" in your code file to use Create<...> methods.

## Simple example
  ```csharp
  // get context
  var context = SqlContextProvider.DefaultInstance.CreateContext("your connection string");

  // create and execute command
  var employees = context.CreateSimple("select * from Employee").ExecuteQuery<Employee>().ToArray();
  ```
## Context provider
For working with certain RDBMS you need appropriate context provider. For most cases default instance of context provider is enough.
  ```csharp
  // for Sql Server
  var sqlProvider = SqlContextProvider.DefaultInstance;

  // for PostgreSQL
  var postgreProvider = NpgsqlContextProvider.DefaultInstance;
  ```

## Getting context
For doing queries to a database, you need to get a context first. There are three types of context based on connection string, connection, and transaction respectively.
1. To get context based on connection string:
  ```csharp
  var context = provider.CreateContext("your database connection string");
  ```
2. To get context based on connection:
  ```csharp
  // dbConnection derived from type System.Data.Common.DbConnection
  var context = provider.CreateContext(dbConnection);
  ```
3. To get context based on transaction:
  ```csharp
  // dbTransaction derived from type System.Data.Common.DbTransaction
  var context = provider.CreateContext(dbTransaction);
  ```
All of these context types have the same functionality regarding to creating an executing commands. 
You can also get ConnectionContext and TransactionContext from ConnectionStringContext, and TransactionContext from ConnectionContext.
  ```csharp
  
  // context from connection string
  var context = provider.CreateContext("your database connection string");
  
  // using connection
  context.UsingConnection(connectionContext =>
  {
      // doing something with connectionContext (attached to opened connection)
      // for example using transaction
      connectionContext.UsingTransaction(transactionContext =>
      {
          // doing something with transactionContext (attached to active transaction)
      });
  });
  // or asynchronously
  await context.UsingConnectionAsync(async connectionContext =>
  {
      // doing something with connectionContext (attached to opened connection)
      // for example using transaction
      await connectionContext.UsingTransactionAsync(async transactionContext =>
      {
          // doing something with transactionContext (attached to active transaction)
      });
  });
  
  // using transaction
  context.UsingTransaction(transactionContext =>
  {
      // doing something with transactionContext (attached to active transaction)
  });
  // or asynchronously
  await context.UsingTransactionAsync(async transactionContext =>
  {
      // doing something with transactionContext (attached to active transaction)
  });
  ```

## Doing queries
Since you have the context, you can work with a database. Most of the functionality of specific context is derived from interface ICommandCreator regardless of context type (connection string, connection or transaction). Most of the methods of the interface ICommandCreator and its extensions return IWrappedCommand. Interface IWrappedCommand provides all methods for command execution, so a combination of these two interfaces and their extensions creates powerful flexibility.

Simple retrieving data
  ```csharp
  var data1 = context.CreateSimple("select * from Employee").ExecuteQuery<Employee>().ToArray();
  // or async
  var data2 = await context.CreateSimple("select * from Employee").ExecuteQueryAsync<Employee>().ToArray();
  ```
Using parameters
  ```csharp
  // simple
  var data3 = context.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ExecuteQuery<Employee>().ToArray();
  // or async
  var data4 = await context.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ExecuteQueryAsync<Employee>().ToArray();
  // parameters mapped to object
  var data5 = context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 }).ExecuteQuery<Employee>().ToArray();
  // or async
  var data6 = await context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 }).ExecuteQueryAsync<Employee>().ToArray();
  ```

**NOTE:** all commands can be executed asynchronously but examples below only show synchronous execution in order not to complicate them.

Using options 
  ```csharp
  // NOTE: options can be set for context or ContextProvider as well as for certain command
  var data7 = context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 },
      new QueryOptions(1000, true))
      .ExecuteQuery<Employee>(new ReadOptions(FieldsSelector.Destination)).ToArray();
  ```
  
Retrieving anonymous entities
  ```csharp
  var data8 = context.CreateSimple("select * from Employee").ExecuteQueryAnonymous(new { ID = 0, Name = ""}).ToArray();
  ```
Retrieving list of values
  ```csharp
  var data9 = context.CreateSimple("select ID from Employee").ExecuteQueryFirstColumn<int>().ToArray();
  ```  
Execute without retrieving data
  ```csharp
  context.CreateSimple("update Company set Name = @p1 where ID = @p0", 2, "Updated Co").ExecuteNonQuery();
  ```  
Insert
  ```csharp
  var emp = new { CompanyID = 2, Name = "New inserted", Phone = "111" };
  context.Insert("Employee", emp);
  // or with returning some inserted fields
  var id = context.CreateInsertWithOutput("Employee", emp, "ID").ExecuteQueryFirstColumn<int>().First();
  ```  
Update
  ```csharp
  var emp = new { ID = 2, CompanyID = 2, Name = "Updated", Phone = "111" };
  context.Update("Employee", emp, "ID");
  ```
Merge
  ```csharp
  var company = new { ID = 3, Name = "New name", DateOfFoundation = DateTime.Now };
  context.Merge("Company", company, "ID");
  ```  
Delete
  ```csharp
  var emp = new { ID = 2, CompanyID = 2, Name = "Updated", Phone = "111" };
  context.Delete("Employee", emp, "ID");
  // or even simplier
  context.Delete("Employee", new { ID = 2 });
  ```
Bulk copy
  ```csharp
  Employee[] newEmployees = new Employee[] {
      new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
      new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
      new Employee() { CompanyID = 2, Name = "New Employee1" }
  };
  
  newEmployees.WriteToServer(context, "Employee");
  ```  
Stored procedures
  ```csharp
  var data10 = context.CreateProcedure("TestProc", new { CompanyID = 1, Age = 40 }).ExecuteQuery<Employee>().ToArray();
  // passing custom DbParameters
  var data11 = context.CreateProcedureSimple("TestProc", new SqlParameter("CompanyID", 1), new SqlParameter("Age", 40)).ExecuteQuery<Employee>().ToArray();
  ```  
Data import
  ```csharp
  // NOTE: Here are only a couple of data import examples, but WriteToServer functionality can be applied for any IWrappedCommand
  context.CreateSimple("select * from Employee").WriteToServer(context, "Employee");
  context.CreateProcedure("TestProc", new { CompanyID = 1, Age = 40 }).WriteToServer(context, "Employee");
  ```

## Extensibility
You can add your own methods accessible from context easily writing extensions for interface ICommandCreator
  ```csharp
  public static partial class CommandCreatorExtensions
  {
      // making your own custom command accessible from all types of contexts (connection string, connection, transaction)
      public static IWrappedCommand CreateSelectTop10Employees(this ICommandCreator creator)
      {
          return creator.Set(c =>
          {
              var cmd = c.CreateCommand("select top 10 * from dbo.Employee");
              cmd.CommandType = System.Data.CommandType.Text;
              cmd.CommandTimeout = 1000;
              return cmd;
          });
      }
  
      // combining Create and Execute
      public static T[] GetTable<T>(this ICommandCreator creator, string tableName) where T : new()
      {
          return creator.CreateSimple($"select * from {tableName}").ExecuteQuery<T>().ToArray();
      }
  }
  ```
  then you can use it everywhere from any type of context
  ```csharp
  // using custom extensions of ICommandCreator
  var data1 = context.CreateSelectTop10Employees().ExecuteQuery<Employee>().ToArray();
  var data2 = context.GetTable<Employee>("Employee");
  ```
