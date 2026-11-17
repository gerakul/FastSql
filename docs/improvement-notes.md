# Improvement notes (phase 4)

Phase 4 improved the library itself, following the [improvement plan](improvement-plan.md): what it costs per
call and per row, what it says when something is wrong, what its public API lacked, and what a release needs.
The design stayed. The plan was approved on 2026-11-13 as recommended, including the "your call" items I16,
I17, I18 and I27. It came in four pull requests: the plan and the benchmarks (I1), performance (I2 to I6, I26
to I28), behavior and public API (I7 to I18), code health and release readiness (I19 to I25).

## What changed

| Item | Change |
|---|---|
| I1 | `benchmarks/Gerakul.FastSql.Benchmarks` (BenchmarkDotNet, in the solution, not packable): reading, the source of a bulk copy, command creation, and the first use of a type in a new process; in-memory readers, no database. |
| I2 | `TypeMembers<T>` keeps the members of a type per `FromTypeOption` and direction, with lookups of their names, in static generic classes: no reflection or LINQ per call. |
| I3 | `FieldSettings.FromType<T>` builds the settings of a type once per option; every call returns a new array of the kept settings. |
| I4 | A query matches the reader fields with the kept members; the two `GroupBy` and two `Join` per query are gone. The constructor that `ReadAllAnonymous` uses is kept the same way. |
| I26 | `CommandCache<T>` keeps the last compiled command per type and kind (insert, insert with output, update, delete, merge, mapped text, procedure). A call with the same provider, table or text, field lists and options reuses it; any other call compiles and replaces it. Not used for settings the caller passes. |
| I27 | Once a type has been read, or written by a bulk copy, 10,000 rows in the process, its setters, constructor or getters are compiled from expression trees on the thread pool; reflection goes on until they are there, called directly. The code that runs for every row of a read is optimized from its first call. A runtime without a compiler for dynamic code stays on reflection. |
| I28 | Property getters and setters and constructors of the user's types are called with `BindingFlags.DoNotWrapExceptions`. |
| I5 | `MappedCommand`'s constructor without LINQ; query options prepared once and read options cloned once per call; the names `@p0`, `@p1`, ... of `CreateSimple` and the `Settings` collection of `UniversalDataReader` kept. |
| I6 | SQL Server: string parameters are declared `nvarchar(4000)` and byte arrays `varbinary(8000)`, longer values `max`. |
| I7 | `DateTime` values are read into `DateOnly` members and `TimeSpan` values into `TimeOnly` members (the other direction worked since phase 1); SQL Server `CreateTable` has column types for `DateOnly`, `TimeOnly`, `TimeSpan` and `DateTimeOffset`. |
| I8 | Exception messages name the fields, members and parameters that do not match; a failed conversion names the field, the value's type and the target type, with the original exception inside. `Messages` holds the texts. |
| I9 | `ArgumentNullException` for null arguments at the public entry points, also of lazy and async methods. |
| I10 | Parameter discovery takes letters and digits of any alphabet (`\p{L}`, `\p{Nd}`). |
| I11 | `UsingConnectionAsync` and `UsingTransactionAsync` with a `CancellationToken`. |
| I12 | `UsingConnection`, `UsingTransaction` and their async versions that return the result of a function. |
| I13 | `ExecuteScalar<T>` and `ExecuteScalarAsync<T>`, converting as the mapping does (`ValueConverter`). |
| I14 | `Insert`, `Update`, `Delete`, `Merge` and their async versions return the number of affected rows. |
| I15 | `UniversalDataReader.Close()` overrides `DbDataReader.Close()` (Q01). |
| I16 | `WriteToServerAsync` for `IAsyncEnumerable<T>`. |
| I17 | `ExecuteQueryFirstColumn()` and `ReadAllFirstColumn()` return `IEnumerable<object?>`. |
| I18 | `ExecuteQuery<T>`, `ExecuteQueryAsync<T>`, `ReadAll<T>` and `ReadAllAsync<T>` take types without a parameterless constructor: such a type is created with its public constructor that has the most parameters. |
| I19 | The PostgreSQL copy of the SQL Server table creation code is gone (three files, never reachable); `ColumnDefinitionOptions` stays. |
| I20 | `MsSqlBulkCopy` and `NpgsqlBulkCopy`: one setup for the sync and the async path. |
| I21 | The conversion rules in `ValueConverter`; commented-out code and "temporary internal" markers gone; `DataReaderAttachedToConnectionStringContext.cs` named after its class. |
| I22 | XML documentation for every public type and member; the packages carry it. |
| I23 | Package metadata (MIT license, project URL, tags, README), Source Link, symbol packages, deterministic CI builds. |
| I24 | CI fails on warnings and packs the libraries (the packages are an artifact, not published). |
| I25 | README: packages, results of `UsingTransaction`, when queries run, records, `ExecuteScalar<T>`, `UseReader`, affected rows, `ToDataReader` with `FieldSettings`, bulk copy of `IAsyncEnumerable<T>`, precompiled commands, cancellation, the options and their precedence, `FieldsSelector`, untrusted input. |

