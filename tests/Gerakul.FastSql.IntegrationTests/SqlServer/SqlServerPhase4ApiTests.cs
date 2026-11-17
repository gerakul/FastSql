using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    [Trait("Provider", "SqlServer")]
    public class SqlServerPhase4ApiTests : Phase4ApiTests<SqlServerDatabase>
    {
        public SqlServerPhase4ApiTests(SqlServerDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int identity(1,1) not null";

        protected override Type ServerExceptionType => typeof(SqlException);

        private ConnectionStringContext Context => Spec.DefaultProvider.CreateContext(Database.ConnectionString);

        // scope_identity() is numeric(38, 0): ExecuteScalar() returns a decimal (Q18), ExecuteScalar<int>() the int
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        public void CreateInsertAndGetID_TypedScalar_ReturnsTheIdentityAsInt(ContextKind kind)
        {
            var table = CreatePersonTableWithIdentity();

            int first = Database.Run(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, new { GroupID = 1, Name = "Alice", Age = 30 }).ExecuteScalar<int>());
            long second = Database.Run(kind, x => ((ISqlCommandCreator)x).CreateInsertAndGetID(table, new { GroupID = 2, Name = "Bob", Age = 40 }).ExecuteScalar<long>());

            first.Should().Be(1);
            second.Should().Be(2L);
        }

        #region I7: date and time columns into DateOnly and TimeOnly members

        public class DateAndTime
        {
            public int ID { get; set; }
            public DateOnly D { get; set; }
            public TimeOnly T { get; set; }
            public DateOnly? NullableD { get; set; }
            public TimeOnly? NullableT { get; set; }
        }

        public class OldDateAndTime
        {
            public int ID { get; set; }
            public DateTime D { get; set; }
            public TimeSpan T { get; set; }
            public DateTime? NullableD { get; set; }
            public TimeSpan? NullableT { get; set; }
        }

        private static readonly DateAndTime[] rows =
        {
            new DateAndTime() { ID = 1, D = new DateOnly(2020, 2, 29), T = new TimeOnly(13, 14, 15, 123), NullableD = new DateOnly(1999, 12, 31), NullableT = new TimeOnly(23, 59, 59) },
            new DateAndTime() { ID = 2, D = DateOnly.MinValue, T = TimeOnly.MinValue, NullableD = null, NullableT = null }
        };

        private string CreateDateAndTimeTable()
        {
            return Database.CreateScratchTable("ID int not null, D date not null, T time not null, NullableD date null, NullableT time null");
        }

        // The driver returns DateTime and TimeSpan for date and time columns. Until phase 4 they could not be read
        // into DateOnly and TimeOnly members (InvalidCastException), although the driver writes such members
        [Theory]
        [MemberData(nameof(ContextKinds.All), MemberType = typeof(ContextKinds))]
        [Trait("Fixed", "I7")]
        public void DateAndTimeColumns_WrittenAndReadWithDateOnlyAndTimeOnlyMembers(ContextKind kind)
        {
            var inserted = CreateDateAndTimeTable();
            var copied = CreateDateAndTimeTable();

            foreach (var row in rows)
            {
                Database.Run(kind, x => x.Insert(inserted, row));
            }

            Database.Run(kind, x => rows.WriteToServer(x, copied));

            foreach (var table in new[] { inserted, copied })
            {
                var read = Database.Run(kind, x => x.CreateSimple($"select * from {table} order by ID").ExecuteQuery<DateAndTime>().ToArray());
                var dates = Database.Run(kind, x => x.CreateSimple($"select NullableD from {table} order by ID").ExecuteQueryFirstColumn<DateOnly?>().ToArray());
                var time = Database.Run(kind, x => x.CreateSimple($"select T from {table} where ID = 1").ExecuteScalar<TimeOnly>());

                read.Should().BeEquivalentTo(rows, o => o.WithStrictOrdering());
                dates.Should().Equal(new DateOnly(1999, 12, 31), null);
                time.Should().Be(new TimeOnly(13, 14, 15, 123));
            }
        }

        [Fact]
        [Trait("Fixed", "I7")]
        public async Task DateAndTimeColumns_ReadWithDateOnlyAndTimeOnlyMembersAsync()
        {
            var table = CreateDateAndTimeTable();
            await rows.WriteToServerAsync(Context, table, Token);

            var read = await Context.CreateSimple($"select * from {table} order by ID").ExecuteQueryAsync<DateAndTime>(cancellationToken: Token).ToListCompat(Token);

            read.Should().BeEquivalentTo(rows, o => o.WithStrictOrdering());
        }

        // models with DateTime and TimeSpan members read the same columns as before
        [Fact]
        public void DateAndTimeColumns_StillReadWithDateTimeAndTimeSpanMembers()
        {
            var table = CreateDateAndTimeTable();
            rows.WriteToServer(Context, table);

            var read = Context.CreateSimple($"select * from {table} order by ID").ExecuteQuery<OldDateAndTime>().ToArray();

            read[0].D.Should().Be(new DateTime(2020, 2, 29));
            read[0].T.Should().Be(new TimeSpan(0, 13, 14, 15, 123));
            read[0].NullableD.Should().Be(new DateTime(1999, 12, 31));
            read[1].NullableT.Should().BeNull();
        }

        // the member says "a date", so the time of a datetime2 value is dropped
        [Fact]
        [Trait("Fixed", "I7")]
        public void DateTimeColumn_ReadIntoDateOnlyMember_DropsTheTime()
        {
            var date = Context.CreateSimple("select cast('2020-02-29 13:14:15' as datetime2)").ExecuteScalar<DateOnly>();
            var dates = Context.CreateSimple("select cast('2020-02-29 23:59:59.9999999' as datetime2) union all select null").ExecuteQueryFirstColumn<DateOnly?>().ToArray();

            date.Should().Be(new DateOnly(2020, 2, 29));
            dates.Should().Equal(new DateOnly(2020, 2, 29), null);
        }

        public class CreatedTypes
        {
            public int ID { get; set; }
            public DateOnly D { get; set; }
            public TimeOnly T { get; set; }
            public TimeSpan? Span { get; set; }
            public DateTimeOffset Offset { get; set; }
        }

        // until phase 4 these member types had no column type: "Can not create table with specified fields"
        [Fact]
        [Trait("Fixed", "I7")]
        public void WriteToServer_CreateTable_DateAndTimeMembersGetTheirColumnTypes()
        {
            var table = "created_" + Guid.NewGuid().ToString("N");
            var items = new[]
            {
                new CreatedTypes() { ID = 1, D = new DateOnly(2020, 2, 29), T = new TimeOnly(13, 14, 15), Span = new TimeSpan(1, 2, 3), Offset = new DateTimeOffset(2020, 2, 29, 13, 14, 15, TimeSpan.FromHours(2)) },
                new CreatedTypes() { ID = 2, D = new DateOnly(2021, 1, 1), T = new TimeOnly(0, 0, 1), Span = null, Offset = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero) }
            };

            items.WriteToServer(Context, table, new SqlBulkOptions(createTable: true));

            var columns = Context.CreateSimple("select COLUMN_NAME + ' ' + DATA_TYPE + ' ' + cast(DATETIME_PRECISION as varchar(10)) + ' ' + IS_NULLABLE "
                + "from INFORMATION_SCHEMA.COLUMNS where TABLE_NAME = @p0 and DATA_TYPE <> 'int' order by ORDINAL_POSITION", table).ExecuteQueryFirstColumn<string>().ToArray();
            var read = Context.CreateSimple($"select * from {table} order by ID").ExecuteQuery<CreatedTypes>().ToArray();

            columns.Should().Equal("D date 0 NO", "T time 7 NO", "Span time 7 YES", "Offset datetimeoffset 7 NO");
            read.Should().BeEquivalentTo(items, o => o.WithStrictOrdering());
        }

        #endregion
    }
}
