using System;
using System.Data;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    public class NpgsqlContextProviderTests : ProviderHooksTests<PostgreSqlSpec>
    {
        private class BinaryMembers
        {
            public byte[] Data { get; set; }
            public object Blob { get; set; }
        }

        // dollar-quoted strings and escape strings (backslash escapes) are literals too; $1 is a positional parameter
        [Theory]
        [Trait("Fixed", "Q11")]
        [InlineData("select $$@x$$, @a")]
        [InlineData("select $tag$ @x $$ @x $tag$, @a")]
        [InlineData("select E'it\\'s @x', @a")]
        [InlineData("select e'\\\\', @a")]
        [InlineData("select $1, @a")]
        public void CreateMapped_AtSignInDollarQuoteOrEscapeString_IsNotAParameter(string commandText)
        {
            DiscoveredNames(commandText, new { a = 1, x = 2 }).Should().Equal("@a");
        }

        // brackets are array subscripts in PostgreSQL, not quotes
        [Fact]
        [Trait("Fixed", "Q11")]
        public void CreateMapped_ParameterInArraySubscript_IsFound()
        {
            DiscoveredNames("select tags[@a] from T", new { a = 1 }).Should().Equal("@a");
        }

        [Fact]
        public void CreateMapped_Parameters_AreNpgsqlParameters()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @a", new { a = 1 });

            context.LastCommand.Should().BeOfType<NpgsqlCommand>();
            context.LastCommand.Parameters[0].Should().BeOfType<NpgsqlParameter>();
        }

        [Fact]
        public void CreateMapped_NullByteArrayMember_BecomesDBNull()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @Data, @Blob", new BinaryMembers());

            Values(context.LastCommand).Should().HaveCount(2).And.AllSatisfy(x => x.Should().BeSameAs(DBNull.Value));
        }

        // P3 (phase 5): a DateTimeOffset goes in UTC
        protected override object ExpectedParameterValue(object value)
        {
            return value is DateTimeOffset dateTimeOffset ? dateTimeOffset.ToUniversalTime() : value;
        }

        // P3 (phase 5): Npgsql refuses a DateTimeOffset with a non-zero offset and closes the connection; timestamptz keeps
        // the instant only, so the parameter gets the same instant in UTC. A value in UTC already is passed as it is
        [Fact]
        [Trait("Fixed", "Q87")]
        public void CreateInsertAndCreateSimple_DateTimeOffsetValue_IsSentInUtc()
        {
            var context = Spec.CreateCapturingContext();
            var withOffset = new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.FromHours(3));
            var utc = new DateTimeOffset(2020, 1, 1, 7, 0, 0, TimeSpan.Zero);

            context.CreateInsert("Events", new { Moment = withOffset, Utc = utc, Missing = (DateTimeOffset?)null });
            var inserted = Values(context.LastCommand);
            context.CreateSimple("select @p0", withOffset);
            var simple = Values(context.LastCommand);

            inserted[0].Should().BeOfType<DateTimeOffset>().Which.Offset.Should().Be(TimeSpan.Zero);
            inserted[0].Should().Be(utc);
            inserted[1].Should().Be(utc);
            inserted[2].Should().BeSameAs(DBNull.Value);
            simple[0].Should().BeOfType<DateTimeOffset>().Which.Offset.Should().Be(TimeSpan.Zero);
            simple[0].Should().Be(utc);
        }

        [Fact]
        public void CreateInsert_NullByteArrayMember_BecomesDBNull()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateInsert("Files", new BinaryMembers());

            Values(context.LastCommand).Should().HaveCount(2).And.AllSatisfy(x => x.Should().BeSameAs(DBNull.Value));
        }

        [Fact]
        public void CreateMapped_NullValueOfByteArraySetting_BecomesDBNull()
        {
            var context = Spec.CreateCapturingContext();
            var settings = new[] { new FieldSettings<BinaryMembers>("Content", typeof(byte[]), x => x.Blob) };

            context.CreateMapped("select @Content", settings, new BinaryMembers());

            Values(context.LastCommand)[0].Should().BeSameAs(DBNull.Value);
        }

        [Fact]
        [Trait("UpgradeSensitive", "NpgsqlParameter type inference")]
        public void CreateMapped_NullMember_ParameterTypeIsInferredFromDBNull()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @Name", new NullableMembers());

            var parameter = (NpgsqlParameter)context.LastCommand.Parameters[0];
            parameter.DbType.Should().Be(DbType.Object);
        }

        // Npgsql 10 resolves the type from the value when the command is executed, so a parameter that only has a value
        // reports no type. Npgsql 4 inferred DbType and NpgsqlDbType from the value (string: String / Text, int: Int32 /
        // Integer, and so on) and threw NotSupportedException for the NpgsqlDbType of an enum.
        [Theory]
        [Trait("UpgradeSensitive", "NpgsqlParameter type inference")]
        [InlineData("string")]
        [InlineData("int")]
        [InlineData("long")]
        [InlineData("short")]
        [InlineData("byte")]
        [InlineData("decimal")]
        [InlineData("double")]
        [InlineData("float")]
        [InlineData("bool")]
        [InlineData("DateTime")]
        [InlineData("DateTimeOffset")]
        [InlineData("TimeSpan")]
        [InlineData("Guid")]
        [InlineData("byte[]")]
        [InlineData("enum")]
        public void AddWithValue_Value_ParameterTypeIsNotInferredBeforeExecution(string kind)
        {
            var parameter = (NpgsqlParameter)ParameterWithValue(SampleValue(kind));

            parameter.DbType.Should().Be(DbType.Object);
            parameter.NpgsqlDbType.Should().Be(NpgsqlDbType.Unknown);
        }
    }
}
