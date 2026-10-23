using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    // I9, SQL Server only: the default case-sensitive mapping, SqlBulkCopy options and their precedence,
    // and table creation (CreateTable with ColumnDefinitionOptions)
    [Trait("Provider", "SqlServer")]
    public class SqlServerBulkCopyOptionsTests : IClassFixture<SqlServerDatabase>
    {
        private const string ColumnMappingError = "The given ColumnMapping does not match up with any column in the source or destination.";

        public SqlServerBulkCopyOptionsTests(SqlServerDatabase database)
        {
            this.Database = database;
        }

        private SqlServerDatabase Database { get; }

        #region Default mapping

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q16")]
        [Trait("UpgradeSensitive", "SqlBulkCopy column mapping")]
        public void WriteToServer_DefaultSelectorAndNamesDifferInCase_ThrowsInvalidOperationException(ContextKind kind)
        {
            var table = CreateNamedTable();

            Action act = () => Database.Run(kind, x => new[] { new { ID = 1, name = "one" } }.WriteToServer(x, table));

            act.Should().Throw<InvalidOperationException>().WithMessage(ColumnMappingError);
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q16")]
        [Trait("UpgradeSensitive", "SqlBulkCopy column mapping")]
        public async Task WriteToServerAsync_DefaultSelectorAndNamesDifferInCase_ThrowsInvalidOperationExceptionAsync(ContextKind kind)
        {
            var table = CreateNamedTable();

            Func<Task> act = () => Database.RunAsync(kind, x => new[] { new { ID = 1, name = "one" } }
                .WriteToServerAsync(x, table, TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ColumnMappingError);
            CountRows(table).Should().Be(0);
        }

        [Fact]
        [Trait("UpgradeSensitive", "SqlBulkCopy column mapping")]
        public void WriteToServer_DefaultSelectorAndSourceMemberNotInDestination_ThrowsInvalidOperationException()
        {
            var table = CreateNamedTable();

            Action act = () => Database.Run(ContextKind.Connection, x => new[] { new { ID = 1, Name = "one", Extra = "extra" } }.WriteToServer(x, table));

            act.Should().Throw<InvalidOperationException>().WithMessage(ColumnMappingError);
            CountRows(table).Should().Be(0);
        }

        // SqlBulkCopy asks the source reader for the ordinal of the mapped name
        [Fact]
        [Trait("UpgradeSensitive", "SqlBulkCopy column mapping")]
        public void WriteToServer_FieldNotInSource_ThrowsKeyNotFoundException()
        {
            var table = CreateNamedTable();

            Action act = () => Database.Run(ContextKind.Connection, x => new[] { new { ID = 1, Name = "one" } }.WriteToServer(x, table, "ID", "Missing"));

            act.Should().Throw<KeyNotFoundException>().WithMessage("The given key 'Missing' was not present in the dictionary.");
            CountRows(table).Should().Be(0);
        }

        // System.Data.SqlClient 4.5.1 cannot read uniqueidentifier values on .NET 9 and later: its SqlBuffer.SqlGuidCaster no longer loads.
        // Unpooled connections, because the failed connections are left in an unusable state.
        [Fact]
        [Trait("Quirk", "Q102")]
        [Trait("UpgradeSensitive", "System.Data.SqlClient 4.5.1 on .NET 9+")]
        public void WriteToServer_UniqueidentifierColumnFromQuery_ThrowsTypeLoadException()
        {
            var source = Database.CreateScratchTable("ID int not null, Uid uniqueidentifier null");
            Database.Execute($"insert into {source} values (1, '6f9619ff-8b86-d011-b42d-00c04fc964ff')");
            var table = Database.CreateScratchTable("ID int not null, Uid uniqueidentifier null");
            var connectionString = Database.CreateSessionTrackedConnectionString("fastsql-" + Guid.NewGuid().ToString("N"));
            var provider = Database.Spec.DefaultProvider;

            Action act = () => provider.CreateContext(connectionString).CreateSimple($"select * from {source}")
                .WriteToServer(provider.CreateContext(connectionString), table);

            act.Should().Throw<TypeLoadException>().WithMessage("Could not load type 'SqlGuidCaster' from assembly 'System.Data.SqlClient*");
            CountRows(table).Should().Be(0);
        }

        #endregion

        #region SqlBulkCopyOptions

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_IdentityColumnInSource_IgnoresSourceValues(ContextKind kind)
        {
            var table = CreateIdentityTable();

            Database.Run(kind, x => IdentityRows.WriteToServer(x, table));

            ReadIds(table).Should().Equal(1, 2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_KeepIdentity_KeepsSourceValues(ContextKind kind)
        {
            var table = CreateIdentityTable();

            Database.Run(kind, x => IdentityRows.WriteToServer(x, table, new SqlBulkOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.KeepIdentity)));

            ReadIds(table).Should().Equal(10, 20);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_KeepIdentity_KeepsSourceValuesAsync(ContextKind kind)
        {
            var table = CreateIdentityTable();

            await Database.RunAsync(kind, x => IdentityRows.WriteToServerAsync(x, table,
                new SqlBulkOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.KeepIdentity), TestContext.Current.CancellationToken));

            ReadIds(table).Should().Equal(10, 20);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_NullIntoColumnWithDefault_WritesDefault(ContextKind kind)
        {
            var table = CreateTableWithDefault();

            Database.Run(kind, x => NullNameRows.WriteToServer(x, table));

            ReadNames(table).Should().Equal("default name");
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_KeepNulls_WritesNull(ContextKind kind)
        {
            var table = CreateTableWithDefault();

            Database.Run(kind, x => NullNameRows.WriteToServer(x, table, new SqlBulkOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.KeepNulls)));

            ReadNames(table).Should().Equal(new string[] { null });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_KeepNulls_WritesNullAsync(ContextKind kind)
        {
            var table = CreateTableWithDefault();

            await Database.RunAsync(kind, x => NullNameRows.WriteToServerAsync(x, table,
                new SqlBulkOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.KeepNulls), TestContext.Current.CancellationToken));

            ReadNames(table).Should().Equal(new string[] { null });
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_RowViolatesCheckConstraint_CopiesRow(ContextKind kind)
        {
            var table = CreateTableWithCheckConstraint();

            Database.Run(kind, x => NegativeAgeRows.WriteToServer(x, table));

            CountRows(table).Should().Be(1);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CheckConstraintsAndRowViolatesConstraint_ThrowsSqlException(ContextKind kind)
        {
            var table = CreateTableWithCheckConstraint();

            Action act = () => Database.Run(kind, x => NegativeAgeRows.WriteToServer(x, table,
                new SqlBulkOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.CheckConstraints)));

            act.Should().Throw<SqlException>().WithMessage($"*conflicted with the CHECK constraint \"{table}_age\"*");
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CheckConstraintsAndRowViolatesConstraint_ThrowsSqlExceptionAsync(ContextKind kind)
        {
            var table = CreateTableWithCheckConstraint();

            Func<Task> act = () => Database.RunAsync(kind, x => NegativeAgeRows.WriteToServerAsync(x, table,
                new SqlBulkOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.CheckConstraints), TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<SqlException>().WithMessage($"*conflicted with the CHECK constraint \"{table}_age\"*");
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_TableWithTrigger_DoesNotFireTrigger(ContextKind kind)
        {
            var table = CreateTableWithTrigger(out var log);

            Database.Run(kind, x => NamedRows.WriteToServer(x, table));

            CountRows(table).Should().Be(2);
            CountRows(log).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_FireTriggers_FiresTrigger(ContextKind kind)
        {
            var table = CreateTableWithTrigger(out var log);

            Database.Run(kind, x => NamedRows.WriteToServer(x, table, new SqlBulkOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.FireTriggers)));

            CountRows(table).Should().Be(2);
            CountRows(log).Should().Be(2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_FireTriggers_FiresTriggerAsync(ContextKind kind)
        {
            var table = CreateTableWithTrigger(out var log);

            await Database.RunAsync(kind, x => NamedRows.WriteToServerAsync(x, table,
                new SqlBulkOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.FireTriggers), TestContext.Current.CancellationToken));

            CountRows(table).Should().Be(2);
            CountRows(log).Should().Be(2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_TableLockBatchSizeTimeoutAndStreaming_CopiesRows(ContextKind kind)
        {
            var table = CreateNamedTable();

            Database.Run(kind, x => ManyNamedRows.WriteToServer(x, table, SmokeOptions()));

            ReadNames(table).Should().Equal(ManyNamedRows.Select(x => x.Name));
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_TableLockBatchSizeTimeoutAndStreaming_CopiesRowsAsync(ContextKind kind)
        {
            var table = CreateNamedTable();

            await Database.RunAsync(kind, x => ManyNamedRows.WriteToServerAsync(x, table, SmokeOptions(), TestContext.Current.CancellationToken));

            ReadNames(table).Should().Equal(ManyNamedRows.Select(x => x.Name));
        }

        [Fact]
        [Trait("UpgradeSensitive", "SqlBulkCopy null check")]
        public void WriteToServer_FailingRowWithoutBatchSize_CopiesNothing()
        {
            var table = CreateTableWithRequiredName();

            Action act = () => Database.Run(ContextKind.Connection, x => RowsWithFailingThird.WriteToServer(x, table));

            act.Should().Throw<InvalidOperationException>().WithMessage("Column 'Name' does not allow DBNull.Value.");
            CountRows(table).Should().Be(0);
        }

        [Fact]
        [Trait("UpgradeSensitive", "SqlBulkCopy null check")]
        public void WriteToServer_FailingRowWithBatchSize_KeepsCompletedBatches()
        {
            var table = CreateTableWithRequiredName();

            Action act = () => Database.Run(ContextKind.Connection, x => RowsWithFailingThird.WriteToServer(x, table, new SqlBulkOptions(batchSize: 1)));

            act.Should().Throw<InvalidOperationException>().WithMessage("Column 'Name' does not allow DBNull.Value.");
            CountRows(table).Should().Be(2);
        }

        // unpooled: after this failure the async copy can return its connection to the pool in the middle of the bulk load
        [Fact]
        [Trait("UpgradeSensitive", "SqlBulkCopy null check")]
        public async Task WriteToServerAsync_FailingRowWithBatchSize_KeepsCompletedBatchesAsync()
        {
            var table = CreateTableWithRequiredName();
            var context = Database.Spec.DefaultProvider.CreateContext(Database.CreateSessionTrackedConnectionString("fastsql-" + Guid.NewGuid().ToString("N")));

            Func<Task> act = () => RowsWithFailingThird.WriteToServerAsync(context, table, new SqlBulkOptions(batchSize: 1), TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Column 'Name' does not allow DBNull.Value.");
            CountRows(table).Should().Be(2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("UpgradeSensitive", "SqlBulkCopy cancellation")]
        public async Task WriteToServerAsync_CancelledToken_ThrowsTaskCanceledExceptionAsync(ContextKind kind)
        {
            var table = CreateNamedTable();

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                Func<Task> act = () => Database.RunAsync(kind, x => NamedRows.WriteToServerAsync(x, table, cancellation.Token));

                await act.Should().ThrowExactlyAsync<TaskCanceledException>();
            }

            CountRows(table).Should().Be(0);
        }

        #endregion

        #region Options precedence

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_ProviderLevelOptions_Apply(ContextKind kind)
        {
            var table = CreateIdentityTable();
            var provider = Database.Spec.CreateProvider();
            ((SqlBulkOptions)provider.BulkOptions).SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity;

            Database.Run(provider, kind, x => IdentityRows.WriteToServer(x, table));

            ReadIds(table).Should().Equal(10, 20);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_ContextLevelOptions_Apply(ContextKind kind)
        {
            var table = CreateIdentityTable();

            Database.Run(kind, x =>
            {
                ((SqlBulkOptions)x.BulkOptions).SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity;
                IdentityRows.WriteToServer(x, table);
            });

            ReadIds(table).Should().Equal(10, 20);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_PerCallOptions_OverrideContextLevelOptions(ContextKind kind)
        {
            var table = CreateIdentityTable();

            Database.Run(kind, x =>
            {
                ((SqlBulkOptions)x.BulkOptions).SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity;
                IdentityRows.WriteToServer(x, table, new SqlBulkOptions(sqlBulkCopyOptions: SqlBulkCopyOptions.Default));
            });

            ReadIds(table).Should().Equal(1, 2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_PerCallBaseOptions_KeepContextLevelSqlServerOptions(ContextKind kind)
        {
            var table = CreateIdentityTable();

            Database.Run(kind, x =>
            {
                ((SqlBulkOptions)x.BulkOptions).SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity;
                IdentityRows.WriteToServer(x, table, new BulkOptions(FieldsSelector.Source));
            });

            ReadIds(table).Should().Equal(10, 20);
        }

        #endregion

        #region CreateTable

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CreateTableFromQuery_ReproducesColumns(ContextKind kind)
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            Database.Run(kind, x => SourceContext().CreateSimple($"select * from {source}").WriteToServer(x, table, new SqlBulkOptions(createTable: true)));

            DescribeColumns(table).Should().Equal(TypedSourceColumns);
            CountRows(table).Should().Be(2);
            CountDifferentRows(source, table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CreateTableFromQuery_ReproducesColumnsAsync(ContextKind kind)
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            await Database.RunAsync(kind, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServerAsync(x, table, new SqlBulkOptions(createTable: true), TestContext.Current.CancellationToken));

            DescribeColumns(table).Should().Equal(TypedSourceColumns);
            CountRows(table).Should().Be(2);
            CountDifferentRows(source, table).Should().Be(0);
        }

        // the reader reports no key information, so only ColumnDefinitionOptions.PrimaryKey makes a primary key
        [Fact]
        [Trait("Quirk", "Q103")]
        [Trait("UpgradeSensitive", "SqlDataReader column schema")]
        public void WriteToServer_CreateTableFromQueryOverPrimaryKey_DoesNotCreatePrimaryKey()
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServer(x, table, new SqlBulkOptions(createTable: true)));

            ReadPrimaryKey(source).Should().Equal("ID");
            ReadPrimaryKey(table).Should().BeEmpty();
        }

        [Fact]
        public void WriteToServer_CreateTableFromQueryWithPrimaryKeyOption_CreatesPrimaryKey()
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServer(x, table, new SqlBulkOptions(createTable: true, columnDefinitionOptions: ColumnDefinitionOptions.FromDefault("ID"))));

            ReadPrimaryKey(table).Should().Equal("ID");
        }

        // the reader reports the non-nullable CLR type for a nullable column, so the column is created as not null
        [Fact]
        [Trait("Quirk", "Q104")]
        [Trait("UpgradeSensitive", "SqlBulkCopy null check")]
        public void WriteToServer_CreateTableFromQueryIgnoringSchemaTable_CreatesValueTypeColumnsAsNotNull()
        {
            var table = NewTableName();

            Action act = () => Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple("select CompanyID, Name, Age from Employee order by ID")
                .WriteToServer(x, table, new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true)));

            act.Should().Throw<InvalidOperationException>().WithMessage("Column 'Age' does not allow DBNull.Value.");
            DescribeColumns(table).Should().Equal("CompanyID int not null", "Name nvarchar(4000) null", "Age int not null");
            CountRows(table).Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q14")]
        public void WriteToServer_CreateTableFromEnumerable_ThrowsNotSupportedException(ContextKind kind)
        {
            var table = NewTableName();

            Action act = () => Database.Run(kind, x => TypedRows.WriteToServer(x, table, new SqlBulkOptions(createTable: true)));

            act.Should().Throw<NotSupportedException>().WithMessage("Specified method is not supported.");
            TableExists(table).Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q14")]
        public async Task WriteToServerAsync_CreateTableFromEnumerable_ThrowsNotSupportedExceptionAsync(ContextKind kind)
        {
            var table = NewTableName();

            Func<Task> act = () => Database.RunAsync(kind, x => TypedRows.WriteToServerAsync(x, table, new SqlBulkOptions(createTable: true),
                TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("Specified method is not supported.");
            TableExists(table).Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q14")]
        public void WriteToServer_CreateTableFromEnumerableIgnoringSchemaTable_CreatesColumnsFromMemberTypes(ContextKind kind)
        {
            var table = NewTableName();

            // the provider-independent BulkOptions work too
            Database.Run(kind, x => TypedRows.WriteToServer(x, table, new BulkOptions(createTable: true, ignoreDataReaderSchemaTable: true)));

            DescribeColumns(table).Should().Equal(TypedRowColumns);
            ReadPrimaryKey(table).Should().BeEmpty();
            ReadTypedRowsWithoutUid(table).Should().BeEquivalentTo(TypedRows, o => o.Excluding(x => x.Uid).WithStrictOrdering());
            ReadUidsAsText(table).Should().Equal(TypedRows.Select(x => x.Uid.ToString().ToUpperInvariant()));
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Quirk", "Q14")]
        public async Task WriteToServerAsync_CreateTableFromEnumerableIgnoringSchemaTable_CreatesColumnsFromMemberTypesAsync(ContextKind kind)
        {
            var table = NewTableName();

            await Database.RunAsync(kind, x => TypedRows.WriteToServerAsync(x, table, new BulkOptions(createTable: true, ignoreDataReaderSchemaTable: true),
                TestContext.Current.CancellationToken));

            DescribeColumns(table).Should().Equal(TypedRowColumns);
            ReadTypedRowsWithoutUid(table).Should().BeEquivalentTo(TypedRows, o => o.Excluding(x => x.Uid).WithStrictOrdering());
            ReadUidsAsText(table).Should().Equal(TypedRows.Select(x => x.Uid.ToString().ToUpperInvariant()));
        }

        public static TheoryData<string, string[]> ColumnDefinitionOptionsCases => new TheoryData<string, string[]>()
        {
            { "Default", new[] { "ID int not null", "Name nvarchar(4000) null", "Data varbinary(8000) null", "Amount decimal(38,18) not null", "Created datetime2(7) not null" } },
            { "Max", new[] { "ID int not null", "Name nvarchar(max) null", "Data varbinary(max) null", "Amount decimal(38,18) not null", "Created datetime2(7) not null" } },
            { "Small", new[] { "ID int not null", "Name nvarchar(255) null", "Data varbinary(255) null", "Amount decimal(19,9) not null", "Created datetime2(3) not null" } }
        };

        [Theory]
        [MemberData(nameof(ColumnDefinitionOptionsCases))]
        public void WriteToServer_CreateTableWithColumnDefinitionOptions_UsesSizesAndPrimaryKey(string options, string[] expectedColumns)
        {
            var table = NewTableName();

            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table,
                new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true, columnDefinitionOptions: WithPrimaryKey(options, "ID"))));

            DescribeColumns(table).Should().Equal(expectedColumns);
            ReadPrimaryKey(table).Should().Equal("ID");
            ReadAll<SizedRow>(table, "ID").Should().BeEquivalentTo(SizedRows, o => o.WithStrictOrdering());
        }

        [Theory]
        [MemberData(nameof(ColumnDefinitionOptionsCases))]
        public async Task WriteToServerAsync_CreateTableWithColumnDefinitionOptions_UsesSizesAndPrimaryKeyAsync(string options, string[] expectedColumns)
        {
            var table = NewTableName();

            await Database.RunAsync(ContextKind.Connection, x => SizedRows.WriteToServerAsync(x, table,
                new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true, columnDefinitionOptions: WithPrimaryKey(options, "ID")),
                TestContext.Current.CancellationToken));

            DescribeColumns(table).Should().Equal(expectedColumns);
            ReadPrimaryKey(table).Should().Equal("ID");
        }

        [Fact]
        [Trait("Quirk", "Q100")]
        public void WriteToServer_CreateTableWithUnsupportedMemberType_ThrowsNullReferenceException()
        {
            var table = NewTableName();

            Action act = () => Database.Run(ContextKind.Connection, x => UnsupportedRows.WriteToServer(x, table,
                new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true)));

            act.Should().Throw<NullReferenceException>().WithMessage("Object reference not set to an instance of an object.");
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        [Trait("Quirk", "Q100")]
        public async Task WriteToServerAsync_CreateTableWithUnsupportedMemberType_ThrowsNullReferenceExceptionAsync()
        {
            var table = NewTableName();

            Func<Task> act = () => Database.RunAsync(ContextKind.Connection, x => UnsupportedRows.WriteToServerAsync(x, table,
                new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true), TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<NullReferenceException>().WithMessage("Object reference not set to an instance of an object.");
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        [Trait("Quirk", "Q100")]
        public void WriteToServer_CreateTableWithUnsupportedMemberTypeNotInFields_ThrowsNullReferenceException()
        {
            var table = NewTableName();

            Action act = () => Database.Run(ContextKind.Connection, x => UnsupportedRows.WriteToServer(x, table,
                new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true), "ID"));

            act.Should().Throw<NullReferenceException>();
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        public void WriteToServer_CreateTableWithUnsupportedMemberTypeAndThrowIfUnsupportedType_ThrowsArgumentOutOfRangeException()
        {
            var table = NewTableName();
            var columnOptions = ColumnDefinitionOptions.FromDefault();
            columnOptions.ThrowIfUnsupportedType = true;

            Action act = () => Database.Run(ContextKind.Connection, x => UnsupportedRows.WriteToServer(x, table,
                new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true, columnDefinitionOptions: columnOptions)));

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("Unsupported type*").WithParameterName("type")
                .Which.ActualValue.Should().Be(typeof(TimeSpan));
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        public async Task WriteToServerAsync_CreateTableWithUnsupportedMemberTypeAndThrowIfUnsupportedType_ThrowsArgumentOutOfRangeExceptionAsync()
        {
            var table = NewTableName();
            var columnOptions = ColumnDefinitionOptions.FromDefault();
            columnOptions.ThrowIfUnsupportedType = true;

            Func<Task> act = () => Database.RunAsync(ContextKind.Connection, x => UnsupportedRows.WriteToServerAsync(x, table,
                new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true, columnDefinitionOptions: columnOptions),
                TestContext.Current.CancellationToken));

            (await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithMessage("Unsupported type*").WithParameterName("type"))
                .Which.ActualValue.Should().Be(typeof(TimeSpan));
            TableExists(table).Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CreateTableWhenTableExists_ThrowsSqlException(ContextKind kind)
        {
            var table = NewTableName();
            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: false)));

            Action act = () => Database.Run(kind, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: false)));

            act.Should().Throw<SqlException>().WithMessage($"There is already an object named '{table}' in the database.");
            CountRows(table).Should().Be(SizedRows.Length);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CreateTableWhenTableExists_ThrowsSqlExceptionAsync(ContextKind kind)
        {
            var table = NewTableName();
            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: false)));

            Func<Task> act = () => Database.RunAsync(kind, x => SizedRows.WriteToServerAsync(x, table, CreateFromMembers(checkIfNotExists: false),
                TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<SqlException>().WithMessage($"There is already an object named '{table}' in the database.");
            CountRows(table).Should().Be(SizedRows.Length);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void WriteToServer_CreateTableWhenTableExistsAndCheckIfNotExists_AppendsRows(ContextKind kind)
        {
            var table = NewTableName();
            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: true)));

            Database.Run(kind, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: true)));

            CountRows(table).Should().Be(SizedRows.Length * 2);
        }

        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public async Task WriteToServerAsync_CreateTableWhenTableExistsAndCheckIfNotExists_AppendsRowsAsync(ContextKind kind)
        {
            var table = NewTableName();
            Database.Run(ContextKind.Connection, x => SizedRows.WriteToServer(x, table, CreateFromMembers(checkIfNotExists: true)));

            await Database.RunAsync(kind, x => SizedRows.WriteToServerAsync(x, table, CreateFromMembers(checkIfNotExists: true),
                TestContext.Current.CancellationToken));

            CountRows(table).Should().Be(SizedRows.Length * 2);
        }

        [Fact]
        public void WriteToServer_CreateTableFromXmlColumn_ThrowsNotSupportedException()
        {
            var source = Database.CreateScratchTable("ID int not null, Doc xml null");
            var table = NewTableName();

            Action act = () => Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServer(x, table, new SqlBulkOptions(createTable: true)));

            act.Should().Throw<NotSupportedException>().WithMessage("Type xml is not supported");
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        public async Task WriteToServerAsync_CreateTableFromXmlColumn_ThrowsNotSupportedExceptionAsync()
        {
            var source = Database.CreateScratchTable("ID int not null, Doc xml null");
            var table = NewTableName();

            Func<Task> act = () => Database.RunAsync(ContextKind.Connection, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServerAsync(x, table, new SqlBulkOptions(createTable: true), TestContext.Current.CancellationToken));

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("Type xml is not supported");
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        public void WriteToServer_CreateTableWithFields_CreatesOnlyListedColumns()
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServer(x, table, new SqlBulkOptions(createTable: true), "ID", "Name"));

            DescribeColumns(table).Should().Equal("ID int not null", "Name nvarchar(50) not null");
            CountRows(table).Should().Be(2);
        }

        [Theory]
        [InlineData("Missing")]
        [InlineData("name")]
        public void WriteToServer_CreateTableWithFieldNotInSource_ThrowsInvalidOperationException(string field)
        {
            var source = CreateTypedSourceTable();
            var table = NewTableName();

            Action act = () => Database.Run(ContextKind.Connection, x => SourceContext().CreateSimple($"select * from {source}")
                .WriteToServer(x, table, new SqlBulkOptions(createTable: true), "ID", field));

            act.Should().Throw<InvalidOperationException>().WithMessage("Can not create table with specified fields");
            TableExists(table).Should().BeFalse();
        }

        [Fact]
        public void WriteToServer_CreateTableInRolledBackTransaction_DiscardsTable()
        {
            var table = NewTableName();

            using (var connection = Database.OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                SizedRows.WriteToServer(Database.Spec.DefaultProvider.CreateContext(transaction), table, CreateFromMembers(checkIfNotExists: false));
                transaction.Rollback();
            }

            TableExists(table).Should().BeFalse();
        }

        #endregion

        #region Helpers

        private static SqlBulkOptions SmokeOptions()
        {
            return new SqlBulkOptions(batchSize: 2, bulkCopyTimeout: 60, sqlBulkCopyOptions: SqlBulkCopyOptions.TableLock, enableStreaming: true);
        }

        private static SqlBulkOptions CreateFromMembers(bool checkIfNotExists)
        {
            return new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true, checkTableIfNotExistsBeforeCreation: checkIfNotExists);
        }

        // From* clone the static instances, which must not be changed
        private static ColumnDefinitionOptions WithPrimaryKey(string options, params string[] primaryKey)
        {
            switch (options)
            {
                case "Default":
                    return ColumnDefinitionOptions.FromDefault(primaryKey);
                case "Max":
                    return ColumnDefinitionOptions.FromMax(primaryKey);
                case "Small":
                    return ColumnDefinitionOptions.FromSmall(primaryKey);
                default:
                    throw new ArgumentOutOfRangeException(nameof(options), options, "Unknown options");
            }
        }

        private static string NewTableName()
        {
            return "bulk_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private DbContext SourceContext()
        {
            return Database.Spec.DefaultProvider.CreateContext(Database.ConnectionString);
        }

        private string CreateNamedTable()
        {
            return Database.CreateScratchTable("ID int not null, Name nvarchar(50) null");
        }

        private string CreateIdentityTable()
        {
            return Database.CreateScratchTable("ID int identity(1,1) not null primary key, Name nvarchar(50) null");
        }

        private string CreateTableWithDefault()
        {
            return Database.CreateScratchTable("ID int not null, Name nvarchar(50) null default 'default name'");
        }

        private string CreateTableWithRequiredName()
        {
            return Database.CreateScratchTable("ID int not null, Name nvarchar(50) not null");
        }

        private string CreateTableWithCheckConstraint()
        {
            var table = Database.CreateScratchTable("ID int not null, Age int null");
            Database.Execute($"alter table {table} add constraint {table}_age check (Age > 0)");
            return table;
        }

        private string CreateTableWithTrigger(out string log)
        {
            var table = CreateNamedTable();
            log = Database.CreateScratchTable("ID int not null");
            Database.Execute($"create trigger {table}_insert on {table} after insert as insert into {log} (ID) select ID from inserted");
            return table;
        }

        private string CreateTypedSourceTable()
        {
            // no uniqueidentifier column: the driver cannot read it on this runtime (Q102)
            var table = Database.CreateScratchTable("ID int not null primary key, Code char(5) not null, Name nvarchar(50) not null, "
                + "Note varchar(20) null, Body nvarchar(max) null, Amount decimal(10, 3) null, Created datetime2(3) not null, Legacy datetime null, "
                + "Duration time(2) null, Moment datetimeoffset(5) null, Data varbinary(100) null, Flag bit not null, Ratio float null");
            Database.Execute($"insert into {table} values "
                + "(1, 'AB', N'first', 'note', N'long text', 12.345, '2020-01-02 03:04:05.678', '2001-02-03 04:05:06', '01:02:03.45', "
                + "'2020-01-02 03:04:05.12345 +02:00', 0x0102, 1, 1.5), "
                + "(2, 'CD', N'second', null, null, null, '2021-06-07 08:09:10', null, null, null, null, 0, null)");
            return table;
        }

        private static string[] TypedSourceColumns => new[]
        {
            "ID int not null",
            "Code char(5) not null",
            "Name nvarchar(50) not null",
            "Note varchar(20) null",
            "Body nvarchar(max) null",
            "Amount decimal(10,3) null",
            "Created datetime2(3) not null",
            "Legacy datetime null",
            "Duration time(2) null",
            "Moment datetimeoffset(5) null",
            "Data varbinary(100) null",
            "Flag bit not null",
            "Ratio float null"
        };

        private static string[] TypedRowColumns => new[]
        {
            "ID int not null",
            "Number int null",
            "Big bigint not null",
            "Small smallint not null",
            "Tiny tinyint not null",
            "Flag bit not null",
            "MaybeFlag bit null",
            "Amount decimal(38,18) not null",
            "Ratio float not null",
            "Single real not null",
            "Created datetime2(7) not null",
            "Modified datetime2(7) null",
            "Uid uniqueidentifier not null",
            "Name nvarchar(4000) null",
            "Data varbinary(8000) null"
        };

        // column name, type with size and nullability, from INFORMATION_SCHEMA
        private string[] DescribeColumns(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple("select COLUMN_NAME as Name, DATA_TYPE as DataType, "
                    + "CHARACTER_MAXIMUM_LENGTH as MaxLength, cast(NUMERIC_PRECISION as int) as Precision, NUMERIC_SCALE as Scale, "
                    + "cast(DATETIME_PRECISION as int) as DateTimePrecision, IS_NULLABLE as IsNullable "
                    + "from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = @p0 order by ORDINAL_POSITION", table)
                .ExecuteQuery<ColumnInfo>().Select(Describe).ToArray());
        }

        private static string Describe(ColumnInfo column)
        {
            string type;
            switch (column.DataType)
            {
                case "decimal":
                case "numeric":
                    type = $"{column.DataType}({column.Precision},{column.Scale})";
                    break;
                case "time":
                case "datetime2":
                case "datetimeoffset":
                    type = $"{column.DataType}({column.DateTimePrecision})";
                    break;
                default:
                    type = column.MaxLength.HasValue ? $"{column.DataType}({(column.MaxLength == -1 ? "max" : column.MaxLength.ToString())})" : column.DataType;
                    break;
            }

            return $"{column.Name} {type} {(column.IsNullable == "YES" ? "null" : "not null")}";
        }

        private string[] ReadPrimaryKey(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple("select k.COLUMN_NAME from INFORMATION_SCHEMA.TABLE_CONSTRAINTS c "
                    + "join INFORMATION_SCHEMA.KEY_COLUMN_USAGE k on k.CONSTRAINT_NAME = c.CONSTRAINT_NAME and k.TABLE_NAME = c.TABLE_NAME "
                    + "where c.TABLE_NAME = @p0 and c.CONSTRAINT_TYPE = 'PRIMARY KEY' order by k.ORDINAL_POSITION", table)
                .ExecuteQueryFirstColumn<string>().ToArray());
        }

        private bool TableExists(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple("select count(*) from INFORMATION_SCHEMA.TABLES where TABLE_NAME = @p0", table)
                .ExecuteQueryFirstColumn<int>().Single()) == 1;
        }

        private int CountRows(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from {table}").ExecuteQueryFirstColumn<int>().Single());
        }

        // rows of the first table missing from the second one
        private int CountDifferentRows(string first, string second)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select count(*) from (select * from {first} except select * from {second}) d")
                .ExecuteQueryFirstColumn<int>().Single());
        }

        private int[] ReadIds(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select ID from {table} order by ID").ExecuteQueryFirstColumn<int>().ToArray());
        }

        private string[] ReadNames(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select Name from {table} order by ID").ExecuteQueryFirstColumn<string>().ToArray());
        }

        private T[] ReadAll<T>(string table, string orderBy) where T : new()
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by {orderBy}").ExecuteQuery<T>().ToArray());
        }

        // every column but Uid: the driver cannot read uniqueidentifier values on this runtime (Q102)
        private TypedRow[] ReadTypedRowsWithoutUid(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple("select ID, Number, Big, Small, Tiny, Flag, MaybeFlag, Amount, Ratio, Single, "
                    + $"Created, Modified, Name, Data from {table} order by ID")
                .ExecuteQuery<TypedRow>(new ReadOptions(FieldsSelector.Source)).ToArray());
        }

        private string[] ReadUidsAsText(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select cast(Uid as char(36)) from {table} order by ID")
                .ExecuteQueryFirstColumn<string>().ToArray());
        }

        #endregion

        #region Models

        private static NamedRow[] NamedRows => new[]
        {
            new NamedRow() { ID = 1, Name = "one" },
            new NamedRow() { ID = 2, Name = "two" }
        };

        private static NamedRow[] ManyNamedRows => Enumerable.Range(1, 5).Select(x => new NamedRow() { ID = x, Name = "name " + x }).ToArray();

        private static NamedRow[] IdentityRows => new[]
        {
            new NamedRow() { ID = 10, Name = "ten" },
            new NamedRow() { ID = 20, Name = "twenty" }
        };

        private static NamedRow[] NullNameRows => new[]
        {
            new NamedRow() { ID = 1, Name = null }
        };

        private static NamedRow[] RowsWithFailingThird => new[]
        {
            new NamedRow() { ID = 1, Name = "one" },
            new NamedRow() { ID = 2, Name = "two" },
            new NamedRow() { ID = 3, Name = null }
        };

        private static AgeRow[] NegativeAgeRows => new[]
        {
            new AgeRow() { ID = 1, Age = -1 }
        };

        private static TypedRow[] TypedRows => new[]
        {
            new TypedRow()
            {
                ID = 1, Number = 42, Big = 5000000000L, Small = -7, Tiny = 200, Flag = true, MaybeFlag = false, Amount = 12.5m, Ratio = 0.25,
                Single = 1.5f, Created = new DateTime(2020, 1, 2, 3, 4, 5), Modified = new DateTime(2021, 2, 3), Uid = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
                Name = "first", Data = new byte[] { 1, 2, 3 }
            },
            new TypedRow()
            {
                ID = 2, Number = null, Big = 0, Small = 0, Tiny = 0, Flag = false, MaybeFlag = null, Amount = 0m, Ratio = 0, Single = 0,
                Created = new DateTime(2022, 3, 4), Modified = null, Uid = Guid.Empty, Name = null, Data = null
            }
        };

        private static SizedRow[] SizedRows => new[]
        {
            new SizedRow() { ID = 1, Name = "first", Data = new byte[] { 1, 2 }, Amount = 1.25m, Created = new DateTime(2020, 1, 2, 3, 4, 5) },
            new SizedRow() { ID = 2, Name = null, Data = null, Amount = 0m, Created = new DateTime(2021, 6, 7) }
        };

        private static UnsupportedRow[] UnsupportedRows => new[]
        {
            new UnsupportedRow() { ID = 1, Duration = TimeSpan.FromMinutes(5) }
        };

        private class NamedRow
        {
            public int ID { get; set; }
            public string Name { get; set; }
        }

        private class AgeRow
        {
            public int ID { get; set; }
            public int? Age { get; set; }
        }

        // one member of each type ColumnDefinition supports
        private class TypedRow
        {
            public int ID { get; set; }
            public int? Number { get; set; }
            public long Big { get; set; }
            public short Small { get; set; }
            public byte Tiny { get; set; }
            public bool Flag { get; set; }
            public bool? MaybeFlag { get; set; }
            public decimal Amount { get; set; }
            public double Ratio { get; set; }
            public float Single { get; set; }
            public DateTime Created { get; set; }
            public DateTime? Modified { get; set; }
            public Guid Uid { get; set; }
            public string Name { get; set; }
            public byte[] Data { get; set; }
        }

        // members whose column sizes depend on ColumnDefinitionOptions
        private class SizedRow
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public byte[] Data { get; set; }
            public decimal Amount { get; set; }
            public DateTime Created { get; set; }
        }

        private class UnsupportedRow
        {
            public int ID { get; set; }
            public TimeSpan Duration { get; set; }
        }

        private class ColumnInfo
        {
            public string Name { get; set; }
            public string DataType { get; set; }
            public int? MaxLength { get; set; }
            public int? Precision { get; set; }
            public int? Scale { get; set; }
            public int? DateTimePrecision { get; set; }
            public string IsNullable { get; set; }
        }

        #endregion
    }
}
