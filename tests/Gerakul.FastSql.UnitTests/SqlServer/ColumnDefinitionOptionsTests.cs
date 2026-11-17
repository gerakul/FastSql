using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Xunit;
using SqlColumnDefinitionOptions = Gerakul.FastSql.SqlServer.ColumnDefinitionOptions;

namespace Gerakul.FastSql.UnitTests.SqlServer
{
    // The static Default, Max and Small instances are shared by the whole test run: these tests only read them
    public class ColumnDefinitionOptionsTests
    {
        private static SqlColumnDefinitionOptions Static(string name)
        {
            switch (name)
            {
                case "Default":
                    return SqlColumnDefinitionOptions.Default;
                case "Max":
                    return SqlColumnDefinitionOptions.Max;
                case "Small":
                    return SqlColumnDefinitionOptions.Small;
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown options");
            }
        }

        private static SqlColumnDefinitionOptions From(string name, params string[] primaryKey)
        {
            switch (name)
            {
                case "Default":
                    return SqlColumnDefinitionOptions.FromDefault(primaryKey);
                case "Max":
                    return SqlColumnDefinitionOptions.FromMax(primaryKey);
                case "Small":
                    return SqlColumnDefinitionOptions.FromSmall(primaryKey);
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown options");
            }
        }

        private static SqlColumnDefinitionOptions Custom()
        {
            return new SqlColumnDefinitionOptions()
            {
                PrimaryKey = new List<string>() { "ID" },
                StringMaxLength = 100,
                BytesMaxLength = 200,
                Precision = 10,
                Scale = 2,
                DateTimeScale = 3,
                ThrowIfUnsupportedType = true
            };
        }

        [Theory]
        [InlineData("Default", 4000, 8000, 38, 18, 7)]
        [InlineData("Max", -1, -1, 38, 18, 7)]
        [InlineData("Small", 255, 255, 19, 9, 3)]
        public void StaticOptions_HaveExpectedValues(string name, int stringMaxLength, int bytesMaxLength, int precision, int scale,
            int dateTimeScale)
        {
            var options = Static(name);

            options.StringMaxLength.Should().Be((short)stringMaxLength);
            options.BytesMaxLength.Should().Be((short)bytesMaxLength);
            options.Precision.Should().Be((byte)precision);
            options.Scale.Should().Be((byte)scale);
            options.DateTimeScale.Should().Be((byte)dateTimeScale);
            options.PrimaryKey.Should().BeNull();
            options.ThrowIfUnsupportedType.Should().BeFalse();
        }

        [Fact]
        public void Constructor_LeavesZeroValues()
        {
            var options = new SqlColumnDefinitionOptions();

            options.PrimaryKey.Should().BeNull();
            options.StringMaxLength.Should().Be(0);
            options.BytesMaxLength.Should().Be(0);
            options.Precision.Should().Be(0);
            options.Scale.Should().Be(0);
            options.DateTimeScale.Should().Be(0);
        }

        [Fact]
        public void ThrowIfUnsupportedType_IsFalseByDefault()
        {
            new SqlColumnDefinitionOptions().ThrowIfUnsupportedType.Should().BeFalse();
        }

        [Fact]
        public void Clone_CopiesEveryProperty()
        {
            var options = Custom();

            var clone = options.Clone();

            clone.Should().NotBeSameAs(options);
            clone.Should().BeEquivalentTo(options);
        }

        [Fact]
        public void Clone_CopiesPrimaryKeyList()
        {
            var options = Custom();

            var clone = options.Clone();
            clone.PrimaryKey.Add("Name");

            clone.PrimaryKey.Should().NotBeSameAs(options.PrimaryKey);
            options.PrimaryKey.Should().Equal("ID");
        }

        [Fact]
        public void Clone_NullPrimaryKey_StaysNull()
        {
            new SqlColumnDefinitionOptions().Clone().PrimaryKey.Should().BeNull();
        }

        [Fact]
        public void FromOption_SetsPrimaryKeyAndCopiesOtherValues()
        {
            var source = Custom();

            var options = SqlColumnDefinitionOptions.FromOption(source, "CompanyID", "ID");

            options.Should().NotBeSameAs(source);
            options.PrimaryKey.Should().Equal("CompanyID", "ID");
            options.Should().BeEquivalentTo(source, o => o.Excluding(x => x.PrimaryKey));
            source.PrimaryKey.Should().Equal("ID");
        }

        [Fact]
        public void FromOption_NoKeys_SetsEmptyPrimaryKey()
        {
            SqlColumnDefinitionOptions.FromOption(Custom()).PrimaryKey.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void FromOption_NullOption_ThrowsNullReferenceException()
        {
            Action act = () => SqlColumnDefinitionOptions.FromOption(null, "ID");

            act.Should().Throw<NullReferenceException>().WithMessage("Object reference not set to an instance of an object.");
        }

        [Theory]
        [InlineData("Default")]
        [InlineData("Max")]
        [InlineData("Small")]
        public void FromStaticOptions_SetPrimaryKeyAndKeepStaticSourceUnchanged(string name)
        {
            var source = Static(name);

            var options = From(name, "ID");

            options.Should().NotBeSameAs(source);
            options.PrimaryKey.Should().Equal("ID");
            options.Should().BeEquivalentTo(source, o => o.Excluding(x => x.PrimaryKey));
            source.PrimaryKey.Should().BeNull();
        }
    }
}