## Changes for users of the library

### Behavior

- **I3 `FieldSettings.FromType<T>`** returns the same `FieldSettings<T>` objects on every call, in a new array;
  before, new objects. The settings are immutable, so only a comparison of references sees the difference.
- **I5 `UniversalDataReader.Settings`** returns the same collection on every access; before, a new collection
  over the same settings.
- **I26 the `CommandTextGenerator`** is called once per distinct command instead of on every call. It matters
  only for a custom generator whose output changes from call to call.
- **I27 compiled accessors**: a type that has been read or written 10,000 rows in a process gets compiled
  accessors, built by one work item on the thread pool. Results and exceptions are the same as with reflection.
- **I28 exceptions of the user's members**: an exception thrown by a property getter (parameters, bulk copy), a
  property setter or a constructor (reading) reaches the caller as it is; before, inside a
  `TargetInvocationException`.
- **I6 SQL Server parameter declarations**: strings are `nvarchar(4000)` up to 4,000 characters and `nvarchar(max)`
  above, byte arrays `varbinary(8000)` up to 8,000 bytes and `varbinary(max)` above; before, the length of each
  value. Values and results are the same; the server keeps one plan per statement instead of one per length.
  `DbParameter` objects of the caller are not touched.
- **I7 date and time members**: a `DateTime` value (what Microsoft.Data.SqlClient returns for `date`) is read
  into a `DateOnly` member, dropping a time part, and a `TimeSpan` value (for `time`) into a `TimeOnly` member;
  a negative `TimeSpan` or one of a day or more throws. Before, both threw `InvalidCastException`. The same for
  `ExecuteQueryFirstColumn<DateOnly>()` and the new `ExecuteScalar<T>`. SQL Server `CreateTable` creates
  `date`, `time` and `datetimeoffset` columns for `DateOnly`, `TimeOnly` / `TimeSpan` and `DateTimeOffset`
  members, which threw before.
- **I8 messages**: the texts of FastSql's exceptions changed (examples in the plan). The types stayed, except a
  duplicate field name in `ToDataReader` / `UniversalDataReader`: an `ArgumentException` from FastSql with the
  parameter name `settings`, instead of the dictionary's `ArgumentException`. A value that cannot be converted
  throws the same exception type as before, with a message that names the field and the types, and the
  original exception as `InnerException`. SQL Server `CreateTable` names what is wrong with the field list: a
  field the source does not have (and when it differs from one only in case: the list is compared with case,
  Q130), a field without a column type, a field listed twice.
- **I9 null arguments** throw `ArgumentNullException` at the call. Before, some threw `NullReferenceException`,
  and lazy and async methods threw only when enumerated or awaited. Still accepted as before: a null transaction
  for `CreateContext(DbTransaction)` and null `fieldType` / `dataTypeName` in `FieldSettings<T>`.
