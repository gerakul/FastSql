using System;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // How provider, context and per command options combine, observed on the commands a capturing context builds.
    // Concrete classes are SqlServer/SqlServerOptionsPrecedenceTests and PostgreSQL/PostgreSqlOptionsPrecedenceTests.
    public abstract class OptionsPrecedenceTests<TSpec> where TSpec : ProviderSpec, new()
    {
        protected TSpec Spec { get; } = new TSpec();

        protected abstract Type QueryOptionsType { get; }

        protected abstract Type BulkOptionsType { get; }

        protected abstract QueryOptions OtherProviderQueryOptions(int? commandTimeoutSeconds);

        public static TheoryData<string> ContextCreations => new TheoryData<string>()
        {
            "CreateContext(connectionString)",
            "CreateContext(connection)",
            "constructor"
        };

        public static TheoryData<string> CommandKinds => new TheoryData<string>()
        {
            "CreateSimple",
            "CreateMapped",
            "CreateProcedure",
            "CreateInsert"
        };

        // commands whose parameter "a" has to match the member "A"
        public static TheoryData<string> MatchingKinds => new TheoryData<string>()
        {
            "CreateMapped(text)",
            "CreateMapped(paramNames)",
            "CreateProcedure(paramNames)"
        };

        protected DbContext CreateContext(ContextProvider provider, string creation)
        {
            switch (creation)
            {
                case "CreateContext(connectionString)":
                    return provider.CreateContext("Server=unused;Database=unused");
                case "CreateContext(connection)":
                    return provider.CreateContext(Spec.CreateConnection());
                case "constructor":
                    return Spec.CreateCapturingContext(provider).Context;
                default:
                    throw new ArgumentOutOfRangeException(nameof(creation), creation, "Unknown context creation");
            }
        }

        private static void CreateCommand(ICommandCreator creator, string kind, QueryOptions queryOptions)
        {
            switch (kind)
            {
                case "CreateSimple":
                    creator.CreateSimple(queryOptions, "select 1");
                    break;
                case "CreateMapped":
                    creator.CreateMapped("select @a", new { a = 1 }, queryOptions);
                    break;
                case "CreateProcedure":
                    creator.CreateProcedure("TestProc", new { a = 1 }, queryOptions);
                    break;
                case "CreateInsert":
                    creator.CreateInsert("Employee", new { a = 1 }, queryOptions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown command kind");
            }
        }

        private static void CreateMatchingCommand(ICommandCreator creator, string kind, QueryOptions queryOptions)
        {
            switch (kind)
            {
                case "CreateMapped(text)":
                    creator.CreateMapped("select @a", new { A = 1 }, queryOptions);
                    break;
                case "CreateMapped(paramNames)":
                    creator.CreateMapped("select @a", new[] { "a" }, new { A = 1 }, queryOptions);
                    break;
                case "CreateProcedure(paramNames)":
                    creator.CreateProcedure("TestProc", new[] { "a" }, new { A = 1 }, queryOptions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown command kind");
            }
        }

        private int CommandTimeout(ContextProvider provider, string kind, QueryOptions queryOptions = null,
            int? contextTimeout = null)
        {
            var context = Spec.CreateCapturingContext(provider);
            if (contextTimeout.HasValue)
            {
                context.Context.QueryOptions.CommandTimeoutSeconds = contextTimeout;
            }

            CreateCommand(context, kind, queryOptions);
            return context.LastCommand.CommandTimeout;
        }

        private int DriverDefaultTimeout()
        {
            using (var connection = Spec.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                return command.CommandTimeout;
            }
        }

        private static void ShouldBeMapped(ICapturingContext context)
        {
            context.LastCommand.Parameters.Count.Should().Be(1);
            context.LastCommand.Parameters[0].Value.Should().Be(1);
        }

        private static void ShouldThrowUnmapped(Action act)
        {
            act.Should().Throw<ArgumentException>().WithMessage("Not all parameters has been mapped on settings (Parameter 'parameters')");
        }

        private ContextProvider ProviderFrom(bool defaultInstance)
        {
            return defaultInstance ? Spec.DefaultProvider : Spec.CreateProvider();
        }

        #region Provider defaults

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Provider_QueryOptions_AreProviderTypeWithValuesUnset(bool defaultInstance)
        {
            var options = ProviderFrom(defaultInstance).QueryOptions;

            options.Should().BeOfType(QueryOptionsType);
            options.CommandTimeoutSeconds.Should().BeNull();
            options.CaseSensitiveParamsMatching.Should().BeNull();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Provider_BulkOptions_AreProviderTypeWithBaseValuesUnset(bool defaultInstance)
        {
            var options = ProviderFrom(defaultInstance).BulkOptions;

            options.Should().BeOfType(BulkOptionsType);
            options.FieldsSelector.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeNull();
            options.CreateTable.Should().BeNull();
            options.IgnoreDataReaderSchemaTable.Should().BeNull();
            options.CheckTableIfNotExistsBeforeCreation.Should().BeNull();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Provider_ReadOptions_AreReadOptionsWithValuesUnset(bool defaultInstance)
        {
            var options = ProviderFrom(defaultInstance).ReadOptions;

            options.Should().BeOfType<ReadOptions>();
            options.FieldsSelector.Should().BeNull();
            options.CaseSensitiveFieldsMatching.Should().BeNull();
            options.FromTypeOption.Should().BeNull();
        }

        [Fact]
        public void Provider_Options_AreSameInstancesOnEveryAccess()
        {
            var provider = Spec.CreateProvider();

            provider.QueryOptions.Should().BeSameAs(provider.QueryOptions);
            provider.BulkOptions.Should().BeSameAs(provider.BulkOptions);
            provider.ReadOptions.Should().BeSameAs(provider.ReadOptions);
        }

        [Fact]
        public void NewProviders_HaveTheirOwnOptions()
        {
            var first = Spec.CreateProvider();
            var second = Spec.CreateProvider();

            first.QueryOptions.Should().NotBeSameAs(second.QueryOptions);
            first.BulkOptions.Should().NotBeSameAs(second.BulkOptions);
            first.ReadOptions.Should().NotBeSameAs(second.ReadOptions);
            first.QueryOptions.Should().NotBeSameAs(Spec.DefaultProvider.QueryOptions);
        }

        #endregion

        #region Context creation

        [Theory]
        [MemberData(nameof(ContextCreations))]
        public void CreateContext_ProviderOptions_AreCopiedIntoNewInstances(string creation)
        {
            var provider = Spec.CreateProvider();
            provider.QueryOptions.CommandTimeoutSeconds = 12;
            provider.QueryOptions.CaseSensitiveParamsMatching = true;
            provider.BulkOptions.FieldsSelector = FieldsSelector.Common;
            provider.BulkOptions.CaseSensitiveFieldsMatching = false;
            provider.ReadOptions.FieldsSelector = FieldsSelector.Both;
            provider.ReadOptions.FromTypeOption = FromTypeOption.PublicField;

            var context = CreateContext(provider, creation);

            context.ContextProvider.Should().BeSameAs(provider);
            context.QueryOptions.Should().BeOfType(QueryOptionsType).And.NotBeSameAs(provider.QueryOptions);
            context.QueryOptions.CommandTimeoutSeconds.Should().Be(12);
            context.QueryOptions.CaseSensitiveParamsMatching.Should().BeTrue();
            context.BulkOptions.Should().BeOfType(BulkOptionsType).And.NotBeSameAs(provider.BulkOptions);
            context.BulkOptions.FieldsSelector.Should().Be(FieldsSelector.Common);
            context.BulkOptions.CaseSensitiveFieldsMatching.Should().BeFalse();
            context.ReadOptions.Should().BeOfType<ReadOptions>().And.NotBeSameAs(provider.ReadOptions);
            context.ReadOptions.FieldsSelector.Should().Be(FieldsSelector.Both);
            context.ReadOptions.FromTypeOption.Should().Be(FromTypeOption.PublicField);
        }

        [Theory]
        [MemberData(nameof(ContextCreations))]
        public void CreateContext_LaterProviderChanges_DoNotReachContext(string creation)
        {
            var provider = Spec.CreateProvider();
            var context = CreateContext(provider, creation);

            provider.QueryOptions.CommandTimeoutSeconds = 12;
            provider.BulkOptions.CreateTable = true;
            provider.ReadOptions.CaseSensitiveFieldsMatching = true;

            context.QueryOptions.CommandTimeoutSeconds.Should().BeNull();
            context.BulkOptions.CreateTable.Should().BeNull();
            context.ReadOptions.CaseSensitiveFieldsMatching.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ContextCreations))]
        public void CreateContext_ContextChanges_DoNotReachProvider(string creation)
        {
            var provider = Spec.CreateProvider();
            var context = CreateContext(provider, creation);

            context.QueryOptions.CommandTimeoutSeconds = 12;
            context.BulkOptions.CreateTable = true;
            context.ReadOptions.CaseSensitiveFieldsMatching = true;

            provider.QueryOptions.CommandTimeoutSeconds.Should().BeNull();
            provider.BulkOptions.CreateTable.Should().BeNull();
            provider.ReadOptions.CaseSensitiveFieldsMatching.Should().BeNull();
        }

        [Fact]
        public void CreateContext_TwoContexts_DoNotShareOptions()
        {
            var provider = Spec.CreateProvider();

            var first = CreateContext(provider, "CreateContext(connectionString)");
            var second = CreateContext(provider, "CreateContext(connectionString)");
            first.QueryOptions.CommandTimeoutSeconds = 12;

            second.QueryOptions.Should().NotBeSameAs(first.QueryOptions);
            second.QueryOptions.CommandTimeoutSeconds.Should().BeNull();
        }

        #endregion

        #region Command timeout precedence

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CommandTimeout_NotSetAnywhere_KeepsDriverDefault(string kind)
        {
            CommandTimeout(Spec.CreateProvider(), kind).Should().Be(DriverDefaultTimeout());
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CommandTimeout_ProviderLevel_IsApplied(string kind)
        {
            var provider = Spec.CreateProvider();
            provider.QueryOptions.CommandTimeoutSeconds = 11;

            CommandTimeout(provider, kind).Should().Be(11);
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CommandTimeout_ContextLevel_OverridesProviderLevel(string kind)
        {
            var provider = Spec.CreateProvider();
            provider.QueryOptions.CommandTimeoutSeconds = 11;

            CommandTimeout(provider, kind, contextTimeout: 22).Should().Be(22);
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CommandTimeout_PerCommand_OverridesContextLevel(string kind)
        {
            var provider = Spec.CreateProvider();
            provider.QueryOptions.CommandTimeoutSeconds = 11;

            CommandTimeout(provider, kind, new QueryOptions(33), 22).Should().Be(33);
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CommandTimeout_PerCommandUnset_FallsBackToContextLevel(string kind)
        {
            CommandTimeout(Spec.CreateProvider(), kind, new QueryOptions(caseSensitiveParamsMatching: false), 22).Should().Be(22);
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CommandTimeout_PerCommandOnly_IsApplied(string kind)
        {
            CommandTimeout(Spec.CreateProvider(), kind, new QueryOptions(33)).Should().Be(33);
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CommandTimeout_ProviderChangedAfterContextCreation_IsIgnored(string kind)
        {
            var provider = Spec.CreateProvider();
            var context = Spec.CreateCapturingContext(provider);
            provider.QueryOptions.CommandTimeoutSeconds = 11;

            CreateCommand(context, kind, null);

            context.LastCommand.CommandTimeout.Should().Be(DriverDefaultTimeout());
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CommandTimeout_ProviderTypedPerCommandOptions_AreApplied(string kind)
        {
            var options = Spec.CreateProvider().QueryOptions.Clone();
            options.CommandTimeoutSeconds = 33;

            CommandTimeout(Spec.CreateProvider(), kind, options).Should().Be(33);
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CommandTimeout_OptionsOfOtherProvider_AreAccepted(string kind)
        {
            CommandTimeout(Spec.CreateProvider(), kind, OtherProviderQueryOptions(33)).Should().Be(33);
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CreateCommand_PerCommandOptions_AreNotChanged(string kind)
        {
            var provider = Spec.CreateProvider();
            provider.QueryOptions.CommandTimeoutSeconds = 11;
            provider.QueryOptions.CaseSensitiveParamsMatching = false;
            var options = new QueryOptions();

            CommandTimeout(provider, kind, options);

            options.CommandTimeoutSeconds.Should().BeNull();
            options.CaseSensitiveParamsMatching.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(CommandKinds))]
        public void CreateCommand_ContextOptions_AreNotFilledWithDefaults(string kind)
        {
            var context = Spec.CreateCapturingContext();

            CreateCommand(context, kind, null);

            context.Context.QueryOptions.CommandTimeoutSeconds.Should().BeNull();
            context.Context.QueryOptions.CaseSensitiveParamsMatching.Should().BeNull();
        }

        #endregion

        #region Case-sensitive parameter matching precedence

        [Theory]
        [MemberData(nameof(MatchingKinds))]
        public void CaseSensitiveParamsMatching_Default_IsCaseInsensitive(string kind)
        {
            var context = Spec.CreateCapturingContext();

            CreateMatchingCommand(context, kind, null);

            ShouldBeMapped(context);
        }

        [Theory]
        [MemberData(nameof(MatchingKinds))]
        public void CaseSensitiveParamsMatching_ProviderLevel_IsApplied(string kind)
        {
            var provider = Spec.CreateProvider();
            provider.QueryOptions.CaseSensitiveParamsMatching = true;
            var context = Spec.CreateCapturingContext(provider);

            ShouldThrowUnmapped(() => CreateMatchingCommand(context, kind, null));
        }

        [Theory]
        [MemberData(nameof(MatchingKinds))]
        public void CaseSensitiveParamsMatching_ContextLevel_IsApplied(string kind)
        {
            var context = Spec.CreateCapturingContext();
            context.Context.QueryOptions.CaseSensitiveParamsMatching = true;

            ShouldThrowUnmapped(() => CreateMatchingCommand(context, kind, null));
        }

        [Theory]
        [MemberData(nameof(MatchingKinds))]
        public void CaseSensitiveParamsMatching_ContextLevelFalse_OverridesProviderLevel(string kind)
        {
            var provider = Spec.CreateProvider();
            provider.QueryOptions.CaseSensitiveParamsMatching = true;
            var context = Spec.CreateCapturingContext(provider);
            context.Context.QueryOptions.CaseSensitiveParamsMatching = false;

            CreateMatchingCommand(context, kind, null);

            ShouldBeMapped(context);
        }

        [Theory]
        [MemberData(nameof(MatchingKinds))]
        public void CaseSensitiveParamsMatching_PerCommandTrue_OverridesDefault(string kind)
        {
            var context = Spec.CreateCapturingContext();

            ShouldThrowUnmapped(() => CreateMatchingCommand(context, kind, new QueryOptions(caseSensitiveParamsMatching: true)));
        }

        [Theory]
        [MemberData(nameof(MatchingKinds))]
        public void CaseSensitiveParamsMatching_PerCommandFalse_OverridesContextLevel(string kind)
        {
            var context = Spec.CreateCapturingContext();
            context.Context.QueryOptions.CaseSensitiveParamsMatching = true;

            CreateMatchingCommand(context, kind, new QueryOptions(caseSensitiveParamsMatching: false));

            ShouldBeMapped(context);
        }

        [Theory]
        [MemberData(nameof(MatchingKinds))]
        public void CaseSensitiveParamsMatching_PerCommandUnset_FallsBackToContextLevel(string kind)
        {
            var context = Spec.CreateCapturingContext();
            context.Context.QueryOptions.CaseSensitiveParamsMatching = true;

            ShouldThrowUnmapped(() => CreateMatchingCommand(context, kind, new QueryOptions(30)));
        }

        #endregion
    }
}
