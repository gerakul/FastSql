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

        [Theory]
        [Trait("UpgradeSensitive", "NpgsqlParameter type inference")]
        [InlineData("string", DbType.String, NpgsqlDbType.Text)]
        [InlineData("int", DbType.Int32, NpgsqlDbType.Integer)]
        [InlineData("long", DbType.Int64, NpgsqlDbType.Bigint)]
        [InlineData("short", DbType.Int16, NpgsqlDbType.Smallint)]
        [InlineData("byte", DbType.Int16, NpgsqlDbType.Smallint)]
        [InlineData("decimal", DbType.Decimal, NpgsqlDbType.Numeric)]
        [InlineData("double", DbType.Double, NpgsqlDbType.Double)]
        [InlineData("float", DbType.Single, NpgsqlDbType.Real)]
        [InlineData("bool", DbType.Boolean, NpgsqlDbType.Boolean)]
        [InlineData("DateTime", DbType.DateTime, NpgsqlDbType.Timestamp)]
        [InlineData("DateTimeOffset", DbType.DateTimeOffset, NpgsqlDbType.TimestampTz)]
        [InlineData("TimeSpan", DbType.Object, NpgsqlDbType.Interval)]
        [InlineData("Guid", DbType.Guid, NpgsqlDbType.Uuid)]
        [InlineData("byte[]", DbType.Binary, NpgsqlDbType.Bytea)]
        public void AddWithValue_Value_InfersParameterType(string kind, DbType expectedDbType, NpgsqlDbType expectedNpgsqlDbType)
        {
            var parameter = (NpgsqlParameter)ParameterWithValue(SampleValue(kind));

            parameter.DbType.Should().Be(expectedDbType);
            parameter.NpgsqlDbType.Should().Be(expectedNpgsqlDbType);
        }

        [Fact]
        [Trait("UpgradeSensitive", "NpgsqlParameter type inference")]
        public void AddWithValue_EnumValue_HasObjectDbTypeAndNoNpgsqlDbType()
        {
            var parameter = (NpgsqlParameter)ParameterWithValue(SampleValue("enum"));

            parameter.DbType.Should().Be(DbType.Object);
            Func<NpgsqlDbType> act = () => parameter.NpgsqlDbType;
            act.Should().Throw<NotSupportedException>().WithMessage("Can't infer NpgsqlDbType for type *+SampleEnum*");
        }
    }
}
