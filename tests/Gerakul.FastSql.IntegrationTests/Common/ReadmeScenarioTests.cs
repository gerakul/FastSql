using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I10: the README snippets in README order, against the seed data (which follows Examples/InitQuery.sql).
    // The snippet lines are kept as the README writes them. The differences, each marked where it happens:
    // - identifiers go through N(...): bare on SQL Server, as in the README, quoted on PostgreSQL, whose seed uses
    //   quoted PascalCase names (the README's "examples for PostgreSQL would look same" holds only with quoting, Q19);
    // - snippets that change data run against scratch copies of Employee and Company, because the seed data is read only;
    // - the async lines keep the README syntax "await ... .ToArray()" on purpose: ToArray is the extension of
    //   System.Interactive.Async 3.2, so these lines change together with the README when the library moves to the
    //   IAsyncEnumerable<T> of .NET (plan §4). Everywhere else async results go through AsyncEnumerableCompat.
    // The extensibility examples have provider specific SQL, so they are in the concrete classes.
    // Concrete classes are in the provider folders.
    public abstract class ReadmeScenarioTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        protected ReadmeScenarioTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        // column definitions of the scratch copies, the same as the seed tables
        protected abstract string EmployeeColumnsDefinition { get; }

        protected abstract string CompanyColumnsDefinition { get; }

        // the README's "new SqlParameter(name, value)", with the provider's parameter type
        protected abstract DbParameter CreateReadmeParameter(string name, object value);

        // an identifier as the README writes it: bare on SQL Server, quoted on PostgreSQL
        protected abstract string N(string identifier);

        // what the README bulk copy and data import examples do on the provider (see the concrete classes)
        protected abstract void AssertBulkCopyResult(Action bulkCopy, string table);

        protected abstract void AssertImportIntoSourceTableResult(ContextKind kind, Action import, string table);

        protected abstract void AssertImportFromProcedureResult(ContextKind kind, Action import, string table);

        #region Simple example

        [Fact]
        public void SimpleExample_ReturnsAllEmployees()
        {
            // get context (the provider of the test class: SqlContextProvider or NpgsqlContextProvider)
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            // create and execute command
            var employees = context.CreateSimple($"select * from {N("Employee")}").ExecuteQuery<Employee>().ToArray();

            employees.Should().BeEquivalentTo(SeedData.Employees);
        }

        #endregion

        #region Context provider

        [Fact]
        public void ContextProvider_DefaultAndNewInstances_RunTheSameQuery()
        {
            // default provider (for most cases) and a specific provider (Examples/Program.cs)
            var defaultProvider = Spec.DefaultProvider;
            var specificProvider = Spec.CreateProvider();

            var fromDefault = defaultProvider.CreateContext(Database.ConnectionString)
                .CreateSimple($"select * from {N("Employee")}").ExecuteQuery<Employee>().ToArray();
            var fromSpecific = specificProvider.CreateContext(Database.ConnectionString)
                .CreateSimple($"select * from {N("Employee")}").ExecuteQuery<Employee>().ToArray();

            specificProvider.Should().BeOfType(defaultProvider.GetType()).And.NotBeSameAs(defaultProvider);
            fromDefault.Should().BeEquivalentTo(SeedData.Employees);
            fromSpecific.Should().BeEquivalentTo(SeedData.Employees);
        }

        #endregion

        #region Getting context

        [Fact]
        public void GettingContext_FromConnectionString_RunsCommands()
        {
            var provider = Spec.DefaultProvider;

            var context = provider.CreateContext(Database.ConnectionString);

            context.Should().BeAssignableTo<ConnectionStringContext>();
            ReadEmployees(context).Should().BeEquivalentTo(SeedData.Employees);
        }

        [Fact]
        public void GettingContext_FromConnection_RunsCommands()
        {
            var provider = Spec.DefaultProvider;

            using (var dbConnection = Database.OpenConnection())
            {
                // dbConnection derived from type System.Data.Common.DbConnection
                var context = provider.CreateContext(dbConnection);

                context.Should().BeAssignableTo<ConnectionContext>();
                ReadEmployees(context).Should().BeEquivalentTo(SeedData.Employees);
            }
        }

        [Fact]
        public void GettingContext_FromTransaction_RunsCommandsInTransaction()
        {
            var table = CreateCompanyTable(false);
            var provider = Spec.DefaultProvider;

            using (var dbConnection = Database.OpenConnection())
            using (var dbTransaction = dbConnection.BeginTransaction())
            {
                // dbTransaction derived from type System.Data.Common.DbTransaction
                var context = provider.CreateContext(dbTransaction);

                context.Should().BeAssignableTo<TransactionContext>();
                ReadEmployees(context).Should().BeEquivalentTo(SeedData.Employees);
                InsertCompany(context, table, 1);

                dbTransaction.Rollback();
            }

            CountRows(table).Should().Be(0);
        }

        [Fact]
        public void UsingConnection_WithNestedUsingTransaction_RunsInOpenedConnectionAndCommits()
        {
            var table = CreateCompanyTable(false);
            var provider = Spec.DefaultProvider;
            DbConnection usedConnection = null;

            // context from connection string
            var context = provider.CreateContext(Database.ConnectionString);

            // using connection
            context.UsingConnection(connectionContext =>
            {
                // doing something with connectionContext (attached to opened connection)
                usedConnection = connectionContext.Connection;
                connectionContext.Connection.State.Should().Be(ConnectionState.Open);
                ReadEmployees(connectionContext).Should().HaveCount(5);
                // for example using transaction
                connectionContext.UsingTransaction(transactionContext =>
                {
                    // doing something with transactionContext (attached to active transaction)
                    transactionContext.Transaction.Connection.Should().BeSameAs(connectionContext.Connection);
                    InsertCompany(transactionContext, table, 1);
                });
            });

            usedConnection.State.Should().Be(ConnectionState.Closed);
            CountRows(table).Should().Be(1);
        }

        [Fact]
        public async Task UsingConnectionAsync_WithNestedUsingTransactionAsync_RunsInOpenedConnectionAndCommitsAsync()
        {
            var table = CreateCompanyTable(false);
            var provider = Spec.DefaultProvider;
            DbConnection usedConnection = null;

            // context from connection string
            var context = provider.CreateContext(Database.ConnectionString);

            // or asynchronously
            await context.UsingConnectionAsync(async connectionContext =>
            {
                // doing something with connectionContext (attached to opened connection)
                usedConnection = connectionContext.Connection;
                connectionContext.Connection.State.Should().Be(ConnectionState.Open);
                (await connectionContext.CreateSimple($"select * from {N("Employee")}").ExecuteQueryAsync<Employee>().ToArray())
                    .Should().HaveCount(5);
                // for example using transaction
                await connectionContext.UsingTransactionAsync(async transactionContext =>
                {
                    // doing something with transactionContext (attached to active transaction)
                    transactionContext.Transaction.Connection.Should().BeSameAs(connectionContext.Connection);
                    await InsertCompanyAsync(transactionContext, table, 1);
                });
            });

            usedConnection.State.Should().Be(ConnectionState.Closed);
            CountRows(table).Should().Be(1);
        }

        [Fact]
        public void UsingTransaction_FromConnectionStringContext_Commits()
        {
            var table = CreateCompanyTable(false);
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            // using transaction
            context.UsingTransaction(transactionContext =>
            {
                // doing something with transactionContext (attached to active transaction)
                transactionContext.Transaction.Should().NotBeNull();
                InsertCompany(transactionContext, table, 1);
            });

            CountRows(table).Should().Be(1);
        }

        [Fact]
        public async Task UsingTransactionAsync_FromConnectionStringContext_CommitsAsync()
        {
            var table = CreateCompanyTable(false);
            var context = Spec.DefaultProvider.CreateContext(Database.ConnectionString);

            // or asynchronously
            await context.UsingTransactionAsync(async transactionContext =>
            {
                // doing something with transactionContext (attached to active transaction)
                transactionContext.Transaction.Should().NotBeNull();
                await InsertCompanyAsync(transactionContext, table, 1);
            });

            CountRows(table).Should().Be(1);
        }

        #endregion

        #region Doing queries

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Data1_SimpleRetrieving_ReturnsAllEmployees(ContextKind kind)
        {
            var data1 = Database.Run(kind, context =>
                context.CreateSimple($"select * from {N("Employee")}").ExecuteQuery<Employee>().ToArray());

            data1.Should().BeEquivalentTo(SeedData.Employees);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Data2_SimpleRetrievingAsync_ReturnsAllEmployeesAsync(ContextKind kind)
        {
            var data2 = await Database.RunAsync(kind, async context =>
                await context.CreateSimple($"select * from {N("Employee")}").ExecuteQueryAsync<Employee>().ToArray());

            data2.Should().BeEquivalentTo(SeedData.Employees);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Data3_PositionalParameters_ReturnsMatchingEmployees(ContextKind kind)
        {
            var data3 = Database.Run(kind, context =>
                context.CreateSimple($"select * from {N("Employee")} where {N("CompanyID")} = @p0 and {N("Age")} > @p1", 1, 40)
                    .ExecuteQuery<Employee>().ToArray());

            data3.Should().BeEquivalentTo(new[] { SeedData.Employees[0] });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Data4_PositionalParametersAsync_ReturnsMatchingEmployeesAsync(ContextKind kind)
        {
            var data4 = await Database.RunAsync(kind, async context =>
                await context.CreateSimple($"select * from {N("Employee")} where {N("CompanyID")} = @p0 and {N("Age")} > @p1", 1, 40)
                    .ExecuteQueryAsync<Employee>().ToArray());

            data4.Should().BeEquivalentTo(new[] { SeedData.Employees[0] });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Data5_ParametersMappedToObject_ReturnsMatchingEmployees(ContextKind kind)
        {
            var data5 = Database.Run(kind, context =>
                context.CreateMapped($"select * from {N("Employee")} where {N("CompanyID")} = @CompanyID and {N("Age")} > @Age", new { CompanyID = 1, Age = 40 })
                    .ExecuteQuery<Employee>().ToArray());

            data5.Should().BeEquivalentTo(new[] { SeedData.Employees[0] });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task Data6_ParametersMappedToObjectAsync_ReturnsMatchingEmployeesAsync(ContextKind kind)
        {
            var data6 = await Database.RunAsync(kind, async context =>
                await context.CreateMapped($"select * from {N("Employee")} where {N("CompanyID")} = @CompanyID and {N("Age")} > @Age", new { CompanyID = 1, Age = 40 })
                    .ExecuteQueryAsync<Employee>().ToArray());

            data6.Should().BeEquivalentTo(new[] { SeedData.Employees[0] });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Data7_QueryAndReadOptions_ReturnsMatchingEmployees(ContextKind kind)
        {
            // NOTE: options can be set for context or ContextProvider as well as for certain command
            var data7 = Database.Run(kind, context =>
                context.CreateMapped($"select * from {N("Employee")} where {N("CompanyID")} = @CompanyID and {N("Age")} > @Age", new { CompanyID = 1, Age = 40 },
                    new QueryOptions(1000, true))
                    .ExecuteQuery<Employee>(new ReadOptions(FieldsSelector.Destination)).ToArray());

            data7.Should().BeEquivalentTo(new[] { SeedData.Employees[0] });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Data8_AnonymousEntities_ReturnsIdsAndNames(ContextKind kind)
        {
            var data8 = Database.Run(kind, context =>
                context.CreateSimple($"select * from {N("Employee")}").ExecuteQueryAnonymous(new { ID = 0, Name = "" }).ToArray());

            data8.Should().BeEquivalentTo(SeedData.Employees.Select(x => new { x.ID, x.Name }));
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Data9_ListOfValues_ReturnsIds(ContextKind kind)
        {
            var data9 = Database.Run(kind, context =>
                context.CreateSimple($"select {N("ID")} from {N("Employee")}").ExecuteQueryFirstColumn<int>().ToArray());

            data9.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
        }

        #endregion

        #region Execute without retrieving data, insert, update, merge, delete

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteNonQuery_UpdateCompanyName_UpdatesOneRow(ContextKind kind)
        {
            // scratch copy of Company instead of the seed table
            var companyTable = CreateCompanyTable(true);

            var affected = Database.Run(kind, context =>
                context.CreateSimple($"update {companyTable} set {N("Name")} = @p1 where {N("ID")} = @p0", 2, "Updated Co").ExecuteNonQuery());

            affected.Should().Be(1);
            ReadCompanies(companyTable).Select(x => x.Name).Should().Equal("Mega Co", "Updated Co");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Insert_AndInsertWithOutput_InsertRowsAndReturnNewId(ContextKind kind)
        {
            // scratch copy of Employee instead of the seed table
            var employeeTable = CreateEmployeeTable(true);

            var id = Database.Run(kind, context =>
            {
                var emp = new { CompanyID = 2, Name = "New inserted", Phone = "111" };
                context.Insert(employeeTable, emp);
                // or with returning some inserted fields
                return context.CreateInsertWithOutput(employeeTable, emp, "ID").ExecuteQueryFirstColumn<int>().First();
            });

            id.Should().Be(7);
            ReadEmployees(employeeTable).Skip(5).Should().BeEquivalentTo(new[]
            {
                new Employee() { ID = 6, CompanyID = 2, Name = "New inserted", Phone = "111" },
                new Employee() { ID = 7, CompanyID = 2, Name = "New inserted", Phone = "111" }
            }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Update_ByID_UpdatesGivenMembersOnly(ContextKind kind)
        {
            var employeeTable = CreateEmployeeTable(true);

            Database.Run(kind, context =>
            {
                var emp = new { ID = 2, CompanyID = 2, Name = "Updated", Phone = "111" };
                context.Update(employeeTable, emp, "ID");
            });

            var expected = SeedData.Employees;
            expected[1].CompanyID = 2;
            expected[1].Name = "Updated";
            expected[1].Phone = "111";
            ReadEmployees(employeeTable).Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Merge_NewCompany_InsertsRow(ContextKind kind)
        {
            var companyTable = CreateCompanyTable(true);
            var before = DateTime.Now;

            Database.Run(kind, context =>
            {
                var company = new { ID = 3, Name = "New name", DateOfFoundation = DateTime.Now };
                context.Merge(companyTable, company, "ID");
            });

            var after = DateTime.Now;
            var companies = ReadCompanies(companyTable);
            companies.Take(2).Should().BeEquivalentTo(SeedData.Companies, o => o.WithStrictOrdering());
            companies.Skip(2).Should().ContainSingle().Which.Name.Should().Be("New name");
            // a date column keeps the date of DateTime.Now (local time of the test machine)
            companies[2].DateOfFoundation.Should().BeOnOrAfter(before.Date).And.BeOnOrBefore(after.Date);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Delete_ByKeyField_DeletesRow(ContextKind kind)
        {
            var employeeTable = CreateEmployeeTable(true);

            Database.Run(kind, context =>
            {
                var emp = new { ID = 2, CompanyID = 2, Name = "Updated", Phone = "111" };
                context.Delete(employeeTable, emp, "ID");
            });

            ReadEmployees(employeeTable).Select(x => x.ID).Should().Equal(1, 3, 4, 5);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Delete_AnonymousObjectWithIdOnly_DeletesRow(ContextKind kind)
        {
            var employeeTable = CreateEmployeeTable(true);

            // or even simplier
            Database.Run(kind, context => context.Delete(employeeTable, new { ID = 2 }));

            ReadEmployees(employeeTable).Select(x => x.ID).Should().Equal(1, 3, 4, 5);
        }

        #endregion

        #region Bulk copy, stored procedures, data import

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void BulkCopy_NewEmployees_ProviderResult(ContextKind kind)
        {
            var employeeTable = CreateEmployeeTable(true);

            AssertBulkCopyResult(() => Database.Run(kind, context =>
            {
                Employee[] newEmployees = new Employee[] {
                    new Employee() { CompanyID = 1, Name = "New Employee1", Age = 23, StartWorking = DateTime.UtcNow },
                    new Employee() { CompanyID = 1, Name = "New Employee2", StartWorking = DateTime.UtcNow },
                    new Employee() { CompanyID = 2, Name = "New Employee1" }
                };

                newEmployees.WriteToServer(context, employeeTable);
            }), employeeTable);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Data10_StoredProcedure_ReturnsMatchingEmployees(ContextKind kind)
        {
            var data10 = Database.Run(kind, context =>
                context.CreateProcedure("TestProc", new { CompanyID = 1, Age = 40 }).ExecuteQuery<Employee>().ToArray());

            data10.Should().BeEquivalentTo(new[] { SeedData.Employees[0] });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void Data11_StoredProcedureWithCustomDbParameters_ReturnsMatchingEmployees(ContextKind kind)
        {
            // passing custom DbParameters
            var data11 = Database.Run(kind, context =>
                context.CreateProcedureSimple("TestProc", CreateReadmeParameter("CompanyID", 1), CreateReadmeParameter("Age", 40))
                    .ExecuteQuery<Employee>().ToArray());

            data11.Should().BeEquivalentTo(new[] { SeedData.Employees[0] });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void DataImport_QueryIntoItsSourceTable_ProviderResult(ContextKind kind)
        {
            // the README imports Employee into itself; here a scratch copy of it
            var employeeTable = CreateEmployeeTable(true);

            AssertImportIntoSourceTableResult(kind, () => Database.Run(kind, context =>
                context.CreateSimple($"select * from {employeeTable}").WriteToServer(context, employeeTable)), employeeTable);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void DataImport_ProcedureIntoTable_ProviderResult(ContextKind kind)
        {
            // TestProc reads the seed table and writes into an empty scratch copy of Employee
            var employeeTable = CreateEmployeeTable(false);

            AssertImportFromProcedureResult(kind, () => Database.Run(kind, context =>
                context.CreateProcedure("TestProc", new { CompanyID = 1, Age = 40 }).WriteToServer(context, employeeTable)), employeeTable);
        }

        #endregion

        #region Helpers

        // a scratch copy of Employee: same columns and generated ID; with the seed rows (IDs 1 to 5) when asked
        protected string CreateEmployeeTable(bool withSeedRows)
        {
            var table = Database.CreateScratchTable(EmployeeColumnsDefinition);
            if (withSeedRows)
            {
                var columns = $"{N("CompanyID")}, {N("Name")}, {N("Phone")}, {N("Age")}, {N("StartWorking")}";
                Database.Execute($"insert into {table} ({columns}) select {columns} from {N("Employee")} order by {N("ID")}");
                ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering(), "the copy should start like the seed table");
            }

            return table;
        }

        // a scratch copy of Company, with the seed rows when asked
        protected string CreateCompanyTable(bool withSeedRows)
        {
            var table = Database.CreateScratchTable(CompanyColumnsDefinition);
            if (withSeedRows)
            {
                Database.Execute($"insert into {table} select * from {N("Company")}");
            }

            return table;
        }

        protected Employee[] ReadEmployees(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by {N("ID")}")
                .ExecuteQuery<Employee>().ToArray());
        }

        protected Company[] ReadCompanies(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by {N("ID")}")
                .ExecuteQuery<Company>().ToArray());
        }

        protected int CountRows(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from {table}")
                .ExecuteQueryFirstColumn<int>().Single());
        }

        private Employee[] ReadEmployees(DbContext context)
        {
            return context.CreateSimple($"select * from {N("Employee")}").ExecuteQuery<Employee>().ToArray();
        }

        private void InsertCompany(DbContext context, string table, int id)
        {
            context.Insert(table, new Company() { ID = id, Name = "Company " + id, DateOfFoundation = new DateTime(2000, 1, 1) });
        }

        private Task InsertCompanyAsync(DbContext context, string table, int id)
        {
            return context.InsertAsync(table, new Company() { ID = id, Name = "Company " + id, DateOfFoundation = new DateTime(2000, 1, 1) });
        }

        #endregion
    }
}
