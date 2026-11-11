# Bug report (modernization phase 2)

Phase 2 fixes bugs without changing the logic, the public contracts or the overall design. This report
lists the bugs found; it was reviewed before any library code changed.

## Status

**Fixed (the 23 safe fixes, approved first):** Q120, Q123, Q70, Q122, Q06, Q05, Q52, Q13/Q53, Q21/Q46, Q40,
Q14, Q124, Q126, Q17, Q31, Q48, Q20, Q100, Q127, Q128, Q61, Q54, Q41/Q42/Q43/Q129. Each fix has tests tagged
`[Trait("Fixed", "Qnn")]`; the tests that pinned the old behavior now assert the fixed one, and the
[quirks register](testing-plan.md#7-quirks-register) marks the items "fixed in phase 2". The public API
snapshot is unchanged.

How the fixes differ from the descriptions below:

- **Q122:** the method's token cancels the reads with `OperationCanceledException`; the enumeration's own
  token (`WithCancellation`) still ends it silently, because that is Q04, which is waiting for a decision.
  FastSql now checks the token before each read, because Npgsql skips it when the next row is already buffered.
- **Q123:** the optional switch to `BeginTransactionAsync` / `CommitAsync` / `RollbackAsync` was not made.
- **Q126:** as described, plus a null ignore list for `Update` / `Merge` (`notKeyIgnoreFields`) means "ignore nothing".
- **Q129:** with `IndexOutOfRangeException`, `SqlBulkCopy` now reports an unknown source field as its own mapping
  error ("The given ColumnName 'x' does not match up with any column in data source.").

**Open, waiting for decisions:** Q121, Q04, Q90/Q125, Q02, Q32, Q09 (both halves), Q11, Q01, Q03/Q47/Q112,
Q33, Q45, Q60, Q92, Q16, Q08. See [Decisions needed](#decisions-needed).

How the list was made:

- All library code was read (`Gerakul.FastSql.Common`, `.SqlServer`, `.PostgreSQL`, about 3,500 lines).
- Every item of the [quirks register](testing-plan.md#7-quirks-register) (Q01 to Q112) was checked:
  is it a bug, and can it be fixed without changing the contract or the design?
- New suspects were reproduced with a small probe program, in memory or against throwaway
  PostgreSQL 18.6 and SQL Server 2022 containers (the images the integration tests use).
  New findings continue the register numbering from Q120.

## How to read this

**Severity**

- **High**: wrong results without an error, or an error that hides the real cause or leaves a
  connection unusable, in ordinary use.
- **Medium**: a plausible call fails or misbehaves, with an exception.
- **Low**: edge cases, unclear exceptions, contract details of `UniversalDataReader`.

**Fix type**

- **Safe**: only calls that fail or return wrong data today behave differently; public API unchanged.
- **Behavior change**: some call that works today would behave differently. Needs your decision.
- **API change**: needs a change to a public signature. Called out separately.

**Evidence**: *probe* (reproduced in this analysis), *test* (pinned by the phase 0 tests, see the
register), *code* (found by reading; not reproduced).

**Recommendation**: **Fix**, **Fix?** (fix recommended, but it is a behavior change, so it needs your OK),
**Decide** (a real trade-off), **Leave**.

## Summary

38 items, 10 of them new (Q120 to Q129): 23 safe fixes I recommend, 5 small behavior changes I recommend
with your OK, 8 open decisions (including the Update half of Q09), and 2 I'd leave as they are and document.
The register items that are not bugs to fix in this phase are listed under
[Not proposed for fixing](#not-proposed-for-fixing).

| ID | Sev. | Bug | Fix type | Evidence | Rec. |
|---|---|---|---|---|---|
| [Q120](#q120-reading-into-a-struct-returns-default-values) | High | Reading into a `struct` returns `default` for every row | Safe | probe | Fix |
| [Q121](#q121-value-conversion-depends-on-the-current-culture) | High | Value conversion uses the current culture: `"1.5"` becomes `15` under de-DE | Behavior change | probe | Fix? |
| [Q123](#q123-usingtransaction-hides-the-real-exception-when-the-rollback-fails) | High | `UsingTransaction` replaces the real exception with the rollback's; transaction never disposed | Safe | probe | Fix |
| [Q70](#q70-async-mapped-queries-leak-the-reader-when-the-mapping-fails) | High | Async mapped queries leak the open reader when building the mapping fails | Safe | test | Fix |
| [Q04](#q04-cancelling-an-async-query-ends-it-silently) | High | Cancelling an async query ends it silently with partial results | Behavior change | test | Fix? |
| [Q122](#q122-the-token-given-to-executequeryasync-stops-working-once-rows-stream) | Medium | The token given to `ExecuteQuery*Async` is ignored once rows stream | Safe | probe | Fix |
| [Q06](#q06-cancellation-tokens-ignored-by-execution-methods) | Medium | Tokens ignored by `ExecuteNonQueryAsync`, `UseReaderAsync` and connection opening | Safe | test | Fix |
| [Q05](#q05-postgresql-writetoserverasync-runs-synchronously) | Medium | PostgreSQL `WriteToServerAsync` runs synchronously and ignores the token | Safe | test | Fix |
| [Q52](#q52-regression-values-that-are-not-iconvertible-cannot-be-read-into-compatible-members) | Medium | Regression from 41af0f9: `Guid`, `byte[]`, arrays cannot be read into `object` / interface members | Safe | test | Fix |
| [Q13, Q53](#q13-q53-get-only-properties-and-indexers-count-as-reading-destinations) | Medium | Get-only properties and indexers count as reading destinations: a model with a computed property cannot be read with the default options | Safe | test | Fix |
| [Q21, Q46](#q21-q46-fromtype-includes-indexers-and-write-only-properties) | Medium | `FieldSettings.FromType` includes indexers and write-only properties, so `Insert` and friends throw | Safe | test | Fix |
| [Q40](#q40-universaldatareader-never-disposes-its-source) | Medium | `UniversalDataReader` never disposes the source enumerator (a lazy query keeps its connection when a copy fails) | Safe | test | Fix |
| [Q14](#q14-sql-server-createtable-from-ienumerablet-fails-by-default) | Medium | SQL Server `CreateTable` from `IEnumerable<T>` fails unless `IgnoreDataReaderSchemaTable` | Safe | test | Fix |
| [Q124](#q124-sql-server-table-existence-check-misses-temp-tables) | Medium | SQL Server `CheckTableIfNotExistsBeforeCreation` does not see temp tables (`#t`) | Safe | probe | Fix |
| [Q09](#q09-update-and-merge-with-only-key-members-generate-invalid-sql) | Medium | `Update` / `Merge` with only key members generate invalid SQL | Merge safe, Update: decide | test | Fix / Decide |
| [Q11](#q11-parameter-discovery-reads--inside-literals-and-comments) | Medium | Parameter discovery takes `@` inside string literals and comments | Safe (risky to implement) | test | Decide |
| [Q90, Q125](#q90-q125-ignore-and-key-lists-are-compared-case-sensitively) | Low | Ignore lists and key lists are compared case-sensitively | Behavior change | test, probe | Fix? |
| [Q126](#q126-a-null-ignore-or-key-list-throws-argumentnullexception) | Low | A null ignore / key list throws `ArgumentNullException` in Insert / Update / Merge | Safe | probe | Fix |
| [Q17](#q17-compiling-with-a-null-parameter-list-throws) | Low | Compiling a mapped command with a null parameter list throws | Safe | test | Fix |
| [Q31](#q31-a-readoptions-instance-is-modified-in-place) | Low | A `ReadOptions` instance passed to raw readers is modified in place | Safe | test | Fix |
| [Q48](#q48-members-hidden-with-new-appear-twice) | Low | Members hidden with `new` appear twice | Safe | test | Fix |
| [Q20](#q20-identifier-quoting-does-not-escape-quote-characters) | Low | Identifier quoting does not escape `]` / `"` | Safe | test | Fix |
| [Q100](#q100-sql-server-createtable-with-an-unsupported-member-type-throws-nullreferenceexception) | Low | SQL Server `CreateTable` with an unsupported member type throws `NullReferenceException` | Safe | test | Fix |
| [Q127](#q127-tokens-dropped-in-bulk-copy-paths) | Low | Tokens dropped in bulk copy paths | Safe | code | Fix |
| [Q128](#q128-reader-schema-iskey--false-overrides-the-primarykey-option) | Low | A reader reporting `IsKey = false` overrides `ColumnDefinitionOptions.PrimaryKey` | Safe | code | Fix |
| [Q61](#q61-createinsertandgetid-casts-the-creator-to-dbcontext) | Low | `CreateInsertAndGetID` casts the creator to `DbContext` | Safe | test | Fix |
| [Q54](#q54-readallanonymous-without-a-public-constructor) | Low | `ReadAllAnonymous` without a public constructor: "Sequence contains no elements" | Safe | test | Fix |
| [Q41, Q42, Q43, Q129](#q41-q42-q43-q129-universaldatareader-small-contract-bugs) | Low | `UniversalDataReader`: token ignored, `AggregateException`, `GetBytes`/`GetChars`, `GetOrdinal` | Safe / exception type | test, code | Fix |
| [Q02](#q02-universaldatareader-nextresult-and-recordsaffected-throw) | Low | `UniversalDataReader.NextResult` / `RecordsAffected` throw `NotImplementedException` | Behavior change | test | Fix? |
| [Q32](#q32-empty-parameter-name-throws-indexoutofrangeexception) | Low | Empty parameter name throws `IndexOutOfRangeException` | Exception type | test | Fix? |
| [Q33](#q33-the-same-parameter-in-two-cases-throws) | Low | `@ID` and `@id` in one text throw "Parameter names has been duplicated" | Behavior change | test | Decide |
| [Q92](#q92-caller-supplied-dbparameter-objects-on-connection-string-contexts) | Low | Second execution with caller-supplied `DbParameter`s throws on connection string contexts | Safe | test | Decide |
| [Q01](#q01-universaldatareaderclose-hides-dbdatareaderclose) | Low | `UniversalDataReader.Close()` hides `DbDataReader.Close()` | API change (binary compatible) | test | Decide |
| [Q03, Q47, Q112](#q03-q47-q112-universaldatareader-enumerator-and-null-values) | Low | `UniversalDataReader.GetEnumerator` yields `T`; `GetValue` returns `null` instead of `DBNull` | Behavior change | test | Decide |
| [Q45](#q45-collection-detection) | Low | Non-generic `IEnumerable` counts as a scalar, `List<byte>` too | Behavior change | test | Decide |
| [Q60](#q60-createsimpletext-null) | Low | `CreateSimple(text, null)` throws `NullReferenceException` | Behavior change | test | Decide |
| [Q16](#q16-sql-server-bulk-copy-maps-names-case-sensitively-by-default) | Low | SQL Server bulk copy maps names case-sensitively by default | Behavior change | test | Leave? |
| [Q08](#q08-delete-with-a-null-key-value-deletes-nothing) | Low | `Delete` with a null key value deletes nothing | Behavior change | test | Leave? |

## New findings

### Q120 Reading into a struct returns default values

- **Where:** `ReadInfoByType<T>.GetValue`, [ReadInfo.cs:324](../Gerakul.FastSql.Common/ReadInfo.cs#L324).
- **What:** `ReadAll<T>` and `ExecuteQuery<T>` accept structs (`where T : new()`), but `FieldInfo.SetValue(obj, …)`
  and `PropertyInfo.SetValue(obj, …)` box `obj` on every call and set the box. Every row comes back as
  `default(T)`, with no error.
- **Repro (probe):** `new[] { new RowClass { A = 7, B = "x" } }.ToDataReader().ReadAll<RowStruct>()` returns
  `A = 0, B = null`. No test covers structs today.
- **Fix:** box once (`object boxed = new T();`), set the members on the box, return `(T)boxed`.

### Q121 Value conversion depends on the current culture

- **Where:** `Convert.ChangeType(value, convertingType)`, [ReadInfo.cs:62](../Gerakul.FastSql.Common/ReadInfo.cs#L62).
- **What:** conversions between strings and numbers or dates use `CultureInfo.CurrentCulture`. Under de-DE,
  a `varchar` value `"1.5"` read into a `decimal` member becomes `15` (`.` is the group separator there),
  a `decimal` read into a `string` member becomes `"1,5"`, and `DateTime` to `string` follows the machine's
  format. The same program returns different data on different machines. The tests don't see it because
  they run with the invariant culture (`xunit.runner.json`).
- **Repro (probe):** the de-DE case above returned `15`.
- **Fix:** `Convert.ChangeType(value, convertingType, CultureInfo.InvariantCulture)`.
- **Impact:** a behavior change for string-to-number/date conversions (and back) on machines with a
  non-invariant culture. Conversions that don't involve strings are unaffected.

### Q122 The token given to ExecuteQuery*Async stops working once rows stream

- **Where:** [DbCommandExtensions.cs:23](../Gerakul.FastSql.Common/DbCommandExtensions.cs#L23),
  [WrappedCommandWithContext.cs:46](../Gerakul.FastSql.Common/WrappedCommandWithContext.cs#L46),
  [Helpers.cs:53](../Gerakul.FastSql.Common/Helpers.cs#L53), [AEnumerable.cs:70](../Gerakul.FastSql.Common/AEnumerable.cs#L70).
- **What:** the token passed to the method is used only to open the connection and execute the reader.
  Reading rows and the cancellation check use the enumerator's token (`WithCancellation`), which is a
  different token unless the caller passes the same one twice.
- **Repro (probe):** `ExecuteQueryFirstColumnAsync<int>(cts.Token)` over five rows, cancelling on the first
  row: all five rows are read, no exception.
- **Fix:** `AEnumerable` gets the method token, and `GetAsyncEnumerator` links it with the enumerator token
  (`CancellationTokenSource.CreateLinkedTokenSource`, disposed with the enumerator); both the start and the
  reads use the linked token. Internal types only. What happens on cancellation is [Q04](#q04-cancelling-an-async-query-ends-it-silently).

### Q123 UsingTransaction hides the real exception when the rollback fails

- **Where:** `ConnectionContext.UsingTransaction` / `UsingTransactionAsync`,
  [ConnectionContext.cs:19](../Gerakul.FastSql.Common/ConnectionContext.cs#L19) (also reached through
  `ConnectionStringContext.UsingTransaction*`).
- **What:** the catch-all block calls `Rollback()` unguarded, also when `Commit()` itself failed. When the
  rollback throws, its exception replaces the original one:
  - PostgreSQL: a failed `COMMIT` (a deferred constraint, or a serialization failure 40001 under
    `SERIALIZABLE`, which PostgreSQL often reports at commit) completes the transaction, so the rollback
    throws `InvalidOperationException` "This NpgsqlTransaction has completed; it is no longer usable."
    Retry logic that checks `PostgresException.SqlState` never sees the real error.
  - SQL Server: after [Q70](#q70-async-mapped-queries-leak-the-reader-when-the-mapping-fails) the open reader makes the
    rollback throw "There is already an open DataReader associated with this Connection…", hiding the
    mapping error.
  - Both: a broken connection makes the rollback throw too.
  - The `DbTransaction` is never disposed.
- **Repro (probe):** a deferred foreign key violation inside `UsingTransaction` on PostgreSQL gives
  `InvalidOperationException`; the same statements without FastSql give `PostgresException 23503`. On SQL
  Server, `UsingTransactionAsync` around an async query with a missing column gives the "open DataReader"
  error instead of the `FieldsSelector` error.
- **Fix:** roll back only when the action failed, not after a failed commit; guard the rollback with
  try/catch and rethrow the original exception; dispose the transaction (`using`).
- **Also (optional):** `UsingTransactionAsync` calls the synchronous `BeginTransaction`, `Commit` and
  `Rollback`, so it blocks a thread on I/O; .NET has async versions of all three.

### Q124 SQL Server: table existence check misses temp tables

- **Where:** `CreateTableScript`, [ColumnDefinition.cs:257](../Gerakul.FastSql.SqlServer/ColumnDefinition.cs#L257).
- **What:** `CheckTableIfNotExistsBeforeCreation` generates `if object_id (N'#t', N'U') is null`. `object_id`
  looks in the current database, and temp tables live in `tempdb`, so the check is always true and a second
  copy into the same temp table tries to create it again. Bulk copying into temp tables is a common use.
  A single quote in the table name also breaks the script.
- **Repro (probe):** two `WriteToServer` calls into `#probe` with `createTable` and the check: the second one
  fails with "There is already an object named '#probe' in the database."
- **Fix:** for names starting with `#`, check `object_id(N'tempdb..#t', N'U')`; double single quotes in the name.

### Q125 Key lists are compared case-sensitively (see Q90)

Found together with Q90; details and the fix are under [Q90, Q125](#q90-q125-ignore-and-key-lists-are-compared-case-sensitively).

### Q126 A null ignore or key list throws ArgumentNullException

- **Where:** [CommandCompilator.cs:110](../Gerakul.FastSql.Common/CommandCompilator.cs#L110) (Insert),
  [156](../Gerakul.FastSql.Common/CommandCompilator.cs#L156) (Update),
  [244](../Gerakul.FastSql.Common/CommandCompilator.cs#L244) (Merge),
  [SqlServer CommandCreatorExtensions.cs:20](../Gerakul.FastSql.SqlServer/CommandCreatorExtensions.cs#L20) (InsertAndGetID).
- **What:** `Insert(t, value, (string[])null)` and the same for `Update`, `Merge` and `CreateInsertAndGetID`
  throw `ArgumentNullException` (parameter `source`, from LINQ). `Delete` (null keys: all members are keys)
  and `CreateInsertWithOutput` (null ignore list: ignore nothing) accept null. An empty key list for
  `Update` / `Merge` produces invalid SQL (`where ;` for Update), which the server rejects.
- **Repro (probe):** confirmed for Insert and Update; Delete works.
- **Fix:** a null ignore list means "ignore nothing"; a null or empty key list for Update / Merge throws a
  clear `ArgumentException`.

### Q127 Tokens dropped in bulk copy paths

- **Where:** [IWrappedCommand.cs:48](../Gerakul.FastSql.Common/IWrappedCommand.cs#L48) and
  [63](../Gerakul.FastSql.Common/IWrappedCommand.cs#L63) (`WriteToServerAsync(…, cancellationToken, …)`
  runs the source query through `UseReaderAsync` without the token);
  [MsSqlBulkCopy.cs:192](../Gerakul.FastSql.SqlServer/MsSqlBulkCopy.cs#L192) (`CREATE TABLE` without the token);
  [MsSqlBulkCopy.cs:213](../Gerakul.FastSql.SqlServer/MsSqlBulkCopy.cs#L213) and
  [NpgsqlBulkCopy.cs:181](../Gerakul.FastSql.PostgreSQL/NpgsqlBulkCopy.cs#L181) (the column lookup ignores its
  `cancellationToken` parameter); [DataReaderAttachedToDbContext.cs:32](../Gerakul.FastSql.Common/DataReaderAttachedToDbContext.cs#L32)
  (connection opened without the token).
- **Fix:** pass the token through.

### Q128 Reader schema IsKey = false overrides the PrimaryKey option

- **Where:** [SqlServer DataReaderExtensions.cs:28](../Gerakul.FastSql.SqlServer/DataReaderExtensions.cs#L28).
- **What:** `IsPrimaryKey = row.IsKey ?? optionKeys.Contains(name)`. A reader that reports `IsKey = false`
  (for example one opened with `CommandBehavior.KeyInfo` and passed to the public `DbDataReader.WriteToServer`)
  makes `CreateTable` ignore `ColumnDefinitionOptions.PrimaryKey`. FastSql's own query readers report `null`,
  so the option works there (pinned by the tests).
- **Fix:** `(row.IsKey ?? false) || optionKeys.Contains(name)`.

### Q129 UniversalDataReader.GetOrdinal throws KeyNotFoundException

Listed under [Q41, Q42, Q43, Q129](#q41-q42-q43-q129-universaldatareader-small-contract-bugs).

## Register items that are bugs

The register describes each behavior in full; this section gives the location, the fix and its impact.

### Q70 Async mapped queries leak the reader when the mapping fails

- **Where:** the start of the async enumerators, [DbCommandExtensions.cs:21](../Gerakul.FastSql.Common/DbCommandExtensions.cs#L21)
  (scoped contexts and raw commands). The reader is stored only after the mapping is built, so the dispose
  action never sees it.
- **Consequence:** the next command on the connection fails; on SQL Server the transaction can be neither
  committed nor rolled back, which together with [Q123](#q123-usingtransaction-hides-the-real-exception-when-the-rollback-fails)
  hides the original error.
- **Fix:** dispose the reader when building the mapping throws. Safe.

### Q04 Cancelling an async query ends it silently

- **Where:** [AEnumerable.cs:70](../Gerakul.FastSql.Common/AEnumerable.cs#L70).
- **What:** `MoveNextAsync` returns `false` when the token is cancelled, so `ToListAsync(token)` and
  `await foreach` return partial results that look complete. A background job stopping on a shutdown token
  can process half a result set as if it were all of it.
- **Fix:** throw `OperationCanceledException` (`ThrowIfCancellationRequested`), the `IAsyncEnumerable`
  convention. Do it together with [Q122](#q122-the-token-given-to-executequeryasync-stops-working-once-rows-stream).
- **Impact:** behavior change: code that cancels on purpose and expects a quiet stop gets an exception.

### Q06 Cancellation tokens ignored by execution methods

- **Where:** [WrappedCommandWithContext.cs:82](../Gerakul.FastSql.Common/WrappedCommandWithContext.cs#L82)
  (`ExecuteNonQueryAsync` on connection string contexts), [WrappedCommandWithScope.cs:113](../Gerakul.FastSql.Common/WrappedCommandWithScope.cs#L113)
  (`UseReaderAsync` on scoped contexts), [ConnectionStringContext.cs:40](../Gerakul.FastSql.Common/ConnectionStringContext.cs#L40)
  (connections opened without the token).
- **Fix:** pass the token; add an internal `UsingConnectionAsync` overload that takes a token, so the public
  signatures stay as they are. Safe.

### Q05 PostgreSQL WriteToServerAsync runs synchronously

- **Where:** [NpgsqlBulkCopy.cs:114](../Gerakul.FastSql.PostgreSQL/NpgsqlBulkCopy.cs#L114).
- **Fix:** `BeginBinaryImportAsync`, `StartRowAsync`, `WriteAsync`, `CompleteAsync` and `reader.ReadAsync`,
  all with the token. Safe.

### Q52 Regression: values that are not IConvertible cannot be read into compatible members

- **Where:** [ReadInfo.cs:62](../Gerakul.FastSql.Common/ReadInfo.cs#L62). Introduced by 41af0f9 ("fix for values
  casting issue"), which replaced the direct assignment with `Convert.ChangeType`.
- **What:** `Guid`, `byte[]`, `DateTimeOffset`, `TimeSpan`, provider types and arrays map only into a member of
  exactly their type; `object`, interface or base-type members throw "Object must implement IConvertible".
  With `FromTypeOption.Collection`, a PostgreSQL array cannot be read into an `IList<int>` member anymore.
- **Fix:** return the value as is when it is already an instance of the member type (what the code did before
  41af0f9), convert otherwise. Same-type values give identical results either way. Safe.

### Q13, Q53 Get-only properties and indexers count as reading destinations

- **Where:** [ReadInfo.cs:109](../Gerakul.FastSql.Common/ReadInfo.cs#L109).
- **What:** with the default `FieldsSelector.Destination`, a model with a computed property
  (`public string FullName => …`) cannot be read at all: the selector demands a column for it, and when there
  is one, setting it throws. Indexers behave the same (as a member named `Item`).
- **Fix:** destinations are properties that have a setter (any visibility, as today: `SetValue` already uses
  private setters) and no index parameters. Safe. With `FieldsSelector.Source` and a column matching a get-only
  property, the error moves from the first row (`ArgumentException`) to the start (`InvalidOperationException`).

### Q21, Q46 FromType includes indexers and write-only properties

- **Where:** [FieldSettings.cs:75](../Gerakul.FastSql.Common/FieldSettings.cs#L75).
- **What:** their getters throw, so `Insert`, `Update`, `Merge`, `CreateProcedure`, `Delete` without a key list
  and bulk copy of such objects fail for any type that has one.
- **Fix:** include only properties with a getter and no index parameters. `FromType` / `GetFields` stop listing
  members that could never be used. Safe.

### Q40 UniversalDataReader never disposes its source

- **Where:** [UniversalDataReader.cs:87](../Gerakul.FastSql.Common/UniversalDataReader.cs#L87); the
  `IEnumerable<T>.WriteToServer*` extensions ([EnumerableExtensions.cs:57](../Gerakul.FastSql.Common/EnumerableExtensions.cs#L57))
  also never dispose the reader they create.
- **What:** when the source is a lazy FastSql query (`ExecuteQuery<T>()` copied to another database) and the
  copy fails, the source reader and its connection stay open until garbage collection.
- **Fix:** dispose the source enumerator in `Dispose`; dispose the reader in the `WriteToServer*` extensions. Safe.

### Q14 SQL Server CreateTable from IEnumerable\<T\> fails by default

- **Where:** [MsSqlBulkCopy.cs:175](../Gerakul.FastSql.SqlServer/MsSqlBulkCopy.cs#L175),
  [DataReaderExtensions.cs:15](../Gerakul.FastSql.SqlServer/DataReaderExtensions.cs#L15).
- **Fix:** for a `UniversalDataReader`, which has no schema table, use its field types (what
  `IgnoreDataReaderSchemaTable` does). Safe.

### Q09 Update and Merge with only key members generate invalid SQL

- **Merge:** SQL Server: leave out the `when matched then update` branch; PostgreSQL: `on conflict (…) do nothing`.
  The meaning ("insert if missing") is kept. Safe.
- **Update:** nothing to set. Options: throw a clear `ArgumentException` when compiling (my recommendation), or
  leave the server's syntax error. **Decide.**

### Q11 Parameter discovery reads @ inside literals and comments

- **Where:** [SqlContextProvider.cs:63](../Gerakul.FastSql.SqlServer/SqlContextProvider.cs#L63),
  [NpgsqlContextProvider.cs:58](../Gerakul.FastSql.PostgreSQL/NpgsqlContextProvider.cs#L58).
- **What:** `CreateMapped("… where Email = 'a@b.com' and ID = @ID", value)` fails with "Not all parameters has
  been mapped on settings", because `@b` is taken as a parameter.
- **Fix:** skip string literals, quoted identifiers and comments before matching (and dollar-quoted strings
  on PostgreSQL). Safe for valid SQL, but it is a small lexer, and a mistake there changes the parameter list
  of queries that work today. **Decide** (I'd fix it, with a wide set of unit cases).

### Q90, Q125 Ignore and key lists are compared case-sensitively

- **Where:** [CommandCompilator.cs:110](../Gerakul.FastSql.Common/CommandCompilator.cs#L110), [145](../Gerakul.FastSql.Common/CommandCompilator.cs#L145),
  [156](../Gerakul.FastSql.Common/CommandCompilator.cs#L156), [244](../Gerakul.FastSql.Common/CommandCompilator.cs#L244),
  [SqlServer CommandCreatorExtensions.cs:20](../Gerakul.FastSql.SqlServer/CommandCreatorExtensions.cs#L20).
- **What:** parameter matching is case-insensitive by default, but the lists are not. Q90: `Insert(t, value, "id")`
  does not ignore `ID`. Q125 (new): `Update(t, value, "id")` leaves `ID` in the `set` list:
  `update T set [ID] = @ID, … where [id] = @id`. SQL Server rejects it for an identity column (probe:
  "Cannot update identity column 'ID'"); on PostgreSQL the quoted `"id"` column does not exist. `Merge` has the
  same problem.
- **Fix:** compare names case-insensitively and use the member's spelling in the generated SQL.
- **Impact:** the Update / Merge cases only fail today. The Insert case is a behavior change: `Insert(t, value, "id")`
  inserts `ID` today and would leave it out.

### Q17 Compiling with a null parameter list throws

- **Where:** [MappedCommand.cs:30](../Gerakul.FastSql.Common/MappedCommand.cs#L30): the null check sets an empty
  map but does not return, so the next lines use the null list.
- **Fix:** treat null as an empty list. Safe.

### Q31 A ReadOptions instance is modified in place

- **Where:** [ReadInfo.cs:78](../Gerakul.FastSql.Common/ReadInfo.cs#L78).
- **Fix:** fill the defaults on a clone. Safe.

### Q48 Members hidden with new appear twice

- **Where:** [FieldSettings.cs:63](../Gerakul.FastSql.Common/FieldSettings.cs#L63) and [75](../Gerakul.FastSql.Common/FieldSettings.cs#L75),
  [ReadInfo.cs:98](../Gerakul.FastSql.Common/ReadInfo.cs#L98) and [109](../Gerakul.FastSql.Common/ReadInfo.cs#L109).
- **What:** such types cannot be written or read at all ("duplicated" errors).
- **Fix:** when a name appears more than once in the hierarchy, keep the most derived member. Safe.

### Q20 Identifier quoting does not escape quote characters

- **Where:** the `ColumnList` / `WhereClause` / `SetClause` / `Merge` / `InsertWithOutput` methods of both text
  generators, and `GetColumnDeclaration` in [SqlServer ColumnDefinition.cs:63](../Gerakul.FastSql.SqlServer/ColumnDefinition.cs#L63).
- **Fix:** double `]` (SQL Server) and `"` (PostgreSQL) inside names. Safe. Parameter names still use the raw
  name (Q30, a design limit), so DML with such names stays invalid; the fix matters for `CREATE TABLE` and the
  PostgreSQL `COPY` column list.

### Q100 SQL Server CreateTable with an unsupported member type throws NullReferenceException

- **Where:** [MsSqlBulkCopy.cs:176](../Gerakul.FastSql.SqlServer/MsSqlBulkCopy.cs#L176): `FromFieldType` returns
  null for unsupported types, and the name filter dereferences it.
- **Fix:** skip null definitions; a selected unsupported field then gives the existing "Can not create table with
  specified fields" error, and an excluded one no longer matters. Safe.

### Q61 CreateInsertAndGetID casts the creator to DbContext

- **Where:** [SqlServer CommandCreatorExtensions.cs:18](../Gerakul.FastSql.SqlServer/CommandCreatorExtensions.cs#L18).
- **Fix:** build the command through `creator.Set(…)`, like the common extensions. Safe.

### Q54 ReadAllAnonymous without a public constructor

- **Where:** [ReadInfo.cs:221](../Gerakul.FastSql.Common/ReadInfo.cs#L221).
- **Fix:** throw an `InvalidOperationException` that names the type. Safe.

### Q41, Q42, Q43, Q129 UniversalDataReader: small contract bugs

- **Q41** `ReadAsync` ignores its token ([UniversalDataReader.cs:148](../Gerakul.FastSql.Common/UniversalDataReader.cs#L148)):
  check it and throw `OperationCanceledException`.
- **Q42** `Read()` over an async source wraps errors in `AggregateException` ([UniversalDataReader.cs:130](../Gerakul.FastSql.Common/UniversalDataReader.cs#L130)):
  use `GetAwaiter().GetResult()`. The exception type changes to the original one.
- **Q43** `GetBytes` / `GetChars` with a null buffer throw `NullReferenceException`; the ADO.NET contract is to
  return the field length. `GetChars` throws for `string` values ([UniversalDataReader.cs:209](../Gerakul.FastSql.Common/UniversalDataReader.cs#L209)).
- **Q129** (new, code) `GetOrdinal` with an unknown name throws `KeyNotFoundException`; `IDataRecord.GetOrdinal`
  documents `IndexOutOfRangeException` ([UniversalDataReader.cs:303](../Gerakul.FastSql.Common/UniversalDataReader.cs#L303)).

### Q02 UniversalDataReader NextResult and RecordsAffected throw

- **Fix:** `NextResult()` returns `false` (there is one result set), `RecordsAffected` returns `-1` (the value for
  a `SELECT`). `HasRows` would need a look-ahead on the source; I'd leave it. Behavior change from exception to
  value. **Fix?**

### Q32 Empty parameter name throws IndexOutOfRangeException

- **Fix:** throw `ArgumentException` naming the problem. Only the exception type changes. **Fix?**

### Q33 The same parameter in two cases throws

- **What:** `… where ID = @ID or ParentID = @id` with the default case-insensitive matching throws "Parameter names
  has been duplicated", although both refer to one member.
- **Fix idea:** with case-insensitive matching, merge names that differ only by case into one parameter.
  **Decide** (low value).

### Q92 Caller-supplied DbParameter objects on connection string contexts

- **What:** each execution rebuilds the command and adds the same `DbParameter` objects again, so the second
  execution throws ("already contained by another collection").
- **Fix idea:** clear the command's parameters after each execution. Lazy queries make it fiddly (clear after the
  enumeration ends). **Decide** (low value).

### Q01 UniversalDataReader.Close() hides DbDataReader.Close()

- **What:** closing through a `DbDataReader` / `IDataReader` reference or `CloseAsync` leaves it open.
- **Fix:** `public override void Close()`. Binary compatible, but the public signature changes (`new` to `override`),
  so the API snapshot changes. **Decide.**

### Q03, Q47, Q112 UniversalDataReader enumerator and null values

- **Q03:** `GetEnumerator()` returns the source enumerator (rows are `T`, sharing the position with `Read`), or
  null for an async source. Contract fix: `new DbEnumerator(this)`.
- **Q47, Q112:** `GetValue` returns `null` instead of `DBNull.Value` for null members. Contract fix: return
  `DBNull.Value` when `IsDBNull` is true. Then reading a `ToDataReader()` null into a non-nullable value-type member
  throws `InvalidCastException`, like database readers, instead of giving `0` (Q112).
- Both are behavior changes for code that reads a `UniversalDataReader` directly. **Decide.**

### Q45 Collection detection

- **Where:** [Helpers.cs:69](../Gerakul.FastSql.Common/Helpers.cs#L69).
- **Fix idea:** a member typed as non-generic `IEnumerable` counts as a collection; only `byte[]` counts as a
  binary scalar (today any `IEnumerable<byte>`, so `List<byte>` members are included and then fail in the driver).
  Changes which members `FromType` returns. **Decide.**

### Q60 CreateSimple(text, null)

- **What:** the `null` binds to the `params` array itself and building the command throws `NullReferenceException`.
- **Options:** treat a null array as "no parameters" (the server then reports the missing `@p0`), or as one null
  parameter (what the caller most likely meant). **Decide.**

### Q16 SQL Server bulk copy maps names case-sensitively by default

- **What:** with the default `FieldsSelector.Source` and `CaseSensitiveFieldsMatching` unset, names go to
  `SqlBulkCopy` as they are, and it matches them case-sensitively.
- **Trade-off:** resolving the names needs the `select top 0` metadata query that the other selectors already run,
  on every copy. Setting `CaseSensitiveFieldsMatching = false` already does that. I'd **leave** it and document it.

### Q08 Delete with a null key value deletes nothing

- **What:** `where [K] = @K` never matches NULL. With no key list (all members are keys, as in the README's
  `Delete("Employee", new { ID = 2 })`), any null member makes the delete silently do nothing.
- **Trade-off:** null-safe comparisons change the generated SQL for every delete, and on PostgreSQL
  `is not distinct from` cannot use a B-tree index. I'd **leave** it and document it.

## Not proposed for fixing

| ID | Why |
|---|---|
| Q07 | `Delete("T", "ID")` binds to the `whereClause` overload: an API design issue, needs an API change. |
| Q10 | PostgreSQL `CreateTable` is not implemented: a missing feature, not a bug. |
| Q12, Q50 | `DBNull` into a non-nullable member throws: by design; `ReadAll()` keeping `DBNull` matches `IDataRecord.GetValues`. For enums the exception type differs (Q50), which is not worth a change. |
| Q15 | When member values are read (create vs execute) follows the context design. |
| Q18 | `CreateInsertAndGetID` returns `decimal` (`scope_identity()`): changing the type breaks callers that cast. |
| Q19 | Table names not quoted on PostgreSQL: deliberate (PR #5 "Remove quoted table names in pgsql"). |
| Q30 | Parameter names equal field names: a design limit of `MappedCommand`; fixing it means a new naming scheme. |
| Q44 | `UniversalDataReader` getters don't check the reader state: harmless, and checks would slow every value read. |
| Q71, Q80 | Gone since phase 1. |
| Q81 | `DateTime` sent as `datetime` on SQL Server: the driver's `AddWithValue` default. Sending `datetime2` would change comparisons against `datetime` columns. |
| Q82 to Q87, Q89 | Driver or server behavior (type inference, enums, offsets, `jsonb`, `COPY` types). Q85 could be solved by writing `COPY` values with the column types, and Q87 by converting between `DateTime` and `DateTimeOffset`, but both are features. |
| Q88 | Array members are skipped unless `FromTypeOption.Collection` is set: the default by design. |
| Q91 | PostgreSQL `on conflict` checks NOT NULL before conflicts: server semantics. |
| Q103, Q104 | `CreateTable` can't know keys or nullability the reader doesn't report. |
| Q110 | One connection cannot serve the source reader and the bulk copy at once. |
| Q111 | PostgreSQL `COPY` writes identity values: server semantics. |

## Noticed, not worth a change

- Commands are never disposed (`SqlCommand` / `NpgsqlCommand` hold nothing that matters after execution).
- If an async query's start fails and the caller calls `MoveNextAsync` again, the start runs again (a new
  connection and execution). `await foreach` never does that.
- The register's "observed but not pinned" SQL Server bulk load problem ("Bulk load data was expected but not
  sent") was not reproduced; worth one more look while fixing [Q127](#q127-tokens-dropped-in-bulk-copy-paths).

## Decisions needed

1. **Q121:** switch value conversion to the invariant culture?
2. **Q04:** throw `OperationCanceledException` when an async query is cancelled?
3. **Q90, Q125:** compare ignore and key lists case-insensitively (changes `Insert(t, value, "id")`)?
4. **Q02, Q32:** accept the small behavior changes (values instead of `NotImplementedException`; `ArgumentException`
   instead of `IndexOutOfRangeException`)?
5. **Q09 Update:** a clear exception when there is nothing to set, or leave the server error?
6. **Q11:** implement literal and comment skipping in parameter discovery?
7. **UniversalDataReader contract (Q01, Q03, Q47 / Q112):** align with `DbDataReader`, partly or not at all?
8. **Q33, Q45, Q60, Q92:** fix or leave?
9. **Q16, Q08:** leave and document, as recommended?

## How the fixes would be done

- On the `fix/bugs` branch: first the 23 safe fixes, in one commit because several of them touch the same files
  (`UniversalDataReader`, the async enumerators, the bulk copies); then the items you decide on.
- Each fixed quirk's tests flip from pinning the old behavior to asserting the fixed one, and the register marks
  the item "fixed in phase 2". Each new finding gets a regression test.
- The public API snapshot stays unchanged, except Q01 if you choose to fix it.
