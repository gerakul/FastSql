# Upgrade notes (modernization phase 1)

Phase 1 moved FastSql to the latest .NET and to the latest versions of its dependencies, without
fixing bugs or changing the design. The phase 0 tests ([testing-plan.md](testing-plan.md)) are the
safety net: every test that changed did so because a runtime or driver changed, and the change is
listed below.

## What changed

| | Before | After |
|---|---|---|
| Library targets | `netstandard1.3`, `netstandard2.0`, `net46` | `net10.0` |
| Examples target | `netcoreapp2.1` | `net10.0` |
| Async sequences | `IAsyncEnumerable<T>` of System.Interactive.Async 3.2.0 | `IAsyncEnumerable<T>` built into .NET |
| SQL Server driver | System.Data.SqlClient 4.5.1 (deprecated) | Microsoft.Data.SqlClient 7.1.1 |
| PostgreSQL driver | Npgsql 4.0.2 | Npgsql 10.0.3 |
| Other packages | System.Reflection.TypeExtensions 4.5.1, System.Data.Common 4.3.0 (old targets only) | removed; System.Security.Cryptography.Pkcs 10.0.13 pinned (see below) |
| Solution | `FastSql.sln` | `FastSql.slnx` |
| Package versions | in each project file | `Directory.Packages.props` (central package management) |
| Shared project settings | repeated in each project file | `Directory.Build.props` (target framework, authors, repository) |
| SDK | any | .NET 10 SDK, pinned in `global.json` (latest feature band) |
| NuGet sources | nuget.org and the unused `gerakul-dotnet` MyGet feed | nuget.org only |

Microsoft.Data.SqlClient 7.1.1 depends on System.Security.Cryptography.Pkcs 9.0.18, which has two
known vulnerabilities (CVE-2026-84008, CVE-2026-70162). The SQL Server project references the fixed
10.0.13 directly; the reference can go once Microsoft.Data.SqlClient depends on a fixed version.

The package versions in the project files (1.1.0 and 1.0.0) are unchanged: bumping them (the changes
below are breaking, so to 2.0.0) is a release decision.

## Changes for users of the library

- **Target framework:** .NET 10 only.
- **Async queries** return the `IAsyncEnumerable<T>` built into .NET. Read them with `await foreach`
  or with `System.Linq.AsyncEnumerable` (part of .NET 10), for example `await query.ToArrayAsync()`
  instead of the Ix `await query.ToArray()`. The cancellation token of an enumeration is given once,
  with `WithCancellation(token)` / `GetAsyncEnumerator(token)`, instead of per `MoveNext` call.
  A cancelled token still ends the enumeration without an exception (Q04).
- **SQL Server:** the public API uses the Microsoft.Data.SqlClient types (`SqlConnection`,
  `SqlTransaction`, `SqlBulkCopyOptions` in `SqlBulkOptions`). Microsoft.Data.SqlClient encrypts
  connections by default, so a server without a trusted certificate needs `TrustServerCertificate=True`
  (or `Encrypt=False`) in the connection string.
