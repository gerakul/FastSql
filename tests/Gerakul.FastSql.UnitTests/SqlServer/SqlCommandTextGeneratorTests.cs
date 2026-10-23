using System;
using AwesomeAssertions;
using Gerakul.FastSql.SqlServer;
using Gerakul.FastSql.Tests.Shared;
using Gerakul.FastSql.UnitTests.Common;
using Xunit;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    public class SqlCommandTextGeneratorTests : CommandTextGeneratorTests<SqlServerSpec>
    {
        private readonly SqlCommandTextGenerator generator = new SqlCommandTextGenerator();

        private static string Lines(params string[] lines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        [Fact]
        public void Provider_CommandTextGenerator_IsSqlCommandTextGenerator()
        {
            Generator.Should().BeOfType<SqlCommandTextGenerator>();
        }

        [Theory]
        [InlineData(new string[0], "")]
        [InlineData(new[] { "A" }, "[A]")]
        [InlineData(new[] { "A", "B", "C" }, "[A], [B], [C]")]
        public void ColumnList_Fields_ReturnsBracketedColumns(string[] fields, string expected)
        {
            generator.ColumnList(fields).Should().Be(expected);
        }

        [Theory]
        [Trait("Quirk", "Q20")]
        [InlineData("a]b", "[a]b]")]
        [InlineData("[A]", "[[A]]")]
        public void ColumnList_QuoteCharacterInName_IsNotEscaped(string field, string expected)
        {
            generator.ColumnList(field).Should().Be(expected);
        }

        [Fact]
        [Trait("Quirk", "Q20")]
        public void WhereClause_QuoteCharacterInName_IsNotEscaped()
        {
            generator.WhereClause("a]b").Should().Be("[a]b] = @a]b");
        }

        [Theory]
        [InlineData(new string[0], "")]
        [InlineData(new[] { "A" }, "[A] = @A")]
        [InlineData(new[] { "A", "B", "C" }, "[A] = @A and [B] = @B and [C] = @C")]
        public void WhereClause_Fields_ReturnsConditionsJoinedWithAnd(string[] fields, string expected)
        {
            generator.WhereClause(fields).Should().Be(expected);
        }

        [Theory]
        [InlineData(new string[0], "")]
        [InlineData(new[] { "A" }, "[A] = @A")]
        [InlineData(new[] { "A", "B", "C" }, "[A] = @A, [B] = @B, [C] = @C")]
        public void SetClause_Fields_ReturnsCommaSeparatedAssignments(string[] fields, string expected)
        {
            generator.SetClause(fields).Should().Be(expected);
        }

        [Fact]
        public void Insert_Fields_ReturnsInsertStatement()
        {
            generator.Insert("Employee", "CompanyID", "Name").Should()
                .Be("insert into Employee ([CompanyID], [Name]) values (@CompanyID, @Name);");
        }

        [Fact]
        public void Insert_NoFields_ReturnsEmptyLists()
        {
            generator.Insert("Employee").Should().Be("insert into Employee () values ();");
        }

        [Fact]
        public void InsertWithOutput_OneOutputField_PutsOutputClauseBeforeValues()
        {
            generator.InsertWithOutput("dbo.Employee", new[] { "CompanyID", "Name" }, "ID").Should()
                .Be("insert into dbo.Employee ([CompanyID], [Name]) output inserted.[ID] values (@CompanyID, @Name);");
        }

        [Fact]
        public void InsertWithOutput_SeveralOutputFields_ListsInsertedColumns()
        {
            generator.InsertWithOutput("Employee", new[] { "Name" }, "ID", "Name").Should()
                .Be("insert into Employee ([Name]) output inserted.[ID], inserted.[Name] values (@Name);");
        }

        [Fact]
        public void InsertWithOutput_NoOutputFields_ProducesEmptyOutputClause()
        {
            generator.InsertWithOutput("Employee", new[] { "Name" }).Should()
                .Be("insert into Employee ([Name]) output  values (@Name);");
        }

        [Fact]
        public void InsertAndGetID_Fields_AppendsScopeIdentity()
        {
            generator.InsertAndGetID("Employee", "CompanyID", "Name").Should()
                .Be("insert into Employee ([CompanyID], [Name]) values (@CompanyID, @Name); select scope_identity();");
        }

        [Fact]
        public void InsertAndGetID_IsInsertFollowedBySelect()
        {
            generator.InsertAndGetID("Employee", "Name").Should().Be(generator.Insert("Employee", "Name") + " select scope_identity();");
        }

        [Fact]
        public void Update_WhereClauseOverload_ReturnsUpdateStatement()
        {
            generator.Update("Employee", "[ID] = @ID", "Name", "Age").Should()
                .Be("update Employee set [Name] = @Name, [Age] = @Age where [ID] = @ID;");
        }

        [Fact]
        public void Update_SingleKey_ReturnsUpdateStatement()
        {
            generator.Update("Employee", new[] { "ID" }, "Name", "Age").Should()
                .Be("update Employee set [Name] = @Name, [Age] = @Age where [ID] = @ID;");
        }

        [Fact]
        public void Update_CompositeKey_JoinsKeysWithAnd()
        {
            generator.Update("Employee", new[] { "CompanyID", "ID" }, "Name").Should()
                .Be("update Employee set [Name] = @Name where [CompanyID] = @CompanyID and [ID] = @ID;");
        }

        [Fact]
        [Trait("Quirk", "Q09")]
        public void Update_NoFields_ProducesEmptySetClause()
        {
            generator.Update("Employee", new[] { "ID" }).Should().Be("update Employee set  where [ID] = @ID;");
        }

        [Fact]
        public void Delete_KeyFields_ReturnsDeleteStatement()
        {
            generator.Delete("Employee", new[] { "CompanyID", "ID" }).Should()
                .Be("delete from Employee where [CompanyID] = @CompanyID and [ID] = @ID;");
        }

        [Fact]
        public void Merge_SingleKey_ReturnsMergeStatement()
        {
            var expected = Lines(
                "merge Employee as target",
                "using (select @ID, @Name, @Age)",
                "as source ([ID], [Name], [Age])",
                "on (target.[ID] = source.[ID])",
                "when matched then",
                "update set [Name] = source.[Name], [Age] = source.[Age]",
                "when not matched then",
                "insert ([ID], [Name], [Age])",
                "values (source.[ID], source.[Name], source.[Age]);");

            generator.Merge("Employee", new[] { "ID" }, "Name", "Age").Should().Be(expected);
        }

        [Fact]
        public void Merge_CompositeKey_JoinsKeyConditionsWithAnd()
        {
            var expected = Lines(
                "merge dbo.Employee as target",
                "using (select @CompanyID, @ID, @Name)",
                "as source ([CompanyID], [ID], [Name])",
                "on (target.[CompanyID] = source.[CompanyID] and target.[ID] = source.[ID])",
                "when matched then",
                "update set [Name] = source.[Name]",
                "when not matched then",
                "insert ([CompanyID], [ID], [Name])",
                "values (source.[CompanyID], source.[ID], source.[Name]);");

            generator.Merge("dbo.Employee", new[] { "CompanyID", "ID" }, "Name").Should().Be(expected);
        }

        [Fact]
        public void Merge_KeyRepeatedInFields_IsDeduplicatedExceptInSetClause()
        {
            var expected = Lines(
                "merge Employee as target",
                "using (select @ID, @Name)",
                "as source ([ID], [Name])",
                "on (target.[ID] = source.[ID])",
                "when matched then",
                "update set [ID] = source.[ID], [Name] = source.[Name]",
                "when not matched then",
                "insert ([ID], [Name])",
                "values (source.[ID], source.[Name]);");

            generator.Merge("Employee", new[] { "ID" }, "ID", "Name").Should().Be(expected);
        }

        [Fact]
        [Trait("Quirk", "Q09")]
        public void Merge_NoNonKeyFields_ProducesEmptySetClause()
        {
            var expected = Lines(
                "merge Employee as target",
                "using (select @ID)",
                "as source ([ID])",
                "on (target.[ID] = source.[ID])",
                "when matched then",
                "update set ",
                "when not matched then",
                "insert ([ID])",
                "values (source.[ID]);");

            generator.Merge("Employee", new[] { "ID" }).Should().Be(expected);
        }

        [Fact]
        public void Merge_Lines_AreSeparatedByEnvironmentNewLine()
        {
            var text = generator.Merge("Employee", new[] { "ID" }, "Name");

            text.Split(Environment.NewLine).Should().HaveCount(9);
        }
    }
}
