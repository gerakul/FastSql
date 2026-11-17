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
    private static ConstructorMembers? largestConstructor;

    // A type with a public constructor without parameters (every struct has one) is read by setting its members,
    // any other type through a constructor
    public static readonly bool HasParameterlessConstructor = typeof(T).IsValueType || typeof(T).GetConstructor(Type.EmptyTypes) != null;

    // threads that come here at the same time build equal objects, and the one stored last stays
    public static ReadMembers ForReading(FromTypeOption fromTypeOption)
    {
        return forReading[(int)fromTypeOption & 7] ??= new ReadMembers(typeof(T), fromTypeOption);
    }

    public static WriteMembers<T> ForWriting(FromTypeOption fromTypeOption)
    {
        return forWriting[(int)fromTypeOption & 7] ??= new WriteMembers<T>(fromTypeOption);
    }

    // the first public constructor: what ReadAllAnonymous has always used (an anonymous type has only one)
    public static ConstructorMembers Constructor => constructor ??= new ConstructorMembers(typeof(T).GetConstructors().FirstOrDefault());

    // the public constructor with the most parameters, the first of them when several have as many: what a type
    // without a parameterless constructor is read through
    public static ConstructorMembers LargestConstructor => largestConstructor ??= GetLargestConstructor();

    private static ConstructorMembers GetLargestConstructor()
    {
        var constructors = typeof(T).GetConstructors();
        var largest = constructors.MaxBy(x => x.GetParameters().Length);

        // the same object when both are the same constructor, so its rows are counted in one place
        return largest == null || largest == constructors[0] ? Constructor : new ConstructorMembers(largest);
    }
}

// finds a member by its name, exactly or regardless of case
internal sealed class NameLookup
{
    private readonly string?[] names;
    private readonly Dictionary<string, int> exact;
    private readonly Dictionary<string, int> ignoringCase;
    private readonly (string First, string Second)? exactDuplicate;
    private readonly (string First, string Second)? ignoringCaseDuplicate;

    public int Count => names.Length;

    public string? this[int index] => names[index];

    public NameLookup(string?[] names)
    {
        this.names = names;
        this.exact = new Dictionary<string, int>(names.Length, StringComparer.Ordinal);
        this.ignoringCase = new Dictionary<string, int>(names.Length, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < names.Length; i++)
        {
            // a member without a name matches nothing
            if (names[i] is string name)
            {
                if (!this.exact.TryAdd(name, i))
                {
                    this.exactDuplicate ??= (name, name);
                }

                if (!this.ignoringCase.TryAdd(name, i))
                {
                    this.ignoringCaseDuplicate ??= (names[this.ignoringCase[name]]!, name);
                }
            }
        }
    }

