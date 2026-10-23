using System.Collections.Generic;
using System.Data.SqlClient;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.UnitTests.Common;
using Xunit;
using SqlColumnDefinitionOptions = Gerakul.FastSql.SqlServer.ColumnDefinitionOptions;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    public class SqlBulkOptionsTests : BulkOptionsContractTests<SqlBulkOptions, SqlBulkOptionsTests.Probe>
    {
        public class Probe : SqlBulkOptions, IBulkOptionsProbe
        {
            public Probe(bool defaultOptions)
                : base(defaultOptions)
            {
            }

            public Probe(BulkOptions source)
                : base(source)
            {
            }

            public BulkOptions DefaultOptions => Default;

            public BulkOptions CallSetDefaults(BulkOptions defaultOptions)
            {
                return SetDefaults(defaultOptions);
            }
        }

        protected override SqlBulkOptions Create()
        {
            return new SqlBulkOptions();
        }

        protected override Probe CreateProbe(bool defaultOptions)
        {
            return new Probe(defaultOptions);
        }

        protected override Probe CreateProbe(BulkOptions source)
        {
            return new Probe(source);
        }

        private static SqlColumnDefinitionOptions NewColumnDefinitionOptions()
        {
            return new SqlColumnDefinitionOptions()
            {
                PrimaryKey = new List<string>() { "ID" },
                StringMaxLength = 100,
                BytesMaxLength = 200,
                Precision = 10,
                Scale = 2,
                DateTimeScale = 3,
                ThrowIfUnsupportedType = true
            };
        }

        private static SqlBulkOptions SqlServerValues(SqlColumnDefinitionOptions columnDefinitionOptions)
        {
            return new SqlBulkOptions(500, 60, SqlBulkCopyOptions.KeepNulls, enableStreaming: true, columnDefinitionOptions: columnDefinitionOptions);
        }

        private static void ShouldHaveSqlServerValuesUnset(SqlBulkOptions options)
        {
            options.BatchSize.Should().BeNull();
            options.BulkCopyTimeout.Should().BeNull();
            options.SqlBulkCopyOptions.Should().BeNull();
            options.EnableStreaming.Should().BeNull();
            options.ColumnDefinitionOptions.Should().BeNull();
        }

        [Fact]
        public void Constructor_AllArguments_AreStored()
        {
            var columnDefinitionOptions = NewColumnDefinitionOptions();

            var options = new SqlBulkOptions(500, 60, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.TableLock, FieldsSelector.Common,
                true, false, true, columnDefinitionOptions, false, true);

            options.BatchSize.Should().Be(500);
            options.BulkCopyTimeout.Should().Be(60);
            options.SqlBulkCopyOptions.Should().Be(SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.TableLock);
            options.FieldsSelector.Should().Be(FieldsSelector.Common);
            options.CaseSensitiveFieldsMatching.Should().BeTrue();
            options.EnableStreaming.Should().BeFalse();
            options.CreateTable.Should().BeTrue();
            options.ColumnDefinitionOptions.Should().BeSameAs(columnDefinitionOptions);
            options.IgnoreDataReaderSchemaTable.Should().BeFalse();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeTrue();
        }

        [Fact]
        public void Constructor_NoArguments_LeavesSqlServerValuesUnset()
        {
            ShouldHaveSqlServerValuesUnset(new SqlBulkOptions());
        }

        [Fact]
        public void Clone_CopiesSqlServerValues()
        {
            var options = SqlServerValues(NewColumnDefinitionOptions());

            var clone = (SqlBulkOptions)options.Clone();

            clone.BatchSize.Should().Be(500);
            clone.BulkCopyTimeout.Should().Be(60);
            clone.SqlBulkCopyOptions.Should().Be(SqlBulkCopyOptions.KeepNulls);
            clone.EnableStreaming.Should().BeTrue();
        }

        [Fact]
        public void Clone_SharesColumnDefinitionOptionsInstance()
        {
            var options = SqlServerValues(NewColumnDefinitionOptions());

            var clone = (SqlBulkOptions)options.Clone();

            clone.ColumnDefinitionOptions.Should().BeSameAs(options.ColumnDefinitionOptions);
        }

        [Fact]
        public void Default_HasSqlServerDefaultValues()
        {
            var defaultOptions = (SqlBulkOptions)new Probe(false).DefaultOptions;

            defaultOptions.BatchSize.Should().BeNull();
            defaultOptions.BulkCopyTimeout.Should().BeNull();
            defaultOptions.SqlBulkCopyOptions.Should().Be(SqlBulkCopyOptions.Default);
            defaultOptions.EnableStreaming.Should().BeNull();
            defaultOptions.ColumnDefinitionOptions.Should().BeSameAs(SqlColumnDefinitionOptions.Default);
        }

        [Fact]
        public void ProtectedConstructor_DefaultOptionsTrue_SetsSqlServerDefaultValues()
        {
            var options = new Probe(true);

            options.BatchSize.Should().BeNull();
            options.BulkCopyTimeout.Should().BeNull();
            options.SqlBulkCopyOptions.Should().Be(SqlBulkCopyOptions.Default);
            options.EnableStreaming.Should().BeNull();
            options.ColumnDefinitionOptions.Should().BeSameAs(SqlColumnDefinitionOptions.Default);
        }

        [Fact]
        public void ProtectedConstructor_DefaultOptionsFalse_LeavesSqlServerValuesUnset()
        {
            ShouldHaveSqlServerValuesUnset(new Probe(false));
        }

        [Fact]
        public void CopyConstructor_SqlBulkOptions_CopiesSqlServerValuesAndSharesColumnDefinitionOptions()
        {
            var source = SqlServerValues(NewColumnDefinitionOptions());

            var options = new Probe(source);

            options.BatchSize.Should().Be(500);
            options.BulkCopyTimeout.Should().Be(60);
            options.SqlBulkCopyOptions.Should().Be(SqlBulkCopyOptions.KeepNulls);
            options.EnableStreaming.Should().BeTrue();
            options.ColumnDefinitionOptions.Should().BeSameAs(source.ColumnDefinitionOptions);
        }

        [Fact]
        public void CopyConstructor_BaseOptions_LeavesSqlServerValuesUnset()
        {
            ShouldHaveSqlServerValuesUnset(new Probe(BaseValues()));
        }

        [Fact]
        public void SetDefaults_SqlBulkOptions_FillsOnlyUnsetSqlServerValues()
        {
            var ownColumnDefinitionOptions = NewColumnDefinitionOptions();
            var options = new Probe(new SqlBulkOptions(batchSize: 100, columnDefinitionOptions: ownColumnDefinitionOptions));

            var result = options.CallSetDefaults(SqlServerValues(NewColumnDefinitionOptions()));

            result.Should().BeSameAs(options);
            options.BatchSize.Should().Be(100);
            options.BulkCopyTimeout.Should().Be(60);
            options.SqlBulkCopyOptions.Should().Be(SqlBulkCopyOptions.KeepNulls);
            options.EnableStreaming.Should().BeTrue();
            options.ColumnDefinitionOptions.Should().BeSameAs(ownColumnDefinitionOptions);
        }

        [Fact]
        public void SetDefaults_SqlBulkOptions_ClonesColumnDefinitionOptions()
        {
            var defaults = SqlServerValues(NewColumnDefinitionOptions());
            var options = new Probe(new SqlBulkOptions());

            options.CallSetDefaults(defaults);

            options.ColumnDefinitionOptions.Should().NotBeSameAs(defaults.ColumnDefinitionOptions);
            options.ColumnDefinitionOptions.Should().BeEquivalentTo(defaults.ColumnDefinitionOptions);
            options.ColumnDefinitionOptions.PrimaryKey.Should().NotBeSameAs(defaults.ColumnDefinitionOptions.PrimaryKey);
        }

        [Fact]
        public void SetDefaults_SqlBulkOptionsWithoutColumnDefinitionOptions_LeavesThemUnset()
        {
            var options = new Probe(new SqlBulkOptions());

            options.CallSetDefaults(new SqlBulkOptions(batchSize: 10));

            options.BatchSize.Should().Be(10);
            options.ColumnDefinitionOptions.Should().BeNull();
        }

        [Fact]
        public void SetDefaults_BaseOptions_FillsOnlyBaseValues()
        {
            var options = new Probe(new SqlBulkOptions());

            options.CallSetDefaults(BaseValues());

            options.FieldsSelector.Should().Be(FieldsSelector.Common);
            options.CreateTable.Should().BeTrue();
            ShouldHaveSqlServerValuesUnset(options);
        }

        [Fact]
        public void SetDefaults_FromOwnDefault_UsesCloneOfStaticColumnDefinitionOptions()
        {
            var options = new Probe(new SqlBulkOptions());

            options.CallSetDefaults(options.DefaultOptions);

            options.SqlBulkCopyOptions.Should().Be(SqlBulkCopyOptions.Default);
            options.ColumnDefinitionOptions.Should().NotBeSameAs(SqlColumnDefinitionOptions.Default);
            options.ColumnDefinitionOptions.Should().BeEquivalentTo(SqlColumnDefinitionOptions.Default);
        }
    }
}
