using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Xunit;
using NpgsqlColumnDefinitionOptions = Gerakul.FastSql.PostgreSQL.ColumnDefinitionOptions;
using SqlColumnDefinitionOptions = Gerakul.FastSql.SqlServer.ColumnDefinitionOptions;

namespace Gerakul.FastSql.UnitTests.PostgreSQL
{
    // The PostgreSQL ColumnDefinitionOptions is a public copy of the SQL Server one that no public PostgreSQL API
    // accepts (Q10). The static Default, Max and Small instances are shared by the whole test run: these tests only
    // read them.
    public class ColumnDefinitionOptionsTests
    {
        private static NpgsqlColumnDefinitionOptions Static(string name)
        {
            switch (name)
            {
                case "Default":
                    return NpgsqlColumnDefinitionOptions.Default;
                case "Max":
                    return NpgsqlColumnDefinitionOptions.Max;
                case "Small":
                    return NpgsqlColumnDefinitionOptions.Small;
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown options");
            }
        }

        private static SqlColumnDefinitionOptions SqlServerStatic(string name)
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

        private static NpgsqlColumnDefinitionOptions From(string name, params string[] primaryKey)
        {
            switch (name)
            {
                case "Default":
                    return NpgsqlColumnDefinitionOptions.FromDefault(primaryKey);
                case "Max":
                    return NpgsqlColumnDefinitionOptions.FromMax(primaryKey);
                case "Small":
                    return NpgsqlColumnDefinitionOptions.FromSmall(primaryKey);
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown options");
            }
        }

        private static NpgsqlColumnDefinitionOptions Custom()
        {
            return new NpgsqlColumnDefinitionOptions()
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

        private static string[] PublicMemberNames(Type type)
        {
            return type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(x => x.MemberType + " " + x.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        private static bool IsVisible(MemberInfo member)
        {
            switch (member)
            {
                case MethodBase method:
                    return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
                case FieldInfo field:
                    return field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;
                case PropertyInfo property:
                    return property.GetAccessors(true).Any(IsVisible);
                default:
                    return false;
            }
        }

        private static IEnumerable<Type> SignatureTypes(MemberInfo member)
        {
            switch (member)
            {
                case MethodInfo method:
                    return method.GetParameters().Select(x => x.ParameterType).Append(method.ReturnType);
                case ConstructorInfo constructor:
                    return constructor.GetParameters().Select(x => x.ParameterType);
                case FieldInfo field:
                    return new[] { field.FieldType };
                case PropertyInfo property:
                    return new[] { property.PropertyType };
                default:
                    return new Type[0];
            }
        }

        private static bool Mentions(Type type, Type target)
        {
            if (type == target)
            {
                return true;
            }

            if (type.HasElementType)
            {
                return Mentions(type.GetElementType(), target);
            }

            return type.IsGenericType && type.GetGenericArguments().Any(x => Mentions(x, target));
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

        [Theory]
        [Trait("Quirk", "Q10")]
        [InlineData("Default")]
        [InlineData("Max")]
        [InlineData("Small")]
        public void StaticOptions_HaveSameValuesAsSqlServerCopy(string name)
        {
            Static(name).Should().BeEquivalentTo(SqlServerStatic(name));
        }

        [Fact]
        [Trait("Quirk", "Q10")]
        public void ColumnDefinitionOptions_HasSamePublicMembersAsSqlServerCopy()
        {
            PublicMemberNames(typeof(NpgsqlColumnDefinitionOptions)).Should().Equal(PublicMemberNames(typeof(SqlColumnDefinitionOptions)));
        }

        [Fact]
        [Trait("Quirk", "Q10")]
        public void ColumnDefinitionOptions_IsNotUsedByAnyOtherPublicMemberOfPostgreSqlAssembly()
        {
            var type = typeof(NpgsqlColumnDefinitionOptions);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            var usages = type.Assembly.GetExportedTypes()
                .Where(x => x != type)
                .SelectMany(x => x.GetMembers(flags).Where(m => IsVisible(m) && SignatureTypes(m).Any(t => Mentions(t, type)))
                    .Select(m => x.Name + "." + m.Name))
                .ToArray();

            usages.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_LeavesZeroValues()
        {
            var options = new NpgsqlColumnDefinitionOptions();

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
            new NpgsqlColumnDefinitionOptions().ThrowIfUnsupportedType.Should().BeFalse();
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
            new NpgsqlColumnDefinitionOptions().Clone().PrimaryKey.Should().BeNull();
        }

        [Fact]
        public void FromOption_SetsPrimaryKeyAndCopiesOtherValues()
        {
            var source = Custom();

            var options = NpgsqlColumnDefinitionOptions.FromOption(source, "CompanyID", "ID");

            options.Should().NotBeSameAs(source);
            options.PrimaryKey.Should().Equal("CompanyID", "ID");
            options.Should().BeEquivalentTo(source, o => o.Excluding(x => x.PrimaryKey));
            source.PrimaryKey.Should().Equal("ID");
        }

        [Fact]
        public void FromOption_NoKeys_SetsEmptyPrimaryKey()
        {
            NpgsqlColumnDefinitionOptions.FromOption(Custom()).PrimaryKey.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void FromOption_NullOption_ThrowsNullReferenceException()
        {
            Action act = () => NpgsqlColumnDefinitionOptions.FromOption(null, "ID");

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
