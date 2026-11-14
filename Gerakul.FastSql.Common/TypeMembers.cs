using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Gerakul.FastSql.Common;

// What FastSql needs to know about a type is found once and kept: the members it maps (per FromTypeOption and
// direction), the lookups of their names, the accessors of their values. Static generic classes hold it, so
// finding it costs no lookup, and nothing outlives a collectible type
internal static class TypeMembers<T>
{
    // FromTypeOption has three flags, which gives eight combinations
    private static readonly ReadMembers?[] forReading = new ReadMembers?[8];
    private static readonly WriteMembers<T>?[] forWriting = new WriteMembers<T>?[8];
    private static ConstructorMembers? constructor;

    // threads that come here at the same time build equal objects, and the one stored last stays
    public static ReadMembers ForReading(FromTypeOption fromTypeOption)
    {
        return forReading[(int)fromTypeOption & 7] ??= new ReadMembers(typeof(T), fromTypeOption);
    }

    public static WriteMembers<T> ForWriting(FromTypeOption fromTypeOption)
    {
        return forWriting[(int)fromTypeOption & 7] ??= new WriteMembers<T>(fromTypeOption);
    }

    public static ConstructorMembers Constructor => constructor ??= new ConstructorMembers(typeof(T));
}

// finds a member by its name, exactly or regardless of case
internal sealed class NameLookup
{
    private readonly Dictionary<string, int> exact;
    private readonly Dictionary<string, int> ignoringCase;
    private readonly bool exactUnique = true;
    private readonly bool ignoringCaseUnique = true;

    public NameLookup(string?[] names)
    {
        this.exact = new Dictionary<string, int>(names.Length, StringComparer.Ordinal);
        this.ignoringCase = new Dictionary<string, int>(names.Length, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < names.Length; i++)
        {
            // a member without a name matches nothing
            if (names[i] is string name)
            {
                this.exactUnique &= this.exact.TryAdd(name, i);
                this.ignoringCaseUnique &= this.ignoringCase.TryAdd(name, i);
            }
        }
    }

    // with duplicates a name does not say which member is meant
    public bool HasDuplicates(bool caseSensitive)
    {
        return caseSensitive ? !exactUnique : !ignoringCaseUnique;
    }

    public bool TryFind(string? name, bool caseSensitive, out int index)
    {
        if (name == null)
        {
            index = -1;
            return false;
        }

        return (caseSensitive ? exact : ignoringCase).TryGetValue(name, out index);
    }
}

// a field or property that a value of a reader is assigned to
internal sealed class ReadMember
{
    private readonly ValueSource source;

    public MemberInfo Member { get; }
    public string Name => Member.Name;

    // goes through reflection until the type has been read a lot, then it is compiled (see ReadMembers.CountRow)
    public Action<object, object?> Setter;

    public ReadMember(FieldInfo field)
    {
        this.Member = field;
        this.source = ValueSource.For(-1, field.FieldType);
        this.Setter = field.SetValue;
    }

    public ReadMember(PropertyInfo property)
    {
        this.Member = property;
        this.source = ValueSource.For(-1, property.PropertyType);

        // an exception of the setter reaches the caller as it is, not inside a TargetInvocationException
        this.Setter = (obj, value) => property.SetValue(obj, value, BindingFlags.DoNotWrapExceptions, null, null, null);
    }

    public ValueSource SourceAt(int ordinal)
    {
        return source with { Ordinal = ordinal };
    }
}

// the members of a type that the fields of a reader are mapped to, for one FromTypeOption
internal sealed class ReadMembers
{
    private readonly Type type;
    private int rowsUntilFast = FastAccessors.RowsBefore;
    private bool fast;

    // fields first, then properties: the order the values are assigned in
    public ReadMember[] All { get; }
    public NameLookup Names { get; }

    public ReadMembers(Type type, FromTypeOption fromTypeOption)
    {
        this.type = type;

        Helpers.GetMembers(type, fromTypeOption, true, out FieldInfo[] fields, out PropertyInfo[] properties);

        this.All = [.. fields.Select(x => new ReadMember(x)), .. properties.Select(x => new ReadMember(x))];
        this.Names = new NameLookup(this.All.Select(x => x.Name).ToArray());
    }

    // Called for every row that is read into the type. When several threads read the type the count is not
    // exact, which only moves the moment the setters are compiled
    public void CountRow()
    {
        if (rowsUntilFast > 0 && --rowsUntilFast == 0 && !fast)
        {
            fast = true;

            foreach (var member in All)
            {
                member.Setter = FastAccessors.CompileSetter(type, member.Member) ?? member.Setter;
            }
        }
    }
}

// a constructor parameter that a value of a reader is passed to
internal readonly record struct ReadParameter(string? Name, int Position, ValueSource Source)
{
    public ValueSource SourceAt(int ordinal)
    {
        return Source with { Ordinal = ordinal };
    }
}

// the constructor that objects without settable members are created with (anonymous types), and its parameters
internal sealed class ConstructorMembers
{
    private int rowsUntilFast = FastAccessors.RowsBefore;
    private bool fast;

    // null when the type has no public constructor
    public ConstructorInfo? Constructor { get; }
    public ReadParameter[] Parameters { get; }
    public NameLookup Names { get; }

    // goes through reflection until the type has been read a lot, then it is compiled (see CountRow)
    public Func<object?[], object> Create;

    public ConstructorMembers(Type type)
    {
        var constructor = type.GetConstructors().FirstOrDefault();

        this.Constructor = constructor;
        this.Parameters = constructor == null ? []
            : constructor.GetParameters().Select(x => new ReadParameter(x.Name, x.Position, ValueSource.For(-1, x.ParameterType))).ToArray();
        this.Names = new NameLookup(this.Parameters.Select(x => x.Name).ToArray());

        // an exception of the constructor reaches the caller as it is, not inside a TargetInvocationException
        this.Create = args => constructor!.Invoke(BindingFlags.DoNotWrapExceptions, null, args, null);
    }

