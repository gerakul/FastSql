using System.Collections.Generic;
using AwesomeAssertions;
using Gerakul.FastSql.Tests.Shared;
using Xunit;

namespace Gerakul.FastSql.UnitTests.Common
{
    // Text generator behavior with the same shape on both providers. The exact provider texts are in
    // SqlServer/SqlCommandTextGeneratorTests and PostgreSQL/NpgsqlCommandTextGeneratorTests.
    public abstract class CommandTextGeneratorTests<TSpec> where TSpec : ProviderSpec, new()
    {
        protected TSpec Spec { get; } = new TSpec();

        protected CommandTextGenerator Generator => Spec.CreateProvider().CommandTextGenerator;

        public static TheoryData<string> TableNames => new TheoryData<string>()
        {
            "Employee",
            "dbo.Employee",
            "[dbo].[Employee]",
            "\"Employee\"",
            "public.\"Employee\"",
            "Db.dbo.Employee",
            "#Employee"
        };

        [Theory]
        [InlineData(new string[0], "")]
        [InlineData(new[] { "A" }, "@A")]
        [InlineData(new[] { "A", "B", "C" }, "@A, @B, @C")]
        public void ParamList_Fields_ReturnsCommaSeparatedParameters(string[] fields, string expected)
        {
            Generator.ParamList(fields).Should().Be(expected);
        }

        [Fact]
        public void Update_KeyFieldsOverload_UsesWhereClauseOfKeys()
        {
            var keys = new List<string>() { "CompanyID", "ID" };

            var text = Generator.Update("Employee", keys, "Name", "Age");

            text.Should().Be(Generator.Update("Employee", Generator.WhereClause("CompanyID", "ID"), "Name", "Age"));
            text.Should().Be($"update Employee set {Spec.Quote("Name")} = @Name, {Spec.Quote("Age")} = @Age "
                + $"where {Spec.Quote("CompanyID")} = @CompanyID and {Spec.Quote("ID")} = @ID;");
        }

        [Fact]
        public void Delete_KeyFieldsOverload_UsesWhereClauseOfKeys()
        {
            var text = Generator.Delete("Employee", "CompanyID", "ID");

            text.Should().Be(Generator.Delete("Employee", Generator.WhereClause("CompanyID", "ID")));
            text.Should().Be($"delete from Employee where {Spec.Quote("CompanyID")} = @CompanyID and {Spec.Quote("ID")} = @ID;");
        }

        [Fact]
        [Trait("Quirk", "Q07")]
        public void Delete_SingleKeyAsString_BindsToWhereClauseOverload()
        {
            Generator.Delete("Employee", "ID").Should().Be("delete from Employee where ID;");
        }

        [Fact]
        public void Delete_SingleKeyAsArray_UsesKeyFieldsOverload()
        {
            Generator.Delete("Employee", new[] { "ID" }).Should().Be($"delete from Employee where {Spec.Quote("ID")} = @ID;");
        }

        [Fact]
        public void Delete_NoKeyFields_ProducesEmptyWhereClause()
        {
            Generator.Delete("Employee").Should().Be("delete from Employee where ;");
        }

        [Fact]
        public void Delete_WhereClause_IsUsedVerbatim()
        {
            Generator.Delete("Employee", "Age > 40 or Name is null").Should().Be("delete from Employee where Age > 40 or Name is null;");
        }

        [Fact]
        public void Update_WhereClause_IsUsedVerbatim()
        {
            Generator.Update("Employee", "Age > 40", "Phone").Should().Be($"update Employee set {Spec.Quote("Phone")} = @Phone where Age > 40;");
        }

        [Theory]
        [MemberData(nameof(TableNames))]
        public void Insert_TableName_IsUsedVerbatim(string tableName)
        {
            Generator.Insert(tableName, "Name").Should().Be($"insert into {tableName} ({Spec.Quote("Name")}) values (@Name);");
        }

        [Theory]
        [MemberData(nameof(TableNames))]
        public void Update_TableName_IsUsedVerbatim(string tableName)
        {
            Generator.Update(tableName, new[] { "ID" }, "Name").Should()
                .Be($"update {tableName} set {Spec.Quote("Name")} = @Name where {Spec.Quote("ID")} = @ID;");
        }

        [Theory]
        [MemberData(nameof(TableNames))]
        public void Delete_TableName_IsUsedVerbatim(string tableName)
        {
            Generator.Delete(tableName, new[] { "ID" }).Should().Be($"delete from {tableName} where {Spec.Quote("ID")} = @ID;");
        }

        [Theory]
        [MemberData(nameof(TableNames))]
        public void InsertWithOutput_TableName_IsUsedVerbatim(string tableName)
        {
            Generator.InsertWithOutput(tableName, new[] { "Name" }, "ID").Should().StartWith($"insert into {tableName} (");
        }

        [Theory]
        [MemberData(nameof(TableNames))]
        public void Merge_TableName_IsUsedVerbatim(string tableName)
        {
            Generator.Merge(tableName, new[] { "ID" }, "Name").Should().Contain($" {tableName} ");
        }

        [Fact]
        [Trait("Quirk", "Q30")]
        public void Insert_FieldNameThatNeedsQuoting_IsQuotedAsColumnButNotAsParameter()
        {
            Generator.Insert("Employee", "First Name").Should()
                .Be($"insert into Employee ({Spec.Quote("First Name")}) values (@First Name);");
        }

        [Fact]
        [Trait("Quirk", "Q30")]
        public void WhereClause_FieldNameThatNeedsQuoting_IsQuotedAsColumnButNotAsParameter()
        {
            Generator.WhereClause("First Name").Should().Be($"{Spec.Quote("First Name")} = @First Name");
        }
    }
}