    // with duplicates a name does not say which member is meant; the first two names that are the same
    public bool HasDuplicates(bool caseSensitive, out string? first, out string? second)
    {
        var duplicate = caseSensitive ? exactDuplicate : ignoringCaseDuplicate;
        first = duplicate?.First;
        second = duplicate?.Second;
        return duplicate != null;
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
    private readonly FieldInfo? field;
    private readonly PropertyInfo? property;

    public MemberInfo Member { get; }
    public string Name => Member.Name;

    // the compiled setter, once the type has been read a lot (see ReadMembers.CountRow); null until then, and for
    // a member whose setter can't be compiled
    public Action<object, object?>? Compiled;

    public ReadMember(FieldInfo field)
    {
        this.Member = field;
        this.field = field;
        this.source = ValueSource.For(-1, field.FieldType);
    }

    public ReadMember(PropertyInfo property)
    {
        this.Member = property;
        this.property = property;
        this.source = ValueSource.For(-1, property.PropertyType);
    }

    // Reflection is called here directly, not through a delegate: on the first rows of a type, before the code is
    // optimized, one more delegate per value made reading slower than calling reflection the way FastSql always did
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void SetValue(object obj, object? value)
    {
        var compiled = Compiled;

        if (compiled != null)
        {
            compiled(obj, value);
        }
        else if (property != null)
        {
            // an exception of the setter reaches the caller as it is, not inside a TargetInvocationException
            property.SetValue(obj, value, BindingFlags.DoNotWrapExceptions, null, null, null);
        }
        else
        {
            field!.SetValue(obj, value);
        }
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

    // the compile of the setters, from the moment the threshold is reached
    public Task? Compiling { get; private set; }

    public ReadMembers(Type type, FromTypeOption fromTypeOption)
    {
        this.type = type;

        Helpers.GetMembers(type, fromTypeOption, true, out FieldInfo[] fields, out PropertyInfo[] properties);

        this.All = [.. fields.Select(x => new ReadMember(x)), .. properties.Select(x => new ReadMember(x))];
        this.Names = new NameLookup(this.All.Select(x => x.Name).ToArray());
    }

    // Called for every row that is read into the type. When several threads read the type the count is not
    // exact, which only moves the moment the setters are compiled
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void CountRow()
    {
        if (rowsUntilFast > 0 && --rowsUntilFast == 0 && !fast)
        {
            fast = true;
            Compiling = FastAccessors.InBackground(() =>
            {
                foreach (var member in All)
                {
                    member.Compiled = FastAccessors.CompileSetter(type, member.Member);
                }
            });
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

// a constructor that objects are created with (anonymous types, records), and its parameters
internal sealed class ConstructorMembers
{
    private int rowsUntilFast = FastAccessors.RowsBefore;
    private bool fast;

    // null when the type has no public constructor
    public ConstructorInfo? Constructor { get; }
    public ReadParameter[] Parameters { get; }
    public NameLookup Names { get; }

    // the compiled constructor, once the type has been read a lot (see CountRow); null until then
    public Func<object?[], object>? Compiled;

    // the compile of the constructor, from the moment the threshold is reached
    public Task? Compiling { get; private set; }

    public ConstructorMembers(ConstructorInfo? constructor)
    {
        this.Constructor = constructor;
        this.Parameters = constructor == null ? []
            : constructor.GetParameters().Select(x => new ReadParameter(x.Name, x.Position, ValueSource.For(-1, x.ParameterType))).ToArray();
        this.Names = new NameLookup(this.Parameters.Select(x => x.Name).ToArray());
    }

    // as ReadMember.SetValue: reflection directly until the constructor is compiled
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public object Create(object?[] args)
    {
        var compiled = Compiled;

        // an exception of the constructor reaches the caller as it is, not inside a TargetInvocationException
        return compiled != null ? compiled(args) : Constructor!.Invoke(BindingFlags.DoNotWrapExceptions, null, args, null);
    }

    // as ReadMembers.CountRow
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void CountRow()
    {
        if (rowsUntilFast > 0 && --rowsUntilFast == 0 && !fast)
        {
            fast = true;
            Compiling = FastAccessors.InBackground(() => Compiled = FastAccessors.CompileConstructor(Constructor!));
        }
    }
}

// the members of a type that values are taken from (parameters, bulk copy), for one FromTypeOption
internal sealed class WriteMembers<T>
{
    private readonly MemberInfo[] members;
    private int rowsUntilFast = FastAccessors.RowsBefore;

    // written by the compile on the thread pool: the settings first, then the flag, so a reader that sees the
    // flag sees the compiled settings
    private volatile FieldSettings<T>[] settings;
    private volatile bool isFast;

    // Shared by all callers, so it must not be changed. The getters go through reflection until a bulk copy has
    // taken many rows of the type, then they are compiled (see CountRow)
    public FieldSettings<T>[] Settings => settings;

    // the getters are compiled, or can't be
    public bool IsFast => isFast;

    // the compile of the getters, from the moment the threshold is reached
    public Task? Compiling { get; private set; }

    public WriteMembers(FromTypeOption fromTypeOption)
    {
        Helpers.GetMembers(typeof(T), fromTypeOption, false, out FieldInfo[] fields, out PropertyInfo[] properties);

        // an exception of a property getter reaches the caller as it is, not inside a TargetInvocationException
        this.members = [.. fields, .. properties];
        this.settings =
        [
            .. fields.Select(x => new FieldSettings<T>(x.Name, x.FieldType, y => x.GetValue(y))),
            .. properties.Select(x => new FieldSettings<T>(x.Name, x.PropertyType,
                y => x.GetValue(y, BindingFlags.DoNotWrapExceptions, null, null, null)))
        ];
        this.isFast = !FastAccessors.IsSupported;
    }

    // Called for every row a reader takes values from; true when the settings have their final getters.
    // As for reading, the count is not exact with several threads
    public bool CountRow()
    {
        if (!isFast && rowsUntilFast > 0 && --rowsUntilFast == 0)
        {
            Compiling = FastAccessors.InBackground(() =>
            {
                var current = settings;
                var compiled = new FieldSettings<T>[current.Length];

                for (int i = 0; i < compiled.Length; i++)
                {
                    var getter = FastAccessors.CompileGetter<T>(members[i]);
                    compiled[i] = getter == null ? current[i] : new FieldSettings<T>(current[i].Name, current[i].FieldType, getter);
                }

                settings = compiled;
                isFast = true;
            });
        }

        return isFast;
    }
}

// Accessors compiled from expression trees, for types that are used a lot. They do what the accessors that go
// through reflection do, several times faster for every value, but building one costs as much as several
// thousand calls through reflection
internal static class FastAccessors
{
    // Rows of a type that go through reflection before its accessors are compiled: more than the rows whose
    // reflection costs as much as the compile (about 7,500 for 8 members), so a type pays for the compile only
    // when it has been used enough to earn it back. With 1,000 the first read of a few thousand rows in a new
    // process was slower than without compiled accessors
    public const int RowsBefore = 10000;

    private static readonly ConstructorInfo targetException = typeof(TargetException).GetConstructor([typeof(string)])!;

    // without a compiler for dynamic code, expression trees are interpreted, which is slower than reflection
    public static bool IsSupported => RuntimeFeature.IsDynamicCodeCompiled;

    // Building the accessors of a type takes a millisecond or more, which the read or the bulk copy that reached
    // the threshold would wait for. They are built on the thread pool instead, and reflection goes on until they
    // are there. The compile methods catch their own exceptions, so the task does not fail
    public static Task InBackground(Action compile)
    {
        return Task.Run(compile);
    }

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
