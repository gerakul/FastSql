# Modernization notes (phase 3)

Phase 3 brought the C# and .NET features that did not exist when FastSql was written into the code,
following the [modernization plan](modernization-plan.md). The design and the public contracts stayed;
the decisions were: nullable reference types (D1) yes, file-scoped namespaces and implicit usings (D2) yes,
plain async iterators (D3 = B).

## What changed

| Item | Change |
|---|---|
| M1 | The hand-written async enumerator (`AEnumerable`, `AEnumerator`, `AsyncState`, four delegates per query) became three async iterator methods (`DbCommandExtensions`, `DataReaderExtensions`, `WrappedCommandWithContext`). |
| M2 | Async paths dispose readers, connections, transactions and the Npgsql binary importer with `DisposeAsync`; `UsingTransactionAsync` uses `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`. |
| M3 | Parameter discovery uses a regex generated at compile time (`[GeneratedRegex]`). |
| M4 | Name matching uses `StringComparer.Ordinal` / `OrdinalIgnoreCase` instead of lower-cased copies, one code path for both cases. |
| M5 | `UniversalDataReader`: `Read()` over an async source needs no task when the move has completed, `GetChars` reads a string in place, `DisposeAsync` disposes an async source without blocking. |
| M6 | `Array.Empty` / `[]` instead of zero-length arrays and empty lists; internal classes are sealed. |
| M7 | Pattern matching and switch expressions; `ColumnDefinition`'s 20-branch type chain is one lookup. |
| M8 | `using` declarations. |
| M9 | Get-only auto-properties, `readonly` fields. |
| M10 | Record structs and tuples for internal data; `ValueSource` holds the nullable / underlying type / enum rules of the mapping in one place instead of four. |
| M11 | No more `GetTypeInfo()` (a `netstandard1.x` relic), `string.Format`, `default(T)`; `??=` in the options' `SetDefaults`; ordinal `StartsWith`. |
| M12 | Generic internal `UsingConnection` / `UsingConnectionAsync` return command results directly; the sync queries of a connection string context share one iterator. |
| M13 | Expression bodies for trivial properties. |
| M14 | `UniversalDataReader.Close()` hides `DbDataReader.Close()` explicitly (`new`), as it did before (Q01); the build has no warnings. |
| M15 | Nullable reference types in the three libraries (the test projects stay disabled). |
| M16 | File-scoped namespaces, implicit usings, `.editorconfig`; the commit is in `.git-blame-ignore-revs`. |

The libraries have 14% fewer lines (5,690 to 4,883); without blank lines, comments, lone braces and `using`
lines, 12% fewer (2,699 to 2,373), mostly from M1, M4, M7 and M10.

## Changes for users of the library

### Behavior

- **Async queries after a failure.** The enumerators are async iterators now, which are finished once they
  have thrown:
  - after an `OperationCanceledException` a further `MoveNextAsync` returns `false`; in phase 2 it threw
    again (Q04). `await foreach` and LINQ stop at the first exception, so they see no difference;
  - after any other failure (a driver error, a mapping error) a further `MoveNextAsync` returns `false`;
    before, it ran the failed step again (after a mapping error it executed the command again).

  Unchanged: a cancelled enumeration token throws before a connection is opened, a token passed to the
  query method that is already cancelled surfaces the driver's own exception, and either token cancels the
  reading of rows (Q122).
- **Npgsql does async I/O asynchronously** in `UsingTransactionAsync` (begin, commit, rollback) and when async
  paths close readers, connections and binary imports; before, these calls blocked a thread.
  Microsoft.Data.SqlClient implements the async versions with its sync calls, so nothing changes for SQL Server.
- **Case-insensitive name matching** (reader fields to members, parameters to members, bulk copy columns,
  `UniversalDataReader.GetOrdinal`) uses `OrdinalIgnoreCase` instead of comparing `ToLowerInvariant` copies.
  The two differ only for a few special letters (the Kelvin sign `K` equals `k` after `ToLowerInvariant`, not
  under `OrdinalIgnoreCase`); key and ignore lists already used `OrdinalIgnoreCase` since phase 2.
- **`UniversalDataReader`**: the `ArgumentException` for duplicate field names names the field as given instead of
  lower-cased; `GetOrdinal(null)` on a case-insensitive reader throws `ArgumentNullException` instead of
  `NullReferenceException`; `GetChars` over a `null` `char[]` value returns 0 instead of throwing
  `NullReferenceException`; `await using` / `DisposeAsync` disposes an async source without blocking.
- **SQL Server table creation** with `CheckTableIfNotExistsBeforeCreation` recognizes temp tables with an ordinal
  comparison; the culture-sensitive one also matched names that start with characters the culture ignores.

### Public API

Binary compatible with phase 2: nothing was removed, renamed or retyped.

