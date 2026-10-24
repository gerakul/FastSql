using System;
using AwesomeAssertions;
using Gerakul.FastSql.PostgreSQL;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    public class NpgsqlCommandTextGeneratorTests : CommandTextGeneratorTests<PostgreSqlSpec>
    {
        private readonly NpgsqlCommandTextGenerator generator = new NpgsqlCommandTextGenerator();

        private static string Lines(params string[] lines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        [Fact]
        public void Provider_CommandTextGenerator_IsNpgsqlCommandTextGenerator()
        {
            Generator.Should().BeOfType<NpgsqlCommandTextGenerator>();
        }

        [Theory]
        [InlineData(new string[0], "")]
        [InlineData(new[] { "A" }, "\"A\"")]
        [InlineData(new[] { "A", "B", "C" }, "\"A\", \"B\", \"C\"")]
        public void ColumnList_Fields_ReturnsQuotedColumns(string[] fields, string expected)
        {
            generator.ColumnList(fields).Should().Be(expected);
        }

        [Theory]
        [Trait("Quirk", "Q20")]
        [InlineData("a\"b", "\"a\"b\"")]
        [InlineData("\"A\"", "\"\"A\"\"")]
        public void ColumnList_QuoteCharacterInName_IsNotEscaped(string field, string expected)
        {
            generator.ColumnList(field).Should().Be(expected);
        }

        [Fact]
        [Trait("Quirk", "Q20")]
        public void WhereClause_QuoteCharacterInName_IsNotEscaped()
        {
            generator.WhereClause("a\"b").Should().Be("\"a\"b\" = @a\"b");
        }

        [Theory]
        [InlineData(new string[0], "")]
        [InlineData(new[] { "A" }, "\"A\" = @A")]
        [InlineData(new[] { "A", "B", "C" }, "\"A\" = @A and \"B\" = @B and \"C\" = @C")]
        public void WhereClause_Fields_ReturnsConditionsJoinedWithAnd(string[] fields, string expected)
        {
            generator.WhereClause(fields).Should().Be(expected);
        }

        [Theory]
        [InlineData(new string[0], "")]
        [InlineData(new[] { "A" }, "\"A\" = @A")]
        [InlineData(new[] { "A", "B", "C" }, "\"A\" = @A, \"B\" = @B, \"C\" = @C")]
        public void SetClause_Fields_ReturnsCommaSeparatedAssignments(string[] fields, string expected)
        {
            generator.SetClause(fields).Should().Be(expected);
        }

        [Fact]
        public void Insert_Fields_ReturnsInsertStatement()
        {
            generator.Insert("public.\"Employee\"", "CompanyID", "Name").Should()
                .Be("insert into public.\"Employee\" (\"CompanyID\", \"Name\") values (@CompanyID, @Name);");
        }

        [Fact]
        [Trait("Quirk", "Q19")]
        public void Insert_UnquotedTableName_IsNotQuotedWhileColumnsAre()
        {
            generator.Insert("Employee", "CompanyID", "Name").Should()
                .Be("insert into Employee (\"CompanyID\", \"Name\") values (@CompanyID, @Name);");
        }

        [Fact]
        [Trait("Quirk", "Q19")]
        public void Update_UnquotedTableName_IsNotQuotedWhileColumnsAre()
        {
            generator.Update("Employee", new[] { "ID" }, "Name").Should()
                .Be("update Employee set \"Name\" = @Name where \"ID\" = @ID;");
        }

        [Fact]
        public void Insert_NoFields_ReturnsEmptyLists()
        {
            generator.Insert("Employee").Should().Be("insert into Employee () values ();");
        }

        [Fact]
        public void InsertWithOutput_OneOutputField_AppendsReturningClause()
        {
            generator.InsertWithOutput("Employee", new[] { "CompanyID", "Name" }, "ID").Should()
                .Be("insert into Employee (\"CompanyID\", \"Name\") values (@CompanyID, @Name) returning \"ID\";");
        }

        [Fact]
        public void InsertWithOutput_SeveralOutputFields_ListsReturnedColumns()
        {
            generator.InsertWithOutput("Employee", new[] { "Name" }, "ID", "Name").Should()
                .Be("insert into Employee (\"Name\") values (@Name) returning \"ID\", \"Name\";");
        }

        [Fact]
        public void InsertWithOutput_NoOutputFields_ProducesEmptyReturningClause()
        {
            generator.InsertWithOutput("Employee", new[] { "Name" }).Should()
                .Be("insert into Employee (\"Name\") values (@Name) returning ;");
        }

        [Fact]
        public void Update_WhereClauseOverload_ReturnsUpdateStatement()
        {
            generator.Update("Employee", "\"ID\" = @ID", "Name", "Age").Should()
                .Be("update Employee set \"Name\" = @Name, \"Age\" = @Age where \"ID\" = @ID;");
        }

        [Fact]
        public void Update_CompositeKey_JoinsKeysWithAnd()
        {
            generator.Update("Employee", new[] { "CompanyID", "ID" }, "Name").Should()
                .Be("update Employee set \"Name\" = @Name where \"CompanyID\" = @CompanyID and \"ID\" = @ID;");
        }

        [Fact]
        [Trait("Quirk", "Q09")]
        public void Update_NoFields_ProducesEmptySetClause()
        {
            generator.Update("Employee", new[] { "ID" }).Should().Be("update Employee set  where \"ID\" = @ID;");
        }

        [Fact]
        public void Delete_KeyFields_ReturnsDeleteStatement()
        {
            generator.Delete("Employee", new[] { "CompanyID", "ID" }).Should()
                .Be("delete from Employee where \"CompanyID\" = @CompanyID and \"ID\" = @ID;");
        }

        [Fact]
        public void Merge_SingleKey_ReturnsInsertOnConflictStatement()
        {
            var expected = Lines(
                "insert into Employee (\"ID\", \"Name\", \"Age\") ",
                "values (@ID, @Name, @Age) ",
                "on conflict (\"ID\") ",
                "do update set \"Name\" = excluded.\"Name\", \"Age\" = excluded.\"Age\";");

            generator.Merge("Employee", new[] { "ID" }, "Name", "Age").Should().Be(expected);
        }

        [Fact]
        public void Merge_CompositeKey_ListsKeysInConflictTarget()
        {
            var expected = Lines(
                "insert into public.\"Employee\" (\"CompanyID\", \"ID\", \"Name\") ",
                "values (@CompanyID, @ID, @Name) ",
                "on conflict (\"CompanyID\", \"ID\") ",
                "do update set \"Name\" = excluded.\"Name\";");

            generator.Merge("public.\"Employee\"", new[] { "CompanyID", "ID" }, "Name").Should().Be(expected);
        }

        [Fact]
        public void Merge_KeyRepeatedInFields_IsDeduplicatedExceptInSetClause()
        {
            var expected = Lines(
                "insert into Employee (\"ID\", \"Name\") ",
                "values (@ID, @Name) ",
                "on conflict (\"ID\") ",
                "do update set \"ID\" = excluded.\"ID\", \"Name\" = excluded.\"Name\";");

            generator.Merge("Employee", new[] { "ID" }, "ID", "Name").Should().Be(expected);
        }

        [Fact]
        [Trait("Quirk", "Q09")]
        public void Merge_NoNonKeyFields_ProducesEmptySetClause()
        {
            var expected = Lines(
                "insert into Employee (\"ID\") ",
                "values (@ID) ",
                "on conflict (\"ID\") ",
                "do update set ;");

            generator.Merge("Employee", new[] { "ID" }).Should().Be(expected);
        }

        [Fact]
        public void Merge_Lines_AreSeparatedByEnvironmentNewLine()
        {
            var text = generator.Merge("Employee", new[] { "ID" }, "Name");

            text.Split(Environment.NewLine).Should().HaveCount(4);
        }
    }
}
