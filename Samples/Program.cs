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
      string cs = @"Data Source=localhost;Initial Catalog=SampleDB1;Persist Security Info=True;User ID=NaviService;Password=Navi100$";

      var aa = new
      {
        Name = "yyyyyyyyyyyy",
        CompanyID = 2,
        Age = 30
      };

      Commands.Compile(aa, "update [dbo].[Employee] set Name = @Name where CompanyID = @CompanyID and Age >= @Age").ExecuteNonQueryAsync(cs, aa).Wait();
      return;
      var pc = Commands.Compile<Employee>("update [dbo].[Employee] set Name = @Name where CompanyID = @CompanyID and Age >= @Age");

      Employee[] arr;
      Employee[] arr1;
      var e = new Employee() { CompanyID = 2, Age = 30, Name = "7777777" };
      SqlExecutor.UsingConnectionAsync(async x =>
      {
        var comm = pc.Create(x.Connection);

        await comm.ExecuteNonQueryAsync(e).ConfigureAwait(false);
        e.CompanyID = 1;
        e.Name = "8888888";
        await comm.ExecuteNonQueryAsync(e).ConfigureAwait(false);

        arr = await x.ExecuteQueryAsync<Employee>("select * from Employee").ToArray();

        var r = await Commands.Compile(aa, "select * from Employee where CompanyID = @CompanyID and Age >= @Age")
          .Create(x.Connection).ExecuteReaderAsync(aa);
        arr1 = await r.ReadAllAsync<Employee>(new ReadOptions(FieldsSelector.Destination)).ToArray();
        r.Close();
      }, cs).Wait();

      Commands.Compile(aa, "update [dbo].[Employee] set Name = @Name where CompanyID = @CompanyID and Age >= @Age").ExecuteNonQueryAsync(connStr, aa).Wait();

      return;



      using (SqlConnection conn = new SqlConnection(cs))
      {
        conn.Open();

        SqlCommand cmd = new SqlCommand("update [dbo].[Employee] set Name = @Na#$me where ID = @ID", conn);

        var p1 = new SqlParameter();
        p1.ParameterName = "@Na#$me";
        cmd.Parameters.Add(p1);

        var p2 = new SqlParameter();
        p2.ParameterName = "@iD";
        cmd.Parameters.Add(p2);

        p1.Value = "111111";
        p2.Value = 22;

        cmd.ExecuteNonQuery();
      }


      return;

      // change sample name to one's you interested in
      Sample1();
      Sample1Async().Wait();

      Console.WriteLine("End");
      //Console.ReadKey();
    }

    // Simple retrieving  entities
    static void Sample1()
    {
      var q = SqlExecutor.ExecuteQuery<Employee>(connStr, "select * from Employee");
      var arr = q.ToArray();
    }

    static async Task Sample1Async()
    {
      var q = SqlExecutor.ExecuteQueryAsync<Employee>(connStr, "select * from Employee");
      var arr = await q.ToArray();
    }

    // Using parameterized query
    static void Sample2()
    {
      var q = SqlExecutor.ExecuteQuery<Employee>(connStr, "select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40);
      var arr = q.ToArray();
    }

    static async Task Sample2Async()
    {
      var q = SqlExecutor.ExecuteQueryAsync<Employee>(connStr, "select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40);
      var arr = await q.ToArray();
    }

    // Using FieldsSelector option: there will only be filled columns contains in source
    static void Sample3()
    {
      var q = SqlExecutor.ExecuteQuery<Employee>(new SqlExecutorOptions(fieldsSelector: FieldsSelector.Source), connStr, "select ID, CompanyID, Name, Phone from Employee where CompanyID = @p0", 1);
      var arr = q.ToArray();
    }

    static async Task Sample3Async()
    {
      var q = SqlExecutor.ExecuteQueryAsync<Employee>(new SqlExecutorOptions(fieldsSelector: FieldsSelector.Source), connStr, "select ID, CompanyID, Name, Phone from Employee where CompanyID = @p0", 1);
      var arr = await q.ToArray();
    }

    // Using FieldsSelector option: there will only be filled columns contains both in source and destination 
    static void Sample4()
    {
      var q = SqlExecutor.ExecuteQuery<OtherEmployee>(new SqlExecutorOptions(fieldsSelector: FieldsSelector.Common), connStr, "select * from Employee");
      var arr = q.ToArray();
    }

    static async Task Sample4Async()
    {
      var q = SqlExecutor.ExecuteQueryAsync<OtherEmployee>(new SqlExecutorOptions(fieldsSelector: FieldsSelector.Common), connStr, "select * from Employee");
      var arr = await q.ToArray();
    }

    // Using options
    static void Sample5()
    {
      var q = SqlExecutor.ExecuteQuery<OtherEmployee>(new SqlExecutorOptions(60, FieldsSelector.Common, false, FromTypeOption.Both), connStr, "select * from Employee");
      var arr = q.ToArray();
    }

    static async Task Sample5Async()
    {
      var q = SqlExecutor.ExecuteQueryAsync<OtherEmployee>(new SqlExecutorOptions(60, FieldsSelector.Common, false, FromTypeOption.Both), connStr, "select * from Employee");
      var arr = await q.ToArray();
    }

    // Retrieving anonimous entities
    static void Sample6()
    {
      var proto = new { Company = default(string), Emp = default(string) };
      var q = SqlExecutor.ExecuteQueryAnonymous(proto, connStr, "select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID");
      var arr = q.ToArray();
    }

    static async Task Sample6Async()
    {
      var proto = new { Company = default(string), Emp = default(string) };
      var q = SqlExecutor.ExecuteQueryAnonymousAsync(proto, connStr, "select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID");
      var arr = await q.ToArray();
    }

    // Retrieving list of values
    static void Sample7()
    {
      var q = SqlExecutor.ExecuteQueryFirstColumn<int>(connStr, "select ID from Employee");
      var arr = q.ToArray();
    }

    static async Task Sample7Async()
    {
      var q = SqlExecutor.ExecuteQueryFirstColumnAsync<int>(connStr, "select ID from Employee");
      var arr = await q.ToArray();
    }

    // Retrieving one value
    static void Sample8()
    {
      var empName = SqlExecutor.ExecuteQueryFirstColumn<string>(connStr, "select Name from Employee where CompanyID = @p0 and Age > @p1", 1, 40).First();
    }

    static async Task Sample8Async()
    {
      var empName = await SqlExecutor.ExecuteQueryFirstColumnAsync<string>(connStr, "select Name from Employee where CompanyID = @p0 and Age > @p1", 1, 40).First();
    }

    // Using query with modification and retrieving data
    static void Sample9()
    {
      int newID = SqlExecutor.ExecuteQueryFirstColumn<int>(connStr, "insert into Employee (CompanyID, Name, Age) values (@p0, @p1, @p2); select cast(scope_identity() as int)", 2, "Mary Grant", 30).First();
    }

    static async Task Sample9Async()
    {
      int newID = await SqlExecutor.ExecuteQueryFirstColumnAsync<int>(connStr, "insert into Employee (CompanyID, Name, Age) values (@p0, @p1, @p2); select cast(scope_identity() as int)", 2, "Mary Grant", 30).First();
    }

    // Query without retrieving data
    static void Sample10()
    {
      SqlExecutor.ExecuteNonQuery(connStr, "update Company set Name = @p1 where ID = @p0", 2, "Updated Co");
    }

    static async Task Sample10Async()
    {
      await SqlExecutor.ExecuteNonQueryAsync(connStr, "update Company set Name = @p1 where ID = @p0", 2, "Updated Co2");
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

      // using SqlExecutor.UsingTransaction method. 
      SqlExecutor.UsingTransaction(x =>
        {
          var arr = x.ExecuteQuery<Employee>("select * from Employee where Age is not null").ToArray();

          x.ExecuteNonQuery("update Employee set Age = Age + 1 where ID = @p0", 3);

          newEmployees.WriteToServer(x.Transaction, "Employee");

        }, connStr);

      // using external transaction
      using (SqlConnection conn = new SqlConnection(connStr))
      {
        conn.Open();

        var tran = conn.BeginTransaction();

        try
        {
          SqlExecutor executor = new SqlExecutor(tran);

          var arr = executor.ExecuteQuery<Employee>("select * from Employee where Age is not null");

          executor.ExecuteNonQuery("update Employee set Age = Age + 1 where ID = @p0", 3);

          newEmployees.WriteToServer(tran, "Employee");
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
        new Employee() { CompanyID = 1, Name = "New Employee13", Age = 23, StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 1, Name = "New Employee23", StartWorking = DateTime.UtcNow },
        new Employee() { CompanyID = 2, Name = "New Employee13" }
      };

      // using SqlExecutor.UsingTransaction method. 
      await SqlExecutor.UsingTransactionAsync(async x =>
      {
        var arr = await x.ExecuteQueryAsync<Employee>("select * from Employee where Age is not null").ToArray();

        await x.ExecuteNonQueryAsync("update Employee set Age = Age + 1 where ID = @p0", 3);

        await newEmployees.WriteToServerAsync(x.Transaction, "Employee");

      }, connStr);

      // using external transaction
      using (SqlConnection conn = new SqlConnection(connStr))
      {
        await conn.OpenAsync();

        var tran = conn.BeginTransaction();

        try
        {
          SqlExecutor executor = new SqlExecutor(tran);

          var arr = await executor.ExecuteQueryAsync<Employee>("select * from Employee where Age is not null").ToArray();

          await executor.ExecuteNonQueryAsync("update Employee set Age = Age + 1 where ID = @p0", 3);

          await newEmployees.WriteToServerAsync(tran, "Employee");
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
      using (SqlConnection conn = new SqlConnection(connStr))
      {
        conn.Open();

        SqlExecutor executor = new SqlExecutor(conn);

        using (SqlDataReader reader = executor.ExecuteReader("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40))
        {
          IEnumerable<Employee> eployeeEnumerable = reader.ReadAll<Employee>();
          var arr = eployeeEnumerable.ToArray();
        }
      }
    }

    static async Task Sample15Async()
    {
      using (SqlConnection conn = new SqlConnection(connStr))
      {
        await conn.OpenAsync();

        SqlExecutor executor = new SqlExecutor(conn);

        using (SqlDataReader reader = await executor.ExecuteReaderAsync("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40))
        {
          IAsyncEnumerable<Employee> eployeeEnumerable = reader.ReadAllAsync<Employee>();
          var arr = await eployeeEnumerable.ToArray();
        }
      }
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
