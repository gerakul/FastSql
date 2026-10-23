using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Xunit;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    public class SqlContextProviderTests : ProviderHooksTests<SqlServerSpec>
    {
        private class BinaryMembers
        {
            public byte[] Data { get; set; }
            public object Blob { get; set; }
        }

        [Fact]
        public void CreateMapped_Parameters_AreSqlParameters()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @a", new { a = 1 });

            context.LastCommand.Should().BeOfType<SqlCommand>();
            context.LastCommand.Parameters[0].Should().BeOfType<SqlParameter>();
        }

        [Fact]
        public void CreateMapped_NullByteArrayMember_BecomesSqlBytesNull()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @Data, @Blob", new BinaryMembers());

            var values = Values(context.LastCommand);
            values[0].Should().BeOfType<SqlBytes>().Which.IsNull.Should().BeTrue();
            values[1].Should().BeSameAs(DBNull.Value);
        }

        [Fact]
        public void CreateInsert_NullByteArrayMember_BecomesSqlBytesNull()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateInsert("Files", new BinaryMembers());

            var values = Values(context.LastCommand);
            values[0].Should().BeOfType<SqlBytes>().Which.IsNull.Should().BeTrue();
            values[1].Should().BeSameAs(DBNull.Value);
        }

        [Fact]
        public void CreateMapped_NullValueOfByteArraySetting_BecomesSqlBytesNull()
        {
            var context = Spec.CreateCapturingContext();
            var settings = new[] { new FieldSettings<BinaryMembers>("Content", typeof(byte[]), x => x.Blob) };

            context.CreateMapped("select @Content", settings, new BinaryMembers());

            Values(context.LastCommand)[0].Should().BeOfType<SqlBytes>().Which.IsNull.Should().BeTrue();
        }

        [Fact]
        [Trait("UpgradeSensitive", "SqlParameter type inference")]
        public void CreateMapped_NullByteArrayMember_ParameterIsVarBinary()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @Data", new BinaryMembers());

            var parameter = (SqlParameter)context.LastCommand.Parameters[0];
            parameter.SqlDbType.Should().Be(SqlDbType.VarBinary);
            parameter.DbType.Should().Be(DbType.Binary);
        }

        [Fact]
        [Trait("UpgradeSensitive", "SqlParameter type inference")]
        public void CreateMapped_NullMember_ParameterTypeIsInferredFromDBNull()
        {
            var context = Spec.CreateCapturingContext();

            context.CreateMapped("select @Name", new NullableMembers());

            var parameter = (SqlParameter)context.LastCommand.Parameters[0];
            parameter.SqlDbType.Should().Be(SqlDbType.NVarChar);
            parameter.DbType.Should().Be(DbType.String);
        }

        [Theory]
        [Trait("UpgradeSensitive", "SqlParameter type inference")]
        [InlineData("string", DbType.String, SqlDbType.NVarChar)]
        [InlineData("int", DbType.Int32, SqlDbType.Int)]
        [InlineData("long", DbType.Int64, SqlDbType.BigInt)]
        [InlineData("short", DbType.Int16, SqlDbType.SmallInt)]
        [InlineData("byte", DbType.Byte, SqlDbType.TinyInt)]
        [InlineData("decimal", DbType.Decimal, SqlDbType.Decimal)]
        [InlineData("double", DbType.Double, SqlDbType.Float)]
        [InlineData("float", DbType.Single, SqlDbType.Real)]
        [InlineData("bool", DbType.Boolean, SqlDbType.Bit)]
        [InlineData("DateTime", DbType.DateTime, SqlDbType.DateTime)]
        [InlineData("DateTimeOffset", DbType.DateTimeOffset, SqlDbType.DateTimeOffset)]
        [InlineData("TimeSpan", DbType.Time, SqlDbType.Time)]
        [InlineData("Guid", DbType.Guid, SqlDbType.UniqueIdentifier)]
        [InlineData("byte[]", DbType.Binary, SqlDbType.VarBinary)]
        [InlineData("enum", DbType.Int32, SqlDbType.Int)]
        public void AddWithValue_Value_InfersParameterType(string kind, DbType expectedDbType, SqlDbType expectedSqlDbType)
        {
            var parameter = (SqlParameter)ParameterWithValue(SampleValue(kind));

            parameter.DbType.Should().Be(expectedDbType);
            parameter.SqlDbType.Should().Be(expectedSqlDbType);
        }
    }
}
