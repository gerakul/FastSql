using System;
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
    // I4: the 10 DbCommandExtensions methods on a provider DbCommand, and what happens to the data reader they open.
    // Concrete classes are in the provider folders.
    public abstract class DbCommandExtensionsTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        private const string DestinationSelectorMessage =
            "FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source";

        protected DbCommandExtensionsTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        private static CancellationToken Token => TestContext.Current.CancellationToken;

        // the driver's message when a command runs on a connection where openCommandText still has an open data reader
        protected abstract string OpenReaderMessage(string openCommandText);

        // the exact type of that exception (derived from InvalidOperationException)
        protected abstract Type OpenReaderExceptionType { get; }

        #region The 10 methods

        [Fact]
        public void ExecuteQuery_ProviderCommand_ReturnsValueArrays()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                var rows = command.ExecuteQuery().ToArray();

                rows.Should().BeEquivalentTo(new[] { new object[] { 1, "Mega Co" }, new object[] { 2, "Oldest Co" } }, o => o.WithStrictOrdering());
            }
        }

        [Fact]
        public async Task ExecuteQueryAsync_ProviderCommand_ReturnsValueArraysAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                var rows = await command.ExecuteQueryAsync(Token).ToListCompat(Token);

                rows.Should().BeEquivalentTo(new[] { new object[] { 1, "Mega Co" }, new object[] { 2, "Oldest Co" } }, o => o.WithStrictOrdering());
            }
        }

        [Fact]
        public void ExecuteQueryOfT_ProviderCommand_ReturnsMappedRows()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectEmployees()))
            {
                var employees = command.ExecuteQuery<Employee>().ToArray();

                employees.Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
            }
        }

        [Fact]
        public async Task ExecuteQueryAsyncOfT_ProviderCommand_ReturnsMappedRowsAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectEmployees()))
            {
                var employees = await command.ExecuteQueryAsync<Employee>(cancellationToken: Token).ToListCompat(Token);

                employees.Should().BeEquivalentTo(SeedData.Employees, o => o.WithStrictOrdering());
            }
        }

        [Fact]
        public void ExecuteQueryOfT_WithReadOptions_OptionsApplied()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                Action withDefaults = () => command.ExecuteQuery<Company>().ToArray();
                var companies = command.ExecuteQuery<Company>(new ReadOptions(FieldsSelector.Common)).ToArray();

                withDefaults.Should().Throw<InvalidOperationException>().WithMessage(DestinationSelectorMessage);
                companies.Should().BeEquivalentTo(CompaniesWithoutDate(), o => o.WithStrictOrdering());
            }
        }

        [Fact]
        public async Task ExecuteQueryAsyncOfT_WithReadOptions_OptionsAppliedAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                var companies = await command.ExecuteQueryAsync<Company>(new ReadOptions(FieldsSelector.Common), Token).ToListCompat(Token);

                companies.Should().BeEquivalentTo(CompaniesWithoutDate(), o => o.WithStrictOrdering());
            }
        }

        [Fact]
        public void ExecuteQueryAnonymous_ProviderCommand_ReturnsAnonymousObjects()
        {
            var proto = new { ID = 0, Name = "", Missing = 0 };

            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                Action withDefaults = () => command.ExecuteQueryAnonymous(proto).ToArray();
                var companies = command.ExecuteQueryAnonymous(proto, new ReadOptions(FieldsSelector.Common)).ToArray();

                withDefaults.Should().Throw<InvalidOperationException>().WithMessage(DestinationSelectorMessage);
                companies.Should().Equal(new { ID = 1, Name = "Mega Co", Missing = 0 }, new { ID = 2, Name = "Oldest Co", Missing = 0 });
            }
        }

        [Fact]
        public async Task ExecuteQueryAnonymousAsync_ProviderCommand_ReturnsAnonymousObjectsAsync()
        {
            var proto = new { ID = 0, Name = "" };

            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                var companies = await command.ExecuteQueryAnonymousAsync(proto, cancellationToken: Token).ToListCompat(Token);
                var withOptions = await command.ExecuteQueryAnonymousAsync(new { ID = 0, Name = "", Missing = 0 }, new ReadOptions(FieldsSelector.Common), Token)
                    .ToListCompat(Token);

                companies.Should().Equal(new { ID = 1, Name = "Mega Co" }, new { ID = 2, Name = "Oldest Co" });
                withOptions.Should().Equal(new { ID = 1, Name = "Mega Co", Missing = 0 }, new { ID = 2, Name = "Oldest Co", Missing = 0 });
            }
        }

        [Fact]
        public void ExecuteQueryFirstColumn_ProviderCommand_ReturnsFirstColumnWithNulls()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectPhonesAndNames()))
            {
                var phones = command.ExecuteQueryFirstColumn().Cast<object>().ToArray();

                phones.Should().Equal(new object[] { "123-123-123", null, "222-333-444", "123-456-789", "111-999-888" });
            }
        }

        [Fact]
        public async Task ExecuteQueryFirstColumnAsync_ProviderCommand_ReturnsFirstColumnWithNullsAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectPhonesAndNames()))
            {
                var phones = await command.ExecuteQueryFirstColumnAsync(Token).ToListCompat(Token);

                phones.Should().Equal(new object[] { "123-123-123", null, "222-333-444", "123-456-789", "111-999-888" });
            }
        }

        [Fact]
        public void ExecuteQueryFirstColumnOfT_ProviderCommand_ReturnsTypedValues()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectAges()))
            {
                var ages = command.ExecuteQueryFirstColumn<int?>().ToArray();

                ages.Should().Equal(46, null, 23, 40, 26);
            }
        }

        [Fact]
        public async Task ExecuteQueryFirstColumnAsyncOfT_ProviderCommand_ReturnsTypedValuesAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectAges()))
            {
                var ages = await command.ExecuteQueryFirstColumnAsync<int?>(Token).ToListCompat(Token);

                ages.Should().Equal(46, null, 23, 40, 26);
            }
        }

        [Fact]
        public void ExecuteQueryOfT_CommandWithParameters_UsesThem()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectNamesByCompanyAndAge()))
            {
                command.Parameters.Add(Spec.CreateParameter("@CompanyID", 2));
                command.Parameters.Add(Spec.CreateParameter("@Age", 20));

                var names = command.ExecuteQueryFirstColumn<string>().ToArray();

                names.Should().Equal("Ivan Antonov", "Kate Donahue");
            }
        }

        [Fact]
        public async Task ExecuteQueryAsyncOfT_CommandWithParameters_UsesThemAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectNamesByCompanyAndAge()))
            {
                command.Parameters.Add(Spec.CreateParameter("@CompanyID", 2));
                command.Parameters.Add(Spec.CreateParameter("@Age", 20));

                var names = await command.ExecuteQueryFirstColumnAsync<string>(Token).ToListCompat(Token);

                names.Should().Equal("Ivan Antonov", "Kate Donahue");
            }
        }

        [Fact]
        public void ExecuteQuery_InsideTransaction_RunsInTransaction()
        {
            var table = Database.CreateScratchTable($"{Q("ID")} int not null");

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            using (var command = CreateCommand(connection, $"insert into {table} ({Q("ID")}) values (1); select count(*) from {table}"))
            {
                command.Transaction = transaction;

                var count = command.ExecuteQueryFirstColumn<int>().ToArray();
                transaction.Rollback();

                count.Should().Equal(1);
            }

            Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumn<int>().Single())
                .Should().Be(0);
        }

        #endregion

        #region Laziness and the data reader

        [Fact]
        public void ExecuteQuery_NotEnumerated_DoesNotExecuteCommand()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, "select * from no_such_table"))
            {
                var rows = command.ExecuteQuery();
                var employees = command.ExecuteQuery<Employee>();
                var anonymous = command.ExecuteQueryAnonymous(new { ID = 0 });
                var column = command.ExecuteQueryFirstColumn();
                var typedColumn = command.ExecuteQueryFirstColumn<int>();

                // nothing ran yet: no error, and the connection is free for another command
                CountCompanies(connection).Should().Be(2);

                ((Action)(() => rows.ToArray())).Should().Throw<DbException>();
                ((Action)(() => employees.ToArray())).Should().Throw<DbException>();
                ((Action)(() => anonymous.ToArray())).Should().Throw<DbException>();
                ((Action)(() => column.Cast<object>().ToArray())).Should().Throw<DbException>();
                ((Action)(() => typedColumn.ToArray())).Should().Throw<DbException>();
                CountCompanies(connection).Should().Be(2);
            }
        }

        [Fact]
        public async Task ExecuteQueryAsync_NotEnumerated_DoesNotExecuteCommandAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, "select * from no_such_table"))
            {
                var rows = command.ExecuteQueryAsync(Token);
                var employees = command.ExecuteQueryAsync<Employee>(cancellationToken: Token);
                var anonymous = command.ExecuteQueryAnonymousAsync(new { ID = 0 }, cancellationToken: Token);
                var column = command.ExecuteQueryFirstColumnAsync(Token);
                var typedColumn = command.ExecuteQueryFirstColumnAsync<int>(Token);

                CountCompanies(connection).Should().Be(2);

                await ((Func<Task>)(() => rows.ToListCompat(Token))).Should().ThrowAsync<DbException>();
                await ((Func<Task>)(() => employees.ToListCompat(Token))).Should().ThrowAsync<DbException>();
                await ((Func<Task>)(() => anonymous.ToListCompat(Token))).Should().ThrowAsync<DbException>();
                await ((Func<Task>)(() => column.ToListCompat(Token))).Should().ThrowAsync<DbException>();
                await ((Func<Task>)(() => typedColumn.ToListCompat(Token))).Should().ThrowAsync<DbException>();
                CountCompanies(connection).Should().Be(2);
            }
        }

        [Fact]
        public void ExecuteQuery_FullyEnumerated_DisposesReader()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectEmployees()))
            {
                command.ExecuteQuery<Employee>().ToArray().Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
                command.ExecuteQuery().ToArray().Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
                command.ExecuteQueryAnonymous(new { ID = 0 }, new ReadOptions(FieldsSelector.Common)).ToArray().Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
                command.ExecuteQueryFirstColumn().Cast<object>().ToArray().Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
                command.ExecuteQueryFirstColumn<int>().ToArray().Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
            }
        }

        [Fact]
        public async Task ExecuteQueryAsync_FullyEnumerated_DisposesReaderAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectEmployees()))
            {
                (await command.ExecuteQueryAsync<Employee>(cancellationToken: Token).ToListCompat(Token)).Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
                (await command.ExecuteQueryAsync(Token).ToListCompat(Token)).Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
                (await command.ExecuteQueryAnonymousAsync(new { ID = 0 }, new ReadOptions(FieldsSelector.Common), Token).ToListCompat(Token)).Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
                (await command.ExecuteQueryFirstColumnAsync(Token).ToListCompat(Token)).Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
                (await command.ExecuteQueryFirstColumnAsync<int>(Token).ToListCompat(Token)).Should().HaveCount(5);
                CountCompanies(connection).Should().Be(2);
            }
        }

        [Fact]
        public void ExecuteQuery_DisposedEarly_ClosesReaderAndConnectionRunsAnotherCommand()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectEmployees()))
            {
                command.ExecuteQuery<Employee>().Take(2).ToArray().Select(x => x.ID).Should().Equal(1, 2);
                CountCompanies(connection).Should().Be(2);
                command.ExecuteQueryFirstColumn<int>().Take(1).ToArray().Should().Equal(1);
                CountCompanies(connection).Should().Be(2);
            }
        }

        [Fact]
        public async Task ExecuteQueryAsync_DisposedEarly_ClosesReaderAndConnectionRunsAnotherCommandAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectEmployees()))
            {
                (await command.ExecuteQueryAsync<Employee>(cancellationToken: Token).TakeCompat(2, Token)).Select(x => x.ID).Should().Equal(1, 2);
                CountCompanies(connection).Should().Be(2);
                (await command.ExecuteQueryFirstColumnAsync<int>(Token).TakeCompat(1, Token)).Should().Equal(1);
                CountCompanies(connection).Should().Be(2);
            }
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver message for a busy connection")]
        public void ExecuteQuery_EnumerationOpen_ReaderBlocksConnectionUntilDisposed()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectEmployees()))
            {
                using (var enumerator = command.ExecuteQuery<Employee>().GetEnumerator())
                {
                    enumerator.MoveNext().Should().BeTrue();

                    Action anotherCommand = () => CountCompanies(connection);

                    anotherCommand.Should().Throw<Exception>().WithMessage(OpenReaderMessage(command.CommandText))
                    .Which.Should().BeOfType(OpenReaderExceptionType);
                }

                CountCompanies(connection).Should().Be(2);
            }
        }

        [Fact]
        [Trait("UpgradeSensitive", "driver message for a busy connection")]
        public async Task ExecuteQueryAsync_EnumerationOpen_ReaderBlocksConnectionUntilDisposedAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectEmployees()))
            {
                using (var enumerator = command.ExecuteQueryAsync<Employee>(cancellationToken: Token).GetEnumeratorCompat(Token))
                {
                    (await enumerator.MoveNextAsync()).Should().BeTrue();

                    Action anotherCommand = () => CountCompanies(connection);

                    anotherCommand.Should().Throw<Exception>().WithMessage(OpenReaderMessage(command.CommandText))
                    .Which.Should().BeOfType(OpenReaderExceptionType);
                }

                CountCompanies(connection).Should().Be(2);
            }
        }

        [Fact]
        public void ExecuteQueryOfT_MappingFails_ReaderDisposed()
        {
            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                Action act = () => command.ExecuteQuery<Company>().ToArray();

                act.Should().Throw<InvalidOperationException>().WithMessage(DestinationSelectorMessage);
                CountCompanies(connection).Should().Be(2);
            }
        }

        [Fact]
        [Trait("Quirk", "Q70")]
        [Trait("UpgradeSensitive", "driver message for a busy connection")]
        public async Task ExecuteQueryAsyncOfT_MappingFails_ReaderLeftOpenAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                using (var enumerator = command.ExecuteQueryAsync<Company>(cancellationToken: Token).GetEnumeratorCompat(Token))
                {
                    Func<Task> moveNext = () => enumerator.MoveNextAsync();

                    await moveNext.Should().ThrowAsync<InvalidOperationException>().WithMessage(DestinationSelectorMessage);
                }

                // the reader was opened before the mapping failed and nothing disposes it, not even the enumerator
                Action anotherCommand = () => CountCompanies(connection);

                anotherCommand.Should().Throw<Exception>().WithMessage(OpenReaderMessage(command.CommandText))
                    .Which.Should().BeOfType(OpenReaderExceptionType);
            }
        }

        [Fact]
        [Trait("Quirk", "Q70")]
        [Trait("UpgradeSensitive", "driver message for a busy connection")]
        public async Task ExecuteQueryAnonymousAsync_MappingFails_ReaderLeftOpenAsync()
        {
            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                Func<Task> act = () => command.ExecuteQueryAnonymousAsync(new { ID = 0, Missing = 0 }, cancellationToken: Token).ToListCompat(Token);

                await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(DestinationSelectorMessage);

                Action anotherCommand = () => CountCompanies(connection);

                anotherCommand.Should().Throw<Exception>().WithMessage(OpenReaderMessage(command.CommandText))
                    .Which.Should().BeOfType(OpenReaderExceptionType);
            }
        }

        #endregion

        #region ReadOptions passed by the caller

        [Fact]
        [Trait("Quirk", "Q31")]
        public void ExecuteQueryOfT_PartlySetReadOptions_UnsetValuesFilledInCallersInstance()
        {
            var readOptions = new ReadOptions(FieldsSelector.Common);

            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                var query = command.ExecuteQuery<Company>(readOptions);

                readOptions.CaseSensitiveFieldsMatching.Should().BeNull();
                query.ToArray().Should().HaveCount(2);
            }

            readOptions.FieldsSelector.Should().Be(FieldsSelector.Common);
            readOptions.CaseSensitiveFieldsMatching.Should().BeFalse();
            readOptions.FromTypeOption.Should().Be(FromTypeOption.Default);
        }

        [Fact]
        [Trait("Quirk", "Q31")]
        public async Task ExecuteQueryAsyncOfT_PartlySetReadOptions_UnsetValuesFilledInCallersInstanceAsync()
        {
            var readOptions = new ReadOptions(FieldsSelector.Common);

            using (var connection = await Database.OpenConnectionAsync())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                (await command.ExecuteQueryAsync<Company>(readOptions, Token).ToListCompat(Token)).Should().HaveCount(2);
            }

            readOptions.CaseSensitiveFieldsMatching.Should().BeFalse();
            readOptions.FromTypeOption.Should().Be(FromTypeOption.Default);
        }

        [Fact]
        [Trait("Quirk", "Q31")]
        public void ExecuteQueryAnonymous_PartlySetReadOptions_UnsetValuesFilledInCallersInstance()
        {
            var readOptions = new ReadOptions(caseSensitiveFieldsMatching: true);

            using (var connection = Database.OpenConnection())
            using (var command = CreateCommand(connection, SelectCompanyIdAndName()))
            {
                command.ExecuteQueryAnonymous(new { ID = 0, Name = "" }, readOptions).ToArray().Should().HaveCount(2);
            }

            readOptions.FieldsSelector.Should().Be(FieldsSelector.Destination);
            readOptions.CaseSensitiveFieldsMatching.Should().BeTrue();
            readOptions.FromTypeOption.Should().Be(FromTypeOption.Default);
        }

        [Fact]
        public void WrappedCommandExecuteQueryOfT_PartlySetReadOptions_CallersInstanceUnchanged()
        {
            var readOptions = new ReadOptions(FieldsSelector.Common);

            Database.Run(ContextKind.Connection, x => x.CreateSimple(SelectCompanyIdAndName()).ExecuteQuery<Company>(readOptions).ToArray())
                .Should().HaveCount(2);

            readOptions.FieldsSelector.Should().Be(FieldsSelector.Common);
            readOptions.CaseSensitiveFieldsMatching.Should().BeNull();
            readOptions.FromTypeOption.Should().BeNull();
        }

        #endregion

        private DbCommand CreateCommand(DbConnection connection, string commandText)
        {
            var command = connection.CreateCommand();
            command.CommandText = commandText;
            return command;
        }

        private int CountCompanies(DbConnection connection)
        {
            using (var command = CreateCommand(connection, $"select count(*) from {Q("Company")}"))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
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

        private string SelectCompanyIdAndName()
        {
            return $"select {Q("ID")}, {Q("Name")} from {Q("Company")} order by {Q("ID")}";
        }

        private string SelectPhonesAndNames()
        {
            return $"select {Q("Phone")}, {Q("Name")} from {Q("Employee")} order by {Q("ID")}";
        }

        private string SelectAges()
        {
            return $"select {Q("Age")} from {Q("Employee")} order by {Q("ID")}";
        }

        private string SelectNamesByCompanyAndAge()
        {
            return $"select {Q("Name")} from {Q("Employee")} where {Q("CompanyID")} = @CompanyID and {Q("Age")} > @Age order by {Q("ID")}";
        }
    }
}
