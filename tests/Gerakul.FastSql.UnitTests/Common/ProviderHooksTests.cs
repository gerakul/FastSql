using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // The protected provider hooks (parameter discovery and naming, null values, AddWithValue, query options),
    // observed through the commands a capturing context builds. Concrete classes are SqlServer/SqlContextProviderTests
    // and PostgreSQL/NpgsqlContextProviderTests.
    public abstract class ProviderHooksTests<TSpec> where TSpec : ProviderSpec, new()
    {
        protected TSpec Spec { get; } = new TSpec();

        protected enum SampleEnum
        {
            First = 1,
            Second = 2
        }

        protected class NullableMembers
        {
            public string Name { get; set; }
            public int? Age { get; set; }
            public DateTime? StartWorking { get; set; }
            public Guid? Key { get; set; }
            public SampleEnum? Kind { get; set; }
            public object Tag { get; set; }
        }

        protected class ProcedureArguments
        {
            public int CompanyID;

            public int Age { get; set; }
        }

        private static readonly Dictionary<string, object> sampleValues = new Dictionary<string, object>()
        {
            ["string"] = "text",
            ["int"] = 42,
            ["long"] = 42L,
            ["short"] = (short)42,
            ["byte"] = (byte)42,
            ["decimal"] = 42.5m,
            ["double"] = 42.5d,
            ["float"] = 42.5f,
            ["bool"] = true,
            ["DateTime"] = new DateTime(2020, 1, 2, 3, 4, 5),
            ["DateTimeOffset"] = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)),
            ["TimeSpan"] = TimeSpan.FromMinutes(90),
            ["Guid"] = new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
            ["byte[]"] = new byte[] { 1, 2, 3 },
            ["enum"] = SampleEnum.Second
        };

        protected static object SampleValue(string kind)
        {
            return sampleValues[kind];
        }

        protected static string[] Names(DbCommand command)
        {
            return command.Parameters.Cast<DbParameter>().Select(x => x.ParameterName).ToArray();
        }

        protected static object[] Values(DbCommand command)
        {
            return command.Parameters.Cast<DbParameter>().Select(x => x.Value).ToArray();
        }

        protected string[] DiscoveredNames<T>(string commandText, T value, QueryOptions queryOptions = null)
        {
            var context = Spec.CreateCapturingContext();
            context.CreateMapped(commandText, value, queryOptions);
            return Names(context.LastCommand);
        }

        // a single parameter added through the provider's AddWithValue
        protected DbParameter ParameterWithValue(object value)
        {
            var context = Spec.CreateCapturingContext();
            context.CreateMapped("select @Value", new[] { new FieldSettings<object>("Value") }, value);
            return context.LastCommand.Parameters.Cast<DbParameter>().Single();
        }

        private int DriverDefaultTimeout()
        {
            using (var connection = Spec.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                return command.CommandTimeout;
            }
        }

        #region Parameter discovery from command text

        [Fact]
        public void CreateMapped_ParameterInText_IsDiscoveredWithAtPrefix()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select * from Employee where ID = @ID", new { ID = 5 });

            Names(context.LastCommand).Should().Equal("@ID");
            Values(context.LastCommand).Should().Equal(5);
        }

        [Fact]
        public void CreateMapped_RepeatedParameter_IsAddedOnce()
        {
            DiscoveredNames("select @a where @a > 0 or @a < 10", new { a = 1 }).Should().Equal("@a");
        }

        [Fact]
        public void CreateMapped_DoubleAtVariables_AreIgnored()
        {
            DiscoveredNames("update T set X = @a; select @@ROWCOUNT, @@IDENTITY", new { a = 1 }).Should().Equal("@a");
        }

        [Fact]
        public void CreateMapped_DoubleAtVariableAtStartOfText_IsIgnored()
        {
            DiscoveredNames("@@ROWCOUNT + @a", new { a = 1 }).Should().Equal("@a");
        }

        [Fact]
        public void CreateMapped_ParameterAtStartOfText_IsDiscovered()
        {
            DiscoveredNames("@a + @b", new { a = 1, b = 2 }).Should().Equal("@a", "@b");
        }

        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\r")]
        public void CreateMapped_ParameterAfterNewLine_IsDiscovered(string newLine)
        {
            DiscoveredNames("select" + newLine + "@a," + newLine + "@b", new { a = 1, b = 2 }).Should().Equal("@a", "@b");
        }

        [Fact]
        public void CreateMapped_ParametersInParentheses_AreDiscovered()
        {
            DiscoveredNames("insert into T (A, B) values (@a,@b)", new { a = 1, b = 2 }).Should().Equal("@a", "@b");
        }

        [Fact]
        public void CreateMapped_SeveralParameters_AreAddedInTextOrder()
        {
            DiscoveredNames("select @c, @a, @b", new { a = 1, b = 2, c = 3 }).Should().Equal("@c", "@a", "@b");
        }

        [Theory]
        [InlineData("select @a1", "@a1")]
        [InlineData("select @A_b", "@A_b")]
        [InlineData("select @_a", "@_a")]
        [InlineData("select @a$b", "@a$b")]
        [InlineData("select @#tmp", "@#tmp")]
        [InlineData("select @1", "@1")]
        [InlineData("select @a.b", "@a")]
        [InlineData("select @a::int", "@a")]
        [InlineData("select @a-1", "@a")]
        [InlineData("select @a\u00e9", "@a")]
        public void CreateMapped_ParameterName_ConsistsOfAsciiLettersDigitsDollarHashUnderscore(string commandText, string expectedName)
        {
            var context = Spec.CreateCapturingContext();
            var settings = new[] { new FieldSettings<int>(expectedName.Substring(1)) };

            context.CreateMapped(commandText, settings, 7);

            Names(context.LastCommand).Should().Equal(expectedName);
            Values(context.LastCommand).Should().Equal(7);
        }

        [Theory]
        [Trait("Quirk", "Q11")]
        [InlineData("select '@x', @a")]
        [InlineData("select 'john@x.com', @a")]
        [InlineData("-- uses @x\nselect @a")]
        [InlineData("/* @x */ select @a")]
        public void CreateMapped_AtSignInLiteralOrComment_IsTreatedAsParameter(string commandText)
        {
            DiscoveredNames(commandText, new { a = 1, x = 2 }).Should().Equal("@x", "@a");
        }

        [Fact]
        [Trait("Quirk", "Q11")]
        public void CreateMapped_EmailAddressWithoutMatchingMember_ThrowsArgumentException()
        {
            var context = Spec.CreateCapturingContext();

            Action act = () => context.CreateMapped("select * from Employee where Email = 'john@example.com' and ID = @ID", new { ID = 1 });

            act.Should().Throw<ArgumentException>()
                .WithMessage("Not all parameters has been mapped on settings (Parameter 'parameters')")
                .And.ParamName.Should().Be("parameters");
        }

        [Fact]
        [Trait("Quirk", "Q33")]
        public void CreateMapped_SameParameterInDifferentCase_ThrowsArgumentException()
        {
            var context = Spec.CreateCapturingContext();

            Action act = () => context.CreateMapped("select @a where @A > 0", new { a = 1 });

            act.Should().Throw<ArgumentException>().WithMessage("Parameter names has been duplicated");
        }

        [Fact]
        [Trait("Quirk", "Q33")]
        public void CreateMapped_SameParameterInDifferentCaseWithCaseSensitiveMatching_AddsTwoParameters()
        {
            DiscoveredNames("select @a where @A > 0", new { a = 1, A = 2 }, new QueryOptions(caseSensitiveParamsMatching: true))
                .Should().Equal("@a", "@A");
        }

        [Fact]
        public void CreateMapped_NoParametersInText_AddsNoParameters()
        {
            DiscoveredNames("select 1", new { a = 1 }).Should().BeEmpty();
        }

        #endregion

        #region Explicit parameter names

        [Theory]
        [InlineData("Age")]
        [InlineData("@Age")]
        [InlineData("age")]
        [InlineData("@AGE")]
        public void CreateMapped_ExplicitName_MatchesMemberIgnoringPrefixAndKeepsNameAsGiven(string name)
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @Age", new[] { name }, new { Age = 40 });

            Names(context.LastCommand).Should().Equal(name);
            Values(context.LastCommand).Should().Equal(40);
        }

        [Fact]
        public void CreateMapped_ExplicitNames_TextIsNotScanned()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @a, @b, '@c'", new[] { "x" }, new { a = 1, x = 2 });

            Names(context.LastCommand).Should().Equal("x");
            Values(context.LastCommand).Should().Equal(2);
        }

        [Fact]
        public void CreateMapped_ExplicitNames_AreAddedInListOrder()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @a, @b", new[] { "@b", "a" }, new { a = 1, b = 2 });

            Names(context.LastCommand).Should().Equal("@b", "a");
            Values(context.LastCommand).Should().Equal(2, 1);
        }

        [Fact]
        [Trait("Quirk", "Q32")]
        public void CreateMapped_EmptyExplicitName_ThrowsIndexOutOfRangeException()
        {
            var context = Spec.CreateCapturingContext();

            Action act = () => context.CreateMapped("select 1", new[] { "" }, new { a = 1 });

            act.Should().Throw<IndexOutOfRangeException>().WithMessage("Index was outside the bounds of the array.");
        }

        #endregion

        #region Null values

        [Fact]
        public void CreateMapped_NullMembersOfNonBinaryTypes_BecomeDBNull()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @Name, @Age, @StartWorking, @Key, @Kind, @Tag", new NullableMembers());

            Names(context.LastCommand).Should().Equal("@Name", "@Age", "@StartWorking", "@Key", "@Kind", "@Tag");
            Values(context.LastCommand).Should().AllSatisfy(x => x.Should().BeSameAs(DBNull.Value));
        }

        [Fact]
        public void CreateInsert_NullMembersOfNonBinaryTypes_BecomeDBNull()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateInsert("Employee", new NullableMembers());

            Values(context.LastCommand).Should().HaveCount(6).And.AllSatisfy(x => x.Should().BeSameAs(DBNull.Value));
        }

        #endregion

        #region AddWithValue

        [Theory]
        [InlineData("string")]
        [InlineData("int")]
        [InlineData("long")]
        [InlineData("decimal")]
        [InlineData("bool")]
        [InlineData("DateTime")]
        [InlineData("DateTimeOffset")]
        [InlineData("Guid")]
        [InlineData("byte[]")]
        [InlineData("enum")]
        public void CreateMapped_Value_IsPassedAsIs(string kind)
        {
            var value = SampleValue(kind);

            var parameter = ParameterWithValue(value);

            parameter.ParameterName.Should().Be("@Value");
            parameter.Value.Should().BeSameAs(value);
            parameter.Direction.Should().Be(ParameterDirection.Input);
        }

        #endregion

        #region Stored procedures

        [Fact]
        public void CreateProcedure_AnonymousType_ParametersComeFromAllMembersNamedWithAt()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateProcedure("TestProc", new { CompanyID = 1, Age = 40 });

            var command = context.LastCommand;
            command.CommandType.Should().Be(CommandType.StoredProcedure);
            command.CommandText.Should().Be("TestProc");
            Names(command).Should().Equal("@CompanyID", "@Age");
            Values(command).Should().Equal(1, 40);
        }

        [Fact]
        public void CreateProcedure_FieldsAndProperties_AllBecomeParameters()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateProcedure("TestProc", new ProcedureArguments() { CompanyID = 1, Age = 40 });

            Names(context.LastCommand).Should().Equal("@CompanyID", "@Age");
            Values(context.LastCommand).Should().Equal(1, 40);
        }

        [Fact]
        public void CreateProcedure_Settings_ParametersAreNamedWithAtPlusSettingName()
        {
            var context = Spec.CreateCapturingContext();
            var settings = new[]
            {
                new FieldSettings<ProcedureArguments>("company_id", typeof(int), x => x.CompanyID),
                new FieldSettings<ProcedureArguments>("Age", typeof(int), x => x.Age)
            };

            context.CreateProcedure("TestProc", settings, new ProcedureArguments() { CompanyID = 1, Age = 40 });

            Names(context.LastCommand).Should().Equal("@company_id", "@Age");
            Values(context.LastCommand).Should().Equal(1, 40);
        }

        [Fact]
        public void CreateProcedure_ExplicitNames_KeepNamesAsGiven()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateProcedure("TestProc", new[] { "CompanyID", "@Age" }, new { CompanyID = 1, Age = 40, Name = "x" });

            Names(context.LastCommand).Should().Equal("CompanyID", "@Age");
            Values(context.LastCommand).Should().Equal(1, 40);
        }

        #endregion

        #region Command timeout

        [Fact]
        public void CreateSimple_NoQueryOptions_KeepsDriverDefaultTimeout()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateSimple("select 1");

            context.LastCommand.CommandTimeout.Should().Be(DriverDefaultTimeout());
        }

        [Fact]
        public void CreateMapped_TimeoutOptionUnset_KeepsDriverDefaultTimeout()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @a", new { a = 1 }, new QueryOptions(caseSensitiveParamsMatching: true));

            context.LastCommand.CommandTimeout.Should().Be(DriverDefaultTimeout());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(45)]
        [InlineData(600)]
        public void CreateSimple_TimeoutOptionWithValue_IsApplied(int timeout)
        {
            var context = Spec.CreateCapturingContext();

            context.CreateSimple(new QueryOptions(timeout), "select 1");

            context.LastCommand.CommandTimeout.Should().Be(timeout);
        }

        [Fact]
        public void CreateProcedure_TimeoutOptionWithValue_IsApplied()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateProcedure("TestProc", new { a = 1 }, new QueryOptions(77));

            context.LastCommand.CommandTimeout.Should().Be(77);
        }

        #endregion
    }
}
