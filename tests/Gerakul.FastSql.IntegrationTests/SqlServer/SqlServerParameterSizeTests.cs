using System;
using System.Linq;
using AwesomeAssertions;
using Gerakul.FastSql.Common;
using Gerakul.FastSql.IntegrationTests.Infrastructure;
using Gerakul.FastSql.Tests.Shared;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Gerakul.FastSql.IntegrationTests.SqlServer
{
    // I6: FastSql declares string and binary parameters with one size for all values that fit in it, so the server
    // keeps one plan for a statement. The driver alone declares the length of each value, which gives a plan per
    // length. The sizes themselves are pinned by the unit tests (SqlParameterSizeTests).
    [Trait("Provider", "SqlServer")]
    public class SqlServerParameterSizeTests : IClassFixture<SqlServerDatabase>
    {
        private readonly SqlServerDatabase database;

        public SqlServerParameterSizeTests(SqlServerDatabase database)
        {
            this.database = database;
        }

        private DbContext Context => database.Spec.DefaultProvider.CreateContext(database.ConnectionString);

        // The declarations of the cached plans of the statements that contain the marker, for example "(@p0 nvarchar(4000))".
        // The server keeps a parameterized statement as its declarations followed by the statement
        private string[] CachedDeclarations(string marker, string statementStart = "select")
        {
            using (var connection = (SqlConnection)database.OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "select st.text from sys.dm_exec_cached_plans cp cross apply sys.dm_exec_sql_text(cp.plan_handle) st "
                    + "where st.text like @marker and st.text not like '%dm_exec_cached_plans%'";
                command.Parameters.AddWithValue("@marker", "%" + marker + "%");

                using (var reader = command.ExecuteReader())
                {
                    var texts = new System.Collections.Generic.List<string>();
                    while (reader.Read())
                    {
                        string text = reader.GetString(0);
                        texts.Add(text.Substring(0, text.IndexOf(")" + statementStart, StringComparison.Ordinal) + 1));
                    }

                    return texts.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                }
            }
        }

        private static string NewMarker()
        {
            return "marker_" + Guid.NewGuid().ToString("N");
        }

        [Fact]
        public void CreateSimple_StringsOfDifferentLengths_ShareOnePlan()
        {
            var marker = NewMarker();
            var table = database.CreateScratchTable("ID int identity primary key, Name nvarchar(100) null");
            database.Execute($"insert into {table} (Name) values ('a'), ('bbb'), ('ccccc')");

            var counts = new[] { "a", "bbb", "ccccc", new string('x', 50), "" }
                .Select(x => Context.CreateSimple($"select count(*) /*{marker}*/ from {table} where Name = @p0", x).ExecuteScalar())
                .ToArray();

            counts.Should().Equal(1, 1, 1, 0, 0);
            CachedDeclarations(marker).Should().Equal("(@p0 nvarchar(4000))");
        }

        // what the change avoids: the driver's own declarations, a plan per length
        [Fact]
        [Trait("UpgradeSensitive", "the driver declares a string parameter with the length of its value")]
        public void DriverAlone_StringsOfDifferentLengths_GetAPlanEach()
        {
            var marker = NewMarker();
            var table = database.CreateScratchTable("ID int identity primary key, Name nvarchar(100) null");

            foreach (var value in new[] { "a", "bbb", "ccccc" })
            {
                using (var connection = (SqlConnection)database.OpenConnection())
                using (var command = new SqlCommand($"select count(*) /*{marker}*/ from {table} where Name = @p0", connection))
                {
                    command.Parameters.AddWithValue("@p0", value);
                    command.ExecuteScalar();
                }
            }

            CachedDeclarations(marker).Should().Equal("(@p0 nvarchar(1))", "(@p0 nvarchar(3))", "(@p0 nvarchar(5))");
        }

        [Fact]
        public void Insert_StringsAndByteArraysOfDifferentLengths_ShareOnePlanAndKeepTheirValues()
        {
            var table = database.CreateScratchTable("ID int not null, Name nvarchar(max) null, Data varbinary(max) null");
            var rows = new[]
            {
                new Document() { ID = 1, Name = "a", Data = new byte[] { 1 } },
                new Document() { ID = 2, Name = "abc", Data = new byte[] { 1, 2, 3 } },
                new Document() { ID = 3, Name = new string('x', 4000), Data = new byte[8000] },
                new Document() { ID = 4, Name = null, Data = null },
                new Document() { ID = 5, Name = new string('y', 4001), Data = new byte[8001] }
            };

            foreach (var row in rows)
            {
                Context.Insert(table, row);
            }

            var read = Context.CreateSimple($"select * from {table} order by ID").ExecuteQuery<Document>().ToArray();
            read.Should().BeEquivalentTo(rows, o => o.WithStrictOrdering());

            // rows 1 to 4 share a plan: the driver declares a null string and a null array with these sizes as well;
            // the values of row 5 do not fit and are sent as max
            CachedDeclarations($"insert into {table} ", "insert").Should().Equal(
                "(@ID int,@Name nvarchar(4000),@Data varbinary(8000))",
                "(@ID int,@Name nvarchar(max) ,@Data varbinary(max) )");
        }

        public class Document
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public byte[] Data { get; set; }
        }
    }
}
