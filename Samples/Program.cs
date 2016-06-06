// To use these samples:
// 1. Create databases SampleDB1 and SampleDB2 on the your instance of SQL Server
// 2. Execute script InitQuery.sql on database master (or any other database)
// 3. Put your connection strings to static fields connStr and connStr2
// 4. Change sample name in Main() function to one's you interested in
// 5. Set breakpoint to end of this sample 
// 6. Execute program in debug mode


using Gerakul.FastSql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Samples
{
  class Program
  {
    // put here your connection strings
    static string connStr = @"Data Source=localhost;Initial Catalog=SampleDB1;Persist Security Info=True;User ID=****;Password=****";
    static string connStr2 = @"Data Source=localhost;Initial Catalog=SampleDB2;Persist Security Info=True;User ID=****;Password=****";

    static void Main(string[] args)
    {
      // change sample name to one's you interested in
      Sample1();
      Sample1Async().Wait();

      Console.WriteLine("End");
      //Console.ReadKey();
    }

    // Simple retrieving  entities
    static void Sample1()
    {
      var q = SimpleCommand.Compile("select * from Employee").ExecuteQuery<Employee>(connStr);
      var arr = q.ToArray();
    }

    static async Task Sample1Async()
    {
      var q = SimpleCommand.Compile("select * from Employee").ExecuteQueryAsync<Employee>(connStr);
      var arr = await q.ToArray();
    }

    // Using parameterized query
    static void Sample2()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ExecuteQuery<Employee>(connStr);
      var arr1 = q1.ToArray();

      // mapped command
      var q2 = MappedCommand.Compile<OtherEmployee>("select * from Employee where CompanyID = @CompanyID and Age > @Age")
        .ExecuteQuery<Employee>(connStr, new OtherEmployee() { CompanyID = 1, Age = 40 });
      var arr2 = q2.ToArray();

      // mapped command with anonymous type
      var a = new { CompanyID = 1, Age = 40 };
      var q3 = MappedCommand.Compile(a, "select * from Employee where CompanyID = @CompanyID and Age > @Age").ExecuteQuery<Employee>(connStr, a);
      var arr3 = q3.ToArray();
    }

    static async Task Sample2Async()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40).ExecuteQueryAsync<Employee>(connStr);
      var arr1 = await q1.ToArray();

      // mapped command
      var q2 = MappedCommand.Compile<OtherEmployee>("select * from Employee where CompanyID = @CompanyID and Age > @Age")
        .ExecuteQueryAsync<Employee>(connStr, new OtherEmployee() { CompanyID = 1, Age = 40 });
      var arr2 = await q2.ToArray();

      // mapped command with anonymous type
      var a = new { CompanyID = 1, Age = 40 };
      var q3 = MappedCommand.Compile(a, "select * from Employee where CompanyID = @CompanyID and Age > @Age").ExecuteQueryAsync<Employee>(connStr, a);
      var arr3 = await q3.ToArray();
    }

    // Using FieldsSelector option: there will only be filled columns contains in source
    static void Sample3()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select ID, CompanyID, Name, Phone from Employee where CompanyID = @p0", 1)
        .ExecuteQuery<Employee>(connStr, new ExecutionOptions(fieldsSelector: FieldsSelector.Source));
      var arr1 = q1.ToArray();

      // mapped command with anonymous type
      var a = new { CompanyID = 1 };
      var q2 = MappedCommand.Compile(a, "select ID, CompanyID, Name, Phone from Employee where CompanyID = @CompanyID")
        .ExecuteQuery<Employee>(connStr, a, new ExecutionOptions(fieldsSelector: FieldsSelector.Source));
      var arr2 = q2.ToArray();
    }

    static async Task Sample3Async()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select ID, CompanyID, Name, Phone from Employee where CompanyID = @p0", 1)
        .ExecuteQueryAsync<Employee>(connStr, new ExecutionOptions(fieldsSelector: FieldsSelector.Source));
      var arr1 = await q1.ToArray();

      // mapped command with anonymous type
      var a = new { CompanyID = 1 };
      var q2 = MappedCommand.Compile(a, "select ID, CompanyID, Name, Phone from Employee where CompanyID = @CompanyID")
        .ExecuteQueryAsync<Employee>(connStr, a, new ExecutionOptions(fieldsSelector: FieldsSelector.Source));
      var arr2 = await q2.ToArray();
    }

    // Using FieldsSelector option: there will only be filled columns contains both in source and destination 
    static void Sample4()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select * from Employee")
        .ExecuteQuery<OtherEmployee>(connStr, new ExecutionOptions(fieldsSelector: FieldsSelector.Common));
      var arr1 = q1.ToArray();
    }

    static async Task Sample4Async()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select * from Employee")
        .ExecuteQueryAsync<OtherEmployee>(connStr, new ExecutionOptions(fieldsSelector: FieldsSelector.Common));
      var arr1 = await q1.ToArray();
    }

    // Using options
    static void Sample5()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select * from Employee")
        .ExecuteQuery<OtherEmployee>(connStr, new ExecutionOptions(60, FieldsSelector.Common, false, FromTypeOption.Both));
      var arr1 = q1.ToArray();
    }

    static async Task Sample5Async()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select * from Employee")
        .ExecuteQueryAsync<OtherEmployee>(connStr, new ExecutionOptions(60, FieldsSelector.Common, false, FromTypeOption.Both));
      var arr1 = await q1.ToArray();
    }

    // Retrieving anonimous entities
    static void Sample6()
    {
      // simple command
      var proto = new { Company = default(string), Emp = default(string) };
      var q1 = SimpleCommand.Compile("select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID")
        .ExecuteQueryAnonymous(proto, connStr);
      var arr1 = q1.ToArray();
    }

    static async Task Sample6Async()
    {
      // simple command
      var proto = new { Company = default(string), Emp = default(string) };
      var q1 = SimpleCommand.Compile("select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID")
        .ExecuteQueryAnonymousAsync(proto, connStr);
      var arr1 = await q1.ToArray();
    }

    // Retrieving list of values
    static void Sample7()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select ID from Employee").ExecuteQueryFirstColumn<int>(connStr);
      var arr1 = q1.ToArray();
    }

    static async Task Sample7Async()
    {
      // simple command
      var q1 = SimpleCommand.Compile("select ID from Employee").ExecuteQueryFirstColumnAsync<int>(connStr);
      var arr1 = await q1.ToArray();
    }

    // Retrieving one value
    static void Sample8()
    {
      // simple command
      var empName1 = SimpleCommand.Compile("select Name from Employee where CompanyID = @p0 and Age > @p1", 1, 40)
        .ExecuteQueryFirstColumn<string>(connStr).First();

      // mapped command with anonymous type
      var a = new { CompanyID = 1, Age = 40 };
      var empName2 = MappedCommand.Compile(a, "select Name from Employee where CompanyID = @CompanyID and Age > @Age")
        .ExecuteQueryFirstColumn<string>(connStr, a).First();
    }

    static async Task Sample8Async()
    {
      // simple command
      var empName1 = await SimpleCommand.Compile("select Name from Employee where CompanyID = @p0 and Age > @p1", 1, 40)
        .ExecuteQueryFirstColumnAsync<string>(connStr).First();

      // mapped command with anonymous type
      var a = new { CompanyID = 1, Age = 40 };
      var empName2 = await MappedCommand.Compile(a, "select Name from Employee where CompanyID = @CompanyID and Age > @Age")
        .ExecuteQueryFirstColumnAsync<string>(connStr, a).First();
    }

    // Using query with modification and retrieving data
    static void Sample9()
    {
      // simple command
      int newID1 = SimpleCommand.Compile("insert into Employee (CompanyID, Name, Age) values (@p0, @p1, @p2); select cast(scope_identity() as int)", 2, "Mary Grant", 30)
        .ExecuteQueryFirstColumn<int>(connStr).First();

      // mapped command
      var emp = new Employee() { CompanyID = 2, Name = "Mary Grant", Age = 30 };
      int newID2 = MappedCommand.Compile<Employee>("insert into Employee (CompanyID, Name, Age) values (@CompanyID, @Name, @Age); select cast(scope_identity() as int)")
        .ExecuteQueryFirstColumn<int>(connStr, emp).First();
    }

    static async Task Sample9Async()
    {
      // simple command
      int newID1 = await SimpleCommand.Compile("insert into Employee (CompanyID, Name, Age) values (@p0, @p1, @p2); select cast(scope_identity() as int)", 2, "Mary Grant", 30)
        .ExecuteQueryFirstColumnAsync<int>(connStr).First();

      // mapped command
      var emp = new Employee() { CompanyID = 2, Name = "Mary Grant", Age = 30 };
      int newID2 = await MappedCommand.Compile<Employee>("insert into Employee (CompanyID, Name, Age) values (@CompanyID, @Name, @Age); select cast(scope_identity() as int)")
        .ExecuteQueryFirstColumnAsync<int>(connStr, emp).First();
    }

    // Query without retrieving data
    static void Sample10()
    {
      // simple command
      SimpleCommand.Compile("update Company set Name = @p1 where ID = @p0", 2, "Updated Co").ExecuteNonQuery(connStr);

      // mapped command with anonymous type
      var a = new { ID = 2, Name = "Updated Co" };
      MappedCommand.Compile(a, "update Company set Name = @Name where ID = @ID").ExecuteNonQuery(connStr, a);
    }

    static async Task Sample10Async()
    {
      // simple command
      await SimpleCommand.Compile("update Company set Name = @p1 where ID = @p0", 2, "Updated Co").ExecuteNonQueryAsync(connStr);

      // mapped command with anonymous type
      var a = new { ID = 2, Name = "Updated Co" };
      await MappedCommand.Compile(a, "update Company set Name = @Name where ID = @ID").ExecuteNonQueryAsync(connStr, a);
    }

    // Bulk insert of entities to database
    static void Sample11()
    {
      Employee[] newEmployees = new Employee[] {
        new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 2, Name = "New Employee1" }
      };

      newEmployees.WriteToServer(connStr, "Employee");
    }

    static async Task Sample11Async()
    {
      Employee[] newEmployees = new Employee[] {
        new Employee() { CompanyID = 1, Name = "New Employee11", Age = 23, StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 1, Name = "New Employee21", StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 2, Name = "New Employee11" }
      };

      await newEmployees.WriteToServerAsync(connStr, "Employee");
    }

    // Bulk insert of anonimous entities. Using options
    static void Sample12()
    {
      Employee[] newEmployees = new Employee[] { 
        new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 2, Name = "New Employee1" }
      };

      newEmployees.Select(x => new
        {
          companyid = x.CompanyID,
          x.Name,
          phone = "111-111-111",
          startWorking = x.StartWorking.HasValue ? x.StartWorking : DateTime.UtcNow,
          x.Age
        }).WriteToServer(new BulkOptions(1000, 100, SqlBulkCopyOptions.Default, FieldsSelector.Source, false, true), connStr, "Employee");
    }

    static async Task Sample12Async()
    {
      Employee[] newEmployees = new Employee[] {
        new Employee() { CompanyID = 1, Name = "New Employee12", Age = 23, StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 1, Name = "New Employee22", StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 2, Name = "New Employee12" }
      };

      await newEmployees.Select(x => new
      {
        companyid = x.CompanyID,
        x.Name,
        phone = "111-111-111",
        startWorking = x.StartWorking.HasValue ? x.StartWorking : DateTime.UtcNow,
        x.Age
      }).WriteToServerAsync(new BulkOptions(1000, 100, SqlBulkCopyOptions.Default, FieldsSelector.Source, false, true), connStr, "Employee");
    }

    // Using transaction
    static void Sample13()
    {
      Employee[] newEmployees = new Employee[] { 
        new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 2, Name = "New Employee1" }
      };

      // simple command
      var precompiled1 = SimpleCommand.Compile("select * from Employee where Age is not null");
      // mapped command
      var precompiled2 = MappedCommand.Compile<Employee>("update Employee set Age = Age + 1 where ID = @ID");

      // using method SqlScope.UsingTransaction
      SqlScope.UsingTransaction(connStr, scope =>
        {
          // using precompiled simple command
          var arr = precompiled1.Create(scope).ExecuteQuery<Employee>().ToArray();

          // using precompiled mapped command
          Employee e = new Employee() { ID = 3 };
          precompiled2.Create(scope, e).ExecuteNonQuery();
          e.ID = 4;
          precompiled2.Create(scope, e).ExecuteNonQuery();

          newEmployees.WriteToServer(scope.Transaction, "Employee");
        });

      // using external transaction
      using (SqlConnection conn = new SqlConnection(connStr))
      {
        conn.Open();

        var tran = conn.BeginTransaction();

        try
        {
          SqlScope scope = new SqlScope(tran);

          // using precompiled simple command
          var arr = precompiled1.Create(scope).ExecuteQuery<Employee>().ToArray();

          // using precompiled mapped command
          Employee e = new Employee() { ID = 3 };
          precompiled2.Create(scope, e).ExecuteNonQuery();
          e.ID = 4;
          precompiled2.Create(scope, e).ExecuteNonQuery();

          newEmployees.WriteToServer(scope.Transaction, "Employee");

          tran.Commit();
        }
        catch
        {
          tran.Rollback();
          throw;
        }
      }
    }

    static async Task Sample13Async()
    {
      Employee[] newEmployees = new Employee[] {
        new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 2, Name = "New Employee1" }
      };

      // simple command
      var precompiled1 = SimpleCommand.Compile("select * from Employee where Age is not null");
      // mapped command
      var precompiled2 = MappedCommand.Compile<Employee>("update Employee set Age = Age + 1 where ID = @ID");

      // using method SqlScope.UsingTransaction
      await SqlScope.UsingTransactionAsync(connStr, async scope =>
      {
        // using precompiled simple command
        var arr = await precompiled1.Create(scope).ExecuteQueryAsync<Employee>().ToArray();

        // using precompiled mapped command
        Employee e = new Employee() { ID = 3 };
        await precompiled2.Create(scope, e).ExecuteNonQueryAsync();
        e.ID = 4;
        await precompiled2.Create(scope, e).ExecuteNonQueryAsync();

        await newEmployees.WriteToServerAsync(scope.Transaction, "Employee");
      });

      // using external transaction
      using (SqlConnection conn = new SqlConnection(connStr))
      {
        await conn.OpenAsync();

        var tran = conn.BeginTransaction();

        try
        {
          SqlScope scope = new SqlScope(tran);

          // using precompiled simple command
          var arr = await precompiled1.Create(scope).ExecuteQueryAsync<Employee>().ToArray();

          // using precompiled mapped command
          Employee e = new Employee() { ID = 3 };
          await precompiled2.Create(scope, e).ExecuteNonQueryAsync();
          e.ID = 4;
          await precompiled2.Create(scope, e).ExecuteNonQueryAsync();

          await newEmployees.WriteToServerAsync(scope.Transaction, "Employee");

          tran.Commit();
        }
        catch
        {
          tran.Rollback();
          throw;
        }
      }
    }

    // Getting IDataReader from IEnumerable<T>
    static void Sample14()
    {
      Employee[] newEmployees = new Employee[] {
        new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 2, Name = "New Employee1" }
      };

      IDataReader dataReader1 = newEmployees.ToDataReader();

      IDataReader dataReader2 = newEmployees.Select(x => new
      {
        companyid = x.CompanyID,
        x.Name,
        phone = "111-111-111",
        startWorking = x.StartWorking.HasValue ? x.StartWorking : DateTime.UtcNow,
        x.Age
      }).ToDataReader();
    }

    // Getting IEnumerable<T> from IDataReader
    static void Sample15()
    {
      SqlScope.UsingConnection(connStr, scope =>
      {
        using (var reader = SimpleCommand.Compile("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 30).Create(scope).ExecuteReader())
        {
          IEnumerable<Employee> eployeeEnumerable = reader.ReadAll<Employee>();
          var arr = eployeeEnumerable.ToArray();
        }
      });
    }

    static async Task Sample15Async()
    {
      await SqlScope.UsingConnectionAsync(connStr, async scope =>
      {
        using (var reader = await SimpleCommand.Compile("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 30).Create(scope).ExecuteReaderAsync())
        {
          var eployeeEnumerable = reader.ReadAllAsync<Employee>();
          var arr = await eployeeEnumerable.ToArray();
        }
      });
    }

    // Data import between databases
    static void Sample16()
    {
      // Using options: SqlBulkCopyOptions
      Import.Execute(new ImportOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.KeepIdentity), connStr, connStr2, "select ID, Name, Age from Employee where Age > @p0", "Person", 30);

      // Simple import
      Import.Execute(connStr, connStr2, "select Name, Age from Employee where Age <= @p0", "Person", 30);
    }

    static async Task Sample16Async()
    {
      // Using options: SqlBulkCopyOptions
      await Import.ExecuteAsync(new ImportOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.KeepIdentity), connStr, connStr2, "select ID, Name, Age from Employee where Age > @p0", "Person", 30);

      // Simple import
      await  Import.ExecuteAsync(connStr, connStr2, "select Name, Age from Employee where Age <= @p0", "Person", 30);
    }
  }

  public class C
  {
    public int ID;
    public int Index1;
    public int Index2;
  }
}
