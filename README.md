_**NOTE:** The old version of FastSql has been moved to old\_version branch_
# FastSql
Light-weight, flexible and easy to use C# library to retrieve and manipulate (including bulk copy) data in SQL databases. Now it has implemented for Sql Server and PostgreSQL. Examples below have been written for Sql Server, but examples for PostgreSQL would look same.

**NOTE:** Put "using Gerakul.FastSql.Common;" in your code file to use Create<...> methods.

## Packages
- `Gerakul.FastSql.SqlServer` for Sql Server, `Gerakul.FastSql.PostgreSQL` for PostgreSQL. Both bring `Gerakul.FastSql.Common`, the part they share.

## Requirements
- .NET 10.
- Sql Server: Microsoft.Data.SqlClient. It encrypts connections by default, so a server without a trusted certificate needs `TrustServerCertificate=True` in the connection string.
- PostgreSQL: Npgsql 10. `CreateProcedure` calls procedures (with `CALL`), not functions.
- On both: `date` and `time` columns can be read into `DateOnly` / `TimeOnly` members as well as into `DateTime` / `TimeSpan` members.

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

  // returning a result
  var count = context.UsingTransaction(transactionContext =>
  {
      transactionContext.Insert("Employee", new { CompanyID = 1, Name = "New" });
      return transactionContext.CreateSimple("select count(*) from Employee").ExecuteScalar<int>();
  });
  ```
When the action throws, the transaction is rolled back and the exception goes to the caller. A context created from a connection or a transaction does not open, close, commit or roll back what it was created from: the connection must be open, and the caller ends the transaction.

**NOTE:** return what you need, not a query. A query returned from `UsingConnection` or `UsingTransaction` (`c => c.CreateSimple(text).ExecuteQuery<Employee>()`, or `ExecuteQueryAsync`) is read only when it is enumerated, after the connection is closed, and fails then; return `ToArray()` or `ToList()` of it. Async work goes into `UsingConnectionAsync` and `UsingTransactionAsync`: the sync methods refuse a delegate that returns a task, because their connection or transaction would end at its first `await`.

## Doing queries
Since you have the context, you can work with a database. Most of the functionality of specific context is derived from interface ICommandCreator regardless of context type (connection string, connection or transaction). Most of the methods of the interface ICommandCreator and its extensions return IWrappedCommand. Interface IWrappedCommand provides all methods for command execution, so a combination of these two interfaces and their extensions creates powerful flexibility.

Simple retrieving data
  ```csharp
  var data1 = context.CreateSimple("select * from Employee").ExecuteQuery<Employee>().ToArray();
  // or async
  var data2 = await context.CreateSimple("select * from Employee").ExecuteQueryAsync<Employee>().ToArrayAsync();
  ```
Using parameters
  ```csharp
  // simple
  var data3 = context.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ExecuteQuery<Employee>().ToArray();
  // or async
  var data4 = await context.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ExecuteQueryAsync<Employee>().ToArrayAsync();
  // parameters mapped to object
  var data5 = context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 }).ExecuteQuery<Employee>().ToArray();
  // or async
  var data6 = await context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 }).ExecuteQueryAsync<Employee>().ToArrayAsync();
  ```

**NOTE:** all commands can be executed asynchronously but examples below only show synchronous execution in order not to complicate them.

**NOTE:** a query runs when its result is enumerated, and its rows are read one at a time; `ToArray()` or `ToList()` reads them all. On a connection string context the connection stays open until the enumeration ends.

**NOTE:** table names, field names and command texts go into the SQL as they are written. Pass values as parameters, and never build names or command texts from input you do not trust.

Using options 
  ```csharp
  // NOTE: options can be set for context or ContextProvider as well as for certain command
  var data7 = context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 },
      new QueryOptions(1000, true))
      .ExecuteQuery<Employee>(new ReadOptions(FieldsSelector.Destination)).ToArray();
  ```
See [Options](#options) for what they are and which one wins.
  
Retrieving anonymous entities
  ```csharp
  var data8 = context.CreateSimple("select * from Employee").ExecuteQueryAnonymous(new { ID = 0, Name = ""}).ToArray();
  ```
Retrieving records and other types without a parameterless constructor
  ```csharp
  public record EmployeeName(int ID, string Name);

  var names = context.CreateSimple("select ID, Name from Employee").ExecuteQuery<EmployeeName>().ToArray();
  ```
**NOTE:** such a type is created with its constructor that has the most parameters, and the fields are passed to the parameters by name.

Retrieving list of values
  ```csharp
  var data9 = context.CreateSimple("select ID from Employee").ExecuteQueryFirstColumn<int>().ToArray();
  ```  
Retrieving a single value
  ```csharp
  var employeeCount = context.CreateSimple("select count(*) from Employee").ExecuteScalar<int>();
  var lastStart = context.CreateSimple("select max(StartWorking) from Employee").ExecuteScalar<DateTime?>();
  ```
**NOTE:** `ExecuteScalar<T>` converts the value the way rows are mapped. A null, or no row at all, gives `null` for a type that can be null and an `InvalidCastException` for one that cannot.

Reading the data reader yourself
  ```csharp
  context.CreateSimple("select * from Employee").UseReader(reader =>
  {
      while (reader.Read())
      {
          // ...
      }
  });
  ```
The reader is closed when the action returns, and so is the connection of a connection string context.

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
  // or returning the generated ID, without naming its column
  var newId = context.CreateInsertAndGetID("Employee", emp).ExecuteScalar<int>();
  ```  
