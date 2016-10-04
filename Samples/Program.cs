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
using System.Threading;
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
            var q = SimpleCommand.ExecuteQuery<Employee>(connStr, "select * from Employee");
            var arr = q.ToArray();
        }

        static async Task Sample1Async()
        {
            var q = SimpleCommand.ExecuteQueryAsync<Employee>(connStr, "select * from Employee");
            var arr = await q.ToArray();
        }

        // Using parameterized query
        static void Sample2()
        {
            // simple command
            var q1 = SimpleCommand.ExecuteQuery<Employee>(connStr, "select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40);
            var arr1 = q1.ToArray();

            // mapped command
            var q2 = MappedCommand.ExecuteQuery<OtherEmployee, Employee>(connStr,
              "select * from Employee where CompanyID = @CompanyID and Age > @Age", new OtherEmployee() { CompanyID = 1, Age = 40 });
            var arr2 = q2.ToArray();
        }

        static async Task Sample2Async()
        {
            // simple command
            var q1 = SimpleCommand.ExecuteQueryAsync<Employee>(connStr, "select * from Employee where CompanyID = @p0 and Age > @p1", 1, 40);
            var arr1 = await q1.ToArray();

            // mapped command
            var q2 = MappedCommand.ExecuteQueryAsync<OtherEmployee, Employee>(connStr,
              "select * from Employee where CompanyID = @CompanyID and Age > @Age", new OtherEmployee() { CompanyID = 1, Age = 40 });
            var arr2 = await q2.ToArray();
        }

        // Using FieldsSelector option: there will only be filled columns contains in source
        static void Sample3()
        {
            // simple command
            var q1 = SimpleCommand.ExecuteQuery<Employee>(new ExecutionOptions(fieldsSelector: FieldsSelector.Source),
              connStr, "select ID, CompanyID, Name, Phone from Employee where CompanyID = @p0", 1);
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
            var q1 = SimpleCommand.ExecuteQueryAsync<Employee>(new ExecutionOptions(fieldsSelector: FieldsSelector.Source), CancellationToken.None,
              connStr, "select ID, CompanyID, Name, Phone from Employee where CompanyID = @p0", 1);
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
            var q1 = SimpleCommand.ExecuteQuery<OtherEmployee>(new ExecutionOptions(fieldsSelector: FieldsSelector.Common), connStr, "select * from Employee");
            var arr1 = q1.ToArray();
        }

        static async Task Sample4Async()
        {
            // simple command
            var q1 = SimpleCommand.ExecuteQueryAsync<OtherEmployee>(new ExecutionOptions(fieldsSelector: FieldsSelector.Common), CancellationToken.None,
              connStr, "select * from Employee");
            var arr1 = await q1.ToArray();
        }

        // Using options
        static void Sample5()
        {
            // simple command
            var q1 = SimpleCommand.ExecuteQuery<OtherEmployee>(new ExecutionOptions(60, FieldsSelector.Common, false, FromTypeOption.Both), connStr, "select * from Employee");
            var arr1 = q1.ToArray();
        }

        static async Task Sample5Async()
        {
            // simple command
            var q1 = SimpleCommand.ExecuteQueryAsync<OtherEmployee>(new ExecutionOptions(60, FieldsSelector.Common, false, FromTypeOption.Both), CancellationToken.None,
              connStr, "select * from Employee");
            var arr1 = await q1.ToArray();
        }

        // Retrieving anonimous entities
        static void Sample6()
        {
            // simple command
            var proto = new { Company = default(string), Emp = default(string) };
            var q1 = SimpleCommand.ExecuteQueryAnonymous(proto, connStr, "select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID");
            var arr1 = q1.ToArray();
        }

        static async Task Sample6Async()
        {
            // simple command
            var proto = new { Company = default(string), Emp = default(string) };
            var q1 = SimpleCommand.ExecuteQueryAnonymousAsync(proto, connStr, "select E.Name as Emp, C.Name as Company from Employee E join Company C on E.CompanyID = C.ID");
            var arr1 = await q1.ToArray();
        }

        // Retrieving list of values
        static void Sample7()
        {
            // simple command
            var q1 = SimpleCommand.ExecuteQueryFirstColumn<int>(connStr, "select ID from Employee");
            var arr1 = q1.ToArray();
        }

        static async Task Sample7Async()
        {
            // simple command
            var q1 = SimpleCommand.ExecuteQueryFirstColumnAsync<int>(connStr, "select ID from Employee");
            var arr1 = await q1.ToArray();
        }

        // Retrieving one value
        static void Sample8()
        {
            // simple command
            var empName1 = SimpleCommand.ExecuteQueryFirstColumn<string>(connStr, "select Name from Employee where CompanyID = @p0 and Age > @p1", 1, 40).First();

            // mapped command with anonymous type
            var a = new { CompanyID = 1, Age = 40 };
            var empName2 = MappedCommand.Compile(a, "select Name from Employee where CompanyID = @CompanyID and Age > @Age")
              .ExecuteQueryFirstColumn<string>(connStr, a).First();
        }

        static async Task Sample8Async()
        {
            // simple command
            var empName1 = await SimpleCommand.ExecuteQueryFirstColumnAsync<string>(connStr, "select Name from Employee where CompanyID = @p0 and Age > @p1", 1, 40).First();

            // mapped command with anonymous type
            var a = new { CompanyID = 1, Age = 40 };
            var empName2 = await MappedCommand.Compile(a, "select Name from Employee where CompanyID = @CompanyID and Age > @Age")
              .ExecuteQueryFirstColumnAsync<string>(connStr, a).First();
        }

        // Using query with modification and retrieving data
        static void Sample9()
        {
            // simple command
            int newID1 = SimpleCommand.ExecuteQueryFirstColumn<int>(connStr,
              "insert into Employee (CompanyID, Name, Age) values (@p0, @p1, @p2); select cast(scope_identity() as int)", 2, "Mary Grant", 30).First();

            // mapped command
            var emp = new Employee() { CompanyID = 2, Name = "Mary Grant", Age = 30 };
            int newID2 = MappedCommand.ExecuteQueryFirstColumn<Employee, int>(connStr,
              "insert into Employee (CompanyID, Name, Age) values (@CompanyID, @Name, @Age); select cast(scope_identity() as int)", emp).First();
        }

        static async Task Sample9Async()
        {
            // simple command
            int newID1 = await SimpleCommand.ExecuteQueryFirstColumnAsync<int>(connStr,
              "insert into Employee (CompanyID, Name, Age) values (@p0, @p1, @p2); select cast(scope_identity() as int)", 2, "Mary Grant", 30).First();

            // mapped command
            var emp = new Employee() { CompanyID = 2, Name = "Mary Grant", Age = 30 };
            int newID2 = await MappedCommand.ExecuteQueryFirstColumnAsync<Employee, int>(connStr,
              "insert into Employee (CompanyID, Name, Age) values (@CompanyID, @Name, @Age); select cast(scope_identity() as int)", emp).First();
        }

        // Query without retrieving data
        static void Sample10()
        {
            // simple command
            SimpleCommand.ExecuteNonQuery(connStr, "update Company set Name = @p1 where ID = @p0", 2, "Updated Co");

            // mapped command with anonymous type
            var a = new { ID = 2, Name = "Updated Co1" };
            MappedCommand.ExecuteNonQuery(connStr, "update Company set Name = @Name where ID = @ID", a);
        }

        static async Task Sample10Async()
        {
            // simple command
            await SimpleCommand.ExecuteNonQueryAsync(connStr, "update Company set Name = @p1 where ID = @p0", 2, "Updated Co");

            // mapped command with anonymous type
            var a = new { ID = 2, Name = "Updated Co1" };
            await MappedCommand.ExecuteNonQueryAsync(connStr, "update Company set Name = @Name where ID = @ID", a);
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

            var simpleQuery = "select * from Employee where Age is not null";

            // mapped command
            var precompiled2 = MappedCommand.Compile<Employee>("update Employee set Age = Age + 1 where ID = @ID");

            // using method SqlScope.UsingTransaction
            SqlScope.UsingTransaction(connStr, scope =>
              {
            // using simple command
            var arr = scope.CreateSimple(simpleQuery).ExecuteQuery<Employee>().ToArray();

            // using precompiled mapped command
            Employee e = new Employee() { ID = 3 };
                  scope.CreateMapped(precompiled2, e).ExecuteNonQuery();
                  e.ID = 4;
                  scope.CreateMapped(precompiled2, e).ExecuteNonQuery();

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

                    // using simple command
                    var arr = scope.CreateSimple(simpleQuery).ExecuteQuery<Employee>().ToArray();

                    // using precompiled mapped command
                    Employee e = new Employee() { ID = 3 };
                    scope.CreateMapped(precompiled2, e).ExecuteNonQuery();
                    e.ID = 4;
                    scope.CreateMapped(precompiled2, e).ExecuteNonQuery();

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

            var simpleQuery = "select * from Employee where Age is not null";

            // mapped command
            var precompiled2 = MappedCommand.Compile<Employee>("update Employee set Age = Age + 1 where ID = @ID");

            // using method SqlScope.UsingTransaction
            await SqlScope.UsingTransactionAsync(connStr, async scope =>
            {
          // using simple command
          var arr = await scope.CreateSimple(simpleQuery).ExecuteQueryAsync<Employee>().ToArray();

          // using precompiled mapped command
          Employee e = new Employee() { ID = 3 };
                await scope.CreateMapped(precompiled2, e).ExecuteNonQueryAsync();
                e.ID = 4;
                await scope.CreateMapped(precompiled2, e).ExecuteNonQueryAsync();

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

                    // using simple command
                    var arr = await scope.CreateSimple(simpleQuery).ExecuteQueryAsync<Employee>().ToArray();

                    // using precompiled mapped command
                    Employee e = new Employee() { ID = 3 };
                    await scope.CreateMapped(precompiled2, e).ExecuteNonQueryAsync();
                    e.ID = 4;
                    await scope.CreateMapped(precompiled2, e).ExecuteNonQueryAsync();

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
                using (var reader = scope.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 30).ExecuteReader())
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
                using (var reader = await scope.CreateSimple("select * from Employee where CompanyID = @p0 and Age > @p1", 1, 30).ExecuteReaderAsync())
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
            await Import.ExecuteAsync(connStr, connStr2, "select Name, Age from Employee where Age <= @p0", "Person", 30);
        }

        // Insert
        static void Sample17()
        {
            var emp = new Employee() { CompanyID = 2, Name = "New inserted", Phone = "111" };

            MappedCommand.Insert(connStr, "Employee", emp, "ID");
            var id = MappedCommand.InsertAndGetId(connStr, "Employee", emp, "ID");

            // using transaction
            SqlScope.UsingTransaction(connStr, scope =>
            {
                emp.Name += "1";
                scope.CreateInsert("Employee", emp, false, "ID").ExecuteNonQuery();
                var id1 = scope.CreateInsert("Employee", emp, true, "ID").ExecuteScalar();
            });
        }

        static async Task Sample17Async()
        {
            var emp = new Employee() { CompanyID = 2, Name = "New inserted", Phone = "111" };

            await MappedCommand.InsertAsync(connStr, "Employee", emp, "ID");
            var id = await MappedCommand.InsertAndGetIdAsync(connStr, "Employee", emp, "ID");

            // using transaction
            await SqlScope.UsingTransactionAsync(connStr, async scope =>
            {
                emp.Name += "1";
                await scope.CreateInsert("Employee", emp, false, "ID").ExecuteNonQueryAsync();
                var id1 = await scope.CreateInsert("Employee", emp, true, "ID").ExecuteScalarAsync();
            });
        }

        // Update
        static void Sample18()
        {
            var emp = new Employee() { ID = 2, CompanyID = 2, Name = "Updated", Phone = "111" };

            MappedCommand.Update(connStr, "Employee", emp, "ID");

            // using transaction
            SqlScope.UsingTransaction(connStr, scope =>
            {
                emp.Name += "1";
                scope.CreateUpdate("Employee", emp, "ID").ExecuteNonQuery();
            });
        }

        static async Task Sample18Async()
        {
            var emp = new Employee() { ID = 4, CompanyID = 2, Name = "Updated", Phone = "111" };

            await MappedCommand.UpdateAsync(connStr, "Employee", emp, "ID");

            // using transaction
            await SqlScope.UsingTransactionAsync(connStr, async scope =>
            {
                emp.Name += "1";
                await scope.CreateUpdate("Employee", emp, "ID").ExecuteNonQueryAsync();
            });
        }

        // using parametrized query with MappedCommand.Prepare
        static void Sample19()
        {
            var q = MappedCommand.Prepare(connStr, "select * from Employee where CompanyID = @CompanyID and Age > @Age",
              new { CompanyID = 1, Age = 40 }).ExecuteQuery<Employee>();

            var arr = q.ToArray();
        }

        static async Task Sample19Async()
        {
            var q = MappedCommand.Prepare(connStr, "select * from Employee where CompanyID = @CompanyID and Age > @Age",
              new { CompanyID = 1, Age = 40 }).ExecuteQueryAsync<Employee>();

            var arr = await q.ToArray();
        }


        // Merge
        static void Sample20()
        {
            var emp = new Employee() { ID = 2, CompanyID = 2, Name = "Merged", Phone = "444" };

            MappedCommand.Merge(connStr, "Employee", emp, "ID");

            // using transaction
            SqlScope.UsingTransaction(connStr, scope =>
            {
                emp.Name += "1";
                scope.CreateMerge("Employee", emp, "ID").ExecuteNonQuery();
            });
        }

        static async Task Sample20Async()
        {
            var emp = new Employee() { ID = 4, CompanyID = 2, Name = "Merged", Phone = "777" };

            await MappedCommand.MergeAsync(connStr, "Employee", emp, "ID");

            // using transaction
            await SqlScope.UsingTransactionAsync(connStr, async scope =>
            {
                emp.Name += "2";
                await scope.CreateMerge("Employee", emp, "ID").ExecuteNonQueryAsync();
            });
        }

    }

    public class C
    {
        public int ID;
        public int Index1;
        public int Index2;
    }
}
