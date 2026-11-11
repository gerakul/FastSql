using System;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;
using Xunit;
using SqlColumnDefinitionOptions = Gerakul.FastSql.SqlServer.ColumnDefinitionOptions;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    public class SqlServerOptionsPrecedenceTests : OptionsPrecedenceTests<SqlServerSpec>
    {
        protected override Type QueryOptionsType => typeof(SqlQueryOptions);

        protected override Type BulkOptionsType => typeof(SqlBulkOptions);

        protected override QueryOptions OtherProviderQueryOptions(int? commandTimeoutSeconds)
        {
            return new NpgsqlQueryOptions(commandTimeoutSeconds);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Provider_BulkOptions_HaveSqlServerValuesUnset(bool defaultInstance)
        {
            var provider = defaultInstance ? Spec.DefaultProvider : Spec.CreateProvider();

            var options = (SqlBulkOptions)provider.BulkOptions;

            options.BatchSize.Should().BeNull();
            options.BulkCopyTimeout.Should().BeNull();
            options.SqlBulkCopyOptions.Should().BeNull();
            options.EnableStreaming.Should().BeNull();
            options.ColumnDefinitionOptions.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ContextCreations))]
        public void CreateContext_SqlServerBulkValues_AreCopiedAndColumnDefinitionOptionsShared(string creation)
        {
            var provider = Spec.CreateProvider();
            var providerOptions = (SqlBulkOptions)provider.BulkOptions;
            providerOptions.BatchSize = 500;
            providerOptions.ColumnDefinitionOptions = SqlColumnDefinitionOptions.FromSmall("ID");

            var contextOptions = (SqlBulkOptions)CreateContext(provider, creation).BulkOptions;

            contextOptions.BatchSize.Should().Be(500);
            contextOptions.ColumnDefinitionOptions.Should().BeSameAs(providerOptions.ColumnDefinitionOptions);
        }
    }
}