- **I10 parameter names** with letters of other alphabets (`@Größe`, `@имя`) are found in command texts; before,
  the name ended at the first such letter and the command failed.
- **I11 cancellation**: with a token that is already cancelled, the new overloads return a cancelled task
  without opening a connection; the token is checked once more before the commit.
- **I12 a result from `UsingConnection` / `UsingTransaction`**: a lambda with a value now binds to the new
  overload, which returns the value instead of dropping it. A lazy query returned from the delegate
  (`c => c.CreateSimple(text).ExecuteQuery<T>()`, also `ExecuteQueryAsync`) compiled before and did nothing; now
  it comes back as a sequence that fails when enumerated, because the connection is closed by then. The README
  and the XML documentation say to return `ToArray()` or `ToList()` of it.
- **I12 async delegates in the sync methods**: C# binds an async lambda, and any lambda that returns a task, to
  the new overloads with a result. The sync `UsingConnection` and `UsingTransaction` refuse such a result with an
  `InvalidOperationException` that names the async method, after rolling the transaction back. Before phase 4
  the same lambda bound to the `Action` overloads (an `async void` lambda, or a task nobody waited for), and the
  connection was closed or the transaction committed at its first `await`, with its exceptions unobserved.
- **I15 `Close()`** through a `DbDataReader` or `IDataReader` reference, and `CloseAsync()`, close a
  `UniversalDataReader` (Q01); before, they left it open.
- **I18 types without a parameterless constructor** can be read with `ExecuteQuery<T>` and `ReadAll<T>`; before,
  they did not compile. Types with one are read as before.

### Public API

Source compatible with phase 3, except two narrow cases named below. Binary compatibility is not kept, but the
next release needs a recompile anyway (it targets `net10.0` only and has Microsoft.Data.SqlClient types in its
signatures).

- **New members:**
  - I11: `ConnectionStringContext.UsingConnectionAsync(action, cancellationToken)`, and
    `UsingTransactionAsync(action, cancellationToken)` with and without an isolation level on
    `ConnectionStringContext` and `ConnectionContext` (5 overloads);
  - I12: `UsingConnection<TResult>`, `UsingConnectionAsync<TResult>`, `UsingTransaction<TResult>` and
    `UsingTransactionAsync<TResult>` (10 methods; the async ones take an optional token);
  - I13: `WrappedCommandExtensions.ExecuteScalar<T>` and `ExecuteScalarAsync<T>`;
  - I16: four `EnumerableExtensions.WriteToServerAsync<T>(this IAsyncEnumerable<T> ...)` overloads;
  - I8: the protected `BulkCopy.CheckFieldSelection(FieldsSelector, IReadOnlyCollection<string>,
    IReadOnlyCollection<string>, StringComparer)`, for bulk copies that name the fields in their message.
- **Changed members:**
  - I14: `Insert`, `Update`, `Delete`, `Merge` return `int` and their async versions `Task<int>` (16 methods).
    Code that passes one of them as a method group to a delegate without a result (`Action<...>`) no longer
    compiles (CS0407); a lambda works.
  - I17: `IWrappedCommand.ExecuteQueryFirstColumn()`, `DbCommandExtensions.ExecuteQueryFirstColumn()` and
    `DataReaderExtensions.ReadAllFirstColumn()` return `IEnumerable<object?>` instead of the non-generic
    `IEnumerable`.
  - I18: the `new()` constraint is gone from `ExecuteQuery<T>` and `ExecuteQueryAsync<T>` (`IWrappedCommand`,
    `DbCommandExtensions`), `ReadAll<T>` and `ReadAllAsync<T>` (8 methods).
  - I15: `UniversalDataReader.Close()` is an `override` instead of `new`; its `Dispose(bool)` override is gone,
    because `DbDataReader.Dispose` calls `Close()`.

  A class of your own that implements `IWrappedCommand` (the other narrow case) has to follow I17 and I18.
