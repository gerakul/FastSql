using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // I3: executing IWrappedCommand against the seed data with every context kind: query methods, parameters,
    // precompiled commands, UseReader, ReadOptions precedence, repeated execution (Q15), empty results, server errors.
    // Concrete classes are in the provider folders.
    public abstract class WrappedCommandQueryTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        private const string DestinationSelectorMessage =
            "FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source";

        protected WrappedCommandQueryTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        private static CancellationToken Token => TestContext.Current.CancellationToken;

        // a batch with two result sets: ID and Name of the companies ordered by ID, then the employee names ordered by ID
        protected abstract string MultipleResultSetsCommandText { get; }

        // the driver's exception type for server errors
        protected abstract Type ServerExceptionType { get; }

        // the message of the server error for "select * from no_such_table"
        protected abstract string NoSuchTableMessage { get; }

        #region Query methods against the seed data

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryOfT_SeedEmployees_ReturnsMappedRows(ContextKind kind)
        {
            var employees = Database.Run(kind, x => x.CreateSimple(SelectEmployees()).ExecuteQuery<Employee>().ToArray());

            employees.Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryOfT_SeedEmployees_ReturnsMappedRowsAsync(ContextKind kind)
        {
            var employees = await Database.RunAsync(kind, x => x.CreateSimple(SelectEmployees())
                .ExecuteQueryAsync<Employee>(cancellationToken: Token).ToListCompat(Token));

            employees.Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQuery_SeedCompanies_ReturnsValueArrays(ContextKind kind)
        {
            var rows = Database.Run(kind, x => x.CreateSimple(SelectCompanyIdAndName()).ExecuteQuery().ToArray());

            rows.Should().BeEquivalentTo(new[] { new object[] { 1, "Mega Co" }, new object[] { 2, "Oldest Co" } }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQuery_SeedCompanies_ReturnsValueArraysAsync(ContextKind kind)
        {
            var rows = await Database.RunAsync(kind, x => x.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAsync(Token).ToListCompat(Token));

            rows.Should().BeEquivalentTo(new[] { new object[] { 1, "Mega Co" }, new object[] { 2, "Oldest Co" } }, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryAnonymous_SeedCompanies_ReturnsAnonymousObjects(ContextKind kind)
        {
            var proto = new { ID = 0, Name = "", DateOfFoundation = DateTime.MinValue };

            var companies = Database.Run(kind, x => x.CreateSimple(SelectCompanies()).ExecuteQueryAnonymous(proto).ToArray());

            companies.Should().Equal(
                new { ID = 1, Name = "Mega Co", DateOfFoundation = new DateTime(1990, 6, 8) },
                new { ID = 2, Name = "Oldest Co", DateOfFoundation = new DateTime(1820, 8, 9) });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryAnonymous_SeedCompanies_ReturnsAnonymousObjectsAsync(ContextKind kind)
        {
            var proto = new { ID = 0, Name = "", DateOfFoundation = DateTime.MinValue };

            var companies = await Database.RunAsync(kind, x => x.CreateSimple(SelectCompanies())
                .ExecuteQueryAnonymousAsync(proto, cancellationToken: Token).ToListCompat(Token));

            companies.Should().Equal(
                new { ID = 1, Name = "Mega Co", DateOfFoundation = new DateTime(1990, 6, 8) },
                new { ID = 2, Name = "Oldest Co", DateOfFoundation = new DateTime(1820, 8, 9) });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryFirstColumn_SeedPhones_ReturnsFirstColumnWithNulls(ContextKind kind)
        {
            var phones = Database.Run(kind, x => x.CreateSimple(SelectPhonesAndNames()).ExecuteQueryFirstColumn().Cast<object>().ToArray());

            phones.Should().Equal(new object[] { "123-123-123", null, "222-333-444", "123-456-789", "111-999-888" });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryFirstColumn_SeedPhones_ReturnsFirstColumnWithNullsAsync(ContextKind kind)
        {
            var phones = await Database.RunAsync(kind, x => x.CreateSimple(SelectPhonesAndNames()).ExecuteQueryFirstColumnAsync(Token).ToListCompat(Token));

            phones.Should().Equal(new object[] { "123-123-123", null, "222-333-444", "123-456-789", "111-999-888" });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryFirstColumnOfT_SeedAges_ReturnsTypedValues(ContextKind kind)
        {
            var ages = Database.Run(kind, x => x.CreateSimple(SelectAges()).ExecuteQueryFirstColumn<int?>().ToArray());

            ages.Should().Equal(46, null, 23, 40, 26);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryFirstColumnOfT_SeedAges_ReturnsTypedValuesAsync(ContextKind kind)
        {
            var ages = await Database.RunAsync(kind, x => x.CreateSimple(SelectAges()).ExecuteQueryFirstColumnAsync<int?>(Token).ToListCompat(Token));

            ages.Should().Equal(46, null, 23, 40, 26);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteScalar_SeedCompanies_ReturnsFirstColumnOfFirstRow(ContextKind kind)
        {
            var name = Database.Run(kind, x => x.CreateSimple(SelectCompanyNames()).ExecuteScalar());

            name.Should().Be("Mega Co");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteScalar_SeedCompanies_ReturnsFirstColumnOfFirstRowAsync(ContextKind kind)
        {
            var name = await Database.RunAsync(kind, x => x.CreateSimple(SelectCompanyNames()).ExecuteScalarAsync(Token));

            name.Should().Be("Mega Co");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteNonQuery_InsertAndUpdate_ReturnsRowsAffected(ContextKind kind)
        {
            var table = CreateTable();

            var inserted = Database.Run(kind, x => x.CreateSimple($"insert into {table} ({Q("ID")}) values (1), (2), (3)").ExecuteNonQuery());
            var insertedMapped = Database.Run(kind, x => x.CreateMapped($"insert into {table} ({Q("ID")}) values (@ID)", new { ID = 4 }).ExecuteNonQuery());
            var updated = Database.Run(kind, x => x.CreateSimple($"update {table} set {Q("ID")} = {Q("ID")} + 10 where {Q("ID")} <= @p0", 2).ExecuteNonQuery());
            var updatedNone = Database.Run(kind, x => x.CreateSimple($"update {table} set {Q("ID")} = 0 where {Q("ID")} < 0").ExecuteNonQuery());

            inserted.Should().Be(3);
            insertedMapped.Should().Be(1);
            updated.Should().Be(2);
            updatedNone.Should().Be(0);
            ReadIds(table).Should().Equal(3, 4, 11, 12);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteNonQuery_InsertAndUpdate_ReturnsRowsAffectedAsync(ContextKind kind)
        {
            var table = CreateTable();

            var inserted = await Database.RunAsync(kind, x => x.CreateSimple($"insert into {table} ({Q("ID")}) values (1), (2), (3)").ExecuteNonQueryAsync(Token));
            var insertedMapped = await Database.RunAsync(kind, x => x.CreateMapped($"insert into {table} ({Q("ID")}) values (@ID)", new { ID = 4 }).ExecuteNonQueryAsync(Token));
            var updated = await Database.RunAsync(kind, x => x.CreateSimple($"update {table} set {Q("ID")} = {Q("ID")} + 10 where {Q("ID")} <= @p0", 2).ExecuteNonQueryAsync(Token));
            var updatedNone = await Database.RunAsync(kind, x => x.CreateSimple($"update {table} set {Q("ID")} = 0 where {Q("ID")} < 0").ExecuteNonQueryAsync(Token));

            inserted.Should().Be(3);
            insertedMapped.Should().Be(1);
            updated.Should().Be(2);
            updatedNone.Should().Be(0);
            ReadIds(table).Should().Equal(3, 4, 11, 12);
        }

        #endregion

        #region Parameters

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateSimple_PositionalParameters_BindToP0AndP1(ContextKind kind)
        {
            var names = Database.Run(kind, x => x.CreateSimple(SelectNamesByCompanyAndAge("@p0", "@p1"), 2, 20)
                .ExecuteQueryFirstColumn<string>().ToArray());

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateSimple_PositionalParameters_BindToP0AndP1Async(ContextKind kind)
        {
            var names = await Database.RunAsync(kind, x => x.CreateSimple(SelectNamesByCompanyAndAge("@p0", "@p1"), 2, 20)
                .ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateSimple_NullArgument_SentAsDbNull(ContextKind kind)
        {
            var names = Database.Run(kind, x => x.CreateSimple(SelectNamesByPhone("@p0"), (object)null)
                .ExecuteQueryFirstColumn<string>().ToArray());

            names.Should().Equal("Anton Ivanov");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateSimple_NullArgument_SentAsDbNullAsync(ContextKind kind)
        {
            var names = await Database.RunAsync(kind, x => x.CreateSimple(SelectNamesByPhone("@p0"), (object)null)
                .ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            names.Should().Equal("Anton Ivanov");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateMapped_NullMember_SentAsDbNull(ContextKind kind)
        {
            var names = Database.Run(kind, x => x.CreateMapped(SelectNamesByPhone("@Phone"), new { Phone = (string)null })
                .ExecuteQueryFirstColumn<string>().ToArray());

            names.Should().Equal("Anton Ivanov");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateMapped_NullMember_SentAsDbNullAsync(ContextKind kind)
        {
            var names = await Database.RunAsync(kind, x => x.CreateMapped(SelectNamesByPhone("@Phone"), new { Phone = (string)null })
                .ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            names.Should().Equal("Anton Ivanov");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateMapped_AnonymousType_MapsMembersToParameters(ContextKind kind)
        {
            var names = Database.Run(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), new { CompanyID = 2, Age = 20 })
                .ExecuteQueryFirstColumn<string>().ToArray());

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateMapped_AnonymousType_MapsMembersToParametersAsync(ContextKind kind)
        {
            var names = await Database.RunAsync(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), new { CompanyID = 2, Age = 20 })
                .ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateMapped_NamedType_MapsFieldsAndPropertiesToParameters(ContextKind kind)
        {
            var filter = new EmployeeFilter() { CompanyID = 2, Age = 20, Unused = "not a parameter" };

            var names = Database.Run(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), filter)
                .ExecuteQueryFirstColumn<string>().ToArray());

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateMapped_NamedType_MapsFieldsAndPropertiesToParametersAsync(ContextKind kind)
        {
            var filter = new EmployeeFilter() { CompanyID = 2, Age = 20, Unused = "not a parameter" };

            var names = await Database.RunAsync(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), filter)
                .ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateMapped_ExplicitNames_PrefixIsOptional(ContextKind kind)
        {
            var names = Database.Run(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), new[] { "CompanyID", "@Age" },
                new { CompanyID = 2, Age = 20 }).ExecuteQueryFirstColumn<string>().ToArray());

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateMapped_ExplicitNames_PrefixIsOptionalAsync(ContextKind kind)
        {
            var names = await Database.RunAsync(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), new[] { "CompanyID", "@Age" },
                new { CompanyID = 2, Age = 20 }).ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateMapped_Settings_ParametersComeFromGetters(ContextKind kind)
        {
            var filter = new EmployeeFilter() { CompanyID = 2, Age = 10 };

            var names = Database.Run(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@Company", "@MinAge"), FilterSettings(), filter)
                .ExecuteQueryFirstColumn<string>().ToArray());

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateMapped_Settings_ParametersComeFromGettersAsync(ContextKind kind)
        {
            var filter = new EmployeeFilter() { CompanyID = 2, Age = 10 };

            var names = await Database.RunAsync(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@Company", "@MinAge"), FilterSettings(), filter)
                .ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateMapped_ExplicitNamesAndSettings_ParametersComeFromGetters(ContextKind kind)
        {
            var filter = new EmployeeFilter() { CompanyID = 2, Age = 10 };

            var names = Database.Run(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@Company", "@MinAge"), new[] { "@Company", "@MinAge" },
                FilterSettings(), filter).ExecuteQueryFirstColumn<string>().ToArray());

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateMapped_ExplicitNamesAndSettings_ParametersComeFromGettersAsync(ContextKind kind)
        {
            var filter = new EmployeeFilter() { CompanyID = 2, Age = 10 };

            var names = await Database.RunAsync(kind, x => x.CreateMapped(SelectNamesByCompanyAndAge("@Company", "@MinAge"), new[] { "@Company", "@MinAge" },
                FilterSettings(), filter).ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            names.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateSimple_PrecompiledCommand_ReusedAcrossContexts(ContextKind kind)
        {
            var compiled = Spec.DefaultProvider.CommandCompilator.CompileSimple(SelectNamesByCompanyAndAge("@p0", "@p1"));

            var first = Database.Run(kind, x => x.CreateSimple(compiled, 1, 30).ExecuteQueryFirstColumn<string>().ToArray());
            var second = Database.Run(kind, x => x.CreateSimple(compiled, 2, 20).ExecuteQueryFirstColumn<string>().ToArray());
            var third = Database.Run(ContextKind.ConnectionString, x => x.CreateSimple(compiled, 1, 20).ExecuteQueryFirstColumn<string>().ToArray());

            first.Should().Equal("John Smith");
            second.Should().Equal("Ivan Antonov", "Kate Donahue");
            third.Should().Equal("John Smith", "Engelbert Kaufmann");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateSimple_PrecompiledCommand_ReusedAcrossContextsAsync(ContextKind kind)
        {
            var compiled = Spec.DefaultProvider.CommandCompilator.CompileSimple(SelectNamesByCompanyAndAge("@p0", "@p1"));

            var first = await Database.RunAsync(kind, x => x.CreateSimple(compiled, 1, 30).ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));
            var second = await Database.RunAsync(kind, x => x.CreateSimple(compiled, 2, 20).ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            first.Should().Equal("John Smith");
            second.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateMapped_PrecompiledCommand_ReusedAcrossContexts(ContextKind kind)
        {
            var compiled = Spec.DefaultProvider.CommandCompilator.CompileMapped<EmployeeFilter>(SelectNamesByCompanyAndAge("@CompanyID", "@Age"));

            var first = Database.Run(kind, x => x.CreateMapped(compiled, new EmployeeFilter() { CompanyID = 1, Age = 30 })
                .ExecuteQueryFirstColumn<string>().ToArray());
            var second = Database.Run(kind, x => x.CreateMapped(compiled, new EmployeeFilter() { CompanyID = 2, Age = 20 })
                .ExecuteQueryFirstColumn<string>().ToArray());
            var third = Database.Run(ContextKind.ConnectionString, x => x.CreateMapped(compiled, new EmployeeFilter() { CompanyID = 1, Age = 20 })
                .ExecuteQueryFirstColumn<string>().ToArray());

            first.Should().Equal("John Smith");
            second.Should().Equal("Ivan Antonov", "Kate Donahue");
            third.Should().Equal("John Smith", "Engelbert Kaufmann");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task CreateMapped_PrecompiledCommand_ReusedAcrossContextsAsync(ContextKind kind)
        {
            var compiled = Spec.DefaultProvider.CommandCompilator.CompileMapped<EmployeeFilter>(SelectNamesByCompanyAndAge("@CompanyID", "@Age"));

            var first = await Database.RunAsync(kind, x => x.CreateMapped(compiled, new EmployeeFilter() { CompanyID = 1, Age = 30 })
                .ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));
            var second = await Database.RunAsync(kind, x => x.CreateMapped(compiled, new EmployeeFilter() { CompanyID = 2, Age = 20 })
                .ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token));

            first.Should().Equal("John Smith");
            second.Should().Equal("Ivan Antonov", "Kate Donahue");
        }

        #endregion

        #region UseReader

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void UseReader_ReaderOpenInsideAndClosedAfterwards(ContextKind kind)
        {
            DbDataReader reader = null;
            var closedInside = true;
            var names = new List<string>();

            Database.Run(kind, x => x.CreateSimple(SelectCompanyNames()).UseReader(r =>
            {
                reader = r;
                closedInside = r.IsClosed;
                while (r.Read())
                {
                    names.Add(r.GetString(0));
                }
            }));

            closedInside.Should().BeFalse();
            reader.IsClosed.Should().BeTrue();
            names.Should().Equal("Mega Co", "Oldest Co");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task UseReaderAsync_WithToken_ReaderOpenInsideAndClosedAfterwardsAsync(ContextKind kind)
        {
            DbDataReader reader = null;
            var closedInside = true;
            var names = new List<string>();

            await Database.RunAsync(kind, x => x.CreateSimple(SelectCompanyNames()).UseReaderAsync(Token, async r =>
            {
                reader = r;
                closedInside = r.IsClosed;
                while (await r.ReadAsync(Token))
                {
                    names.Add(r.GetString(0));
                }
            }));

            closedInside.Should().BeFalse();
            reader.IsClosed.Should().BeTrue();
            names.Should().Equal("Mega Co", "Oldest Co");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task UseReaderAsync_WithoutToken_ReaderOpenInsideAndClosedAfterwardsAsync(ContextKind kind)
        {
            DbDataReader reader = null;
            var closedInside = true;
            var names = new List<string>();

            await Database.RunAsync(kind, x => x.CreateSimple(SelectCompanyNames()).UseReaderAsync(async r =>
            {
                reader = r;
                closedInside = r.IsClosed;
                while (await r.ReadAsync(Token))
                {
                    names.Add(r.GetString(0));
                }
            }));

            closedInside.Should().BeFalse();
            reader.IsClosed.Should().BeTrue();
            names.Should().Equal("Mega Co", "Oldest Co");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void UseReader_MultipleResultSets_ReachableWithNextResult(ContextKind kind)
        {
            var first = new List<string>();
            var second = new List<string>();
            var hasSecond = false;
            var hasThird = true;

            Database.Run(kind, x => x.CreateSimple(MultipleResultSetsCommandText).UseReader(r =>
            {
                while (r.Read())
                {
                    first.Add(r.GetString(1));
                }

                hasSecond = r.NextResult();
                while (r.Read())
                {
                    second.Add(r.GetString(0));
                }

                hasThird = r.NextResult();
            }));

            first.Should().Equal("Mega Co", "Oldest Co");
            hasSecond.Should().BeTrue();
            second.Should().Equal(SeedData.Employees.Select(x => x.Name));
            hasThird.Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task UseReaderAsync_MultipleResultSets_ReachableWithNextResultAsync(ContextKind kind)
        {
            var first = new List<string>();
            var second = new List<string>();
            var hasSecond = false;
            var hasThird = true;

            await Database.RunAsync(kind, x => x.CreateSimple(MultipleResultSetsCommandText).UseReaderAsync(Token, async r =>
            {
                while (await r.ReadAsync(Token))
                {
                    first.Add(r.GetString(1));
                }

                hasSecond = await r.NextResultAsync(Token);
                while (await r.ReadAsync(Token))
                {
                    second.Add(r.GetString(0));
                }

                hasThird = await r.NextResultAsync(Token);
            }));

            first.Should().Equal("Mega Co", "Oldest Co");
            hasSecond.Should().BeTrue();
            second.Should().Equal(SeedData.Employees.Select(x => x.Name));
            hasThird.Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQuery_MultipleResultSets_ReadsOnlyFirstResultSet(ContextKind kind)
        {
            var proto = new { ID = 0, Name = "" };

            var rows = Database.Run(kind, x => x.CreateSimple(MultipleResultSetsCommandText).ExecuteQuery().ToArray());
            var companies = Database.Run(kind, x => x.CreateSimple(MultipleResultSetsCommandText).ExecuteQueryAnonymous(proto).ToArray());
            var ids = Database.Run(kind, x => x.CreateSimple(MultipleResultSetsCommandText).ExecuteQueryFirstColumn<int>().ToArray());

            rows.Should().BeEquivalentTo(new[] { new object[] { 1, "Mega Co" }, new object[] { 2, "Oldest Co" } }, o => o.WithStrictOrdering());
            companies.Should().Equal(new { ID = 1, Name = "Mega Co" }, new { ID = 2, Name = "Oldest Co" });
            ids.Should().Equal(1, 2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQuery_MultipleResultSets_ReadsOnlyFirstResultSetAsync(ContextKind kind)
        {
            var proto = new { ID = 0, Name = "" };

            var rows = await Database.RunAsync(kind, x => x.CreateSimple(MultipleResultSetsCommandText).ExecuteQueryAsync(Token).ToListCompat(Token));
            var companies = await Database.RunAsync(kind, x => x.CreateSimple(MultipleResultSetsCommandText)
                .ExecuteQueryAnonymousAsync(proto, cancellationToken: Token).ToListCompat(Token));
            var ids = await Database.RunAsync(kind, x => x.CreateSimple(MultipleResultSetsCommandText).ExecuteQueryFirstColumnAsync<int>(Token).ToListCompat(Token));

            rows.Should().BeEquivalentTo(new[] { new object[] { 1, "Mega Co" }, new object[] { 2, "Oldest Co" } }, o => o.WithStrictOrdering());
            companies.Should().Equal(new { ID = 1, Name = "Mega Co" }, new { ID = 2, Name = "Oldest Co" });
            ids.Should().Equal(1, 2);
        }

        #endregion

        #region ReadOptions precedence

        // the company query below has no DateOfFoundation column, so the default Destination selector rejects it
        // and the Common selector reads the other members

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryOfT_DefaultReadOptions_DestinationSelectorRequiresAllMembers(ContextKind kind)
        {
            Action act = () => Database.Run(kind, x => x.CreateSimple(SelectCompanyIdAndName()).ExecuteQuery<Company>().ToArray());

            act.Should().Throw<InvalidOperationException>().WithMessage(DestinationSelectorMessage);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryOfT_DefaultReadOptions_DestinationSelectorRequiresAllMembersAsync(ContextKind kind)
        {
            var exception = await CatchQueryExceptionAsync(Spec.DefaultProvider, kind, x => x.CreateSimple(SelectCompanyIdAndName())
                .ExecuteQueryAsync<Company>(cancellationToken: Token).ToListCompat(Token));

            exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(DestinationSelectorMessage);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryOfT_ProviderReadOptions_Applied(ContextKind kind)
        {
            var provider = Spec.CreateProvider();
            provider.ReadOptions.FieldsSelector = FieldsSelector.Common;

            var companies = Database.Run(provider, kind, x => x.CreateSimple(SelectCompanyIdAndName()).ExecuteQuery<Company>().ToArray());

            companies.Should().BeEquivalentTo(CompaniesWithoutDate(), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryOfT_ProviderReadOptions_AppliedAsync(ContextKind kind)
        {
            var provider = Spec.CreateProvider();
            provider.ReadOptions.FieldsSelector = FieldsSelector.Common;

            var companies = await Database.RunAsync(provider, kind, x => x.CreateSimple(SelectCompanyIdAndName())
                .ExecuteQueryAsync<Company>(cancellationToken: Token).ToListCompat(Token));

            companies.Should().BeEquivalentTo(CompaniesWithoutDate(), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryOfT_ContextReadOptions_OverrideProviderReadOptions(ContextKind kind)
        {
            var provider = Spec.CreateProvider();
            provider.ReadOptions.FieldsSelector = FieldsSelector.Common;

            Action act = () => Database.Run(provider, kind, x =>
            {
                x.ReadOptions.FieldsSelector = FieldsSelector.Destination;
                return x.CreateSimple(SelectCompanyIdAndName()).ExecuteQuery<Company>().ToArray();
            });

            act.Should().Throw<InvalidOperationException>().WithMessage(DestinationSelectorMessage);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryOfT_ContextReadOptions_OverrideProviderReadOptionsAsync(ContextKind kind)
        {
            var provider = Spec.CreateProvider();
            provider.ReadOptions.FieldsSelector = FieldsSelector.Common;

            var exception = await CatchQueryExceptionAsync(provider, kind, x =>
            {
                x.ReadOptions.FieldsSelector = FieldsSelector.Destination;
                return x.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAsync<Company>(cancellationToken: Token).ToListCompat(Token);
            });

            exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(DestinationSelectorMessage);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryOfT_PerCallReadOptions_OverrideContextReadOptions(ContextKind kind)
        {
            var provider = Spec.CreateProvider();

            var companies = Database.Run(provider, kind, x =>
            {
                x.ReadOptions.FieldsSelector = FieldsSelector.Destination;
                return x.CreateSimple(SelectCompanyIdAndName()).ExecuteQuery<Company>(new ReadOptions(FieldsSelector.Common)).ToArray();
            });

            companies.Should().BeEquivalentTo(CompaniesWithoutDate(), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryOfT_PerCallReadOptions_OverrideContextReadOptionsAsync(ContextKind kind)
        {
            var provider = Spec.CreateProvider();

            var companies = await Database.RunAsync(provider, kind, x =>
            {
                x.ReadOptions.FieldsSelector = FieldsSelector.Destination;
                return x.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAsync<Company>(new ReadOptions(FieldsSelector.Common), Token).ToListCompat(Token);
            });

            companies.Should().BeEquivalentTo(CompaniesWithoutDate(), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryOfT_PerCallReadOptions_OverrideProviderReadOptions(ContextKind kind)
        {
            var provider = Spec.CreateProvider();
            provider.ReadOptions.FieldsSelector = FieldsSelector.Common;

            Action act = () => Database.Run(provider, kind, x => x.CreateSimple(SelectCompanyIdAndName())
                .ExecuteQuery<Company>(new ReadOptions(FieldsSelector.Destination)).ToArray());

            act.Should().Throw<InvalidOperationException>().WithMessage(DestinationSelectorMessage);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryOfT_PerCallReadOptionsPartlySet_UnsetValuesComeFromContext(ContextKind kind)
        {
            var provider = Spec.CreateProvider();

            var companies = Database.Run(provider, kind, x =>
            {
                x.ReadOptions.FieldsSelector = FieldsSelector.Common;
                return x.CreateSimple(SelectCompanyIdAndName()).ExecuteQuery<Company>(new ReadOptions(caseSensitiveFieldsMatching: false)).ToArray();
            });

            companies.Should().BeEquivalentTo(CompaniesWithoutDate(), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryOfT_PerCallReadOptionsPartlySet_UnsetValuesComeFromContextAsync(ContextKind kind)
        {
            var provider = Spec.CreateProvider();

            var companies = await Database.RunAsync(provider, kind, x =>
            {
                x.ReadOptions.FieldsSelector = FieldsSelector.Common;
                return x.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAsync<Company>(new ReadOptions(caseSensitiveFieldsMatching: false), Token)
                    .ToListCompat(Token);
            });

            companies.Should().BeEquivalentTo(CompaniesWithoutDate(), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryOfT_ProviderCaseSensitiveMatching_LowerCaseColumnsDoNotMatch(ContextKind kind)
        {
            var provider = Spec.CreateProvider();
            provider.ReadOptions.CaseSensitiveFieldsMatching = true;

            var insensitive = Database.Run(kind, x => x.CreateSimple(SelectCompaniesWithLowerCaseColumns()).ExecuteQuery<Company>().ToArray());
            Action act = () => Database.Run(provider, kind, x => x.CreateSimple(SelectCompaniesWithLowerCaseColumns()).ExecuteQuery<Company>().ToArray());
            var perCall = Database.Run(provider, kind, x => x.CreateSimple(SelectCompaniesWithLowerCaseColumns())
                .ExecuteQuery<Company>(new ReadOptions(caseSensitiveFieldsMatching: false)).ToArray());

            insensitive.Should().BeEquivalentTo(SeedData.Companies, o => o.WithStrictOrdering());
            act.Should().Throw<InvalidOperationException>().WithMessage(DestinationSelectorMessage);
            perCall.Should().BeEquivalentTo(SeedData.Companies, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQueryAnonymous_ReadOptions_PerCallOverridesContext(ContextKind kind)
        {
            var provider = Spec.CreateProvider();
            var proto = new { ID = 0, Name = "", DateOfFoundation = DateTime.MinValue };

            Action act = () => Database.Run(provider, kind, x => x.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAnonymous(proto).ToArray());
            var companies = Database.Run(provider, kind, x =>
            {
                x.ReadOptions.FieldsSelector = FieldsSelector.Destination;
                return x.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAnonymous(proto, new ReadOptions(FieldsSelector.Common)).ToArray();
            });

            act.Should().Throw<InvalidOperationException>().WithMessage(DestinationSelectorMessage);
            companies.Should().Equal(
                new { ID = 1, Name = "Mega Co", DateOfFoundation = default(DateTime) },
                new { ID = 2, Name = "Oldest Co", DateOfFoundation = default(DateTime) });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQueryAnonymous_ReadOptions_ContextOverridesProviderAsync(ContextKind kind)
        {
            var provider = Spec.CreateProvider();
            provider.ReadOptions.FieldsSelector = FieldsSelector.Destination;
            var proto = new { ID = 0, Name = "", DateOfFoundation = DateTime.MinValue };

            var companies = await Database.RunAsync(provider, kind, x =>
            {
                x.ReadOptions.FieldsSelector = FieldsSelector.Common;
                return x.CreateSimple(SelectCompanyIdAndName()).ExecuteQueryAnonymousAsync(proto, cancellationToken: Token).ToListCompat(Token);
            });

            companies.Should().Equal(
                new { ID = 1, Name = "Mega Co", DateOfFoundation = default(DateTime) },
                new { ID = 2, Name = "Oldest Co", DateOfFoundation = default(DateTime) });
        }

        #endregion

        #region Executing a command twice (Q15)

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteNonQuery_SameCommandTwice_RunsTwice(ContextKind kind)
        {
            var table = CreateTable();

            var results = Database.Run(kind, x =>
            {
                var command = x.CreateSimple($"insert into {table} ({Q("ID")}) values (@p0)", 7);
                return new[] { command.ExecuteNonQuery(), command.ExecuteNonQuery() };
            });

            results.Should().Equal(1, 1);
            ReadIds(table).Should().Equal(7, 7);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteNonQuery_SameCommandTwice_RunsTwiceAsync(ContextKind kind)
        {
            var table = CreateTable();

            var results = await Database.RunAsync(kind, async x =>
            {
                var command = x.CreateSimple($"insert into {table} ({Q("ID")}) values (@p0)", 7);
                return new[] { await command.ExecuteNonQueryAsync(Token), await command.ExecuteNonQueryAsync(Token) };
            });

            results.Should().Equal(1, 1);
            ReadIds(table).Should().Equal(7, 7);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void ExecuteQuery_SameCommandAndSameEnumerableTwice_ReturnsRowsEachTime(ContextKind kind)
        {
            var results = Database.Run(kind, x =>
            {
                var command = x.CreateSimple(SelectCompanyNames());
                var names = command.ExecuteQueryFirstColumn<string>();
                return new[] { command.ExecuteQueryFirstColumn<string>().ToArray(), names.ToArray(), names.ToArray() };
            });

            results.Should().AllSatisfy(x => x.Should().Equal("Mega Co", "Oldest Co"));
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task ExecuteQuery_SameCommandAndSameEnumerableTwice_ReturnsRowsEachTimeAsync(ContextKind kind)
        {
            var results = await Database.RunAsync(kind, async x =>
            {
                var command = x.CreateSimple(SelectCompanyNames());
                var names = command.ExecuteQueryFirstColumnAsync<string>(Token);
                return new[]
                {
                    await command.ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token),
                    await names.ToListCompat(Token),
                    await names.ToListCompat(Token)
                };
            });

            results.Should().AllSatisfy(x => x.Should().Equal("Mega Co", "Oldest Co"));
        }

        [Fact]
        [Trait("Quirk", "Q15")]
        public void CreateMapped_MemberChangedAfterCreation_ConnectionStringContextReadsValueAtEachExecution()
        {
            var filter = new EmployeeFilter() { CompanyID = 1, Age = 0 };

            var results = Database.Run(ContextKind.ConnectionString, x =>
            {
                var command = x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), filter);
                filter.CompanyID = 2;
                var first = command.ExecuteQueryFirstColumn<string>().ToArray();
                filter.Age = 30;
                var second = command.ExecuteQueryFirstColumn<string>().ToArray();
                return new[] { first, second };
            });

            results[0].Should().Equal("Ivan Antonov", "Kate Donahue");
            results[1].Should().Equal("Ivan Antonov");
        }

        [Fact]
        [Trait("Quirk", "Q15")]
        public async Task CreateMapped_MemberChangedAfterCreation_ConnectionStringContextReadsValueAtEachExecutionAsync()
        {
            var filter = new EmployeeFilter() { CompanyID = 1, Age = 0 };

            var results = await Database.RunAsync(ContextKind.ConnectionString, async x =>
            {
                var command = x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), filter);
                filter.CompanyID = 2;
                var first = await command.ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token);
                filter.Age = 30;
                var second = await command.ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token);
                return new[] { first, second };
            });

            results[0].Should().Equal("Ivan Antonov", "Kate Donahue");
            results[1].Should().Equal("Ivan Antonov");
        }

        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        [Trait("Quirk", "Q15")]
        public void CreateMapped_MemberChangedAfterCreation_ScopedContextKeepsValueAtCreation(ContextKind kind)
        {
            var filter = new EmployeeFilter() { CompanyID = 1, Age = 0 };

            var results = Database.Run(kind, x =>
            {
                var command = x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), filter);
                filter.CompanyID = 2;
                var first = command.ExecuteQueryFirstColumn<string>().ToArray();
                filter.Age = 30;
                var second = command.ExecuteQueryFirstColumn<string>().ToArray();
                return new[] { first, second };
            });

            results[0].Should().Equal("John Smith", "Engelbert Kaufmann");
            results[1].Should().Equal("John Smith", "Engelbert Kaufmann");
        }

        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        [Trait("Quirk", "Q15")]
        public async Task CreateMapped_MemberChangedAfterCreation_ScopedContextKeepsValueAtCreationAsync(ContextKind kind)
        {
            var filter = new EmployeeFilter() { CompanyID = 1, Age = 0 };

            var results = await Database.RunAsync(kind, async x =>
            {
                var command = x.CreateMapped(SelectNamesByCompanyAndAge("@CompanyID", "@Age"), filter);
                filter.CompanyID = 2;
                var first = await command.ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token);
                filter.Age = 30;
                var second = await command.ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token);
                return new[] { first, second };
            });

            results[0].Should().Equal("John Smith", "Engelbert Kaufmann");
            results[1].Should().Equal("John Smith", "Engelbert Kaufmann");
        }

        [Fact]
        [Trait("Quirk", "Q15")]
        public void Set_ExecutedTwice_ConnectionStringContextCreatesCommandAtEachExecution()
        {
            var commands = new List<DbCommand>();
            var commandsAfterCreation = -1;

            var results = Database.Run(ContextKind.ConnectionString, x =>
            {
                var command = x.Set(scope => CreateCommand(scope, commands));
                commandsAfterCreation = commands.Count;
                return new[] { command.ExecuteScalar(), command.ExecuteScalar() };
            });

            results.Should().Equal("Mega Co", "Mega Co");
            commandsAfterCreation.Should().Be(0);
            commands.Should().HaveCount(2);
            commands[0].Should().NotBeSameAs(commands[1]);
            commands[0].Connection.Should().NotBeSameAs(commands[1].Connection);
        }

        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        [Trait("Quirk", "Q15")]
        public void Set_ExecutedTwice_ScopedContextReusesCommandCreatedAtCreation(ContextKind kind)
        {
            var commands = new List<DbCommand>();
            var commandsAfterCreation = -1;

            var results = Database.Run(kind, x =>
            {
                var command = x.Set(scope => CreateCommand(scope, commands));
                commandsAfterCreation = commands.Count;
                return new[] { command.ExecuteScalar(), command.ExecuteScalar() };
            });

            results.Should().Equal("Mega Co", "Mega Co");
            commandsAfterCreation.Should().Be(1);
            commands.Should().HaveCount(1);
        }

        [Fact]
        [Trait("Quirk", "Q15")]
        public async Task Set_ExecutedTwice_ConnectionStringContextCreatesCommandAtEachExecutionAsync()
        {
            var commands = new List<DbCommand>();
            var commandsAfterCreation = -1;

            var results = await Database.RunAsync(ContextKind.ConnectionString, async x =>
            {
                var command = x.Set(scope => CreateCommand(scope, commands));
                commandsAfterCreation = commands.Count;
                return new[] { await command.ExecuteScalarAsync(Token), await command.ExecuteScalarAsync(Token) };
            });

            results.Should().Equal("Mega Co", "Mega Co");
            commandsAfterCreation.Should().Be(0);
            commands.Should().HaveCount(2);
            commands[0].Should().NotBeSameAs(commands[1]);
        }

        [Theory]
        [InlineData(ContextKind.Connection)]
        [InlineData(ContextKind.Transaction)]
        [Trait("Quirk", "Q15")]
        public async Task Set_ExecutedTwice_ScopedContextReusesCommandCreatedAtCreationAsync(ContextKind kind)
        {
            var commands = new List<DbCommand>();
            var commandsAfterCreation = -1;

            var results = await Database.RunAsync(kind, async x =>
            {
                var command = x.Set(scope => CreateCommand(scope, commands));
                commandsAfterCreation = commands.Count;
                return new[] { await command.ExecuteScalarAsync(Token), await command.ExecuteScalarAsync(Token) };
            });

            results.Should().Equal("Mega Co", "Mega Co");
            commandsAfterCreation.Should().Be(1);
            commands.Should().HaveCount(1);
        }

        #endregion

        #region Empty result sets

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void QueryMethods_NoRows_ReturnEmptyResults(ContextKind kind)
        {
            var sql = SelectNoCompanies();
            var proto = new { ID = 0, Name = "", DateOfFoundation = DateTime.MinValue };

            Database.Run(kind, x =>
            {
                x.CreateSimple(sql).ExecuteQuery<Company>().Should().BeEmpty();
                x.CreateSimple(sql).ExecuteQuery().Should().BeEmpty();
                x.CreateSimple(sql).ExecuteQueryAnonymous(proto).Should().BeEmpty();
                x.CreateSimple(sql).ExecuteQueryFirstColumn().Cast<object>().Should().BeEmpty();
                x.CreateSimple(sql).ExecuteQueryFirstColumn<int>().Should().BeEmpty();
                x.CreateSimple(sql).ExecuteScalar().Should().BeNull();
                x.CreateSimple(sql).UseReader(r => r.HasRows.Should().BeFalse());
            });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task QueryMethods_NoRows_ReturnEmptyResultsAsync(ContextKind kind)
        {
            var sql = SelectNoCompanies();
            var proto = new { ID = 0, Name = "", DateOfFoundation = DateTime.MinValue };

            await Database.RunAsync(kind, async x =>
            {
                (await x.CreateSimple(sql).ExecuteQueryAsync<Company>(cancellationToken: Token).ToListCompat(Token)).Should().BeEmpty();
                (await x.CreateSimple(sql).ExecuteQueryAsync(Token).ToListCompat(Token)).Should().BeEmpty();
                (await x.CreateSimple(sql).ExecuteQueryAnonymousAsync(proto, cancellationToken: Token).ToListCompat(Token)).Should().BeEmpty();
                (await x.CreateSimple(sql).ExecuteQueryFirstColumnAsync(Token).ToListCompat(Token)).Should().BeEmpty();
                (await x.CreateSimple(sql).ExecuteQueryFirstColumnAsync<int>(Token).ToListCompat(Token)).Should().BeEmpty();
                (await x.CreateSimple(sql).ExecuteScalarAsync(Token)).Should().BeNull();
                await x.CreateSimple(sql).UseReaderAsync(Token, r =>
                {
                    r.HasRows.Should().BeFalse();
                    return Task.CompletedTask;
                });
            });
        }

        #endregion

        #region Server errors

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "driver exception type")]
        public void ExecuteMethods_ServerError_ThrowDbException(ContextKind kind)
        {
            var sql = "select * from no_such_table";

            AssertServerError(() => Database.Run(kind, x => x.CreateSimple(sql).ExecuteNonQuery()));
            AssertServerError(() => Database.Run(kind, x => x.CreateSimple(sql).ExecuteScalar()));
            AssertServerError(() => Database.Run(kind, x => x.CreateSimple(sql).ExecuteQuery().ToArray()));
            AssertServerError(() => Database.Run(kind, x => x.CreateSimple(sql).ExecuteQuery<Company>().ToArray()));
            AssertServerError(() => Database.Run(kind, x => x.CreateSimple(sql).ExecuteQueryFirstColumn<int>().ToArray()));
            AssertServerError(() => Database.Run(kind, x => x.CreateSimple(sql).UseReader(r => { })));
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "driver exception type")]
        public async Task ExecuteMethods_ServerError_ThrowDbExceptionAsync(ContextKind kind)
        {
            var sql = "select * from no_such_table";

            await AssertServerErrorAsync(() => Database.RunAsync(kind, x => x.CreateSimple(sql).ExecuteNonQueryAsync(Token)));
            await AssertServerErrorAsync(() => Database.RunAsync(kind, x => x.CreateSimple(sql).ExecuteScalarAsync(Token)));
            await AssertServerErrorAsync(() => Database.RunAsync(kind, x => x.CreateSimple(sql).ExecuteQueryAsync(Token).ToListCompat(Token)));
            await AssertServerErrorAsync(() => Database.RunAsync(kind, x => x.CreateSimple(sql).ExecuteQueryAsync<Company>(cancellationToken: Token).ToListCompat(Token)));
            await AssertServerErrorAsync(() => Database.RunAsync(kind, x => x.CreateSimple(sql).ExecuteQueryFirstColumnAsync<int>(Token).ToListCompat(Token)));
            await AssertServerErrorAsync(() => Database.RunAsync(kind, x => x.CreateSimple(sql).UseReaderAsync(Token, r => Task.CompletedTask)));
        }

        #endregion

        // Runs an async query expected to fail while preparing the mapping. On scoped contexts that failure leaves the
        // data reader open (Q70), so a transaction can then be neither committed nor rolled back on SQL Server:
        // the connection is closed without completing it, which discards the transaction.
        private async Task<Exception> CatchQueryExceptionAsync(ContextProvider provider, ContextKind kind, Func<DbContext, Task> query)
        {
            switch (kind)
            {
                case ContextKind.ConnectionString:
                    return await CatchAsync(() => query(provider.CreateContext(Database.ConnectionString)));
                case ContextKind.Connection:
                    using (var connection = await Database.OpenConnectionAsync())
                    {
                        return await CatchAsync(() => query(provider.CreateContext(connection)));
                    }
                case ContextKind.Transaction:
                    using (var connection = await Database.OpenConnectionAsync())
                    {
                        var transaction = connection.BeginTransaction();
                        return await CatchAsync(() => query(provider.CreateContext(transaction)));
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown context kind");
            }
        }

        private static async Task<Exception> CatchAsync(Func<Task> action)
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private void AssertServerError(Action act)
        {
            var exception = act.Should().Throw<DbException>().WithMessage(NoSuchTableMessage).Which;
            exception.Should().BeOfType(ServerExceptionType);
        }

        private async Task AssertServerErrorAsync(Func<Task> act)
        {
            var exception = (await act.Should().ThrowAsync<DbException>().WithMessage(NoSuchTableMessage)).Which;
            exception.Should().BeOfType(ServerExceptionType);
        }

        private DbCommand CreateCommand(ScopedContext scope, List<DbCommand> commands)
        {
            var command = scope.CreateCommand(SelectCompanyNames());
            commands.Add(command);
            return command;
        }

        private static IList<FieldSettings<EmployeeFilter>> FilterSettings()
        {
            return new[]
            {
                new FieldSettings<EmployeeFilter>("Company", typeof(int), x => x.CompanyID),
                new FieldSettings<EmployeeFilter>("MinAge", typeof(int), x => x.Age + 10)
            };
        }

        private static Company[] CompaniesWithoutDate()
        {
            return new[]
            {
                new Company() { ID = 1, Name = "Mega Co" },
                new Company() { ID = 2, Name = "Oldest Co" }
            };
        }

        private string Q(string identifier)
        {
            return Spec.Quote(identifier);
        }

        private string SelectEmployees()
        {
            return $"select * from {Q("Employee")} order by {Q("ID")}";
        }

        private string SelectCompanies()
        {
            return $"select * from {Q("Company")} order by {Q("ID")}";
        }

        private string SelectNoCompanies()
        {
            return $"select * from {Q("Company")} where {Q("ID")} < 0";
        }

        private string SelectCompanyIdAndName()
        {
            return $"select {Q("ID")}, {Q("Name")} from {Q("Company")} order by {Q("ID")}";
        }

        private string SelectCompanyNames()
        {
            return $"select {Q("Name")} from {Q("Company")} order by {Q("ID")}";
        }

        private string SelectCompaniesWithLowerCaseColumns()
        {
            return $"select {Q("ID")} as id, {Q("Name")} as name, {Q("DateOfFoundation")} as dateoffoundation from {Q("Company")} order by {Q("ID")}";
        }

        private string SelectPhonesAndNames()
        {
            return $"select {Q("Phone")}, {Q("Name")} from {Q("Employee")} order by {Q("ID")}";
        }

        private string SelectAges()
        {
            return $"select {Q("Age")} from {Q("Employee")} order by {Q("ID")}";
        }

        private string SelectNamesByCompanyAndAge(string companyParameter, string ageParameter)
        {
            return $"select {Q("Name")} from {Q("Employee")} where {Q("CompanyID")} = {companyParameter} and {Q("Age")} > {ageParameter} order by {Q("ID")}";
        }

        private string SelectNamesByPhone(string phoneParameter)
        {
            return $"select {Q("Name")} from {Q("Employee")} where {Q("Phone")} is not distinct from {phoneParameter} order by {Q("ID")}";
        }

        private string CreateTable()
        {
            return Database.CreateScratchTable($"{Q("ID")} int not null");
        }

        private int[] ReadIds(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select {Q("ID")} from {table} order by {Q("ID")}")
                .ExecuteQueryFirstColumn<int>().ToArray());
        }

        // mapped to parameters through its public members: a field and a property, plus a property no parameter uses
        private class EmployeeFilter
        {
            public int CompanyID;

            public int Age { get; set; }

            public string Unused { get; set; }
        }
    }
}
