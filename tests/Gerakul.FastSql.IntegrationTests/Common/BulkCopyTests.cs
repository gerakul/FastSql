using System;
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
    // I9: bulk copy on both providers. Sources are in-memory objects (IEnumerable<T>), a data reader over the seed data
    // (DbDataReader) and a query of another context (IWrappedCommand); the destination is each kind of context.
    // Every overload has its own test, so some calls deliberately pass no cancellation token.
    // Provider-specific behavior is in SqlServerBulkCopyOptionsTests and PostgreSqlBulkCopyTests.
    public abstract class BulkCopyTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        private const string SourceSelectorError = "FieldsSelector has value FieldsSelector.Source, but source has fields which are not presented in destination";
        private const string DestinationSelectorError = "FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source";
        private const string BothSelectorError = "FieldsSelector has value FieldsSelector.Both, but source fields do not match with destination fields";

        protected BulkCopyTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        // column types of the scratch tables
        protected abstract string IdentityColumnType { get; }

        protected abstract string TextColumnType { get; }

        protected abstract string DateTimeColumnType { get; }

        #region Overloads with an IEnumerable<T> source

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_Enumerable_CopiesAllRows(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => NewEmployees.WriteToServer(x, table));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(NewEmployees), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_EnumerableWithOptions_AppliesOptions(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            // Comment has no destination column: copying works only because the options select the common columns
            Database.Run(kind, x => CommentedEmployees.WriteToServer(x, table, new BulkOptions(FieldsSelector.Common)));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(NewEmployees), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_Enumerable_CopiesAllRowsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => NewEmployees.WriteToServerAsync(x, table));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(NewEmployees), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_EnumerableWithOptions_AppliesOptionsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => CommentedEmployees.WriteToServerAsync(x, table, new BulkOptions(FieldsSelector.Common)));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(NewEmployees), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_EnumerableWithToken_CopiesAllRowsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => NewEmployees.WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(NewEmployees), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_EnumerableWithOptionsAndToken_AppliesOptionsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => CommentedEmployees.WriteToServerAsync(x, table, new BulkOptions(FieldsSelector.Common),
                TestContext.Current.CancellationToken));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(NewEmployees), o => o.WithStrictOrdering());
        }

        #endregion

        #region Overloads with a DbDataReader source

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_Reader_CopiesAllRows(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => UseSourceReader(EmployeeQuery(), r => r.WriteToServer(x, table)));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_ReaderWithOptions_AppliesOptions(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => UseSourceReader(EmployeeQuery(withComment: true), r => r.WriteToServer(x, table, new BulkOptions(FieldsSelector.Common))));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_Reader_CopiesAllRowsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => UseSourceReaderAsync(EmployeeQuery(), r => r.WriteToServerAsync(x, table)));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_ReaderWithOptions_AppliesOptionsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => UseSourceReaderAsync(EmployeeQuery(withComment: true),
                r => r.WriteToServerAsync(x, table, new BulkOptions(FieldsSelector.Common))));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_ReaderWithToken_CopiesAllRowsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => UseSourceReaderAsync(EmployeeQuery(),
                r => r.WriteToServerAsync(x, table, TestContext.Current.CancellationToken)));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_ReaderWithOptionsAndToken_AppliesOptionsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => UseSourceReaderAsync(EmployeeQuery(withComment: true),
                r => r.WriteToServerAsync(x, table, new BulkOptions(FieldsSelector.Common), TestContext.Current.CancellationToken)));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        #endregion

        #region Overloads with an IWrappedCommand source (data import)

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_Command_CopiesAllRows(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => SourceContext().CreateSimple(EmployeeQuery()).WriteToServer(x, table));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CommandWithOptions_AppliesOptions(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => SourceContext().CreateSimple(EmployeeQuery(withComment: true))
                .WriteToServer(x, table, new BulkOptions(FieldsSelector.Common)));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_Command_CopiesAllRowsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => SourceContext().CreateSimple(EmployeeQuery()).WriteToServerAsync(x, table));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CommandWithOptions_AppliesOptionsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => SourceContext().CreateSimple(EmployeeQuery(withComment: true))
                .WriteToServerAsync(x, table, new BulkOptions(FieldsSelector.Common)));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CommandWithToken_CopiesAllRowsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => SourceContext().CreateSimple(EmployeeQuery())
                .WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CommandWithOptionsAndToken_AppliesOptionsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => SourceContext().CreateSimple(EmployeeQuery(withComment: true))
                .WriteToServerAsync(x, table, new BulkOptions(FieldsSelector.Common), TestContext.Current.CancellationToken));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        #endregion

        #region Transactions

        [Fact]
        public void WriteToServer_TransactionRolledBack_DiscardsCopiedRows()
        {
            var table = CreateEmployeeTable();

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                var context = Spec.DefaultProvider.CreateContext(transaction);
                NewEmployees.WriteToServer(context, table);
                context.CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumn<int>().Single().Should().Be(3);

                transaction.Rollback();
            }

            CountRows(table).Should().Be(0);
        }

        [Fact]
        public async Task WriteToServerAsync_TransactionRolledBack_DiscardsCopiedRowsAsync()
        {
            var table = CreateEmployeeTable();

            using (var connection = await Database.OpenConnectionAsync())
            using (var transaction = connection.BeginTransaction())
            {
                var context = Spec.DefaultProvider.CreateContext(transaction);
                await SourceContext().CreateSimple(EmployeeQuery()).WriteToServerAsync(context, table, TestContext.Current.CancellationToken);
                context.CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumn<int>().Single().Should().Be(5);

                transaction.Rollback();
            }

            CountRows(table).Should().Be(0);
        }

        #endregion

        #region Fields

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_FieldsSubset_CopiesOnlyListedColumns(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => NewEmployees.WriteToServer(x, table, "CompanyID", "Name"));

            ReadEmployees(table).Should().BeEquivalentTo(OnlyCompanyAndName(ToEmployees(NewEmployees)), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_FieldsSubset_CopiesOnlyListedColumnsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => NewEmployees.WriteToServerAsync(x, table, TestContext.Current.CancellationToken, "CompanyID", "Name"));

            ReadEmployees(table).Should().BeEquivalentTo(OnlyCompanyAndName(ToEmployees(NewEmployees)), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_ReaderFieldsSubset_CopiesOnlyListedColumns(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => UseSourceReader(EmployeeQuery(), r => r.WriteToServer(x, table, "CompanyID", "Name")));

            ReadEmployees(table).Should().BeEquivalentTo(OnlyCompanyAndName(SeedData.Employees), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_ReaderFieldsSubset_CopiesOnlyListedColumnsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => UseSourceReaderAsync(EmployeeQuery(),
                r => r.WriteToServerAsync(x, table, TestContext.Current.CancellationToken, "CompanyID", "Name")));

            ReadEmployees(table).Should().BeEquivalentTo(OnlyCompanyAndName(SeedData.Employees), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_FieldsSubsetWithOptions_CopiesOnlyListedColumns(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => SourceContext().CreateSimple(EmployeeQuery())
                .WriteToServer(x, table, new BulkOptions(FieldsSelector.Common), "CompanyID", "Name"));

            ReadEmployees(table).Should().BeEquivalentTo(OnlyCompanyAndName(SeedData.Employees), o => o.WithStrictOrdering());
        }

        #endregion

        #region Empty source

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_EmptyEnumerable_CopiesNothing(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => new NewEmployee[0].WriteToServer(x, table));

            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_EmptyEnumerable_CopiesNothingAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => new NewEmployee[0].WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_EmptyQueryResult_CopiesNothing(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => SourceContext().CreateSimple(EmployeeQuery(where: "1 = 0")).WriteToServer(x, table));

            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_EmptyQueryResult_CopiesNothingAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => SourceContext().CreateSimple(EmployeeQuery(where: "1 = 0"))
                .WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            CountRows(table).Should().Be(0);
        }

        #endregion

        #region Column mapping

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_SourceMembersInOtherOrder_MapsByName(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => ReorderedEmployees.WriteToServer(x, table));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(NewEmployees), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_SourceMembersInOtherOrder_MapsByNameAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => ReorderedEmployees.WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(NewEmployees), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_SourceColumnsInOtherOrder_MapsByName(ContextKind kind)
        {
            var table = CreateEmployeeTable();
            var query = $"select {Q("StartWorking")}, {Q("Age")}, {Q("Phone")}, {Q("Name")}, {Q("CompanyID")} from {Q("Employee")} order by {Q("ID")}";

            Database.Run(kind, x => SourceContext().CreateSimple(query).WriteToServer(x, table));

            ReadEmployees(table).Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CaseInsensitiveMatching_MapsNamesDifferingInCase(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => LowerCaseEmployees.WriteToServer(x, table, new BulkOptions(caseSensitiveFieldsMatching: false)));

            ReadEmployees(table).Should().BeEquivalentTo(OnlyCompanyAndName(ToEmployees(NewEmployees)), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CaseInsensitiveMatching_MapsNamesDifferingInCaseAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => LowerCaseEmployees.WriteToServerAsync(x, table, new BulkOptions(caseSensitiveFieldsMatching: false),
                TestContext.Current.CancellationToken));

            ReadEmployees(table).Should().BeEquivalentTo(OnlyCompanyAndName(ToEmployees(NewEmployees)), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CaseSensitiveMatchingAndNamesDifferInCase_ThrowsInvalidOperationException(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Action act = () => Database.Run(kind, x => LowerCaseEmployees.WriteToServer(x, table, new BulkOptions(caseSensitiveFieldsMatching: true)));

            act.Should().Throw<InvalidOperationException>().WithMessage(SourceSelectorError);
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CaseSensitiveMatchingAndNamesDifferInCase_ThrowsInvalidOperationExceptionAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Func<Task> act = () => Database.RunAsync(kind, x => LowerCaseEmployees.WriteToServerAsync(x, table,
                new BulkOptions(caseSensitiveFieldsMatching: true), TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(SourceSelectorError);
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CaseSensitiveMatchingAndSameNames_CopiesRows(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => NewEmployees.WriteToServer(x, table, new BulkOptions(caseSensitiveFieldsMatching: true)));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(NewEmployees), o => o.WithStrictOrdering());
        }

        #endregion

        #region Fields selectors

        public enum SourceShape
        {
            SameColumns,
            ExtraSourceColumn,
            MissingSourceColumn,
            NoCommonColumn
        }

        // null means the copy succeeds; Source is combined with case-insensitive matching, otherwise it is not checked
        public static TheoryData<FieldsSelector, SourceShape, string> FieldsSelectorCases => new TheoryData<FieldsSelector, SourceShape, string>()
        {
            { FieldsSelector.Source, SourceShape.SameColumns, null },
            { FieldsSelector.Source, SourceShape.ExtraSourceColumn, SourceSelectorError },
            { FieldsSelector.Source, SourceShape.MissingSourceColumn, null },
            { FieldsSelector.Source, SourceShape.NoCommonColumn, SourceSelectorError },
            { FieldsSelector.Destination, SourceShape.SameColumns, null },
            { FieldsSelector.Destination, SourceShape.ExtraSourceColumn, null },
            { FieldsSelector.Destination, SourceShape.MissingSourceColumn, DestinationSelectorError },
            { FieldsSelector.Destination, SourceShape.NoCommonColumn, DestinationSelectorError },
            { FieldsSelector.Common, SourceShape.SameColumns, null },
            { FieldsSelector.Common, SourceShape.ExtraSourceColumn, null },
            { FieldsSelector.Common, SourceShape.MissingSourceColumn, null },
            { FieldsSelector.Common, SourceShape.NoCommonColumn, null },
            { FieldsSelector.Both, SourceShape.SameColumns, null },
            { FieldsSelector.Both, SourceShape.ExtraSourceColumn, BothSelectorError },
            { FieldsSelector.Both, SourceShape.MissingSourceColumn, BothSelectorError },
            { FieldsSelector.Both, SourceShape.NoCommonColumn, BothSelectorError }
        };

        [Theory]
        [MemberData(nameof(FieldsSelectorCases))]
        public void WriteToServer_FieldsSelector_ComparesSourceWithDestination(FieldsSelector selector, SourceShape shape, string expectedError)
        {
            var table = CreatePairTable();

            Action act = () => Database.Run(ContextKind.Connection, x => WritePairs(shape, x, table, SelectorOptions(selector)));

            if (expectedError == null)
            {
                act();
                ReadPairs(table).Should().BeEquivalentTo(ExpectedPairs(shape), o => o.WithStrictOrdering());
            }
            else
            {
                act.Should().Throw<InvalidOperationException>().WithMessage(expectedError);
                CountRows(table).Should().Be(0);
            }
        }

        [Theory]
        [MemberData(nameof(FieldsSelectorCases))]
        public async Task WriteToServerAsync_FieldsSelector_ComparesSourceWithDestinationAsync(FieldsSelector selector, SourceShape shape, string expectedError)
        {
            var table = CreatePairTable();

            Func<Task> act = () => Database.RunAsync(ContextKind.Connection, x => WritePairsAsync(shape, x, table, SelectorOptions(selector)));

            if (expectedError == null)
            {
                await act();
                ReadPairs(table).Should().BeEquivalentTo(ExpectedPairs(shape), o => o.WithStrictOrdering());
            }
            else
            {
                await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(expectedError);
                CountRows(table).Should().Be(0);
            }
        }

        [Fact]
        public void WriteToServer_UndefinedFieldsSelector_ThrowsArgumentException()
        {
            var table = CreatePairTable();

            Action act = () => Database.Run(ContextKind.Connection, x => WritePairs(SourceShape.SameColumns, x, table, new BulkOptions((FieldsSelector)0)));

            act.Should().Throw<ArgumentException>().WithMessage("Unknown FieldsSelector. Value: 0");
            CountRows(table).Should().Be(0);
        }

        [Fact]
        public async Task WriteToServerAsync_UndefinedFieldsSelector_ThrowsArgumentExceptionAsync()
        {
            var table = CreatePairTable();

            Func<Task> act = () => Database.RunAsync(ContextKind.Connection, x => WritePairsAsync(SourceShape.SameColumns, x, table, new BulkOptions((FieldsSelector)0)));

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("Unknown FieldsSelector. Value: 0");
            CountRows(table).Should().Be(0);
        }

        #endregion

        #region Nulls and identity

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_NullValues_WritesNulls(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => EmployeesWithNulls.WriteToServer(x, table));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(EmployeesWithNulls), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_NullValues_WritesNullsAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => EmployeesWithNulls.WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            ReadEmployees(table).Should().BeEquivalentTo(ToEmployees(EmployeesWithNulls), o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_IdentityColumnNotInSource_GeneratesValues(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            Database.Run(kind, x => NewEmployees.WriteToServer(x, table));
            Database.Run(kind, x => NewEmployees.WriteToServer(x, table));

            ReadEmployees(table).Select(x => x.ID).Should().Equal(1, 2, 3, 4, 5, 6);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_IdentityColumnNotInSource_GeneratesValuesAsync(ContextKind kind)
        {
            var table = CreateEmployeeTable();

            await Database.RunAsync(kind, x => NewEmployees.WriteToServerAsync(x, table, TestContext.Current.CancellationToken));
            await Database.RunAsync(kind, x => NewEmployees.WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            ReadEmployees(table).Select(x => x.ID).Should().Equal(1, 2, 3, 4, 5, 6);
        }

        #endregion

        #region Helpers

        private string Q(string identifier)
        {
            return Spec.Quote(identifier);
        }

        // same columns as the seeded Employee table
        private string CreateEmployeeTable()
        {
            return Database.CreateScratchTable($"{Q("ID")} {IdentityColumnType}, {Q("CompanyID")} int not null, {Q("Name")} {TextColumnType} not null, "
                + $"{Q("Phone")} {TextColumnType} null, {Q("Age")} int null, {Q("StartWorking")} {DateTimeColumnType} null");
        }

        private string CreatePairTable()
        {
            return Database.CreateScratchTable($"{Q("A")} int null, {Q("B")} {TextColumnType} null");
        }

        // the seed employees without ID, so that copying them into an empty table reproduces SeedData.Employees
        private string EmployeeQuery(bool withComment = false, string where = null)
        {
            return $"select {Q("CompanyID")}, {Q("Name")}, {Q("Phone")}, {Q("Age")}, {Q("StartWorking")}"
                + (withComment ? $", 'from seed' as {Q("Comment")}" : "")
                + $" from {Q("Employee")}"
                + (where == null ? "" : $" where {where}")
                + $" order by {Q("ID")}";
        }

        // source of IWrappedCommand imports: another context of the same database
        private DbContext SourceContext()
        {
            return Spec.DefaultProvider.CreateContext(Database.ConnectionString);
        }

        private void UseSourceReader(string query, Action<DbDataReader> action)
        {
            using (var connection = Database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                using (var reader = command.ExecuteReader())
                {
                    action(reader);
                }
            }
        }

        private async Task UseSourceReaderAsync(string query, Func<DbDataReader, Task> action)
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
                {
                    await action(reader);
                }
            }
        }

        private Employee[] ReadEmployees(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by {Q("ID")}")
                .ExecuteQuery<Employee>().ToArray());
        }

        private Pair[] ReadPairs(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select {Q("A")}, {Q("B")} from {table} order by {Q("A")}")
                .ExecuteQuery<Pair>().ToArray());
        }

        private int CountRows(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from {table}")
                .ExecuteQueryFirstColumn<int>().Single());
        }

        private static Employee[] ToEmployees(NewEmployee[] rows)
        {
            return rows.Select((x, i) => new Employee()
            {
                ID = i + 1,
                CompanyID = x.CompanyID,
                Name = x.Name,
                Phone = x.Phone,
                Age = x.Age,
                StartWorking = x.StartWorking
            }).ToArray();
        }

        private static Employee[] OnlyCompanyAndName(Employee[] employees)
        {
            return employees.Select(x => new Employee() { ID = x.ID, CompanyID = x.CompanyID, Name = x.Name }).ToArray();
        }

        private static BulkOptions SelectorOptions(FieldsSelector selector)
        {
            return selector == FieldsSelector.Source
                ? new BulkOptions(selector, caseSensitiveFieldsMatching: false)
                : new BulkOptions(selector);
        }

        private static void WritePairs(SourceShape shape, DbContext context, string table, BulkOptions options)
        {
            switch (shape)
            {
                case SourceShape.SameColumns:
                    Pairs.WriteToServer(context, table, options);
                    break;
                case SourceShape.ExtraSourceColumn:
                    Pairs.Select(x => new PairWithExtra() { A = x.A, B = x.B, C = "extra" }).WriteToServer(context, table, options);
                    break;
                case SourceShape.MissingSourceColumn:
                    Pairs.Select(x => new OnlyA() { A = x.A }).WriteToServer(context, table, options);
                    break;
                case SourceShape.NoCommonColumn:
                    Pairs.Select(x => new OnlyC() { C = x.B }).WriteToServer(context, table, options);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown shape");
            }
        }

        private static Task WritePairsAsync(SourceShape shape, DbContext context, string table, BulkOptions options)
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            switch (shape)
            {
                case SourceShape.SameColumns:
                    return Pairs.WriteToServerAsync(context, table, options, cancellationToken);
                case SourceShape.ExtraSourceColumn:
                    return Pairs.Select(x => new PairWithExtra() { A = x.A, B = x.B, C = "extra" }).WriteToServerAsync(context, table, options, cancellationToken);
                case SourceShape.MissingSourceColumn:
                    return Pairs.Select(x => new OnlyA() { A = x.A }).WriteToServerAsync(context, table, options, cancellationToken);
                case SourceShape.NoCommonColumn:
                    return Pairs.Select(x => new OnlyC() { C = x.B }).WriteToServerAsync(context, table, options, cancellationToken);
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown shape");
            }
        }

        private static Pair[] ExpectedPairs(SourceShape shape)
        {
            switch (shape)
            {
                case SourceShape.SameColumns:
                case SourceShape.ExtraSourceColumn:
                    return Pairs;
                case SourceShape.MissingSourceColumn:
                    return Pairs.Select(x => new Pair() { A = x.A }).ToArray();
                default:
                    return new Pair[0];
            }
        }

        #endregion

        #region Models

        private static NewEmployee[] NewEmployees => new[]
        {
            new NewEmployee() { CompanyID = 1, Name = "New Employee1", Phone = "555-0101", Age = 23, StartWorking = new DateTime(2020, 5, 17, 9, 30, 0) },
            new NewEmployee() { CompanyID = 1, Name = "New Employee2", Phone = null, Age = 31, StartWorking = new DateTime(2021, 2, 3) },
            new NewEmployee() { CompanyID = 2, Name = "New Employee3", Phone = "555-0103", Age = null, StartWorking = null }
        };

        private static NewEmployee[] EmployeesWithNulls => new[]
        {
            new NewEmployee() { CompanyID = 1, Name = "Nobody1" },
            new NewEmployee() { CompanyID = 2, Name = "Nobody2" }
        };

        private static CommentedEmployee[] CommentedEmployees => NewEmployees
            .Select(x => new CommentedEmployee()
            {
                CompanyID = x.CompanyID,
                Name = x.Name,
                Phone = x.Phone,
                Age = x.Age,
                StartWorking = x.StartWorking,
                Comment = "not in the destination"
            }).ToArray();

        private static ReorderedEmployee[] ReorderedEmployees => NewEmployees
            .Select(x => new ReorderedEmployee() { StartWorking = x.StartWorking, Age = x.Age, Phone = x.Phone, Name = x.Name, CompanyID = x.CompanyID })
            .ToArray();

        private static LowerCaseEmployee[] LowerCaseEmployees => NewEmployees
            .Select(x => new LowerCaseEmployee() { companyid = x.CompanyID, name = x.Name })
            .ToArray();

        private static Pair[] Pairs => new[]
        {
            new Pair() { A = 1, B = "one" },
            new Pair() { A = 2, B = "two" }
        };

        // an Employee without ID, so the identity column is generated
        private class NewEmployee
        {
            public int CompanyID { get; set; }
            public string Name { get; set; }
            public string Phone { get; set; }
            public int? Age { get; set; }
            public DateTime? StartWorking { get; set; }
        }

        private class CommentedEmployee : NewEmployee
        {
            public string Comment { get; set; }
        }

        private class ReorderedEmployee
        {
            public DateTime? StartWorking { get; set; }
            public int? Age { get; set; }
            public string Phone { get; set; }
            public string Name { get; set; }
            public int CompanyID { get; set; }
        }

        private class LowerCaseEmployee
        {
            public int companyid { get; set; }
            public string name { get; set; }
        }

        // public fields: the copy reads fields as well as properties
        private class Pair
        {
            public int A;
            public string B;
        }

        private class PairWithExtra
        {
            public int A;
            public string B;
            public string C;
        }

        private class OnlyA
        {
            public int A;
        }

        private class OnlyC
        {
            public string C;
        }

        #endregion
    }
}
