using System;
using System.Data;
using System.Data.Common;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // U9. Providers and contexts without a database: unopened connections, no transactions.
    // Concrete classes are in the provider folders and give the provider-specific types.
    public abstract class ContextProviderTests<TSpec> where TSpec : ProviderSpec, new()
    {
        // a transaction of no provider
        private class FakeTransaction : DbTransaction
        {
            public override IsolationLevel IsolationLevel => IsolationLevel.Unspecified;

            protected override DbConnection DbConnection => null;

            public override void Commit()
            {
            }

            public override void Rollback()
            {
            }
        }

        protected TSpec Spec { get; } = new TSpec();

        // the other provider, for connections that do not fit this one
        protected abstract ProviderSpec OtherSpec { get; }

        protected abstract Type CommandTextGeneratorType { get; }

        protected abstract Type QueryOptionsType { get; }

        protected abstract Type BulkOptionsType { get; }

        protected abstract Type ConnectionStringContextType { get; }

        protected abstract Type ConnectionContextType { get; }

        protected abstract Type TransactionContextType { get; }

        // the driver transaction type the provider casts to
        protected abstract Type TransactionType { get; }

        // the public constructors of the provider context classes
        protected abstract ConnectionStringContext NewConnectionStringContext(ContextProvider provider, string connectionString);

        protected abstract ConnectionContext NewConnectionContext(ContextProvider provider, DbConnection connection);

        // without a transaction: building a context does not need one
        protected abstract TransactionContext NewTransactionContext(ContextProvider provider);

        #region Instances

        [Fact]
        public void DefaultInstance_RepeatedAccess_ReturnsSameInstance()
        {
            Spec.DefaultProvider.Should().BeSameAs(Spec.DefaultProvider);
            Spec.DefaultProvider.Should().BeOfType(Spec.CreateProvider().GetType());
        }

        [Fact]
        public void Constructor_NewInstances_AreIndependent()
        {
            var first = Spec.CreateProvider();
            var second = Spec.CreateProvider();
            var defaultTimeout = Spec.DefaultProvider.QueryOptions.CommandTimeoutSeconds;

            first.Should().NotBeSameAs(second).And.NotBeSameAs(Spec.DefaultProvider);
            first.CommandTextGenerator.Should().NotBeSameAs(second.CommandTextGenerator).And.NotBeSameAs(Spec.DefaultProvider.CommandTextGenerator);
            first.QueryOptions.Should().NotBeSameAs(second.QueryOptions).And.NotBeSameAs(Spec.DefaultProvider.QueryOptions);
            first.BulkOptions.Should().NotBeSameAs(second.BulkOptions).And.NotBeSameAs(Spec.DefaultProvider.BulkOptions);
            first.ReadOptions.Should().NotBeSameAs(second.ReadOptions).And.NotBeSameAs(Spec.DefaultProvider.ReadOptions);
            first.CommandCompilator.Should().NotBeSameAs(second.CommandCompilator).And.NotBeSameAs(Spec.DefaultProvider.CommandCompilator);

            first.QueryOptions.CommandTimeoutSeconds = 77;

            second.QueryOptions.CommandTimeoutSeconds.Should().BeNull();
            Spec.DefaultProvider.QueryOptions.CommandTimeoutSeconds.Should().Be(defaultTimeout);
        }

        [Fact]
        public void Members_NewInstance_HaveProviderTypes()
        {
            AssertMemberTypes(Spec.CreateProvider());
        }

        [Fact]
        public void Members_DefaultInstance_HaveProviderTypes()
        {
            AssertMemberTypes(Spec.DefaultProvider);
        }

        [Fact]
        public void Members_RepeatedAccess_ReturnSameInstances()
        {
            var provider = Spec.CreateProvider();

            provider.CommandTextGenerator.Should().BeSameAs(provider.CommandTextGenerator);
            provider.QueryOptions.Should().BeSameAs(provider.QueryOptions);
            provider.BulkOptions.Should().BeSameAs(provider.BulkOptions);
            provider.ReadOptions.Should().BeSameAs(provider.ReadOptions);
            provider.CommandCompilator.Should().BeSameAs(provider.CommandCompilator);
        }

        #endregion

        #region CreateContext

        [Fact]
        public void CreateContext_ConnectionString_ReturnsConnectionStringContext()
        {
            var provider = CreateProviderWithChangedOptions();

            var context = provider.CreateContext("Server=unused");

            context.Should().BeOfType(ConnectionStringContextType);
            context.ConnectionString.Should().Be("Server=unused");
            AssertTakesOptionsOf(provider, context);
        }

        [Fact]
        public void CreateContext_UnopenedConnection_ReturnsConnectionContextAndLeavesConnectionClosed()
        {
            var provider = CreateProviderWithChangedOptions();
            var connection = Spec.CreateConnection();

            var context = provider.CreateContext(connection);

            context.Should().BeOfType(ConnectionContextType);
            context.Connection.Should().BeSameAs(connection);
            connection.State.Should().Be(ConnectionState.Closed);
            AssertTakesOptionsOf(provider, context);
        }

        [Fact]
        public void CreateContext_ConnectionOfOtherProvider_ThrowsInvalidCastException()
        {
            var connection = OtherSpec.CreateConnection();
            var expectedType = Spec.CreateConnection().GetType();

            Action act = () => Spec.CreateProvider().CreateContext(connection);

            act.Should().Throw<InvalidCastException>()
                .WithMessage($"Unable to cast object of type '{connection.GetType().FullName}' to type '{expectedType.FullName}'.");
        }

        [Fact]
        public void CreateContext_NullTransaction_ReturnsTransactionContextWithoutTransaction()
        {
            var provider = CreateProviderWithChangedOptions();

            var context = provider.CreateContext((DbTransaction)null);

            context.Should().BeOfType(TransactionContextType);
            context.Transaction.Should().BeNull();
            AssertTakesOptionsOf(provider, context);
        }

        [Fact]
        public void CreateContext_TransactionOfOtherType_ThrowsInvalidCastException()
        {
            Action act = () => Spec.CreateProvider().CreateContext(new FakeTransaction());

            act.Should().Throw<InvalidCastException>()
                .WithMessage($"Unable to cast object of type 'FakeTransaction[{typeof(TSpec).FullName}]' to type '{TransactionType.FullName}'.");
        }

        #endregion

        #region Context constructors

        [Fact]
        public void ConnectionStringContextConstructor_Provider_TakesProviderOptions()
        {
            var provider = CreateProviderWithChangedOptions();

            var context = NewConnectionStringContext(provider, "Server=unused");

            context.Should().BeOfType(ConnectionStringContextType);
            context.ConnectionString.Should().Be("Server=unused");
            AssertTakesOptionsOf(provider, context);
        }

        [Fact]
        public void ConnectionContextConstructor_Provider_TakesProviderOptions()
        {
            var provider = CreateProviderWithChangedOptions();
            var connection = Spec.CreateConnection();

            var context = NewConnectionContext(provider, connection);

            context.Should().BeOfType(ConnectionContextType);
            context.Connection.Should().BeSameAs(connection);
            connection.State.Should().Be(ConnectionState.Closed);
            AssertTakesOptionsOf(provider, context);
        }

        [Fact]
        public void TransactionContextConstructor_Provider_TakesProviderOptions()
        {
            var provider = CreateProviderWithChangedOptions();

            var context = NewTransactionContext(provider);

            context.Should().BeOfType(TransactionContextType);
            context.Transaction.Should().BeNull();
            AssertTakesOptionsOf(provider, context);
        }

        #endregion

        #region DbContext members

        [Fact]
        public void ContextProviderAndCommandCompilator_EachContextKind_AreThoseOfProvider()
        {
            var provider = Spec.CreateProvider();
            var contexts = new DbContext[]
            {
                provider.CreateContext("Server=unused"),
                provider.CreateContext(Spec.CreateConnection()),
                provider.CreateContext((DbTransaction)null),
                NewConnectionStringContext(provider, "Server=unused"),
                NewConnectionContext(provider, Spec.CreateConnection()),
                NewTransactionContext(provider)
            };

            foreach (var context in contexts)
            {
                context.ContextProvider.Should().BeSameAs(provider);
                context.CommandCompilator.Should().BeSameAs(provider.CommandCompilator);
            }
        }

        [Fact]
        public void ContextProviderAndCommandCompilator_ContextOfDefaultInstance_AreThoseOfDefaultInstance()
        {
            var context = Spec.DefaultProvider.CreateContext("Server=unused");

            context.ContextProvider.Should().BeSameAs(Spec.DefaultProvider);
            context.CommandCompilator.Should().BeSameAs(Spec.DefaultProvider.CommandCompilator);
        }

        #endregion

        private ContextProvider CreateProviderWithChangedOptions()
        {
            var provider = Spec.CreateProvider();
            provider.QueryOptions.CommandTimeoutSeconds = 41;
            provider.BulkOptions.CaseSensitiveFieldsMatching = true;
            provider.ReadOptions.FieldsSelector = FieldsSelector.Common;
            return provider;
        }

        private void AssertTakesOptionsOf(ContextProvider provider, DbContext context)
        {
            context.ContextProvider.Should().BeSameAs(provider);
            context.QueryOptions.Should().BeOfType(QueryOptionsType);
            context.QueryOptions.CommandTimeoutSeconds.Should().Be(41);
            context.BulkOptions.Should().BeOfType(BulkOptionsType);
            context.BulkOptions.CaseSensitiveFieldsMatching.Should().BeTrue();
            context.ReadOptions.Should().BeOfType<ReadOptions>();
            context.ReadOptions.FieldsSelector.Should().Be(FieldsSelector.Common);

            // copies, not the provider's instances (cloning in detail is covered with the options)
            context.QueryOptions.Should().NotBeSameAs(provider.QueryOptions);
            context.BulkOptions.Should().NotBeSameAs(provider.BulkOptions);
            context.ReadOptions.Should().NotBeSameAs(provider.ReadOptions);
        }

        private void AssertMemberTypes(ContextProvider provider)
        {
            provider.CommandTextGenerator.Should().BeOfType(CommandTextGeneratorType);
            provider.QueryOptions.Should().BeOfType(QueryOptionsType);
            provider.BulkOptions.Should().BeOfType(BulkOptionsType);
            provider.ReadOptions.Should().BeOfType<ReadOptions>();
            provider.CommandCompilator.Should().BeOfType<CommandCompilator>();
        }
    }
}
