# Improvement plan (phase 4)

Status: approved on 2026-11-13 as recommended, including the "your call" items (I16, I17, I18, I27), after
a revision that followed the probes (see [What the probes changed](#what-the-probes-changed)). Being
implemented; the results go into `docs/improvement-notes.md`.

Phases 0 to 3 gave FastSql tests, current .NET and drivers, bug fixes and modern C#. This phase improves
the library itself: what it costs per call and per row, what it tells the user when something is wrong,
what the public API lacks, and what a release needs.

Rules for this phase:

- The overall design stays: context providers, the three context kinds, `ICommandCreator` /
  `IWrappedCommand`, compiled commands, `FieldSettings`, `UniversalDataReader`, bulk copy.
- Internal changes are not limited.
- Public contract changes are minor, and each one is listed and justified in
  [C. Public API](#c-public-api). Users of 1.x have to recompile for the next release anyway (it targets
  `net10.0` only and has Microsoft.Data.SqlClient types in its signatures), so what matters is that
  existing source code still compiles and means the same.
- Every behavior change is listed with its item and goes into the release notes.

The items are numbered I1 to I28 (I26 to I28 were added after the probes; the other numbers are the ones
of the first version). **Rec.** is my recommendation: **Do**, **Your call** (I say which way I lean), or
it is in [Not proposed](#not-proposed). The decisions I need are at the [end](#decisions-needed).

## What the probes changed

The first version of this plan had no measurements, because the probe program could not run. Now they
ran: timings in memory, the accessor strategies each in its own process, the compiler on stand-ins for the
new overloads, and both drivers against SQL Server 2022 and PostgreSQL 18.6 containers. Setup: .NET
10.0.15, the phase 3 laptop, Microsoft.Data.SqlClient 7.1.1, Npgsql 10.0.3; `Stopwatch` timings, the best
of several rounds. The probes are scratch programs outside the repository; I1 turns the performance ones
into the benchmark project.

What is different from the first version:

1. **Compiled accessors are no longer part of I3 and I4.** They are fast per value (a setter 22 ns to 6 ns)
   but cost four times more than reflection the first time a type is used (2.5 ms against 0.6 ms for a
   type with 8 members), which I had put at "well under a millisecond". They are now a separate, optional
   item, I27, and only for types that prove hot.
2. **New item I26: the last compiled command is kept per type.** I had listed caching compiled commands
   under "not proposed", expecting little to be left once the metadata is cached. Measured, half of the
   cost of an `Insert` or `CreateMapped` call is left (text generation, parameter discovery, building the
   command). A cache of one entry per type and command kind removes it and cannot grow.
3. **New item I28: exceptions thrown by the user's members arrive unwrapped.** It was a side effect of
   the compiled accessors; it is worth having either way and costs one flag.
4. **What to expect end to end is smaller than "faster" suggests.** Against a local database one round
   trip takes 0.7 to 0.8 ms, and FastSql's own work is under 1% of it. The gains are in allocations per
   operation (55% to 78% less) and in client CPU on large results (see the tables in section A).
5. **I1 no longer needs a CI workflow:** local runs work again.
6. **Confirmed as written:** I6 (ten string lengths give ten cached plans, one with a fixed size), I7
   (the table of date and time types, and that writing works on both providers), I10, I12 (no call becomes
   ambiguous), I15, I17. I14 got one caveat (method groups).
7. **Corrected:** two reasons under "Not proposed" were guesses and one was wrong (`SqlBulkCopy` does not
   read the reader's schema or `HasRows` at all).

## Summary

| ID | Change | Gain | Behavior change | Public API | Rec. |
|---|---|---|---|---|---|
| I1 | Benchmarks in the repository | repeatable numbers, regressions noticed | none | none | Do |
| I2 | Member metadata cached per type | no reflection and LINQ per call | none | none | Do |
| I3 | `FieldSettings.FromType` cached | 2.5 µs and 6 KB less per `Insert` / `CreateMapped` call | none | none | Do |
| I4 | Reading: per-query setup from the cache | a one-row query: 4.4 µs and 8 KB become about 0.7 µs and 1 KB | none | none | Do |
| I26 | The last compiled command is kept per type | `Insert` / `CreateMapped`: 5.6 µs and 14 KB become about 0.5 µs and 2 KB | text generator called once per command | none | Do |
| I27 | Fast accessors for hot types | large reads up to 13% faster, PostgreSQL bulk copy 10% | none (with I28) | none | Your call (yes) |
| I5 | Small allocations on the command path | less garbage per command | none | none | Do |
| I6 | SQL Server: stable sizes for string and binary parameters | one cached plan per statement instead of one per value length | declared parameter type | none | Do |
| I7 | `DateOnly` / `TimeOnly` members on both providers | one model works on SQL Server and PostgreSQL | reads that fail today work | none | Do |
| I8 | Error messages that name the problem | faster diagnosis | message texts, one exception type | one protected overload | Do |
| I28 | Exceptions of the user's members arrive unwrapped | the real exception, not `TargetInvocationException` | exception type of failing members | none | Do |
| I9 | Argument checks at the public entry points | clear, early errors | exception type and time of failing calls | none | Do |
| I10 | Parameter discovery accepts non-ASCII letters | `@Größe` works in `CreateMapped` | a call that fails today works | none | Do |
| I11 | `CancellationToken` for `UsingConnectionAsync` / `UsingTransactionAsync` | open, begin and commit can be cancelled | none | 5 new overloads | Do |
| I12 | `UsingConnection` / `UsingTransaction` that return a result | no captured variables | none | 10 new methods | Do |
| I13 | `ExecuteScalar<T>` | typed scalar results | none | 2 new extension methods | Do |
| I14 | `Insert` / `Update` / `Delete` / `Merge` return the affected rows | "was a row updated?" without dropping to `Create...` | none | return type of 16 methods | Do |
| I15 | `UniversalDataReader.Close()` overrides `DbDataReader.Close()` | closing through a base reference works (Q01) | such a close now closes | `new` becomes `override` | Do |
| I16 | `WriteToServerAsync` for `IAsyncEnumerable<T>` | one call, as for `IEnumerable<T>` | none | 4 new extension methods | Your call (yes) |
| I17 | `ExecuteQueryFirstColumn()` returns `IEnumerable<object?>` | LINQ without `Cast<object>()`, same shape as the async method | none | return type of 3 methods | Your call (yes) |
| I18 | `ExecuteQuery<T>` for types without a parameterless constructor | records as query results | none for code that compiles today | a constraint removed from 8 methods | Your call (yes) |
| I19 | PostgreSQL: remove the unused copy of the SQL Server table creation code | 210 lines less | none | none | Do |
| I20 | Bulk copies: one code path for sync and async setup | 70 lines less, no drift | none | none | Do |
| I21 | Leftovers | readability | none | none | Do |
| I22 | XML documentation for the public API | IntelliSense, API docs in the package | none | none | Do |
| I23 | Package metadata, Source Link, symbols | usable packages | none | none | Do |
| I24 | CI: warnings as errors, pack | regressions caught | none | none | Do |
| I25 | README: what exists but is not described | users find the features | none | none | Do |

## A. Performance

### What it costs today (measured)

In memory, a type with 8 members (`int`, `string`, `decimal`, `DateTime`, `int?`, `bool`, `long`, `Guid`).
"Possible" is a prototype of the item named; for the command rows it is today's precompiled command.

| Operation | Today | Possible | With |
|---|---:|---:|---|
| `ReadAll<T>`, one row (the setup of a query) | 4.4 µs, 8.1 KB | 0.64 µs, 1.1 KB | I2, I4 |
| `ReadAll<T>`, 1,000 rows | 327 µs, 118 KB | 93 µs, 87 KB | I27 (a reader loop without mapping: 38 µs) |
| `FieldSettings.FromType<T>()` | 2.5 µs, 6.1 KB | nothing | I3 |
| `CreateInsert(table, value)`, up to the `SqlCommand` | 5.6 µs, 13.8 KB | 0.45 µs, 1.9 KB | I3, I26 (plain ADO.NET: 0.34 µs, 1.7 KB) |
| `CreateMapped(text, value)`, 3 parameters | 5.5 µs, 14.0 KB | 0.26 µs, 1.1 KB | I3, I26 |
| `ToDataReader`, 1,000 rows read with `GetValue` | 118 µs | 54 µs | I27 |
| 1,000 objects through a constructor with 8 arguments | 147 µs | 16 µs | I27 |

Where a `CreateMapped` call spends its 5.5 µs: `FromType` 2.5 µs, parameter discovery 1.45 µs, building the
`MappedCommand` 1.36 µs. An `Insert`: `FromType` 2.5 µs, the text 0.5 µs, the `MappedCommand` 1.4 µs, the
rest list handling. So caching the metadata alone (I2, I3) leaves about half.

End to end, against local containers (one round trip takes 0.7 to 0.8 ms there):

| Operation | SQL Server today | possible | PostgreSQL today | possible |
|---|---:|---:|---:|---:|
| Query, 10,000 rows of 8 columns | 11.4 ms | 9.9 ms (-13%) | 10.9 ms | 10.5 ms (-4%) |
| the same with a plain reader loop, no objects | 8.1 ms | | 10.5 ms | |
| Query, one row | 815 µs, 12.8 KB | 791 µs, 5.6 KB | 712 µs, 10.0 KB | 690 µs, 2.9 KB |
| `Insert(table, value)` | 850 µs, 15.6 KB | 827 µs, 3.6 KB | 680 µs, 15.3 KB | 665 µs, 3.3 KB |
| Bulk copy, 200,000 rows of 8 columns | 640 to 680 ms | 615 to 630 ms | 435 ms | 394 ms (-10%) |

What this says:

- **For a single operation the time does not change in a way anyone could measure** (the differences in
  the one-row and insert lines are inside the noise). What changes is the garbage: 55% to 78% less
  allocated per operation, which a service under load feels as fewer collections.
- **Large results** gain 4% to 13% of wall-clock time with fast accessors. The mapping's own CPU drops by
  three quarters, but reading from the network is most of the time.
- **Bulk copy** gains about 10% on PostgreSQL with fast getters and nothing clear on SQL Server.

### I1 Benchmarks in the repository

- `benchmarks/Gerakul.FastSql.Benchmarks` (BenchmarkDotNet, in the solution, not packable), with in-memory
  readers only, no database: the operations of the first table above, sync and async, plus first use of a
  type.
- Run locally, before and after the performance PR; the results go into the notes of this phase.
- Why in the repository: the phase 3 benchmarks were a scratch project that no longer exists, and so are
  this plan's probes. No CI workflow: shared runners are too noisy for these numbers, and local runs work
  again.

### I2 Member metadata cached per type

The result of `Helpers.GetMembers` is kept per type, `FromTypeOption` and direction (reading or writing),
together with name lookups (ordinal and case-insensitive) and the "two members differ only by case" flag.
Held in static generic classes, so there is no dictionary lookup and nothing outlives a collectible type.
Same members, same order.

### I3 `FieldSettings.FromType` cached

`FromType<T>` builds the settings once per type and option. The public method still returns a new array
on every call (callers may replace elements), holding the same `FieldSettings<T>` objects, which are
immutable. FastSql's own callers use the cached array. The getters stay as they are (see I27).

### I4 Reading: per-query setup from the cache

Per query, each reader field is looked up in the cached name dictionary (I2); the two `GroupBy` and two
`Join` calls go, and so does the reflection over the type. The constructor and its parameters of
`ReadAllAnonymous` are cached the same way. Same checks, same order of assignment, same results. This is
what a one-row query pays today: 4.4 µs and 8.1 KB.

### I26 The last compiled command is kept per type

- Now: `context.Insert("Employee", emp)` compiles the insert command for `Employee` on every call:
  field list, SQL text, parameter map. `CreateMapped(text, value)` parses the text for parameters on every
  call. The precompiled commands (`CompileInsert` + `CreateMapped(command, value)`) avoid that, but the
  short forms are what the README shows and what most code uses.
- Change: for each type and kind of command (insert, insert with output, update, delete, merge, mapped
  text, procedure) the compilator keeps the last command it compiled together with its inputs (provider,
  table name or command text, field lists, options). A call with equal inputs gets that command; any other
  call compiles and replaces it. Commands are immutable, so sharing one is safe.
- Bounded by construction: at most seven small entries per type, whatever the application does. A loop
  over one table hits every time; anonymous parameter types hit every time too, because each call site
  has its own type.
- Not used when the caller passes its own `FieldSettings`.
- Behavior: the `CommandTextGenerator` is called once per distinct command instead of on every call. It
  matters only for a custom generator whose output changes between calls.

### I27 Fast accessors for hot types (your call; I lean yes)

- **What:** once about 1,000 rows of a type have been read, its setters (or its constructor) are replaced
  by compiled ones; the same for the getters once a bulk copy of `IEnumerable<T>` has written about 1,000
  rows. A type below the threshold keeps reflection and pays nothing.
- **Measured per value:** setter 22 ns to 6 ns, getter 14.5 ns to 7 ns, constructor with 8 arguments 147 ns
  to 16 ns per object. In the tables above: `ReadAll<T>` 3.5 times faster in memory; 10,000 rows from SQL
  Server 13% faster, from PostgreSQL 4%; bulk copy 10% faster on PostgreSQL.
- **Why only for hot types:** building the fast accessors is slow. First use of a type with 8 members,
  getters and setters:

  | Strategy | First type in the process | Each further type | Per value afterwards |
  |---|---:|---:|---|
  | Reflection (today) | 2.5 ms | 0.6 ms | get 14.5 ns, set 22 ns |
  | Expression trees | 15 ms | 2.5 ms | get 7 ns, set 6 ns |
  | Delegates on the accessor methods, no code generation | 6 ms | 1.3 ms | get 7 ns, set 6 ns |
  | `DynamicMethod` with IL | 4.7 ms | 1.9 ms | get 7 ns, set 6 ns |

  With expression trees a type has to read about 7,500 rows before it comes out ahead. Compiling at first
  use would make every type pay, also a lookup table read once and the anonymous parameter object of a
  query that runs once.
- **Which strategy:** expression trees. With the threshold the building cost falls only on types that
  earn it back, and expression trees are the least code and the only one of the three simple options that
  also covers constructors and fields (`readonly` fields keep reflection: an expression tree cannot assign
  them). A runtime that cannot compile dynamic code (`RuntimeFeature.IsDynamicCodeCompiled` is false) stays
  on reflection.
- **Behavior:** none, given I28. Both kinds of accessor were checked against each other on private and
  `init` setters, hidden (`new`) and virtual members, nullable and enum members, `object` and interface
  members, struct targets, interface types, records and anonymous types; `null` into a non-nullable
  value-type member gives the default value with both (Q112), and a `null` object keeps today's
  `TargetException` through an explicit check.
- **Size:** about 100 lines. If you would rather not have this machinery, I2 to I4 and I26 stand on their
  own: they give all of the allocation gains and none of the per-row ones.
- Not part of this item: typed reader calls (`GetInt32` instead of `GetValue`). They would save the boxing
  per value, but the drivers return different things from typed and untyped calls (Npgsql: `DateOnly`
  from `GetValue`, `DateTime` from `GetDateTime`), so the results could change.

### I5 Small allocations on the command path

- `MappedCommand<T>`'s constructor: a dictionary instead of two `GroupBy` and a `Join` (1.4 µs and 3.1 KB
  today; it runs for every precompiled command and every miss of I26).
- `CreateMapped` / `CreateProcedure` prepare the query options twice (in the extension method and again in
  `MappedCommand.Create`); a reading clones `ReadOptions` twice. Once each.
- `SimpleCommand` formats the names `@p0`, `@p1`, ... on every call; the first ones are kept.
- `UniversalDataReader.Settings` creates a collection on every access; it is kept.

### I6 SQL Server: stable sizes for string and binary parameters

- Now: `AddWithValue` declares a string parameter as `nvarchar(n)` with `n` the length of this value, so
  `where Name = @Name` gets a separate cached plan for every length of `Name` that is ever passed. Probe:
  the same statement with strings of 10 lengths left 10 plans in the cache (`(@p0 nvarchar(1))` to
  `(@p0 nvarchar(10))`). `byte[]` behaves the same way (`varbinary(1)`, `varbinary(2)`, ...).
- Change (in `SqlContextProvider.AddParamWithValue`): `nvarchar(4000)` for strings up to 4,000 characters
  and `varbinary(8000)` for arrays up to 8,000 bytes; longer values are `max`, as today. Probe: one plan
  for the 10 strings. These are the sizes EF Core uses, and Dapper does the same for strings. A `null`
  string is already sent as `nvarchar(4000)`, so after the change a null and a non-null value share a plan
  too. `DbParameter` objects supplied by the caller are not touched.
- Behavior: the declared parameter type changes; values and results do not (`'abc'` still arrives with a
  data length of 6 bytes; `like` works the same).
- Not changed: `decimal`. It has the same effect (a plan per precision and scale of the value: six
  decimals of different shapes left six plans), but a parameter declared wider than the column can make
  the server convert the column instead of the parameter, which can cost an index seek. Without the
  column's type there is no safe fixed declaration.

## B. Behavior

### I7 `DateOnly` and `TimeOnly` members on both providers

This is the open question from phase 1 ([upgrade notes](upgrade-notes.md#open-questions-for-later-phases)).
What the drivers return from `GetValue`, and what FastSql does with it (probed on both servers):

| Column | Driver value | `DateTime` / `TimeSpan` member | `DateOnly` / `TimeOnly` member |
|---|---|---|---|
| PostgreSQL `date`, `time` | `DateOnly`, `TimeOnly` (Npgsql 10) | works (converted since phase 1) | works |
| SQL Server `date`, `time` | `DateTime`, `TimeSpan` | works | **`InvalidCastException`** |

So a model with `DateOnly` members, the natural type for a `date` column today, cannot be read from SQL
Server, and a model cannot be shared between the providers. Answers to the four questions of the note:

- **Convert in the other direction: yes.** A `DateTime` value into a `DateOnly` member
  (`DateOnly.FromDateTime`; a time part is dropped, as the member's type says) and a `TimeSpan` value into a
  `TimeOnly` member (`TimeOnly.FromTimeSpan`; a negative value or one of a day or more throws). All four
  combinations of the table then work on both providers, also for `ExecuteQueryFirstColumn<DateOnly>()`.
- **Other member types (`string`, `object`): no.** An `object` member gets the driver's value, which is
  right. A `string` member for a date column worked with Npgsql 4 only as a side effect of `DateTime` being
  `IConvertible`; `Guid`, `TimeSpan` and `DateTimeOffset` values never converted to `string` either.
- **An option instead of always converting: no.** The conversion happens only where the alternative is an
  exception, so an option could only turn a working read into a failing one.
- **Drop the old conversion in a major version: no.** `DateTime` members for `date` columns are in every
  existing model.

Writing needs nothing from FastSql. Probed: `DateOnly` / `TimeOnly` values work as parameters (`CreateSimple`,
`Insert`, in a `where`) and in bulk copy on both servers, and `DateTime` / `TimeSpan` members work
everywhere except PostgreSQL bulk copy into `date` / `time` columns (Q85, unchanged). The tests will pin
these.

Also: SQL Server `CreateTable` gets column types for `DateOnly` (`date`), `TimeOnly` and `TimeSpan`
(`time`) and `DateTimeOffset` (`datetimeoffset`); such members fail today ("Can not create table", probed).

### I8 Error messages that name the problem

The messages say that something is wrong, not what. Examples:

| Today | Proposed |
|---|---|
| FieldsSelector has value FieldsSelector.Destination, but destination has fields which are not presented in source | FieldsSelector.Destination: the reader has no field for the members 'Age', 'Phone' of Employee |
| Not all parameters has been mapped on settings | No member of Employee matches the parameters '@Agee' |
| Reader field names has been duplicated | The reader has more than one field named 'Name' (names are compared case-insensitively) |
| Destination object field names has been duplicated | The members 'Name' and 'name' of Employee differ only by case (names are compared case-insensitively) |
| Parameter names has been duplicated | Parameter '@ID' is listed more than once |
| Can not create table with specified fields | Cannot create table Employee: no column type for the fields 'Tags' (String[]) |
| An item with the same key has already been added. Key: id (`ToDataReader`) | Field 'id' is defined more than once |

- Exception types stay, except the last row: `ArgumentException` from FastSql instead of the dictionary's
  (the same type, another message).
- About 30 test assertions pin message texts and change with them.
- Public API: the bulk copies need the field names for their message, so `BulkCopy` gets a second protected
  `CheckFieldSelection` overload that takes the name lists; the existing one (counts) stays.

### I28 Exceptions of the user's members arrive unwrapped

- Now: when a property getter (writing), a property setter or a constructor (reading) of the user's type
  throws, the caller gets a `TargetInvocationException` with the real exception inside, because FastSql
  calls these members through reflection.
- Change: reflection is told not to wrap (`BindingFlags.DoNotWrapExceptions`); the caller gets the
  exception the member threw. Probed for properties and constructors.
- Behavior: the exception type of a failing member. No pinned test expects the wrapper.

### I9 Argument checks at the public entry points

- Now: a `null` connection string, command text, table name, delegate, settings list or source sequence
  fails somewhere inside, often later (when the command runs or the query is enumerated), as
  `NullReferenceException` or a driver error.
- Change: `ArgumentNullException` with the parameter name, at the call. Lazy queries still run lazily; only
  the argument check moves to the call.
- Not checked: the `value` of a mapped command (a `null` value is valid with a single-value
  `FieldSettings<T>(name)`), and `CreateSimple(text, null)` (Q60, left as it is in phase 2).

### I10 Parameter discovery accepts non-ASCII letters

`CreateMapped("select @Größe + 1", value)` finds the parameter `@Gr` and throws "Not all parameters has been
mapped", because the name pattern knows ASCII letters only. Probed: both servers and both drivers accept
`@Größe` and `@Имя`, and FastSql does too when the names are passed explicitly, so only the discovery is
in the way. The pattern becomes letters (`\p{L}`), digits, `_`, `$`, `#`; the code that skips literals and
comments (Q11) is not touched. `Insert` / `Update` / `Merge` are not affected (they do not parse text).

## C. Public API

Each change, why, and what it means for existing code. "Additive" means nothing existing changes.

### I11 `CancellationToken` for `UsingConnectionAsync` and `UsingTransactionAsync`

```csharp
// ConnectionStringContext
Task UsingConnectionAsync(Func<ConnectionContext, Task> action, CancellationToken cancellationToken);
Task UsingTransactionAsync(Func<TransactionContext, Task> action, CancellationToken cancellationToken);
Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action, CancellationToken cancellationToken);
// ConnectionContext
Task UsingTransactionAsync(Func<TransactionContext, Task> action, CancellationToken cancellationToken);
Task UsingTransactionAsync(IsolationLevel isolationLevel, Func<TransactionContext, Task> action, CancellationToken cancellationToken);
```

- **Why:** these are the only async entry points without a token. The commands inside can be cancelled,
  but opening the connection, beginning the transaction and the commit cannot. The first overload already
  exists as an internal method.
- The token cancels open, begin and commit. The rollback after a failure runs without it, so a cancelled
  transaction is still rolled back.
- **Compatibility:** additive.

### I12 `UsingConnection` and `UsingTransaction` that return a result

```csharp
// ConnectionStringContext
TResult UsingConnection<TResult>(Func<ConnectionContext, TResult> func);
Task<TResult> UsingConnectionAsync<TResult>(Func<ConnectionContext, Task<TResult>> func, CancellationToken cancellationToken = default);
// ConnectionStringContext and ConnectionContext
TResult UsingTransaction<TResult>(Func<TransactionContext, TResult> func);
TResult UsingTransaction<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, TResult> func);
Task<TResult> UsingTransactionAsync<TResult>(Func<TransactionContext, Task<TResult>> func, CancellationToken cancellationToken = default);
Task<TResult> UsingTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<TransactionContext, Task<TResult>> func, CancellationToken cancellationToken = default);
```

- **Why:** a value computed inside a connection or transaction (a new ID, a loaded object) has to leave
  through a captured variable today:

  ```csharp
  int id = 0;
  context.UsingTransaction(t => { id = t.CreateInsertWithOutput("Employee", emp, "ID").ExecuteQueryFirstColumn<int>().First(); });
  ```

  The first two methods already exist as internal ones (phase 3, M12).
- **Compatibility:** additive. Probed with the compiler, on stand-ins with these signatures next to the
  existing ones and the I11 overloads, for 23 call shapes: none is ambiguous. Lambdas and method groups
  without a value (`c => c.Insert(...)`, `c => { ... }`, `async c => { await ...; }`,
  `c => c.InsertAsync(...)`) bind to the existing methods. A lambda or method group with a value
  (`c => c.CreateSimple(text).ExecuteNonQuery()`, `async c => await ...` returning a value) binds to the
  new overload; it does the same and the value is dropped.
- To document: a lazy query result (`ExecuteQuery`) must be materialized inside the scope (`ToArray`),
  because the connection is closed when the method returns. `UsingConnection(c => c.CreateSimple(text).ExecuteQuery<T>())`
  compiles today and does nothing; with the new overload it returns a sequence that fails when enumerated.

### I13 `ExecuteScalar<T>`

```csharp
// WrappedCommandExtensions
static T ExecuteScalar<T>(this IWrappedCommand cmd);
static Task<T> ExecuteScalarAsync<T>(this IWrappedCommand cmd, CancellationToken cancellationToken = default);
```

- **Why:** `ExecuteScalar()` returns `object?`, and the typed way to read one value is a sequence: the
  README's own example for an inserted ID is `...ExecuteQueryFirstColumn<int>().First()`.
  `CreateInsertAndGetID(...).ExecuteScalar()` returns a boxed `decimal` that has to be converted by hand (Q18).
- Values are converted by the rules of the mapping (enums, nullable types, invariant culture, I7). Both
  drivers return `null` when there is no row and `DBNull` for a database null (probed); either gives `null`
  for a nullable `T`, and for a non-nullable value type it throws, as the mapping does (Q12).
- **Compatibility:** additive; extension methods, so `IWrappedCommand` itself does not change (the
  compiler takes `ExecuteScalar<int>()` next to the interface's `ExecuteScalar()`, probed).

### I14 `Insert`, `Update`, `Delete`, `Merge` return the affected rows

`void` becomes `int`, `Task` becomes `Task<int>`, for the 16 shortcut methods of
`CommandCreatorExtensions`.

- **Why:** the commands return the number of affected rows and the shortcuts throw it away. "Was the row
  there?" after an `Update` or `Delete` (optimistic concurrency, delete if exists) needs
  `CreateUpdate(...).ExecuteNonQuery()` today.
- **Compatibility:** source compatible for calls: a statement, a lambda (to `Action` or `Func<Task>`), an
  expression-bodied `void` or `Task` member and `await` all compile as before (probed). One exception: a
  method group converted to a `void` delegate (`Action<...> a = context.Insert;`) no longer compiles
  (CS0407); with these generic `params` methods that is hardly written. Not binary compatible (see the
  rules above).

### I15 `UniversalDataReader.Close()` overrides `DbDataReader.Close()` (Q01)

- **Why:** `Close()` hides the virtual base method (`new`). Closing the reader through a `DbDataReader` or
  `IDataReader` reference, or with `CloseAsync()`, does nothing, and the source stays open (probed:
  `IsClosed` stays false). Phase 2 left it because the signature changes; this phase allows that.
- The base class routes `Dispose`, `DisposeAsync` and `CloseAsync` through the virtual `Close()` (probed),
  so the override becomes the one place that closes.
- **Compatibility:** source and binary compatible. Behavior: a close through a base reference now closes
  the reader and disposes the source.

### I16 `WriteToServerAsync` for `IAsyncEnumerable<T>` (your call; I lean yes)

Four extension methods, the same overloads as for `IEnumerable<T>` (with and without `BulkOptions` and
`CancellationToken`).

- **Why:** `ToDataReader` accepts an async sequence, but the one-call bulk copy exists for `IEnumerable<T>`
  only. With an async source the caller creates the reader, copies and disposes it.
- **Compatibility:** additive.

### I17 `ExecuteQueryFirstColumn()` returns `IEnumerable<object?>` (your call; I lean yes)

`IWrappedCommand.ExecuteQueryFirstColumn()`, `DbCommandExtensions.ExecuteQueryFirstColumn()` and
`DataReaderExtensions.ReadAllFirstColumn()` return the non-generic `IEnumerable`; their async versions
return `IAsyncEnumerable<object?>`.

- **Why:** the non-generic interface has no LINQ (`.Cast<object>()` first), and the sync and async methods
  differ for no reason.
- **Compatibility:** source compatible for callers: `foreach` with a typed variable, `Cast<T>()`,
  `OfType<T>()`, assignment to `IEnumerable` and `GetEnumerator()` compile and give the same results
  (probed). One call changes for the better: `string.Join(",", result)` prints the values instead of the
  name of the iterator type. A class outside FastSql that implements `IWrappedCommand` has to change its
  return type. Not binary compatible.

### I18 `ExecuteQuery<T>` for types without a parameterless constructor (your call; I lean yes)

The `where T : new()` constraint goes from `IWrappedCommand.ExecuteQuery<T>` / `ExecuteQueryAsync<T>`,
`DbCommandExtensions.ExecuteQuery<T>` / `ExecuteQueryAsync<T>` and the four `DataReaderExtensions.ReadAll<T>` /
`ReadAllAsync<T>` overloads.

- **Why:** positional records (`record Employee(int ID, string Name)`) are the usual shape of a query
  result today, and `ExecuteQuery<Employee>()` does not compile for them. FastSql can already build objects
  through a constructor, but only as `ExecuteQueryAnonymous(proto)`, which needs an instance of the type.
- **Change:** a type with a public parameterless constructor is read as today (members are set). A type
  without one is read through its public constructor, by the rules of `ExecuteQueryAnonymous` (parameters
  match columns by name; with several public constructors, the one with the most parameters).
- The constructor path is not the slow one: it reads 1,000 rows in 227 µs today, the member path in 327 µs.
- **Compatibility:** code that compiles today behaves the same. A class outside FastSql that implements
  `IWrappedCommand` has to drop the constraint. Not binary compatible.

## D. Code health

### I19 PostgreSQL: remove the unused copy of the SQL Server table creation code

`Gerakul.FastSql.PostgreSQL` has internal copies of `ColumnDefinition`, `DataReaderExtensions` and
`EnumerableExtensions` that generate SQL Server syntax (`[name]`, `nvarchar`, `object_id`) and are never
called: `CreateTable` throws `NotImplementedException` on PostgreSQL (Q10). About 210 lines go. The public
`ColumnDefinitionOptions` of the PostgreSQL package is unused too, but it is public, so it stays until
table creation for PostgreSQL is decided (see [Later](#later-features-that-would-fit-the-design)).

### I20 Bulk copies: one code path for sync and async setup

`MsSqlBulkCopy` and `NpgsqlBulkCopy` each have the field selection, the mapping and the writer setup twice
(`WriteToServer` and `WriteToServerAsync`, about 50 lines each). Private helpers return the mapping and
the configured `SqlBulkCopy`; the two methods keep only what differs (the calls with and without `await`).
About 70 lines go.

### I21 Leftovers

- The conversion rules move from `ReadInfo<T>` into one static class (I13 uses them too).
- Commented-out code and the `// ::: temporary internal` markers go; `DataReaderAttachedToDbContext.cs` is
  renamed after the class it contains (`DataReaderAttachedToConnectionStringContext`).
- The three async row loops (`DbCommandExtensions`, `DataReaderExtensions`, `WrappedCommandWithContext`)
  share one.

## E. Release readiness

### I22 XML documentation for the public API

The public API has no documentation comments. Types and members get summaries (overload families share
one text with `inheritdoc`), with the things that surprise: when a query runs (lazily), who closes what,
which names are matched how, that table names go into the SQL text as given (never pass user input). The
packages ship the XML files (`GenerateDocumentationFile`).

### I23 Package metadata, Source Link, symbols

In `Directory.Build.props`, for the three libraries: license expression (MIT), project URL, tags, the
README as the package readme, Source Link with symbol packages (`snupkg`), deterministic CI builds.
`Examples` and the tests are not packable. The versions stay as they are (1.1.0, 1.1.0, 1.0.0): the next
version number is your release decision.

### I24 CI

- The build fails on warnings (the build has none since phase 3; this keeps it so).
- A `dotnet pack` step, so a broken package is noticed; the packages are uploaded as artifacts, not published.

### I25 README: what exists but is not described

Precompiled commands (`CompileInsert` + `CreateMapped(command, value)`), `ToDataReader` and custom
`FieldSettings`, `UseReader`, cancellation, the options and their precedence (provider, context, call),
`FieldsSelector`, the package names; plus whatever this phase adds.

## Not proposed

- **A cache of compiled commands keyed by SQL text or table**, beyond the one entry per type of I26: it
  can grow without limit when texts are built dynamically.
- **Compiled accessors at first use, for every type:** see the table in I27.
- **Typed reader calls and one compiled materializer per result shape** (what Dapper does): see I27.
- **`UniversalDataReader`: read each member once per row.** `IsDBNull` and `GetValue` both call the getter,
  but the consumers rarely call both. Probed with a reader that counts calls: `SqlBulkCopy` and FastSql's
  PostgreSQL copy call only `GetValue`, one getter call per cell; `SqlBulkCopy` calls `IsDBNull` as well
  only with `EnableStreaming` (930 getter calls instead of 500 for 100 rows of 5 columns).
- **`UniversalDataReader` schema (`GetSchemaTable`) and `HasRows`:** nothing asks for them. `SqlBulkCopy`
  calls only `FieldCount`, `GetName`, `GetOrdinal`, `Read` and `GetValue` (probed, sync and async), and so
  does the PostgreSQL copy.
- **Sharing the lexer of the two providers:** 40 duplicated lines, but sharing needs either new public
  helpers or `InternalsVisibleTo`.
- **Typed contexts from the typed providers** (so `CreateInsertAndGetID` needs no cast, Q18): doubles the
  `Using...` methods of both providers.
- **Renames** (`CommandCompilator`, the namespace of `CommandTextGenerator`), **`IList<T>` parameters to
  `IEnumerable<T>`**: not minor, or ambiguous overloads.
- **Decided in phase 2 and not reopened:** Q03 / Q47 / Q112 (`UniversalDataReader` enumerator and `null`
  instead of `DBNull`), Q33 (`@ID` and `@id` in one text), Q45 (collection detection), Q60
  (`CreateSimple(text, null)`), Q92 (caller's `DbParameter` objects used twice). Tell me if one of them
  should come into this phase. Q92 would be a small change now: clearing the command's parameters after
  each execution lets both drivers take the same parameter objects again (probed).

## Later: features that would fit the design

Not part of this plan; listed so they are not lost. Each is a feature with its own decisions.

- **PostgreSQL `CreateTable`** (Q10): the option exists and throws. Needs a type mapping and its own
  options; the unused public `ColumnDefinitionOptions` would get a purpose or go.
- **PostgreSQL bulk copy with the column types** (Q85): binary `COPY` writes the type of the value, so an
  `int` into `bigint` or `numeric`, a `DateTime` into `date` and a `string` into `jsonb` fail with protocol
  errors. Probed: writing each value with the destination column's type makes all four work, and breaks
  one copy that works today (a `DateTime` of unspecified kind into `timestamptz`: Npgsql then accepts UTC
  only). So it needs a rule for when to pass the type, and the full type matrix.
- **`NpgsqlDataSource` contexts:** Npgsql's recommended way to configure connections (enum and JSON
  mappings, password providers) is not reachable from a connection string context.
- **Rows copied as the result of `WriteToServer`**, **functions through `CreateProcedure` on PostgreSQL**,
  **a naming convention for PostgreSQL** (`first_name` to `FirstName`).

## Verification

- Unit and integration tests, locally and in CI. Tests change only where an item lists a behavior change;
  each such change is named in its PR.
- Public API: the approved files change only for the items that say so (I8, I11 to I18).
- Benchmarks (I1): before and after the performance PR. A regression blocks the item that causes it; the
  first use of a type is measured too, so I27 cannot make cold starts slower unnoticed.
- New tests: every behavior change, every new API member, the conversions of I7 on both providers, the
  parameter declarations of I6, and for I27 the same mapping tests run below and above the threshold.

## Delivery

Branches off `modernization`, one worktree and one PR into `modernization` each:

1. `improve/phase4`: this plan and the benchmarks with their baseline (I1).
2. Performance: I2 to I6, I26, and I27 if you want it.
3. Behavior and public API: I7 to I18, I28.
4. Code health and release readiness: I19 to I25, and `docs/improvement-notes.md` (what changed, the
   behavior changes for users, the public API changes, the benchmark results).

## Decisions needed

Answered on 2026-11-13: yes to 1 to 4 as recommended (I27 yes; I16, I17, I18 yes; the PR split as
proposed), nothing more for 5.

1. **The recommended set** (every "Do" row, now including I26 and I28): go ahead as proposed, or which
   items to drop or change?
2. **I27, fast accessors for hot types:** yes or no. I lean yes: it is where the per-row cost is, and the
   threshold keeps it from costing anything where it does not pay.
3. **I16, I17, I18** (the "your call" API changes): yes or no for each. I lean yes for all three.
4. **The PR split** above: fine, or one PR, or another cut?
5. Anything from [Not proposed](#not-proposed) or [Later](#later-features-that-would-fit-the-design) that
   should come into this phase?