- **PostgreSQL:**
  - `CreateProcedure` and `CreateProcedureSimple` call procedures with `CALL` (Npgsql 7 and later), so
    they can no longer call functions. Call a function with `CreateSimple("select * from f(@p0, ...)")`.
  - `timestamptz` values read as UTC `DateTime` (Npgsql 6 and later), a `DateTimeOffset` with a non-zero
    offset cannot be written to `timestamptz`, and `DateTime.MinValue` / `MaxValue` map to
    `-infinity` / `infinity`.
  - `date` and `time` read as `DateOnly` and `TimeOnly` (Npgsql 10). FastSql converts them for
    `DateTime` and `TimeSpan` members, so existing models keep working; `ExecuteQuery()` (untyped) and
    members of other types (`object`, `string`) get the new types. See [Open questions](#open-questions-for-later-phases).

## Library code changes

The only logic changes are the ones the new runtime and drivers required:

- `AEnumerable` implements the built-in `IAsyncEnumerable<T>`: `GetAsyncEnumerator(token)` instead of
  `GetEnumerator()`, `MoveNextAsync()` with the token of the enumerator instead of `MoveNext(token)`,
  `DisposeAsync()` with the body of the old `Dispose()`.
- `UniversalDataReader` uses `GetAsyncEnumerator()` and `MoveNextAsync()`; `Read()` still blocks on the
  move and still wraps source errors in `AggregateException` (Q42).
- The SQL Server library uses the `Microsoft.Data.SqlClient` namespace.
- `ReadInfo.GetValueFromReader` converts `DateOnly` into a `DateTime` member (midnight, `Unspecified`)
  and `TimeOnly` into a `TimeSpan` member (Npgsql 10).

## Behavior changes found by the tests

The tests tagged `UpgradeSensitive` pinned these; each change is reflected in the tests and in the
[quirks register](testing-plan.md#7-quirks-register).

### Async sequences (built-in `IAsyncEnumerable<T>`)

- A token cannot change between moves, so the second half of Q04 (going on with a live token after a
  cancelled one) cannot happen anymore. Everything else about the enumerators is unchanged.

### Microsoft.Data.SqlClient 7.1.1

- `uniqueidentifier` values can be read again (Q80 is gone).
- `SqlBulkCopy` rounds extra decimal scale, like Insert (the decimal half of Q82 is gone;
  `datetimeoffset` is still truncated).
- A `decimal(38,10)` value that fits `System.Decimal` only with fewer decimal places is read with the
  scale lowered instead of throwing `OverflowException`.
- Messages: "Execution Timeout Expired." (sync `ExecuteNonQuery` adds the server's "Operation cancelled
  by user." line), "open DataReader associated with this Connection" instead of "this Command",
  "The given ColumnName 'x' does not match up with any column in data source.", and the bulk copy
  decimal conversion message names the value and the column.
- Unchanged: parameter type inference, `DateTime` parameters sent as `datetime` (Q81), enums (Q83),
  `datetimeoffset` offsets (Q84), cancellation exception types.

### Npgsql 10.0.3

- `CALL` for `CommandType.StoredProcedure` (see above). The seeded `TestProc` is a function, so on
  PostgreSQL the shared stored procedure, README and smoke tests now pin the server's 42809 error ("is not
  a procedure"), whose message shows the arguments FastSql passed; new tests run real procedures.
- `date` / `time` as `DateOnly` / `TimeOnly`; binary COPY writes those as `date` / `time`, so a `date`
  column copied from a query works now (part of Q85 is gone).
- `timestamptz` as UTC (Q87 changed), infinity mapping, no `DateTimeOffset` with a non-zero offset.
- `CommandTimeout` applies to async execution (Q71 is gone); the timeout's inner exception is a
  `TimeoutException` instead of an `IOException`.
- `AddWithValue` no longer infers `DbType` / `NpgsqlDbType` before execution.
- Unmapped enums and unsigned values throw `InvalidCastException` (Q89 changed); an `interval` with
  months cannot be read as `TimeSpan`; a `numeric` beyond `System.Decimal` has a different message.
- `NpgsqlOperationInProgressException` no longer derives from `InvalidOperationException`; a bulk copy
  on a busy connection throws `InvalidOperationException` "Connection is busy" (Q110 changed).
- `PostgresException` messages get POSITION / DETAIL lines: the tests pin the first line.

## Open questions for later phases

- **`DateOnly` / `TimeOnly` handling (to research).** The conversion in `ReadInfo` keeps `DateTime` and
  `TimeSpan` members working, which was the goal for this phase. Not decided yet: whether FastSql should
  also convert for other member types (`object`, `string`, as Npgsql 4's `DateTime` allowed), offer an
  option instead of always converting, convert in the other direction (a `DateTime` value into a
  `DateOnly` member), or recommend `DateOnly` / `TimeOnly` members and drop the conversion in a major
  version. The behavior is pinned by `ValueConversionTests` (unit) and `PostgreSqlTypeMappingTests`
  (integration), tagged `UpgradeSensitive`.
- **Functions through `CreateProcedure` on PostgreSQL.** They need a `select` that FastSql does not
  generate; a helper could, but that is a new feature.
- **.NET 11** is due in November 2026; retargeting is a one-line change in `Directory.Build.props`
  (and the SDK in `global.json`).
- **The bulk load problem** noted at the end of the quirks register was not checked again with
  Microsoft.Data.SqlClient.
