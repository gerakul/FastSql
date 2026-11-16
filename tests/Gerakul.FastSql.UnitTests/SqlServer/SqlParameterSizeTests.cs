using System;
using System.Data;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    // I6. The driver declares a string parameter as nvarchar(n) and a byte array as varbinary(n), n being the
    // length of the value, which gives a statement a cached plan per length. FastSql declares one size for all
    // the values that fit in it. That the server then keeps one plan is checked by the integration tests.
    public class SqlParameterSizeTests
    {
        private class Document
        {
            public int ID { get; set; }
            public string Title { get; set; }
            public byte[] Content { get; set; }
        }

        private readonly ICapturingContext context = new SqlServerSpec().CreateCapturingContext();

        private SqlParameter Simple(object value)
        {
            context.CreateSimple("select @p0", value);
            return (SqlParameter)context.LastCommand.Parameters[0];
        }

        // what the driver does on its own
        private static SqlParameter Driver(object value)
        {
            return new SqlCommand().Parameters.AddWithValue("@p0", value);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(4000)]
        public void String_UpTo4000Characters_IsDeclaredWithSize4000(int length)
        {
            var value = new string('x', length);

            var parameter = Simple(value);

            parameter.Size.Should().Be(4000);
            parameter.SqlDbType.Should().Be(SqlDbType.NVarChar);
            parameter.Value.Should().Be(value);
        }

        [Theory]
        [InlineData(4001)]
        [InlineData(100000)]
        public void String_LongerThan4000Characters_IsDeclaredAsMax(int length)
        {
            var value = new string('x', length);

            var parameter = Simple(value);

            parameter.Size.Should().Be(-1);
            parameter.SqlDbType.Should().Be(SqlDbType.NVarChar);
            parameter.Value.Should().Be(value);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(8000)]
        public void ByteArray_UpTo8000Bytes_IsDeclaredWithSize8000(int length)
        {
            var value = new byte[length];

            var parameter = Simple(value);

            parameter.Size.Should().Be(8000);
            parameter.SqlDbType.Should().Be(SqlDbType.VarBinary);
            parameter.Value.Should().BeSameAs(value);
        }

        [Fact]
        public void ByteArray_LongerThan8000Bytes_IsDeclaredAsMax()
        {
            var parameter = Simple(new byte[8001]);

            parameter.Size.Should().Be(-1);
            parameter.SqlDbType.Should().Be(SqlDbType.VarBinary);
        }

        public static TheoryData<object> OtherValues => new TheoryData<object>()
        {
            1, 2L, (short)3, (byte)4, true, 1.5m, 123.456m, 2.5, 3.5f, new DateTime(2020, 1, 2), new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
            new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero), TimeSpan.FromHours(1), 'c', DBNull.Value
        };

        [Theory]
        [MemberData(nameof(OtherValues))]
        public void OtherValue_IsDeclaredAsTheDriverDeclaresIt(object value)
        {
            var parameter = Simple(value);
            var driver = Driver(value);

            parameter.Size.Should().Be(driver.Size);
            parameter.SqlDbType.Should().Be(driver.SqlDbType);
            parameter.Precision.Should().Be(driver.Precision);
            parameter.Scale.Should().Be(driver.Scale);
        }

        [Fact]
        public void NullValue_IsDeclaredAsTheDriverDeclaresIt()
        {
            var parameter = Simple(null);
            var driver = Driver(DBNull.Value);

            parameter.Value.Should().Be(DBNull.Value);
            parameter.Size.Should().Be(driver.Size);
            parameter.SqlDbType.Should().Be(driver.SqlDbType);
        }

        [Fact]
        public void MappedCommand_StringAndByteArrayMembers_AreDeclaredWithTheFixedSizes()
        {
            context.CreateInsert("Document", new Document() { ID = 1, Title = "abc", Content = new byte[] { 1, 2, 3 } });

            var parameters = context.LastCommand.Parameters;
            ((SqlParameter)parameters["ID"]).SqlDbType.Should().Be(SqlDbType.Int);
            ((SqlParameter)parameters["Title"]).Size.Should().Be(4000);
            ((SqlParameter)parameters["Content"]).Size.Should().Be(8000);
        }

        [Fact]
        public void MappedCommand_NullMembers_AreDeclaredAsTheDriverDeclaresThem()
        {
            context.CreateInsert("Document", new Document() { ID = 1, Title = null, Content = null });

            var parameters = context.LastCommand.Parameters;
            var title = (SqlParameter)parameters["Title"];
            var content = (SqlParameter)parameters["Content"];
            title.Value.Should().Be(DBNull.Value);
            title.Size.Should().Be(Driver(DBNull.Value).Size);
            content.Size.Should().Be(Driver(System.Data.SqlTypes.SqlBytes.Null).Size);
        }

        [Fact]
        public void ParameterOfTheCaller_IsNotChanged()
        {
            var own = new SqlParameter("@p0", "abc");
            var ownWithSize = new SqlParameter("@p1", SqlDbType.NVarChar, 50) { Value = "abc" };
            int sizeBefore = own.Size;

            context.CreateSimple("select @p0, @p1", own, ownWithSize);

            context.LastCommand.Parameters[0].Should().BeSameAs(own);
            own.Size.Should().Be(sizeBefore);
            ownWithSize.Size.Should().Be(50);
        }
    }
}