**NOTE:** `CreateInsertAndGetID` returns `scope_identity()` on Sql Server, a `decimal`, and the inserted row's value of its `serial` or identity column on PostgreSQL, a `bigint`; `ExecuteScalar<int>()` and `ExecuteScalar<long>()` read both, and a table without such a column gives null. Leave the ID out of the object, or name it in the ignore list: `CreateInsertAndGetID("Employee", employee, "ID")`.

Update
  ```csharp
  var emp = new { ID = 2, CompanyID = 2, Name = "Updated", Phone = "111" };
  context.Update("Employee", emp, "ID");
  ```
**NOTE:** `Insert`, `Update`, `Delete` and `Merge` return the number of rows affected, so `context.Update("Employee", emp, "ID") == 0` says that there was no such row.

Merge
  ```csharp
  var company = new { ID = 3, Name = "New name", DateOfFoundation = DateTime.Now };
  context.Merge("Company", company, "ID");
  ```  
**NOTE:** when every member is a key field, Merge inserts the row if it is missing and leaves an existing row as it is.

Delete
  ```csharp
  var emp = new { ID = 2, CompanyID = 2, Name = "Updated", Phone = "111" };
  context.Delete("Employee", emp, "ID");
  // or even simpler
  context.Delete("Employee", new { ID = 2 });
  ```
**NOTE:** key values are compared with `=`, so a null key value matches no rows. Without key fields every member is a key field, so an object with a null member deletes nothing.

Bulk copy
  ```csharp
  Employee[] newEmployees = new Employee[] {
      new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
      new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
      new Employee() { CompanyID = 2, Name = "New Employee1" }
  };
  
  newEmployees.WriteToServer(context, "Employee");
  ```  
**NOTE:** on Sql Server the default bulk options pass the field names to SqlBulkCopy as they are, and SqlBulkCopy matches them with the column names case-sensitively. To match names regardless of case, set `CaseSensitiveFieldsMatching = false` in the bulk options, for example `new SqlBulkOptions(caseSensitiveFieldsMatching: false)`; FastSql then reads the table's column names first.

An `IAsyncEnumerable<T>` is written the same way, with `await rows.WriteToServerAsync(context, "Employee")`.

Bulk copy can create the table first, on both databases:
  ```csharp
  newEmployees.WriteToServer(context, "NewEmployee", new BulkOptions(createTable: true));

  // the column sizes and the primary key (NpgsqlBulkOptions takes the PostgreSQL ColumnDefinitionOptions)
  newEmployees.WriteToServer(context, "NewEmployee", new SqlBulkOptions(createTable: true, checkTableIfNotExistsBeforeCreation: true,
      columnDefinitionOptions: ColumnDefinitionOptions.FromSmall("ID")));
  ```
The column types come from the source: for a query of the same database, the types of its columns; for objects, or a query of the other database, the types of the fields, where a value type gives a `not null` column. `checkTableIfNotExistsBeforeCreation` creates the table only when it does not exist. On PostgreSQL the column names are quoted, so they keep their case; a table in the `pg_temp` schema (`pg_temp.new_employee`) is a temporary table; and a query reports nullability and keys only when it is read with `CommandBehavior.KeyInfo`, so without it every column is nullable and there is no primary key unless `ColumnDefinitionOptions.PrimaryKey` names one.

