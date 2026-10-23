using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    // U8, SQL Server only: CreateInsertAndGetID and ISqlCommandCreator
    public class SqlCommandCreatorTests
    {
        private class Person
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int? Age { get; set; }
        }

        // implements the interface without being a context
        private class CustomSqlCommandCreator : ISqlCommandCreator
        {
            public IWrappedCommand Set(Func<ScopedContext, DbCommand> commandGetter)
            {
                return null;
            }
        }

        private readonly SqlServerSpec spec = new SqlServerSpec();
        private readonly ICapturingContext context;
        private readonly Person person = new Person() { ID = 1, Name = "John", Age = 30 };

        public SqlCommandCreatorTests()
        {
            this.context = spec.CreateCapturingContext();
        }

        [Fact]
        public void CreateInsertAndGetID_IgnoreFields_InsertThenSelectScopeIdentity()
        {
            ((ISqlCommandCreator)context.Context).CreateInsertAndGetID("Person", person, "ID");

            var command = context.LastCommand;
            command.CommandText.Should().Be(new SqlCommandTextGenerator().InsertAndGetID("Person", "Name", "Age"));
            command.CommandText.Should().EndWith(" select scope_identity();");
            command.CommandType.Should().Be(CommandType.Text);
            command.CommandTimeout.Should().Be(new SqlCommand().CommandTimeout);
            ParametersOf(command).Should().Equal(("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateInsertAndGetID_QueryOptionsAndIgnoreFields_AppliesQueryOptions()
        {
            ((ISqlCommandCreator)context.Context).CreateInsertAndGetID("Person", person, new QueryOptions(123), "ID");

            var command = context.LastCommand;
            command.CommandText.Should().Be(new SqlCommandTextGenerator().InsertAndGetID("Person", "Name", "Age"));
            command.CommandTimeout.Should().Be(123);
            ParametersOf(command).Should().Equal(("Name", "John"), ("Age", 30));
        }

        [Fact]
        public void CreateInsertAndGetID_NoIgnoreFields_InsertsAllMembers()
        {
            ((ISqlCommandCreator)context.Context).CreateInsertAndGetID("Person", person);

            context.LastCommand.CommandText.Should().Be(new SqlCommandTextGenerator().InsertAndGetID("Person", "ID", "Name", "Age"));
            ParametersOf(context.LastCommand).Should().Equal(("ID", 1), ("Name", "John"), ("Age", 30));
        }

        [Fact]
        [Trait("Quirk", "Q18")]
        public void CreateContext_ReturnsBaseTypes_ContextsImplementISqlCommandCreatorAtRuntime()
        {
            var provider = new SqlContextProvider();

            var connectionStringContext = provider.CreateContext("unused");
            var connectionContext = provider.CreateContext(new SqlConnection());
            var transactionContext = provider.CreateContext((DbTransaction)null);

            // the static types do not offer CreateInsertAndGetID, so callers need a cast
            StaticTypeOf(connectionStringContext).Should().Be(typeof(ConnectionStringContext));
            StaticTypeOf(connectionContext).Should().Be(typeof(ConnectionContext));
            StaticTypeOf(transactionContext).Should().Be(typeof(TransactionContext));
            typeof(ISqlCommandCreator).IsAssignableFrom(typeof(ConnectionStringContext)).Should().BeFalse();
            typeof(ISqlCommandCreator).IsAssignableFrom(typeof(ConnectionContext)).Should().BeFalse();
            typeof(ISqlCommandCreator).IsAssignableFrom(typeof(TransactionContext)).Should().BeFalse();

            connectionStringContext.Should().BeAssignableTo<ISqlCommandCreator>();
            connectionContext.Should().BeAssignableTo<ISqlCommandCreator>();
            transactionContext.Should().BeAssignableTo<ISqlCommandCreator>();
            ((ISqlCommandCreator)connectionStringContext).CreateInsertAndGetID("Person", person, "ID").Should().NotBeNull();
        }

        [Fact]
        public void ContextClasses_ImplementISqlCommandCreator()
        {
            var expected = new[] { typeof(ICommandCreator), typeof(ISqlCommandCreator) };

            typeof(SqlConnectionStringContext).GetInterfaces().Should().BeEquivalentTo(expected);
            typeof(SqlConnectionContext).GetInterfaces().Should().BeEquivalentTo(expected);
            typeof(SqlTransactionContext).GetInterfaces().Should().BeEquivalentTo(expected);
            typeof(ISqlCommandCreator).GetInterfaces().Should().Equal(typeof(ICommandCreator));
            typeof(ISqlCommandCreator).GetMembers().Should().BeEmpty();
        }

        [Fact]
        [Trait("Quirk", "Q61")]
        public void CreateInsertAndGetID_CreatorIsNotDbContext_ThrowsInvalidCastException()
        {
            var creator = new CustomSqlCommandCreator();

            Action act = () => creator.CreateInsertAndGetID("Person", person, "ID");

            act.Should().Throw<InvalidCastException>()
                .WithMessage("Unable to cast object of type 'CustomSqlCommandCreator' to type 'Gerakul.FastSql.Common.DbContext'.");

            // the common extensions only call Set, so they accept the same creator
            creator.CreateInsert("Person", person, "ID").Should().BeNull();
        }

        private static Type StaticTypeOf<T>(T value)
        {
            return typeof(T);
        }

        private static List<(string Name, object Value)> ParametersOf(DbCommand command)
        {
            return command.Parameters.Cast<DbParameter>().Select(x => (x.ParameterName, x.Value)).ToList();
        }
    }
}