    // as ReadMembers.CountRow
    public void CountRow()
    {
        if (rowsUntilFast > 0 && --rowsUntilFast == 0 && !fast)
        {
            fast = true;
            Create = FastAccessors.CompileConstructor(Constructor!) ?? Create;
        }
    }
}

// the members of a type that values are taken from (parameters, bulk copy), for one FromTypeOption
internal sealed class WriteMembers<T>
{
    private readonly MemberInfo[] members;
    private int rowsUntilFast = FastAccessors.RowsBefore;

    // Shared by all callers, so it must not be changed. The getters go through reflection until a bulk copy has
    // taken many rows of the type, then they are compiled (see CountRow)
    public FieldSettings<T>[] Settings { get; private set; }

    // the getters are compiled, or can't be
    public bool IsFast { get; private set; }

    public WriteMembers(FromTypeOption fromTypeOption)
    {
        Helpers.GetMembers(typeof(T), fromTypeOption, false, out FieldInfo[] fields, out PropertyInfo[] properties);

        // an exception of a property getter reaches the caller as it is, not inside a TargetInvocationException
        this.members = [.. fields, .. properties];
        this.Settings =
        [
            .. fields.Select(x => new FieldSettings<T>(x.Name, x.FieldType, y => x.GetValue(y))),
            .. properties.Select(x => new FieldSettings<T>(x.Name, x.PropertyType,
                y => x.GetValue(y, BindingFlags.DoNotWrapExceptions, null, null, null)))
        ];
        this.IsFast = !FastAccessors.IsSupported;
    }

    // Called for every row a reader takes values from; true when the settings have their final getters.
    // As for reading, the count is not exact with several threads
    public bool CountRow()
    {
        if (!IsFast && rowsUntilFast > 0 && --rowsUntilFast == 0)
        {
            var settings = new FieldSettings<T>[Settings.Length];

            for (int i = 0; i < settings.Length; i++)
            {
                var getter = FastAccessors.CompileGetter<T>(members[i]);
                settings[i] = getter == null ? Settings[i] : new FieldSettings<T>(Settings[i].Name, Settings[i].FieldType, getter);
            }

            Settings = settings;
            IsFast = true;
        }

        return IsFast;
    }
}

// Accessors compiled from expression trees, for types that are used a lot. They do what the accessors that go
// through reflection do, several times faster for every value, but building one costs as much as several
// thousand calls through reflection
internal static class FastAccessors
{
    // rows of a type that go through reflection before its accessors are compiled
    public const int RowsBefore = 1000;

    private static readonly ConstructorInfo targetException = typeof(TargetException).GetConstructor([typeof(string)])!;

    // without a compiler for dynamic code, expression trees are interpreted, which is slower than reflection
    public static bool IsSupported => RuntimeFeature.IsDynamicCodeCompiled;

    // null when the setter can't be compiled (a readonly field, for example): reflection stays
    public static Action<object, object?>? CompileSetter(Type type, MemberInfo member)
    {
        if (!IsSupported)
        {
            return null;
        }

        try
        {
            var target = Expression.Parameter(typeof(object));
            var value = Expression.Parameter(typeof(object));

            // the object is boxed when the type is a struct, and the setter works on the box
            Expression instance = type.IsValueType ? Expression.Unbox(target, type) : Expression.Convert(target, type);
            Expression assign = member is PropertyInfo property
                ? Expression.Call(instance, property.GetSetMethod(true)!, FromObject(value, property.PropertyType))
                : Expression.Assign(Expression.Field(instance, (FieldInfo)member), FromObject(value, ((FieldInfo)member).FieldType));

            return Expression.Lambda<Action<object, object?>>(assign, target, value).Compile();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Func<T, object?>? CompileGetter<T>(MemberInfo member)
    {
        if (!IsSupported)
        {
            return null;
        }

        try
        {
            var item = Expression.Parameter(typeof(T));
            Expression body = Expression.Convert(Expression.MakeMemberAccess(item, member), typeof(object));

            if (!typeof(T).IsValueType)
            {
                // the exception reflection throws for a null object
                var message = member is FieldInfo ? "Non-static field requires a target." : "Non-static method requires a target.";
                body = Expression.Condition(
                    Expression.ReferenceEqual(item, Expression.Constant(null)),
                    Expression.Throw(Expression.New(targetException, Expression.Constant(message)), typeof(object)),
                    body);
            }

            return Expression.Lambda<Func<T, object?>>(body, item).Compile();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Func<object?[], object>? CompileConstructor(ConstructorInfo constructor)
    {
        if (!IsSupported)
        {
            return null;
        }

        try
        {
            var args = Expression.Parameter(typeof(object[]));
            var parameters = constructor.GetParameters()
                .Select((x, i) => FromObject(Expression.ArrayIndex(args, Expression.Constant(i)), x.ParameterType));

            return Expression.Lambda<Func<object?[], object>>(Expression.Convert(Expression.New(constructor, parameters), typeof(object)), args).Compile();
        }
        catch (Exception)
        {
            return null;
        }
    }

    // null for a value type that has no null gives its default value, as reflection does
    private static Expression FromObject(Expression value, Type type)
    {
        return type.IsValueType && Nullable.GetUnderlyingType(type) == null
            ? Expression.Condition(Expression.ReferenceEqual(value, Expression.Constant(null)), Expression.Default(type), Expression.Convert(value, type))
            : Expression.Convert(value, type);
    }
}
