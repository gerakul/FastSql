# Modernization plan (phase 3)

Status: implemented; what changed, the behavior changes and the benchmark results are in
[modernization-notes.md](modernization-notes.md).

FastSql was written for `netstandard1.3` / `net46` and C# 7. Phase 1 moved it to `net10.0` and phase 2
fixed its bugs, but the code still looks like it did in 2018. This phase brings in the C# and .NET features
that did not exist then, where they make the code simpler, safer or faster.

Rules for this phase:

- The design and the public contracts stay. Nothing public is removed or renamed; the only public API
  changes are the ones listed per item (annotations and two members).
- Behavior may change slightly; every change is listed below and will go into the release notes.
- Performance must not get worse: the hot paths are benchmarked before and after (see [Verification](#verification)).
- The code style of the project stays where it is a matter of taste rather than of language version:
  Allman braces, `this.` in constructors, block bodies for methods.

The items are numbered M1 to M17. Three of them needed a decision: [D1 to D3](#decisions), all answered.

## Summary

| ID | Change | Gain | Behavior change | Public API |
|---|---|---|---|---|
| M1 | Async iterators instead of the hand-written async enumerator | less code, fewer allocations per row | small (D3 = B) | none |
| M2 | Async disposal and async transactions | Npgsql: no thread blocked on commit, close, dispose | none | none |
| M3 | Regex source generator for parameter discovery | faster command compilation | none | none |
| M4 | Ordinal case-insensitive comparers instead of lower-cased copies | fewer allocations, half the branches | special Unicode letters only | none |
| M5 | UniversalDataReader: no allocations per row, async disposal | faster bulk copy from async sources | none | adds `DisposeAsync` override |
| M6 | `Array.Empty`, sealed internal classes | fewer allocations | none | none |
| M7 | Pattern matching and switch expressions | readability | none | none |
| M8 | `using` declarations | readability | none | none |
| M9 | Get-only auto-properties, `readonly` fields | immutability | none | none |
| M10 | Record structs and tuples for internal data | readability, one place for type rules | none | none |
| M11 | Remove relics of old targets and old C# | readability, ordinal string ops | culture-ignorable characters only | none |
| M12 | Less ceremony around connection scopes | one closure less per call | none | none |
| M13 | Expression bodies for trivial properties | readability | none | none |
| M14 | `new` on `UniversalDataReader.Close()` | removes the only compiler warning | none (Q01 stays) | `new` modifier |
| M15 | Nullable reference types (D1) | null contracts for users and the compiler | none | annotations |
| M16 | File-scoped namespaces, implicit usings, `.editorconfig` (D2) | less nesting and boilerplate | none | none |
| M17 | Benchmark results and notes | proof of "not slower" | — | — |

## Decisions

- **D1 — nullable reference types (M15): yes.** Enabled in the three libraries. They are the biggest
  difference between 2018 and today's C# (nullable *value* types, `int?`, are already used everywhere in the
  options). Cost: about 250 annotations, and the approved public API files change (annotations only, no
  runtime change).
- **D2 — file-scoped namespaces and implicit usings (M16): yes.** As the last commit, purely mechanical,
  listed in `.git-blame-ignore-revs`. Earlier phases kept block-scoped namespaces on purpose; Allman braces
  and `this.` stay.
- **D3 — cancellation semantics of async queries (M1): B, plain iterators.** A compiler-generated iterator
  is finished once it has thrown, so after an `OperationCanceledException` the next `MoveNextAsync` returns
  `false`; before this phase it threw again (the phase 2 fix of Q04). The options were:
  - A: keep throwing on every later `MoveNextAsync`, with a small guard enumerator around the iterators;
  - B: plain iterators, the behavior of the language feature.

  `await foreach` and LINQ stop at the first exception, so they behave the same under both; only code that
  calls `MoveNextAsync` by hand after catching the exception sees the difference. .NET itself has no rule
  for this (`IAsyncEnumerator<T>` does not say what happens after an exception). A probe (cancel after the
  first row, then call `MoveNextAsync` three more times, SQLite in memory) showed both behaviors:

  | Source | move 1 | move 2 | move 3 | move 4 |
  |---|---|---|---|---|
  | C# async iterator (`yield return`) | true | `OperationCanceledException` | false | false |
  | `System.Linq.AsyncEnumerable.Select` | true | `OperationCanceledException` | false | false |
  | Dapper `QueryUnbufferedAsync` | true | `TaskCanceledException` | false | false |
  | EF Core `AsAsyncEnumerable` | true | `TaskCanceledException` | `TaskCanceledException` | `TaskCanceledException` |
  | FastSql before this phase | true | `OperationCanceledException` | `OperationCanceledException` | `OperationCanceledException` |

  With B, a token passed to the query method that is already cancelled still surfaces the driver's own
  exception, as before (the iterators take the two tokens separately and check the enumeration's first).

## A. Modern .NET: runtime and performance

### M1 Async iterators instead of the hand-written async enumerator

`AEnumerable`, `AEnumerator`, `AsyncState`, `Helpers.CreateAsyncEnumerable` and the three
`CreateAsyncEnumerable` helpers (`DbCommandExtensions`, `DataReaderExtensions`, `WrappedCommandWithContext`)
are a state machine built from four delegates, written before C# 8 had `async IAsyncEnumerable<T>` methods.
They become three short iterator methods, for example:

```csharp
var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
await using var readerScope = reader.ConfigureAwait(false);
var info = readInfoGetter(reader);
while (true)
{
    cancellationToken.ThrowIfCancellationRequested();
    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) break;
    yield return info.GetValue();
}
```

- `[EnumeratorCancellation]` links the token given to the query method with the one given to the
  enumeration, which is what `AEnumerable.GetAsyncEnumerator` does by hand today (Q122).
- Same laziness (nothing runs before the first `MoveNextAsync`, mapping errors surface there), same
  disposal order (reader, then connection), `Current` is the default before the first and after the last row.
- Faster: today every row goes through two delegates and, when the driver completes asynchronously, two
  allocated state machines; the compiler's iterator reuses itself as the source of every `ValueTask`.
- One edge case changes with either D3 option: after a failure that is not a cancellation (driver error,
  mapping error) a further `MoveNextAsync` returns `false`; today it runs the failed step again (for a
  mapping error: executes the command again).
- Files: `AEnumerable.cs` and `AsyncState.cs` are deleted; `ReadInfo` no longer owns the reader.

### M2 Async disposal and async transactions

The async paths dispose synchronously and run transactions with the sync API, because `IAsyncDisposable`
and `DbTransaction.CommitAsync` did not exist in `netstandard2.0`:

- `ConnectionContext.UsingTransactionAsync`: `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`, and
  `await using` for the transaction. A failing rollback still does not hide the action's exception (Q123).
- `await using` for the connection in `ConnectionStringContext.UsingConnectionAsync`, for the reader in both
  `UseReaderAsync` implementations and `EnumerableExtensions.WriteToServerAsync`, and for the binary
  importer in `NpgsqlBulkCopy.WriteToServerAsync`.
- With Npgsql, commit, rollback, closing a reader with unread rows and returning a connection to the pool
  no longer block a thread on network I/O. Microsoft.Data.SqlClient implements the async versions with the
  sync calls (base class defaults), so nothing changes for SQL Server.

### M3 Regex source generator for parameter discovery

`SqlContextProvider` and `NpgsqlContextProvider` parse `@name` parameters with an interpreted `Regex`
held in a mutable static field, on every `CompileMapped` / `CreateMapped` call. `[GeneratedRegex]`
(.NET 7) builds the matcher at compile time. Same pattern, same results; the classes become `partial`
(not visible in the public API). `.Cast<Match>()` goes too: `MatchCollection` is `IEnumerable<Match>`
since .NET Core 2.0.

### M4 Ordinal case-insensitive comparers instead of lower-cased copies

Name matching makes lower-cased copies (`x.Name.ToLowerInvariant()`) and has a case-sensitive and a
case-insensitive copy of the same code in `MappedCommand`, `ReadInfoFactory.CreateByType`,
`ReadInfoFactory.CreateAnonymous`, `MsSqlBulkCopy` and `NpgsqlBulkCopy`. With `StringComparer.Ordinal` /
`StringComparer.OrdinalIgnoreCase` passed to `GroupBy`, `Join` and the dictionary of `UniversalDataReader`,
each pair becomes one path and no strings are copied; `UniversalDataReader.GetOrdinal` on a
case-insensitive reader no longer allocates on every call.

Behavior: the two comparisons differ only for a few special letters (the Kelvin sign `K` equals `k` after
`ToLowerInvariant` but not under `OrdinalIgnoreCase`). Key and ignore lists use `OrdinalIgnoreCase` since
phase 2, so all name matching becomes consistent. The duplicate-name `ArgumentException` of
`UniversalDataReader` names the field as given instead of lower-cased.

### M5 UniversalDataReader: no allocations per row, async disposal

- `Read()` over an `IAsyncEnumerable<T>` source turns every `MoveNextAsync` into a `Task` to block on it;
  when the value task has already completed (buffered rows, in-memory sources) it is read directly.
- `GetChars` over a `string` value copies the whole string (`ToCharArray()`) on every call; it copies the
  requested slice instead.
- Overrides `DisposeAsync`, so `await using` disposes an async source with `await`; today
  `DbDataReader.DisposeAsync` calls `Dispose`, which blocks on the source. Public API: the override is new.

### M6 Fewer allocations

- `new T[0]` and empty `new List<string>()` placeholders become `Array.Empty<T>()` / `[]` (`MappedCommand`,
  `Helpers`, `CommandCompilator`, `CreateInsertAndGetID`, both providers' `DataReaderExtensions`).
- Internal classes become `sealed` (faster type checks and devirtualization; the .NET analyzers recommend it).

## B. Modern C#: language

### M7 Pattern matching and switch expressions

- `x is T` followed by a cast `(T)x` becomes `x is T t`: `ReadAsyncInternal`, both providers'
  `GetColumnDefinitions`, the `Connection` / `Transaction` properties of both bulk copies.
- `GetBulkWriter` becomes a switch expression on the context type; `!(o is SqlBulkOptions opt)` becomes `is not`.
- `ColumnDefinition.GetTypeDeclaration` becomes a switch expression with `or` patterns, and the 20-branch
  `if` chain of `ColumnDefinition.FromFieldType` (one branch per type and per nullable type) becomes one
  lookup on the underlying type, with the same results, unsupported types (enums, `char`) included.
  Both providers have the same copy of this class; both change.

### M8 `using` declarations

`using (var x = ...) { ... }` blocks that last until the end of the method become `using var x = ...;`
(and `await using`, M2): one nesting level less in `ConnectionStringContext`, `ConnectionContext`, both
wrapped commands, `DbCommandExtensions`, `EnumerableExtensions` and both bulk copies.

### M9 Get-only auto-properties and `readonly` fields

- `{ get; private set; }` properties that are set only in constructors become `{ get; }`: `FieldSettings<T>`
  (6), `SimpleCommand` and `MappedCommand` (`CommandText`, `CommandType`). Private setters are not part of
  the public API, so it does not change.
- About 25 private fields assigned only in constructors become `readonly` (`contextProvider`, the arrays of
  `UniversalDataReader`, ...). The protected fields of `BulkCopy` stay as they are: they are public API.

### M10 Record structs and tuples for internal data

- `ReaderField`, `MappedCommand.ParMap` and the `Mapping` classes of both bulk copies become
  `readonly record struct`s (`NpgsqlBulkCopy` writes the reader ordinals into its mappings afterwards; they
  become a separate array). Anonymous types in the joins become tuples.
- `ReadInfoByType`, `ReadInfoAnonymous` and `ReadInfoFirstColumn<T>` keep five parallel arrays per member
  kind (`fi`, `fiInd`, `fiNullable`, `fiConvertingType`, `fiIsEnum`, ...), and the rules "nullable when a
  reference type or `Nullable<T>`", "convert to the underlying type", "is an enum" are written four times.
  They become one array of `(member, ValueSource)` per kind, where `ValueSource` is a record struct created
  in one place.

### M11 Relics of old targets and old C#

- `type.GetTypeInfo().IsValueType` / `.IsEnum` (needed in `netstandard1.x`) become `type.IsValueType` /
  `type.IsEnum`.
- `string.Format` becomes string interpolation; `default(CancellationToken)` / `default(T)` become `default`;
  `Groups["Name"].ToString()` becomes `.Value`; `type.Equals(typeof(x))` becomes `type == typeof(x)`;
  `name.Substring(1)` becomes `name[1..]`.
- `tableName.StartsWith("#")` is a culture-sensitive comparison; it becomes `StartsWith('#')` and
  `StartsWith("[#", StringComparison.Ordinal)`. Differs only for names that start with characters the culture
  ignores (zero-width characters).
- Unused `using` directives go.

### M12 Less ceremony around connection scopes

`WrappedCommandWithContext.ExecuteNonQuery(Async)` / `ExecuteScalar(Async)` assign a captured `result`
variable inside a lambda. Internal generic `UsingConnection<TResult>` / `UsingConnectionAsync<TResult>`
on `ConnectionStringContext` return the value directly (one closure and one async state machine less per
call). The five sync query methods share one iterator helper, and the five async ones one async iterator (M1).

### M13 Expression bodies for trivial properties

`get { return x; }` properties become `=> x` (`UniversalDataReader.Depth`, `IsClosed`, `FieldCount`, the
indexers, `DbContext.CommandCompilator`, ...), as some properties already are. Methods keep block bodies.

### M14 `new` on `UniversalDataReader.Close()`

`Close()` hides `DbDataReader.Close()` (Q01, kept as it is in phase 2), which is the build's only warning
(CS0114). The `new` modifier states the hiding explicitly; nothing else changes. The approved public API
shows the modifier.

## C. Decided by you

### M15 Nullable reference types (D1)

`<Nullable>enable</Nullable>` in the three libraries; the test projects stay `disable`.

- Annotated: parameters that accept `null` (`ReadOptions? readOptions = null`, the `QueryOptions` /
  `BulkOptions` parameters, ignore and key lists where `null` means "none"), results that can be `null`
  (`ExecuteScalar` → `object?`, `FieldSettings<T>.Getter` → `Func<T, object?>`), nullable option
  properties (`SqlBulkOptions.ColumnDefinitionOptions`, `ColumnDefinitionOptions.PrimaryKey`).
- No runtime null checks are added. Where the compiler points to a real null bug, it is reported in the
  PR, not fixed silently.
- Measured on the current code: about 120 warnings in Common and 80 in each provider.

### M16 File-scoped namespaces, implicit usings, `.editorconfig` (D2)

- `namespace X;` instead of `namespace X { ... }`: one indentation level less in every file.
- `<ImplicitUsings>enable</ImplicitUsings>` in the libraries; the `using System;`, `System.Linq`,
  `System.Threading.Tasks`, ... lines go.
- An `.editorconfig` records the project's style (file-scoped namespaces, Allman braces, ...) so IDEs keep it.
- One mechanical commit, listed in `.git-blame-ignore-revs`.

## Considered and not proposed

- **Records for the options classes** (`QueryOptions`, `BulkOptions`, `ReadOptions`): public, mutable by
  design, with their own `Clone` / `SetDefaults` semantics; value equality would change their behavior.
- **Primary constructors:** the captured parameters are not `readonly`, and the explicit `this.` assignments
  are the project's style.
- **`params ReadOnlySpan<string>` / `params IEnumerable<string>`** (C# 13) and **`IEnumerable<object>`
  instead of the non-generic `IEnumerable`** of `ExecuteQueryFirstColumn()`: binary-breaking API changes.
- **`CancellationToken` overloads** for `UsingConnectionAsync` / `UsingTransactionAsync`: new public API.
- **Faster member access** (compiled getters and setters, caching `FieldSettings.FromType` and the member
  lists per type): the largest remaining performance opportunity, since every `Insert` / `CreateMapped`
  reflects over the type again. It is a caching design rather than a newer language or runtime feature,
  and reflection invoke is already emit-based in .NET 8+. Worth a phase of its own.
- **`SearchValues` in the literal and comment stripper of parameter discovery:** a small gain for code
  that phase 2 had to get exactly right (Q11).
- **Raw string literals for generated SQL:** `Merge` uses `Environment.NewLine`; a raw literal would put
  the source file's line endings into the generated text instead.
- **`UniversalDataReader.Close()` as an override:** Q01, decided "leave" in phase 2.
- **Trimming / Native AOT annotations:** the mapping is reflection-based by design.
- **Turning on the .NET analyzers in the build** (`AnalysisLevel`): most findings are public API names
  (parameter names of `UniversalDataReader` overrides, `CancellationToken` position) that cannot change;
  the useful ones are covered above (M4, M6, M11).

## Verification

- Unit and integration tests, locally (Docker) and in CI. Tests change only where a listed behavior
  changes; each such change is named in the PR.
- Public API: the approved files change only for the items that say so (M5, M14, M15).
- Benchmarks (BenchmarkDotNet, in a scratch project that is not committed), the current `modernization`
  branch against this branch, time and allocations:
  - async reading: `ReadAllAsync<T>` and `DbCommand.ExecuteQueryAsync<T>` over an in-memory reader;
  - sync reading: `ReadAll<T>`, `ReadAllAnonymous`;
  - `ToDataReader` over `IEnumerable<T>` and `IAsyncEnumerable<T>`, read to the end;
  - command compilation: `CompileMapped` with parameter discovery, `CreateInsert`.

  A regression blocks the item that causes it. The results go into the notes.

## Delivery

Branch `modernize/csharp` (worktree `.claude/worktrees/modernize-csharp`), one PR into `modernization`:

1. this plan;
2. M1, M2 (async);
3. M3 to M6 (performance);
4. M7 to M14 (language);
5. M15 (nullable reference types, if D1 = yes);
6. M16 (style, if D2 = yes);
7. M17: `docs/modernization-notes.md` (what changed, behavior changes for users, benchmark results),
   updates to the quirks register in `testing-plan.md` where a pinned behavior changed.