- **Nullable annotations** (compile-time only; they matter to projects that enable nullable reference types):
  - parameters that accept `null`: `QueryOptions?`, `ReadOptions?`, `BulkOptions?`, ignore lists, parameter name
    lists and `notKeyIgnoreFields` (`null` means none), `CreateSimple(..., params object?[] parameters)`,
    `ContextProvider.AddParamWithValue(..., object? value)` and `ApplyQueryOptions(..., QueryOptions?)`;
  - results that can be `null`: `IWrappedCommand.ExecuteScalar()` → `object?`, `ExecuteScalarAsync` →
    `Task<object?>`, `ExecuteQueryFirstColumnAsync()` / `ReadAllFirstColumnAsync()` → `IAsyncEnumerable<object?>`,
    `FieldSettings<T>.Getter` → `Func<T, object?>`, `ColumnDefinitionOptions.PrimaryKey`,
    `SqlBulkOptions.ColumnDefinitionOptions`.

  Generic results (`ExecuteQuery<T>`, `ExecuteQueryFirstColumn<T>`) keep `T`: the caller says whether values
  can be `null` (`ExecuteQueryFirstColumn<string?>()`), as with Dapper and EF Core.
- **New member:** `UniversalDataReader.DisposeAsync()` (an override of `DbDataReader.DisposeAsync`).

## Tests

- Two tests changed, both for D3: `ReadAllAsyncTests.ReadAsync_TokenCancelledAfterFirstRow_MoveNextThrowsWithoutReadingAsync`
  and `ExecutionSemanticsTests.MoveNext_TokenCancelledInTheMiddle_ThrowsOperationCanceledExceptionAsync` expect
  `false` from the `MoveNextAsync` after the cancellation. The Q04 row of the
  [quirks register](testing-plan.md#7-quirks-register) says so.
- The approved public API files changed for M5 (`DisposeAsync`) and M15 (annotations). M14 did not change them:
  `new` on a non-virtual method is not part of the metadata.
- CI uploads the `*.received.txt` files of a failing public API test as an artifact, so the approved files can be
  updated also when the tests cannot run locally.

## Performance

BenchmarkDotNet 0.15.8, .NET 10.0.15, AMD Ryzen 7 7735HS (a laptop), Windows 11. The `modernization` branch
before this phase against this branch, from a scratch project that is not in the repository; the readers are
in-memory `DataTable` readers with 1,000 rows of five columns (`int`, `string`, `decimal`, `DateTime`, `int?`),
the asynchronous driver is one that completes every `ReadAsync` asynchronously, as a driver waiting for the
network does.

| Benchmark | Time before | Time after | Allocated before | Allocated after |
|---|---:|---:|---:|---:|
| `CompileMapped<T>` with parameter discovery, SQL Server | 10.86 µs | 5.15 µs | 14.19 KB | 13.34 KB |
| `CompileMapped<T>` with parameter discovery, PostgreSQL | 10.35 µs | 5.20 µs | 14.19 KB | 13.34 KB |
| `CreateInsert`, SQL Server ¹ | 3.82 µs | 3.91 µs | 9.74 KB | 9.58 KB |
| `ReadAll<T>` ¹ | 317 µs | 350 µs | 183.3 KB | 182.0 KB |
| `ReadAllAnonymous` | 312 µs | 287 µs | 254.7 KB | 253.6 KB |
| `ReadAllAsync<T>` | 387 µs | 337 µs | 206.9 KB | 205.6 KB |
| `DbCommand.ExecuteQueryAsync<T>` | 427 µs | 352 µs | 207.1 KB | 182.4 KB |
| `ExecuteQueryFirstColumnAsync<int>` | 95.4 µs | 83.7 µs | 48.1 KB | 24.7 KB |
| `ExecuteQueryFirstColumnAsync<int>`, asynchronous driver | 718 µs | 519 µs | 400.0 KB | 126.6 KB |
| `ToDataReader` over `List<T>`, read to the end | 66.2 µs | 65.4 µs | 95.0 KB | 95.0 KB |
| `ToDataReader` over an in-memory `IAsyncEnumerable<T>`, `Read()` | 77.5 µs | 71.2 µs | 95.1 KB | 95.1 KB |
| `ToDataReader` over a FastSql async query, `Read()` ¹ | 470 µs | 479 µs | 302.0 KB | 277.3 KB |
| `GetOrdinal` on a case-insensitive `UniversalDataReader`, 100 calls | 1.71 µs | 0.86 µs | 3.91 KB | 0 |

¹ Slower in the run above, which ran the branch second. The laptop throttles in long runs, so the three were
run again in the other order, with three launches each: before / after `CreateInsert` 3.89 / 3.73 µs,
`ReadAll<T>` 367 / 320 µs, `ToDataReader` over a FastSql async query 498 / 457 µs. Allocations do not depend
on the order, and none grew.

Where the gains come from: parameter discovery (M3, M4), the async iterators, which allocate nothing per row
(M1; with an asynchronous driver about 280 fewer bytes per row), and the comparers (M4). The mapping itself
(reflection per member and row) is unchanged; it is the largest remaining cost, see "Faster member access" in
the plan.

## Notes

- The drivers' async overrides were checked by reflection: Microsoft.Data.SqlClient 7.1.1 inherits
  `CommitAsync`, `RollbackAsync`, `BeginDbTransactionAsync` and `DisposeAsync` from the base classes (which
  call the sync methods); Npgsql 10.0.3 overrides all of them.
- Beyond the plan: M11 also uses `??=` in the options' `SetDefaults`, and M16 also removes trailing whitespace
  in the library files.
- The compiler's nullable analysis found no null bug that the tests did not already cover.
