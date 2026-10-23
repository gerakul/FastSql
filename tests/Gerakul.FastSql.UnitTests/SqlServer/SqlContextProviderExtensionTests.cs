using System;
using System.Data.Common;
using System.Data.SqlClient;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    // Protected extension points of ContextProvider that the built-in providers never use: ApplyQueryOptions with null
    // options, and GetTyped asking for a clone (both built-in providers always copy the options and answer needClone = false).
    // The code that reacts to them is in Common; SQL Server is just the provider subclassed here.
    public class SqlContextProviderExtensionTests
    {
        private readonly SqlServerSpec spec = new SqlServerSpec();

        private class ExtendedProvider : SqlContextProvider
        {
            private readonly bool cloneTypedOptions;

            public ExtendedProvider(bool cloneTypedOptions)
            {
                this.cloneTypedOptions = cloneTypedOptions;
            }

            public void CallApplyQueryOptions(DbCommand cmd, QueryOptions queryOptions)
            {
                ApplyQueryOptions(cmd, queryOptions);
            }

            // options that already have the provider type are passed on as they are, with the clone flag of the test
            protected override QueryOptions GetTyped(QueryOptions options, out bool needClone)
            {
                if (options is SqlQueryOptions)
                {
                    needClone = cloneTypedOptions;
                    return options;
                }

                return base.GetTyped(options, out needClone);
            }

            protected override BulkOptions GetTyped(BulkOptions options, out bool needClone)
            {
                if (options is SqlBulkOptions)
                {
                    needClone = cloneTypedOptions;
                    return options;
                }

                return base.GetTyped(options, out needClone);
            }
        }

        private class Row
        {
            public int ID { get; set; }
        }

        [Fact]
        public void ApplyQueryOptions_NullOptions_LeavesCommandUnchanged()
        {
            var provider = new ExtendedProvider(false);

            using (var command = new SqlCommand())
            {
                command.CommandTimeout = 17;

                provider.CallApplyQueryOptions(command, null);

                command.CommandTimeout.Should().Be(17);
            }
        }

        [Fact]
        public void ApplyQueryOptions_TimeoutSet_SetsCommandTimeout()
        {
            var provider = new ExtendedProvider(false);

            using (var command = new SqlCommand())
            {
                provider.CallApplyQueryOptions(command, new QueryOptions(5));

                command.CommandTimeout.Should().Be(5);
            }
        }

        // with needClone the context fills a clone with the defaults, without it the instance it got back
        [Theory]
        [InlineData(true, null)]
        [InlineData(false, false)]
        public void CreateSimple_GetTypedReturnsCallerOptions_NeedCloneDecidesWhetherCallerOptionsAreFilled(bool needClone, bool? expectedCaseSensitivity)
        {
            var context = spec.CreateCapturingContext(new ExtendedProvider(needClone));
            var options = new SqlQueryOptions(5);

            context.CreateSimple(options, "select 1");

            context.LastCommand.CommandTimeout.Should().Be(5);
            options.CaseSensitiveParamsMatching.Should().Be(expectedCaseSensitivity);
        }

        // the options are prepared before the copy starts, so the unopened connection of the capturing context is enough
        [Theory]
        [InlineData(true, null)]
        [InlineData(false, FieldsSelector.Source)]
        public void WriteToServer_GetTypedReturnsCallerOptions_NeedCloneDecidesWhetherCallerOptionsAreFilled(bool needClone, FieldsSelector? expectedSelector)
        {
            var context = spec.CreateCapturingContext(new ExtendedProvider(needClone));
            var options = new SqlBulkOptions(batchSize: 10);
            var reader = new[] { new Row() { ID = 1 } }.ToDataReader();

            Action act = () => reader.WriteToServer(context.Context, "Target", options);

            act.Should().Throw<InvalidOperationException>();
            options.BatchSize.Should().Be(10);
            options.FieldsSelector.Should().Be(expectedSelector);
        }
    }
}
