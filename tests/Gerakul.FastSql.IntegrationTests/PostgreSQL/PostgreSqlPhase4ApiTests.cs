using System;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.PostgreSQL
{
    [Trait("Provider", "PostgreSQL")]
    public class PostgreSqlPhase4ApiTests : Phase4ApiTests<PostgreSqlDatabase>
    {
        public PostgreSqlPhase4ApiTests(PostgreSqlDatabase database)
            : base(database)
        {
        }

        protected override string IdentityColumnType => "int generated always as identity";

        protected override Type ServerExceptionType => typeof(PostgresException);

        private ConnectionStringContext Context => Spec.DefaultProvider.CreateContext(Database.ConnectionString);

        // I7: Npgsql returns DateTime for timestamp and TimeSpan for interval; DateOnly and TimeOnly members take them.
        // date and time columns are DateOnly and TimeOnly already (PostgreSqlTypeMappingTests)
        [Fact]
        [Trait("Fixed", "I7")]
        public void TimestampAndIntervalColumns_ReadIntoDateOnlyAndTimeOnlyMembers()
        {
            var date = Context.CreateSimple("select cast('2020-02-29 13:14:15' as timestamp)").ExecuteScalar<DateOnly>();
            var time = Context.CreateSimple("select cast('13:14:15' as interval)").ExecuteScalar<TimeOnly>();
            var dates = Context.CreateSimple("select cast('2020-02-29 23:59:59' as timestamp) union all select null").ExecuteQueryFirstColumn<DateOnly?>().ToArray();

            date.Should().Be(new DateOnly(2020, 2, 29));
            time.Should().Be(new TimeOnly(13, 14, 15));
            dates.Should().Equal(new DateOnly(2020, 2, 29), null);
        }

        // an interval of a day or more is not a time of day
        [Fact]
        [Trait("Fixed", "I7")]
        public void IntervalOfMoreThanADay_ReadIntoTimeOnlyMember_Throws()
        {
            Action act = () => Context.CreateSimple("select cast('25:00:00' as interval)").ExecuteScalar<TimeOnly>();

            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("Cannot read the result (TimeSpan) into TimeOnly: *");
        }
    }
}
