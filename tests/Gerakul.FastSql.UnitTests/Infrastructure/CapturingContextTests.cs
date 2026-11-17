using System.Data;
using System.Data.Common;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Infrastructure
{
    // checks the test infrastructure itself; concrete classes are in the provider folders
    public abstract class CapturingContextTests<TSpec> where TSpec : ProviderSpec, new()
    {
        protected TSpec Spec { get; } = new TSpec();

        [Fact]
        public void CreateSimple_CommandIsCapturedWithoutExecution()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateSimple("select @p0", 5);

            context.Commands.Should().ContainSingle();
            var command = context.LastCommand;
            command.CommandText.Should().Be("select @p0");
            command.Connection.State.Should().Be(ConnectionState.Closed);
            command.Parameters.Cast<DbParameter>().Select(x => x.Value).Should().Equal(5);
        }

        [Fact]
        public void CreateMapped_ParametersAreCaptured()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select * from Employee where CompanyID = @CompanyID and Age > @Age", new { CompanyID = 1, Age = 40 });

            context.LastCommand.Parameters.Cast<DbParameter>().Select(x => x.Value).Should().Equal(1, 40);
        }

        [Fact]
        public void CreateCapturingContext_WithoutProvider_UsesNewProviderInstance()
        {
            var context = Spec.CreateCapturingContext();

            context.Context.ContextProvider.Should().NotBeSameAs(Spec.DefaultProvider);
        }

        [Fact]
        public void CreateCapturingContext_WithProvider_UsesGivenProvider()
        {
            var provider = Spec.CreateProvider();

            var context = Spec.CreateCapturingContext(provider);

            context.Context.ContextProvider.Should().BeSameAs(provider);
        }

        [Fact]
        public void CreateParameter_CreatesProviderParameter()
        {
            DbParameter expectedType;
            using (var connection = Spec.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                expectedType = command.CreateParameter();
            }

            var parameter = Spec.CreateParameter("Result", 7, ParameterDirection.Output);

            parameter.Should().BeOfType(expectedType.GetType());
            parameter.ParameterName.Should().Be("Result");
            parameter.Value.Should().Be(7);
            parameter.Direction.Should().Be(ParameterDirection.Output);
        }

        [Fact]
        public void CreateParameter_NullValue_BecomesDBNull()
        {
            var parameter = Spec.CreateParameter("Value", null);

            parameter.Value.Should().Be(System.DBNull.Value);
        }
    }
}