- **Documentation:** every public type and member has XML documentation (I22), shipped in the packages.

## Tests

| | Unit tests | Integration tests |
|---|---:|---:|
| End of phase 3 | 1,833 | 2,577 |
| Performance (I2 to I6, I26 to I28) | 1,911 | 2,580 |
| Behavior and public API (I7 to I18) | 2,120 | 2,674 |
| Code health and release readiness (I19 to I25) | 2,120 | 2,674 |

All pass locally on every branch; the integration tests run against SQL Server 2022 and PostgreSQL 18.6
containers.

- **New tests:** `FastAccessorTests` (every mapping below and above the threshold of I27, with the same rows,
  exceptions and edge cases), `CommandCacheTests` (I26: hits, misses and what a hit must not ignore),
  `ManyFieldsTests`, `SqlParameterSizeTests` and `SqlServerParameterSizeTests` (I6, including the plan cache of
  the server), `ArgumentCheckTests` (I9), `ConstructorReadingTests` (I18), `ExecuteScalarTests` (I13),
  `ErrorMessageTests` and `SqlCreateTableMessageTests` (I8), date and time conversions on both providers (I7),
  non-ASCII parameter names (I10), and `Phase4ApiTests` with its SQL Server and PostgreSQL variants (I11 to I16
  against real databases, including async delegates refused by the sync `Using` methods).
- **Tests that pinned what changed:**
  - I3 and I5: `FieldSettingsTests.FromType_EachCall_ReturnsNewInstances` and
    `UniversalDataReaderTests.Settings_ReturnsNewReadOnlyCollectionOverSameSettings` became
    `FromType_EachCall_ReturnsANewArrayOfTheSettingsKeptForTheType` and
    `Settings_ReturnsTheSameReadOnlyCollectionEveryTime`;
  - I8: assertions on message texts became patterns on the new texts;
  - I9: tests of null arguments expect `ArgumentNullException` at the call (one also its parameter name,
    `settings` instead of `source`);
  - I10: the parameter discovery tests take non-ASCII names;
  - I15: the Q01 tests expect the reader to be closed (`[Trait("Fixed", "Q01")]`);
  - I7: the SQL Server `CreateTable` test of an unsupported member type uses `Version` instead of `TimeSpan`,
    which has a column type now.
- **Public API files:** `Gerakul.FastSql.Common.txt` changed in the behavior PR, by exactly the members listed
  above; the SQL Server and PostgreSQL files did not change (I19 removed internal code only).
