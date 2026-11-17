using System;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.SqlServer;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    // I8: the message of a table that cannot be created names what is wrong with the field list. The script is
    // built before anything runs, so a context over a connection that is never opened is enough
    public class SqlCreateTableMessageTests
    {
        public class Person
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public int? Age { get; set; }
        }

        private static readonly Person[] persons = { new Person() { ID = 1, Name = "Alice" } };

        private static Action WriteWithFields(params string[] fields)
        {
            var context = SqlContextProvider.DefaultInstance.CreateContext(new SqlConnection());
            var options = new SqlBulkOptions(createTable: true, ignoreDataReaderSchemaTable: true);

            return () => persons.ToDataReader().WriteToServer(context, "NewTable", options, fields);
        }

        [Fact]
        public void FieldNotInSource_IsNamed()
        {
            WriteWithFields("ID", "Missing").Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot create table NewTable: the source has no field 'Missing'");
        }

        // the list is compared with the source's names case-sensitively (Q130); the message says so
        [Fact]
        [Trait("Quirk", "Q130")]
        public void FieldThatDiffersOnlyInCase_SaysThatTheListIsComparedWithCase()
        {
            WriteWithFields("id", "Name").Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot create table NewTable: the source has no field 'id' (it has 'ID': the field list is compared with case)");
        }

        // the reasons used to be empty for a field listed twice
        [Fact]
        public void FieldListedTwice_IsNamed()
        {
            WriteWithFields("ID", "Name", "ID").Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot create table NewTable: the field list names 'ID' more than once");
        }

        [Fact]
        public void SeveralProblems_AreAllNamed()
        {
            WriteWithFields("ID", "ID", "Missing").Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot create table NewTable: the source has no field 'Missing'; the field list names 'ID' more than once");
        }
    }
}