`BulkCopyTimeout` (seconds, 0 for no limit) sets the timeout of the copy on both databases. When it expires, Npgsql also closes the connection.

`ToDataReader` presents objects as a data reader, which anything that reads a `DbDataReader` can take. `FieldSettings` choose its fields when the public members are not the ones to write:
  ```csharp
  var reader = newEmployees.ToDataReader(
      new FieldSettings<Employee>("CompanyID", typeof(int), x => x.CompanyID),
      new FieldSettings<Employee>("Name", typeof(string), x => x.Name?.Trim()));

  reader.WriteToServer(context, "Employee");
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
The target context can be of another database, also of the other provider: this is how data is copied between databases.

Precompiled commands
  ```csharp
  // the command text and the parameter map are built once
  var insert = context.CommandCompilator.CompileInsert<Employee>("Employee", "ID");
  foreach (var employee in newEmployees)
  {
      context.CreateMapped(insert, employee).ExecuteNonQuery();
  }

  var byCompany = context.CommandCompilator.CompileSimple("select * from Employee where CompanyID = @p0");
  var data12 = context.CreateSimple(byCompany, 1).ExecuteQuery<Employee>().ToArray();
  ```
**NOTE:** `Insert`, `CreateMapped` and the other methods that take an object keep the last command they built for each type and kind of command, so repeating a call with the same table and fields costs little without precompiling. Precompile to keep several commands of one kind, or commands built from your own `FieldSettings`.

Cancellation
  ```csharp
  using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
  var token = cts.Token;

  var data13 = await context.CreateSimple("select * from Employee").ExecuteQueryAsync<Employee>(cancellationToken: token).ToArrayAsync(token);

  await context.UsingTransactionAsync(async transactionContext =>
  {
      await transactionContext.CreateInsert("Employee", new { CompanyID = 1, Name = "New" }).ExecuteNonQueryAsync(token);
  }, token);
  ```
The token of `UsingConnectionAsync` and `UsingTransactionAsync` cancels opening the connection, beginning and committing the transaction; the action gets no token, so pass it to the commands inside yourself.

## Options
There are three kinds of options:
- `QueryOptions` (`SqlQueryOptions`, `NpgsqlQueryOptions`): the command timeout, and whether parameters are matched to members with regard to case (default: no).
- `ReadOptions`: how the fields of a result are matched to members: `FieldsSelector` (default `Destination`), which members are filled (`FromTypeOption`; by default the public fields and properties, except the ones of collection types, that is anything enumerable other than `string` and sequences of bytes such as `byte[]`; a property without a setter is not filled), and the case (default: ignored).
- `BulkOptions` (`SqlBulkOptions`, `NpgsqlBulkOptions`): how the fields of the source are matched to the columns of the table (`FieldsSelector`, default `Source`), the case, table creation and how its columns are declared (`ColumnDefinitionOptions`), the timeout, and for Sql Server the other settings of `SqlBulkCopy` (batch size, `SqlBulkCopyOptions`, streaming).

A value is taken from the first place that sets it (a property that is `null` sets nothing): the options of the call, then the options of the context, then the defaults. A context starts with a copy of its provider's options, so a change to the provider's options applies to the contexts created after it, and a change to a context's options applies to that context only:
  ```csharp
  // for every context created from this provider afterwards
  var provider = new SqlContextProvider();
  provider.QueryOptions.CommandTimeoutSeconds = 60;

  // for this context
  var context = provider.CreateContext("your database connection string");
  context.ReadOptions.CaseSensitiveFieldsMatching = true;

  // for this call
  var data14 = context.CreateSimple(new QueryOptions(commandTimeoutSeconds: 300), "select * from Employee").ExecuteQuery<Employee>().ToArray();
  ```

`FieldsSelector` says which names must have a counterpart. For reading, the source is the fields of the result and the destination is the members of the type; for bulk copy, the source is the fields that are written and the destination is the columns of the table. A name without a counterpart that the selector requires is an `InvalidOperationException`; any other name is left out.

| Value | Requires |
|---|---|
| `Source` | a counterpart for every name of the source |
| `Destination` | a counterpart for every name of the destination |
| `Common` | nothing: the names that both sides have are used |
| `Both` | a counterpart for every name on both sides |

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