- **Quirks register** ([testing plan](testing-plan.md#7-quirks-register)): Q01 is fixed (I15); Q10, Q18, Q33 and
  Q52 say what phase 4 changed around them; Q130 is new (the field list of SQL Server `CreateTable` is compared
  with case).

## Performance

BenchmarkDotNet 0.15.8, .NET 10.0.15 (SDK 11.0.100), AMD Ryzen 7 7735HS (a laptop), Windows 11; the benchmark
project of I1, with in-memory readers only, and a type with 8 members (`int`, `string`, `decimal`, `DateTime`,
`int?`, `bool`, `long`, `Guid`); the commands are built for a row with a null `Age`, and the ADO.NET floor builds
the same command on the same connection. Before: the start of phase 4 (the plan and benchmarks PR, which changes no
library code). After: the end of phase 4, all four PRs. Both were run back to back in one session, with 5 warm-up
and 15 measured iterations. `dotnet run ... -- --validate` checks, in CI, that the benchmarks read the values of the
data and create the commands they should.

**Reading**

| Benchmark | Time before | Time after | | Allocated before | Allocated after | |
|---|---:|---:|---|---:|---:|---|
| `ReaderLoop`, 1 row ¹ | 4 ns | 4 ns | same | 0 | 0 |  |
| `ReadAll_Class`, 1 row | 3.75 µs | 287 ns | 13.1x less | 8.14 KB | 0.62 KB | 13.0x less |
| `ReadAll_Struct`, 1 row | 3.81 µs | 293 ns | 13.0x less | 8.20 KB | 0.69 KB | 11.9x less |
| `ReadAllAnonymous_Record`, 1 row | 1.81 µs | 289 ns | 6.3x less | 4.39 KB | 0.72 KB | 6.1x less |
| `ReadAllAnonymous_AnonymousType`, 1 row | 1.79 µs | 333 ns | 5.4x less | 4.39 KB | 0.72 KB | 6.1x less |
| `ReadAll_Objects`, 1 row | 45 ns | 46 ns | same | 0.31 KB | 0.31 KB | same |
| `ReadAllFirstColumn_Int`, 1 row | 31 ns | 29 ns | 1.1x less | 0.23 KB | 0.22 KB | 1.1x less |
| `ReadAllAsync_Class`, 1 row | 3.85 µs | 373 ns | 10.3x less | 8.27 KB | 0.76 KB | 10.9x less |
| `ExecuteQuery_Class`, 1 row | 3.76 µs | 506 ns | 7.4x less | 8.36 KB | 0.84 KB | 9.9x less |
| `ExecuteQueryAsync_Class`, 1 row | 4.16 µs | 552 ns | 7.5x less | 8.54 KB | 1.02 KB | 8.3x less |
| `ReaderLoop`, 1,000 rows ¹ | 4.06 µs | 4.07 µs | same | 0 | 0 |  |
| `ReadAll_Class`, 1,000 rows | 273 µs | 55.23 µs | 5.0x less | 118 KB | 86.48 KB | 1.4x less |
| `ReadAll_Struct`, 1,000 rows | 254 µs | 61.96 µs | 4.1x less | 118 KB | 86.54 KB | 1.4x less |
| `ReadAllAnonymous_Record`, 1,000 rows | 159 µs | 58.98 µs | 2.7x less | 215 KB | 172 KB | 1.2x less |
| `ReadAllAnonymous_AnonymousType`, 1,000 rows | 157 µs | 63.25 µs | 2.5x less | 215 KB | 172 KB | 1.2x less |
| `ReadAll_Objects`, 1,000 rows ¹ | 22.75 µs | 19.16 µs | 1.2x less | 86.16 KB | 86.16 KB | same |
| `ReadAllFirstColumn_Int`, 1,000 rows | 5.25 µs | 4.71 µs | 1.1x less | 0.23 KB | 0.22 KB | 1.1x less |
| `ReadAllAsync_Class`, 1,000 rows | 314 µs | 86.90 µs | 3.6x less | 141 KB | 110 KB | 1.3x less |
| `ExecuteQuery_Class`, 1,000 rows | 260 µs | 56.24 µs | 4.6x less | 118 KB | 86.70 KB | 1.4x less |
| `ExecuteQueryAsync_Class`, 1,000 rows | 296 µs | 84.50 µs | 3.5x less | 118 KB | 86.95 KB | 1.4x less |

**The source of a bulk copy**

| Benchmark | Time before | Time after | | Allocated before | Allocated after | |
|---|---:|---:|---|---:|---:|---|
| `FromType` | 1.73 µs | 32 ns | 53.6x less | 6.13 KB | 0.09 KB | 71.3x less |
| `ToDataReader_GetValue` | 142 µs | 34.80 µs | 4.1x less | 180 KB | 173 KB | same |
| `ToDataReader_IsDBNullAndGetValue` | 277 µs | 66.75 µs | 4.2x less | 352 KB | 345 KB | same |

**Command creation**

| Benchmark | Time before | Time after | | Allocated before | Allocated after | |
|---|---:|---:|---|---:|---:|---|
| `AdoNet_Insert` ¹ | 231 ns | 211 ns | 1.1x less | 1.70 KB | 1.70 KB | same |
| `CreateInsert` | 5.19 µs | 429 ns | 12.1x less | 13.86 KB | 1.91 KB | 7.3x less |
| `CreateInsert_Precompiled` | 405 ns | 398 ns | same | 1.87 KB | 1.87 KB | same |
| `CreateUpdate` | 6.08 µs | 443 ns | 13.7x less | 15.60 KB | 2.09 KB | 7.5x less |
| `CreateDelete` | 4.08 µs | 132 ns | 30.9x less | 10.17 KB | 0.77 KB | 13.1x less |
| `CreateMerge` | 7.47 µs | 430 ns | 17.4x less | 20.73 KB | 2.09 KB | 9.9x less |
| `CreateMapped_Text` | 5.32 µs | 195 ns | 27.2x less | 14.06 KB | 1.05 KB | 13.3x less |
| `CreateMapped_Text_AnonymousType` | 3.59 µs | 203 ns | 17.7x less | 9.50 KB | 1.09 KB | 8.7x less |
| `CreateMapped_Precompiled` | 184 ns | 179 ns | same | 1.05 KB | 1.05 KB | same |
| `CreateMapped_Text_PostgreSql` | 4.97 µs | 162 ns | 30.7x less | 14.27 KB | 1.27 KB | 11.3x less |
| `CreateProcedure` | 4.46 µs | 446 ns | 10.0x less | 12.82 KB | 2.05 KB | 6.2x less |
| `CreateSimple` | 192 ns | 145 ns | 1.3x less | 1.25 KB | 1.16 KB | 1.1x less |
| `CompileInsert` | 4.44 µs | 5 ns | 968.5x less | 11.99 KB | 0.03 KB | 383.7x less |
| `CompileMapped_Text` | 4.73 µs | 2 ns | 2433.3x less | 12.97 KB | 0 | none left |

¹ Runs no code that phase 4 changed; the difference is the spread between runs on this laptop, which is up to
about 10% for a benchmark and in both directions.

Where the gains come from: the setup of a query and of a command no longer analyzes the type (I2 to I4), the
short `Create...` forms reuse the last compiled command (I26), and the mapping of a type that has been read or
written 10,000 rows uses compiled accessors (I27), which is the per-row part (1,000 rows: 273 to 55 µs, with a
reader loop that does no mapping at 4 µs). Allocations drop by 6 to 13 times per operation where the setup was
the cost; per row, the boxing of the values remains. The precompiled commands (`CreateMapped(compiled, value)`)
were already cheap and stay as they were.

The behavior PR keeps these numbers: run back to back with the performance PR alone, its reads are within the
spread (`ReadAllAsync<T>` with 1,000 rows, 86 against 96 µs, varied from 76 to 101 µs between runs of the same
code), after the fix described in the [notes](#notes).

**First use of a type in a new process** (20 processes per benchmark, one call each, after a warm-up of the
library with another type), the start and the end of phase 4 back to back, in two rounds:

| Benchmark | Before (1) | After (1) | Before (2) | After (2) |
|---|---:|---:|---:|---:|
| `Read_OneRow` | 594 µs | 573 µs | 587 µs | 573 µs |
| `Read_5000Rows` | 5,010 µs | 4,640 µs | 5,141 µs | 4,762 µs |
| `Read_20000Rows` | 14,864 µs | 11,463 µs | 15,403 µs | 11,873 µs |
| `CreateInsert` | 1,081 µs | 1,004 µs | 1,082 µs | 1,030 µs |
| `ToDataReader_5000Rows` | 4,043 µs | 3,909 µs | 4,016 µs | 3,968 µs |
| `ToDataReader_20000Rows` | 8,298 µs | 7,428 µs | 8,270 µs | 7,624 µs |

The 20,000-row cases cross the threshold of I27, so the accessors are compiled during the call, on the thread
pool. How I27 got there, each version of the performance PR measured back to back against the start of phase 4,
with the cold 5,000-row read:

| I27 version | Before | After |
|---|---:|---:|
| as planned: compiled at 1,000 rows, on the thread that reads | 4,395 µs | 5,945 µs (+35%) |
| compiled on the thread pool | 4,336 µs | 4,927 µs (+14%) |
| and after 10,000 rows instead of 1,000 ² | 5,005 µs | 6,141 to 7,022 µs |
| and reflection called directly, row code optimized from its first call ² | 5,005 µs | 4,777 µs (-5%) |

² With the longer warm-up of the benchmark, which makes both branches slower. Without any compile the first read
was still slower: with tiered compilation off both branches took the same time (3.95 and 3.98 ms), so the cost
was the unoptimized code of a new process making more calls per value (a delegate around reflection) and per row.

End to end, against a database, the plan measured what to expect: a single query or insert does not get
measurably faster (a round trip to a local server takes 0.7 to 0.8 ms), but allocates 55% to 78% less; large
results gain up to 13% (SQL Server) and 4% (PostgreSQL) of wall-clock time; bulk copy from objects about 10% on
PostgreSQL. I6 was checked against the server: the same statement with strings of ten lengths leaves one plan in
the cache instead of ten (`SqlServerParameterSizeTests`).

## Notes

- **I27 deviates from the plan**, because the first-use benchmark showed a regression, which the plan does not
  allow (see [Performance](#performance)):
  - the accessors are compiled on the thread pool instead of on the thread that reaches the threshold, and
    after 10,000 rows of a type instead of about 1,000, above the break-even the plan measured (about 7,500
    rows);
  - until then reflection is called directly, not through a delegate, and the methods that run for every row of
    a read are optimized from their first call (`MethodImplOptions.AggressiveOptimization`): in a new process the
    row code runs unoptimized at first, and its extra calls made the first read of a type slower than before
    phase 4 (with tiered compilation off, both took the same time).
- The behavior PR first made every row of a typed read 19% to 43% slower: the conversion of a value held the
  exception handling that names the field (I8), which kept it from being inlined, and the sync queries of
  `DbCommandExtensions` shared one iterator that took the reading as a delegate, to check their arguments at the
  call (I9). The conversion has a separate method for the exception handling now, and each query its own
  iterator again; back to back, the reads are as fast as with the performance PR alone.
- After the review of the behavior PR: the sync `UsingConnection` and `UsingTransaction` refuse async delegates
  (see Behavior), and the SQL Server `CreateTable` message names a field listed twice, for which it had no reason
  at all. Its case-sensitive comparison of the field list stayed, as it was before phase 4 (Q130).
- I28 came with the performance PR instead of the behavior PR: the compiled accessors and reflection must throw
  the same exceptions, which needs I28. The conversion rules moved into `ValueConverter` (I21) in the behavior
  PR, because `ExecuteScalar<T>` (I13) uses them.
- Not done from I21: the three async row loops (`DbCommandExtensions`, `DataReaderExtensions`,
  `WrappedCommandWithContext`) still have one loop each. A shared loop would be an async iterator inside the
  iterator of each method, one more state machine per row.
- I22: overloads repeat the text of their family instead of `inheritdoc` with a reference to one overload, so
  every overload shows the whole text in IntelliSense. Where one text does not fit all overloads (the `Using`
  methods with and without a result or a token, `ToDataReader` with and without `caseSensitive`), each overload
  has its own.
- I24: the build fails on NuGet audit warnings too, so a new advisory for a dependency fails CI until the
  dependency is updated (or the warning is let through with `WarningsNotAsErrors`).
- I1: after the first measurement, the first-use benchmarks got a longer warm-up (50,000 rows, more than any
  threshold) and reads of 20,000 rows, which cross the threshold of I27.
- I1, after the review of the benchmark PR: a validation run (`-- --validate`, in CI) checks the values every read
  benchmark returns and the commands every command benchmark creates; the anonymous prototype is created once, out
  of the measurement; the ADO.NET floor builds its command on the same connection as FastSql, for a row with a null
  `Age`. The tables above were measured after these changes.
