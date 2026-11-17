using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.Common
{
    // Shared helpers of the data modification tests (I7): scratch tables, rows written and read back,
    // server errors. Seed tables are never modified, every test creates its own scratch tables.
    public abstract class DataModificationTests<TDatabase> : IClassFixture<TDatabase> where TDatabase : DatabaseFixture
    {
        protected DataModificationTests(TDatabase database)
        {
            this.Database = database;
        }

        protected TDatabase Database { get; }

        protected ProviderSpec Spec => Database.Spec;

        // column type of a generated key that rejects explicit values
        protected abstract string IdentityColumnType { get; }

        // the exception type the driver throws for server errors
        protected abstract Type ServerExceptionType { get; }

        protected string Q(string identifier)
        {
            return Spec.Quote(identifier);
        }

        #region Tables

        // Person rows with a generated ID
        protected string CreatePersonTableWithIdentity()
        {
            return Database.CreateScratchTable($"{Q("ID")} {IdentityColumnType} primary key, {Q("GroupID")} int not null, "
                + $"{Q("Name")} varchar(50) null, {Q("Age")} int null");
        }

        // Person rows with an ID given by the caller
        protected string CreatePersonTable()
        {
            return Database.CreateScratchTable($"{Q("ID")} int not null primary key, {Q("GroupID")} int not null, "
                + $"{Q("Name")} varchar(50) null, {Q("Age")} int null");
        }

        // Membership rows with a composite key (GroupID, Code)
        protected string CreateMembershipTable()
        {
            return Database.CreateScratchTable($"{Q("GroupID")} int not null, {Q("Code")} varchar(10) not null, "
                + $"{Q("Name")} varchar(50) null, primary key ({Q("GroupID")}, {Q("Code")})");
        }

        #endregion

        #region Rows through plain SQL

        protected void InsertPersons(string table, params Person[] persons)
        {
            foreach (var person in persons)
            {
                Database.Execute($"insert into {table} ({Q("ID")}, {Q("GroupID")}, {Q("Name")}, {Q("Age")}) "
                    + $"values ({person.ID}, {person.GroupID}, {Literal(person.Name)}, {Literal(person.Age)})");
            }
        }

        protected void InsertMemberships(string table, params Membership[] memberships)
        {
            foreach (var membership in memberships)
            {
                Database.Execute($"insert into {table} ({Q("GroupID")}, {Q("Code")}, {Q("Name")}) "
                    + $"values ({membership.GroupID}, {Literal(membership.Code)}, {Literal(membership.Name)})");
            }
        }

        protected Person[] ReadPersons(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by {Q("ID")}")
                .ExecuteQuery<Person>().ToArray());
        }

        protected Membership[] ReadMemberships(string table)
        {
            return Database.Run(ContextKind.ConnectionString, x => x.CreateSimple($"select * from {table} order by {Q("GroupID")}, {Q("Code")}")
                .ExecuteQuery<Membership>().ToArray());
        }

        private static string Literal(string value)
        {
            return value == null ? "null" : "'" + value.Replace("'", "''") + "'";
        }

        private static string Literal(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "null";
        }

        #endregion

        #region Server errors

        // the driver's server error type with a message matching the pattern (AwesomeAssertions wildcards)
        protected void ShouldThrowServerError(Action action, string messagePattern)
        {
            action.Should().Throw<DbException>().WithMessage(messagePattern)
                .Which.Should().BeOfType(ServerExceptionType);
        }

        protected async Task ShouldThrowServerErrorAsync(Func<Task> action, string messagePattern)
        {
            var assertion = await action.Should().ThrowAsync<DbException>();
            assertion.WithMessage(messagePattern)
                .Which.Should().BeOfType(ServerExceptionType);
        }

        #endregion

        #region Models

        protected class Person
        {
            public int ID { get; set; }
            public int GroupID { get; set; }
            public string Name { get; set; }
            public int? Age { get; set; }
        }

        protected class Membership
        {
            public int GroupID { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
        }

        #endregion
    }
}
